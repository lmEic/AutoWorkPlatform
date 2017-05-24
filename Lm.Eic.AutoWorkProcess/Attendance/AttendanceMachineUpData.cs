using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Lm.Eic.Uti.Common.YleeDbHandler;
using Lm.Eic.Uti.Common.YleeExtension.FileOperation;
using Lm.Eic.Uti.Common.YleeExtension.Conversion;
using System.IO;
using Lm.Eic.AutoWorkProcess;

namespace Lm.Eic.AutoWorkProcess.Attendance
{
    /// <summary>
    /// 时间日志回调
    /// </summary>
    /// <returns></returns>
    internal delegate void PingCallback
         (String TerminalType, Int32 TerminalID, String SerialNumber, Int32 TransactionID);
    internal delegate Boolean AlarmLogCallback
        (String TerminalType, Int32 TerminalID, String SerialNumber, Int32 TransactionID,
        DateTime LogTime, Int64 UserID, Int32 DoorID,
        String AlarmType);
    internal delegate Boolean AdminLogCallback
        (String TerminalType, Int32 TerminalID, String SerialNumber, Int32 TransactionID,
        DateTime LogTime, Int64 AdminID, Int64 UserID,
        String Action,
        Int32 Result);
    internal delegate Boolean TimeLogCallback
        (String TerminalType, Int32 TerminalID, String SerialNumber, Int32 TransactionID,
        DateTime LogTime, Int64 UserID, Int32 DoorID,
        String AttendanceStatus,
        String VerifyMode,
        Int32 JobCode,
        String Antipass,
        Byte[] Photo);

    internal class AttendanceUpdateLogServer : IDisposable
    {
        public Boolean m_Disposed;
        public UInt16 m_PortNo;
        public TcpListener m_Listner;
        static LinkedList<AttendanceUpdateTerminal> m_TerminalList = new LinkedList<AttendanceUpdateTerminal>();

        public TimeLogCallback m_TimeLogCallBack = null;
        public AdminLogCallback m_AdminLogCallBack = null;
        public AlarmLogCallback m_AlarmLogCallBack = null;
        public PingCallback m_PingCallBack = null;

        public AttendanceUpdateLogServer(UInt16 portNo,
            TimeLogCallback timeLogCallback,
            AdminLogCallback adminLogCallback,
            AlarmLogCallback alarmLogCallback,
            PingCallback pingCallback)
        {
            try
            {
                // Initialize objects.
                m_Disposed = false;
                m_PortNo = portNo;
                m_TimeLogCallBack = timeLogCallback;
                m_AdminLogCallBack = adminLogCallback;
                m_AlarmLogCallBack = alarmLogCallback;
                m_PingCallBack = pingCallback;

                // Start TCP Listner.
                m_Listner = new TcpListener(IPAddress.Any, m_PortNo);
                m_Listner.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                m_Listner.Start();

                // Begin Accept.
                m_Listner.BeginAcceptTcpClient(new AsyncCallback(AttendanceUpdateLogServer.OnAccept), this);

            }
            catch (Exception ex)
            {
                ErrorMessageTracer.LogErrorMsgToFile("", ex);
            }

        }

        ~AttendanceUpdateLogServer()
        {
            CleanUp(false);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="dispose"></param>
        private void CleanUp(bool dispose)
        {
            if (m_Disposed)
                return;

            m_Disposed = true;

            if (dispose)
            {
                // Dispose the listener and terminals.
                try
                {
                    m_Listner.Stop();
                    foreach (AttendanceUpdateTerminal e in m_TerminalList)
                    {
                        if (e != null)
                            e.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    ErrorMessageTracer.LogErrorMsgToFile("CleanUp", ex);
                }
            }
        }

        public void Dispose()
        {
            CleanUp(true);
        }

        public static void OnAccept(IAsyncResult iar)
        {
            AttendanceUpdateLogServer server = (AttendanceUpdateLogServer)iar.AsyncState;
            AttendanceUpdateTerminal term = new AttendanceUpdateTerminal(server.m_TimeLogCallBack,
                server.m_AdminLogCallBack,
                server.m_AlarmLogCallBack,
                server.m_PingCallBack);
            try
            {
                // Establish connection and add a terminal into the list.
                term.EstablishConnect(server.m_Listner.EndAcceptTcpClient(iar));
                m_TerminalList.AddLast(term);
                // For disposed listener.
                server.m_Listner.BeginAcceptTcpClient(new AsyncCallback(AttendanceUpdateLogServer.OnAccept), server);
            }
            catch (Exception ex)
            {
                term.Dispose();
                ErrorMessageTracer.LogErrorMsgToFile("OnAccept", ex);
            }
        }
    }

    internal class AttendanceUpdateTerminal : IDisposable
    {

        public Boolean m_Disposed;
        public TcpClient m_Client;
        public NetworkStream m_Stream;
        public Timer m_TimerAlive;
        public Byte[] m_TmpBuffer;
        public Byte[] m_RxBuffer;
        public int m_RxCount;

        private const int MaxMessageSize = 2048 + 8 * 1024 * 2;     // Maximum message size // 18K
        private const int PingTimeout = 30 * 1000;  // Ping timeout

        public TimeLogCallback m_TimeLogCallBack = null;
        public AdminLogCallback m_AdminLogCallBack = null;
        public AlarmLogCallback m_AlarmLogCallBack = null;
        public PingCallback m_PingCallBack = null;
        // Clean up client
        private void CleanUp(Boolean disposing)
        {
            if (m_Disposed)
                return;

            m_Disposed = true;

            if (disposing)
            {
                // Dispose client objects
                m_TimerAlive.Change(Timeout.Infinite, Timeout.Infinite);
                try
                {
                    m_Stream.Close();
                    m_Client.Close();
                }
                catch (Exception ex)
                {
                    ErrorMessageTracer.LogErrorMsgToFile("CleanUp", ex);
                }
            }
        }

        public void Dispose()
        {
            CleanUp(true);
        }

        public AttendanceUpdateTerminal(TimeLogCallback timeLogCallback,
            AdminLogCallback adminLogCallback,
            AlarmLogCallback alarmLogCallback,
            PingCallback pingCallback)
        {
            // Initialize objects.
            m_Disposed = false;

            m_TimeLogCallBack = timeLogCallback;
            m_AdminLogCallBack = adminLogCallback;
            m_AlarmLogCallBack = alarmLogCallback;
            m_PingCallBack = pingCallback;

            // Message buffer
            m_TmpBuffer = new Byte[MaxMessageSize];
            m_RxBuffer = new Byte[MaxMessageSize];
            m_RxCount = 0;

            m_TimerAlive = new Timer(new TimerCallback(this.OnAliveTimerExpired));
            RestartAliveTimer();
        }

        ~AttendanceUpdateTerminal()
        {
            CleanUp(false);
        }

        // When alive timer is expired
        public void OnAliveTimerExpired(Object stateInfo)
        {
            this.Dispose();
        }

        // Restart alive timer
        public void RestartAliveTimer()
        {
            m_TimerAlive.Change(PingTimeout, Timeout.Infinite);
        }

        // Establish connection to terminal
        public void EstablishConnect(TcpClient client)
        {
            m_Client = client;
            m_Stream = m_Client.GetStream();

            RestartAliveTimer();
            m_Stream.BeginRead(m_TmpBuffer, 0, MaxMessageSize,
                new AsyncCallback(AttendanceUpdateTerminal.OnReceive), this);
        }

        // Send the stream to terminal
        public static void OnSend(IAsyncResult iar)
        {
            AttendanceUpdateTerminal term = (AttendanceUpdateTerminal)iar.AsyncState;
            try
            {
                term.m_Stream.EndWrite(iar);
            }
            catch (Exception ex)
            {
                ErrorMessageTracer.LogErrorMsgToFile("OnSend", ex);
            }
        }

        // Check message is end
        private static Boolean CheckMessageEnd(Byte c)
        {
            return (c == 0);
        }

        private string GetElementValue(XmlDocument doc, string elementName)
        {
            string rtnValue = string.Empty;
            try
            {
                foreach (XmlElement x in doc.DocumentElement.ChildNodes)
                {
                    if (x.Name == elementName)
                    {
                        rtnValue = x.InnerText.Trim();
                        break;
                    }

                }
            }
            catch (Exception ex)
            {
                ErrorMessageTracer.LogErrorMsgToFile("GetElementValue", ex);
            }
            return rtnValue;
        }

        private void SendReply(String replyType, Int32 transId)
        {
            string msg =
                "<?xml version=\"1.0\"?>" +
                "<Message>" +
                "<Request>" +
                replyType +
                "</Request>" +
                "<TransID>" +
                transId.ToString() +
                "</TransID>" +
                "</Message>";

            byte[] buffer = new byte[msg.Length + 1];
            System.Text.Encoding.ASCII.GetBytes(msg).CopyTo(buffer, 0);
            buffer[msg.Length] = 0;
            try
            {
                m_Stream.BeginWrite(buffer, 0, buffer.Length, new AsyncCallback(AttendanceUpdateTerminal.OnSend), this);
            }
            catch (System.Exception ex)
            {
                ErrorMessageTracer.LogErrorMsgToFile("SendReply", ex);
            }

        }


        private int ParseElement(string el, int defaultValue = 0)
        {
            int v;
            if (int.TryParse(el, out v))
            {
                return v;
            }
            else
            {
                return defaultValue;
            }
        }

        private void OnTimeLog(XmlDocument doc, String termType, Int32 termId, String serialNumber, Int32 transId)
        {
            Int32 year = 0, month = 0, day = 0, hour = 0, minute = 0, second = 0;
            Int64 userID = -1;
            Int32 doorID = -1;
            String attendStatus = string.Empty;
            String verifyMode = string.Empty;
            Int32 jobCode = 0;
            String antipassStatus = string.Empty;
            Byte[] photo = null;

            //---------------------- Log Time
            try
            {
                year = ParseElement(GetElementValue(doc, "Year"));
                month = ParseElement(GetElementValue(doc, "Month"));
                day = ParseElement(GetElementValue(doc, "Day"));
                hour = ParseElement(GetElementValue(doc, "Hour"));
                minute = ParseElement(GetElementValue(doc, "Minute"));
                second = ParseElement(GetElementValue(doc, "Second"));
                //---------------------- User ID
                userID = ParseElement(GetElementValue(doc, "UserID"), -1);
                //---------------------- Door ID
                doorID = ParseElement(GetElementValue(doc, "DoorID"));
                //---------------------- Time attendance status
                attendStatus = GetElementValue(doc, "AttendStat");
                //---------------------- Verification mode
                verifyMode = GetElementValue(doc, "VerifMode");
                //---------------------- Jobcode
                jobCode = ParseElement(GetElementValue(doc, "JobCode"), -1);
                //---------------------- Antipass status
                antipassStatus = GetElementValue(doc, "APStat");
                // Photo taken
                photo = null;
                if (GetElementValue(doc, "Photo").Trim().ToUpper() == "YES")
                {
                    String logImage = GetElementValue(doc, "LogImage");
                    if (logImage != null)
                        photo = Convert.FromBase64String(logImage);
                }
            }
            catch (System.Exception ex)
            {
                photo = null;
                ErrorMessageTracer.LogErrorMsgToFile("OnTimeLog", ex);
            }

            Boolean logProcessed = false;
            try
            {
                if (m_TimeLogCallBack != null)
                    logProcessed = m_TimeLogCallBack(termType, termId, serialNumber, transId, new DateTime(year, month, day, hour, minute, second), userID, doorID, attendStatus, verifyMode, jobCode, antipassStatus, photo);

                if (logProcessed)
                    SendReply("UploadedLog", transId);
                else
                    SendReply("Error", transId);
            }
            catch (Exception ex)
            {
                ErrorMessageTracer.LogErrorMsgToFile("OnTimeLog", ex);
            }

        }

        private void OnAdminLog(XmlDocument doc, String termType, Int32 termId, String serialNumber, Int32 transId)
        {
            Int32 year, month, day, hour, minute, second;
            Int64 adminID;
            Int64 userID;
            String action;
            Int32 result;

            //--------------------- Log Time
            try
            {
                year = int.Parse(GetElementValue(doc, "Year"));
                month = int.Parse(GetElementValue(doc, "Month"));
                day = int.Parse(GetElementValue(doc, "Day"));
                hour = int.Parse(GetElementValue(doc, "Hour"));
                minute = int.Parse(GetElementValue(doc, "Minute"));
                second = int.Parse(GetElementValue(doc, "Second"));
            }
            catch (System.Exception ex)
            {
                year = 0;
                month = 0;
                day = 0;
                hour = 0;
                minute = 0;
                second = 0;

                ErrorMessageTracer.LogErrorMsgToFile("OnAdminLog", ex);
            }

            //--------------------- Administrator ID
            try
            {
                adminID = Int64.Parse(GetElementValue(doc, "AdminID"));
            }
            catch (System.Exception ex)
            {
                adminID = -1;
                ErrorMessageTracer.LogErrorMsgToFile("OnAdminLog", ex);
            }

            //---------------------- User ID
            try
            {
                userID = Int64.Parse(GetElementValue(doc, "UserID"));
            }
            catch (System.Exception ex)
            {
                userID = -1;
                ErrorMessageTracer.LogErrorMsgToFile("OnAdminLog", ex);
            }

            //---------------------- Action
            try
            {
                action = GetElementValue(doc, "Action");
            }
            catch (System.Exception ex)
            {
                action = "";
                ErrorMessageTracer.LogErrorMsgToFile("OnAdminLog", ex);
            }

            //---------------------- Result
            try
            {
                result = Int32.Parse(GetElementValue(doc, "Stat"));
            }
            catch (System.Exception ex)
            {
                result = -1;
                ErrorMessageTracer.LogErrorMsgToFile("OnAdminLog", ex);
            }

            Boolean logProcessed = false;
            try
            {
                if (m_AdminLogCallBack != null)
                    logProcessed = m_AdminLogCallBack(termType, termId, serialNumber, transId, new DateTime(year, month, day, hour, minute, second), adminID, userID, action, result);
                if (logProcessed)
                    SendReply("UploadedLog", transId);
                else
                    SendReply("Error", transId);
            }
            catch (Exception ex)
            {
                ErrorMessageTracer.LogErrorMsgToFile("OnAdminLog", ex);
            }
        }

        private void OnAlarmLog(XmlDocument doc, String termType, Int32 termId, String serialNumber, Int32 transId)
        {
            Int32 year, month, day, hour, minute, second;
            Int64 userID;
            Int32 doorID;
            String alarmType;

            //--------------------- Log Time
            try
            {
                year = int.Parse(GetElementValue(doc, "Year"));
                month = int.Parse(GetElementValue(doc, "Month"));
                day = int.Parse(GetElementValue(doc, "Day"));
                hour = int.Parse(GetElementValue(doc, "Hour"));
                minute = int.Parse(GetElementValue(doc, "Minute"));
                second = int.Parse(GetElementValue(doc, "Second"));
            }
            catch (System.Exception ex)
            {
                year = 0;
                month = 0;
                day = 0;
                hour = 0;
                minute = 0;
                second = 0;
                ErrorMessageTracer.LogErrorMsgToFile("OnAlarmLog", ex);
            }

            //---------------------- User ID
            try
            {
                userID = Int64.Parse(GetElementValue(doc, "UserID"));
            }
            catch (System.Exception ex)
            {
                userID = -1;
                ErrorMessageTracer.LogErrorMsgToFile("OnAlarmLog", ex);
            }

            //---------------------- Door ID
            try
            {
                doorID = Int32.Parse(GetElementValue(doc, "DoorID"));
            }
            catch (System.Exception ex)
            {
                doorID = -1;
                ErrorMessageTracer.LogErrorMsgToFile("OnAlarmLog", ex);
            }

            //---------------------- Alarm Type
            try
            {
                alarmType = GetElementValue(doc, "Type");
            }
            catch (System.Exception ex)
            {
                alarmType = "";
                ErrorMessageTracer.LogErrorMsgToFile("OnAlarmLog", ex);
            }

            Boolean logProcessed = false;
            try
            {
                if (m_AlarmLogCallBack != null)
                    logProcessed = m_AlarmLogCallBack(termType, termId, serialNumber, transId, new DateTime(year, month, day, hour, minute, second), userID, doorID, alarmType);
                if (logProcessed)
                    SendReply("UploadedLog", transId);
                else
                    SendReply("Error", transId);
            }
            catch (Exception ex)
            {
                ErrorMessageTracer.LogErrorMsgToFile("OnAlarmLog", ex);
            }
        }

        private void OnPing(String termType, Int32 termId, String serialNumber, Int32 transId)
        {
            try
            {
                SendReply("KeptAlive", transId);
                if (m_PingCallBack != null)
                    m_PingCallBack(termType, termId, serialNumber, transId);
            }
            catch (System.Exception ex)
            {
                ErrorMessageTracer.LogErrorMsgToFile("OnPing", ex);
            }

        }

        // Parse a message
        private void ParseMessage(string message)
        {
            try
            {
                XmlDocument doc = new XmlDocument();
                String termType;
                Int32 termId;
                String serialNumber;
                String eventType;
                Int32 transId;
                doc.Load(new StringReader(message));
                //----------------- Terminal type
                termType = GetElementValue(doc, "TerminalType");
                //----------------- Terminal ID
                termId = ParseElement(GetElementValue(doc, "TerminalID"), -1);
                //----------------- Serial Number
                serialNumber = GetElementValue(doc, "DeviceSerialNo");
                //----------------- Transaction ID
                transId = ParseElement(GetElementValue(doc, "TransID"), -1);
                //------------------ Event
                eventType = GetElementValue(doc, "Event");
                switch (eventType)
                {
                    case "TimeLog":
                        OnTimeLog(doc, termType, termId, serialNumber, transId);
                        break;

                    case "AdminLog":
                        OnAdminLog(doc, termType, termId, serialNumber, transId);
                        break;

                    case "Alarm":
                        OnAlarmLog(doc, termType, termId, serialNumber, transId);
                        break;

                    case "KeepAlive":
                        OnPing(termType, termId, serialNumber, transId);
                        break;
                }
            }
            catch (Exception ex)
            {
                ErrorMessageTracer.LogErrorMsgToFile("ParseMessage", ex);
            }

        }

        public Boolean ParseBuffer(out Int32 consumed)
        {
            Byte[] data = m_RxBuffer;
            int size = m_RxCount;
            consumed = 0;

            if (m_RxCount == MaxMessageSize)
                return false;

            Int32 end = Array.FindIndex(data, 0, m_RxCount, CheckMessageEnd);
            if (end == -1)
            {
                consumed = 0;
                return true;
            }

            ParseMessage(System.Text.Encoding.ASCII.GetString(data, 0, end));

            for (; end < m_RxCount; end++)
            {
                if (data[end] != 0)
                    break;
            }
            if (end != m_RxCount)
                end++;
            consumed = end;

            return true;
        }

        public static void OnReceive(IAsyncResult iar)
        {
            AttendanceUpdateTerminal term = (AttendanceUpdateTerminal)iar.AsyncState;

            int recieved;
            ///尝试读取 如果读取错误 清除Term 重新读取
            try
            {
                if (term == null) return;

                recieved = term.m_Stream.EndRead(iar);
                if (recieved <= 0)
                    throw new Exception("connection closed");

                if (recieved > MaxMessageSize - term.m_RxCount)
                    recieved = MaxMessageSize - term.m_RxCount;

                Array.Copy(term.m_TmpBuffer, 0, term.m_RxBuffer, term.m_RxCount, recieved);
                term.m_RxCount += recieved;

                while (term.m_RxCount > 0)
                {
                    int consumed;

                    // Parse Buffer
                    if (!term.ParseBuffer(out consumed))
                        throw new Exception("handle failed");

                    // Remove Consumed Part of Buffer
                    if (consumed > 0)
                    {
                        term.m_RxCount -= consumed;
                        Array.Copy(term.m_RxBuffer, consumed, term.m_RxBuffer, 0, term.m_RxCount);
                    }
                    else
                    {
                        break;
                    }
                }

                // Restart alive timer
                term.RestartAliveTimer();
                term.m_Stream.BeginRead(term.m_TmpBuffer, 0, MaxMessageSize,
                    new AsyncCallback(AttendanceUpdateTerminal.OnReceive), term);
            }
            catch (Exception ex)
            {
                term.Dispose();
                ErrorMessageTracer.LogErrorMsgToFile("OnReceive", ex);
            }
        }

    }
    public class AttendanceUpSynchronous
    {
        AttendanceUpdateLogServer m_LogServer;          // Log Server
        Boolean m_Running;              // Is Running Monitor Thread
        ManualResetEvent m_StopEvent;   // stop event

        public Action<List<string>> ReportUpdataMsg { get; set; }


        List<MsgCell> msgList = new List<MsgCell>();
        int rowId = 0;
        List<string> msgSblist = new List<string>();
        /// <summary>
        /// 扫描端口
        /// </summary>
        private UInt16 _portNum = 5005;
        public UInt16 PortNum
        {
            set { _portNum = value; }
            get { return _portNum; }
        }
        #region 数据库入存
        /// <summary>
        /// 所有用户列表
        /// </summary>
        //private List<AttendFingerPrintDataInTimeModel> AllUserList = new List<AttendFingerPrintDataInTimeModel>();
        private List<ArWorkerInfo> userList = null;
        private ArWorkerInfo GetUser(long userId)
        {
            if (userList == null || userList.Count == 0)
                userList = WorkerManager.GetWorkerInfos();
            ArWorkerInfo user = userList.FirstOrDefault(e => e.WorkerId == userId.ToString().PadLeft(6, '0'));
            if (user == null)
            {
                userList = WorkerManager.GetWorkerInfos();
                user = userList.FirstOrDefault(e => e.WorkerId == userId.ToString().PadLeft(6, '0'));
            }
            return user;
        }
        /// <summary>
        /// 存入到数据数库内
        /// </summary>
        /// <param name="UserID"></param>
        /// <param name="anVerifyMode"></param>
        /// <param name="anLogDate"></param>
        /// <param name="astrSerialNo"></param>
        private bool Add_FingerPrintDataInTime(long UserID, string anVerifyMode, DateTime anLogDate, string astrSerialNo)
        {
            try
            {
                bool returnBool = false;
                var user = GetUser(UserID);
                if (user == null) return false;
                var m = new AttendFingerPrintDataInTimeModel();
                m.WorkerId = user.WorkerId;
                m.WorkerName = user.Name;
                m.CardID = user.CardID;
                if (astrSerialNo == null) m.MachineId = string.Empty;
                m.MachineId = astrSerialNo;
                switch (anVerifyMode)
                {
                    case "FP":
                        m.CardType = "指纹";
                        break;
                    case "Face":
                        m.CardType = "脸部";
                        break;
                    case "":
                        m.CardType = "卡片";
                        break;
                    case "Card":
                        m.CardType = "卡片";
                        break;
                    default:
                        m.CardType = "其它";
                        break;
                }
                m.SlodCardTime = anLogDate;
                m.SlodCardDate = anLogDate.Date;
                if (anVerifyMode == "FP" || anVerifyMode == "Face")
                    returnBool = AttendFingerPrintDataHandler.InsertDataTo(m) > 0;
                return returnBool;
            }
            catch (Exception ex)
            {
                ErrorMessageTracer.LogErrorMsgToFile("Add_FingerPrintDataInTime", ex);
                return false;
            }
        }
        #endregion

        #region  处理返回的信息

        public Boolean OnTimeLog(String terminalType, Int32 terminalID, String serialNumber, Int32 transactionID, DateTime logTime,
            Int64 userID, Int32 doorID, String attendanceStatus, String verifyMode, Int32 jobCode, String antipass, Byte[] photo)
        {
            String msg;
            try
            {
                msg = "[" + terminalType + ":";
                msg += terminalID.ToString();
                msg += " MachineID=" + serialNumber + "] ";
                msg += "TimeLog";
                msg += "(" + transactionID.ToString() + ") ";
                msg += logTime.ToString() + ", ";
                msg += "UserID=" + String.Format("{0}, ", userID);
                msg += "Door=" + doorID.ToString() + ", ";
                msg += "AttendStat=" + attendanceStatus + ", ";
                msg += "VMode=" + verifyMode + ", ";
                msg += "JobCode=" + jobCode.ToString() + ", ";
                msg += "Antipass=" + antipass + ", ";
                if (photo == null)
                    msg += "Photo=No";
                else
                {
                    msg += "Photo=Yes ";
                    msg += "(" + Convert.ToString(photo.Length) + "bytes)";
                }
                if (!Add_FingerPrintDataInTime(userID, verifyMode, logTime, serialNumber))
                {
                    string fileName = @"C:\AutoProcessWorker\Log\" + logTime.ToDateStr() + ".txt";
                    fileName.AppendFile(msg);
                }
                RetrunShowInfo(msg);
                return true;
            }
            catch (Exception ex)
            {
                ErrorMessageTracer.LogErrorMsgToFile("OnTimeLog", ex);
                return false;
            }
        }

        public Boolean OnAdminLog(String terminalType, Int32 terminalID, String serialNumber, Int32 transactionID, DateTime logTime,
            Int64 adminID, Int64 userID, String action, Int32 result)
        {
            String msg;

            try
            {
                msg = "[" + terminalType + ":";
                msg += terminalID.ToString();
                msg += " MachineId=" + serialNumber + "] ";
                msg += "AdminLog";
                msg += "(" + transactionID.ToString() + ") ";
                msg += logTime.ToString() + ", ";
                msg += "AdminID=" + String.Format("{0}, ", adminID);
                msg += "UserID=" + String.Format("{0}, ", userID);
                msg += "Action=" + action + ", ";
                msg += "Status=" + String.Format("{0:D}", result);
                RetrunShowInfo(msg);
                return true;
            }
            catch (Exception ex)
            {
                ErrorMessageTracer.LogErrorMsgToFile("OnAdminLog", ex);
                return false;
            }
        }

        public Boolean OnAlarmLog(String terminalType, Int32 terminalID, String serialNumber, Int32 transactionID, DateTime logTime,
            Int64 userID, Int32 doorID, String alarmType)
        {
            String msg;
            try
            {
                msg = "[" + terminalType + ":";
                msg += terminalID.ToString();
                msg += " SN=" + serialNumber + "] ";
                msg += "AlarmLog";
                msg += "(" + transactionID.ToString() + ") ";
                msg += logTime.ToString() + ", ";//'at'
                msg += "UserID=" + String.Format("{0}, ", userID);
                msg += "Door=" + doorID.ToString() + ", ";
                msg += "Type=" + alarmType;
                RetrunShowInfo(msg);
                return true;
            }
            catch (Exception ex)
            {
                ErrorMessageTracer.LogErrorMsgToFile("OnAlarmLog", ex);
                return false;
            }
        }

        public void OnPing(String terminalType, Int32 terminalID, String serialNumber, Int32 transactionID)
        {
            try
            {
                String msg = "[" + terminalType + ":";
                msg += terminalID.ToString();
                msg += " SN=" + serialNumber + "] ";
                msg += "KeepAlive";
                msg += "(" + transactionID.ToString() + ") ";
                RetrunShowInfo(msg);
            }
            catch (System.Exception ex)
            {
                ErrorMessageTracer.LogErrorMsgToFile("OnPing", ex);
            }

        }

        private void RetrunShowInfo(string msg)
        {
            if (msgList.Count >= 100)
            {
                msgList.Clear();
                rowId = 0;
                msgSblist.Clear();
            }
            rowId += 1;
            msgList.Add(new MsgCell() { RowId = rowId, Msg = msg });

            msgList = msgList.OrderByDescending(o => o.RowId).ToList();
            msgList.ForEach(m => { msgSblist.Add(m.Msg); });

            if (ReportUpdataMsg != null)
            {
                ReportUpdataMsg(msgSblist);
            }
        }

        #endregion

        public AttendanceUpSynchronous()
        {
            m_Running = false;
        }
        /// <summary>
        /// 关闭异步操作
        /// </summary>
        public void ClosingAttendanceUpSynchronous()
        {
            if (m_Running)
            {
                m_Running = false;
                m_StopEvent.WaitOne();
            }
        }

        /// <summary>
        /// 开启异步操作
        /// </summary>
        public void OpenAttendanceUpSynchronous()
        {
            m_Running = true;
            m_StopEvent = new ManualResetEvent(false);

            ThreadPool.QueueUserWorkItem(new WaitCallback(MonitoringThread));
        }
        private void MonitoringThread(object state)
        {
            m_LogServer = new AttendanceUpdateLogServer(_portNum, OnTimeLog, OnAdminLog, OnAlarmLog, OnPing);   // Create and start log server.
                                                                                                                // Watch stop signal.
            while (m_Running)
            {
                Thread.Sleep(100);  // Simulate some lengthy operations.
            }
            m_LogServer.Dispose();  // Dispose log server
            m_StopEvent.Set();
        }
    }

    public class MsgCell
    {
        public int RowId { get; set; }
        public string Msg { get; set; }
    }
}
