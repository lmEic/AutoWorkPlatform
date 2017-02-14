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

        ~AttendanceUpdateLogServer()
        {
            CleanUp(false);
        }

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
                catch
                {

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
            }
            catch
            {
                term.Dispose();
            }

            try
            {
                // For disposed listener.
                server.m_Listner.BeginAcceptTcpClient(new AsyncCallback(AttendanceUpdateLogServer.OnAccept), server);
            }
            catch
            {

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
                catch
                {
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
            catch
            {

            }
        }

        // Check message is end
        private static Boolean CheckMessageEnd(Byte c)
        {
            return (c == 0);
        }

        private string GetElementValue(XmlDocument doc, string elementName)
        {
            foreach (XmlElement x in doc.DocumentElement.ChildNodes)
            {
                if (x.Name == elementName)
                    return x.InnerText;
            }
            throw new Exception();
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
            m_Stream.BeginWrite(buffer, 0, buffer.Length, new AsyncCallback(AttendanceUpdateTerminal.OnSend), this);
        }

        private void OnTimeLog(XmlDocument doc, String termType, Int32 termId, String serialNumber, Int32 transId)
        {
            Int32 year, month, day, hour, minute, second;
            Int64 userID;
            Int32 doorID;
            String attendStatus;
            String verifyMode;
            Int32 jobCode;
            String antipassStatus;
            Byte[] photo;

            //---------------------- Log Time
            try
            {
                year = int.Parse(GetElementValue(doc, "Year"));
                month = int.Parse(GetElementValue(doc, "Month"));
                day = int.Parse(GetElementValue(doc, "Day"));
                hour = int.Parse(GetElementValue(doc, "Hour"));
                minute = int.Parse(GetElementValue(doc, "Minute"));
                second = int.Parse(GetElementValue(doc, "Second"));
            }
            catch (System.Exception)
            {
                year = 0;
                month = 0;
                day = 0;
                hour = 0;
                minute = 0;
                second = 0;
            }

            //---------------------- User ID
            try
            {
                userID = Int64.Parse(GetElementValue(doc, "UserID"));
            }
            catch (System.Exception)
            {
                userID = -1;
            }

            //---------------------- Door ID
            try
            {
                doorID = Int32.Parse(GetElementValue(doc, "DoorID"));
            }
            catch (System.Exception)
            {
                doorID = -1;
            }

            //---------------------- Time attendance status
            try
            {
                attendStatus = GetElementValue(doc, "AttendStat");
            }
            catch (System.Exception)
            {
                attendStatus = "";
            }

            //---------------------- Verification mode
            try
            {
                verifyMode = GetElementValue(doc, "VerifMode");
            }
            catch (System.Exception)
            {
                verifyMode = "";
            }

            //---------------------- Jobcode
            try
            {
                jobCode = Int32.Parse(GetElementValue(doc, "JobCode"));
            }
            catch (System.Exception)
            {
                jobCode = -1;
            }

            //---------------------- Antipass status
            try
            {
                antipassStatus = GetElementValue(doc, "APStat");
            }
            catch (System.Exception)
            {
                antipassStatus = "";
            }

            // Photo taken
            photo = null;
            try
            {
                if (GetElementValue(doc, "Photo") == "Yes")
                {
                    String logImage = GetElementValue(doc, "LogImage");
                    if (logImage != null)
                        photo = Convert.FromBase64String(logImage);
                }
            }
            catch (System.Exception)
            {
                photo = null;
            }

            Boolean logProcessed = false;
            try
            {
                if (m_TimeLogCallBack != null)
                    logProcessed = m_TimeLogCallBack(termType, termId, serialNumber, transId, new DateTime(year, month, day, hour, minute, second), userID, doorID, attendStatus, verifyMode, jobCode, antipassStatus, photo);
            }
            catch (Exception)
            {
            }
            if (logProcessed)
                SendReply("UploadedLog", transId);
            else
                SendReply("Error", transId);
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
            catch (System.Exception)
            {
                year = 0;
                month = 0;
                day = 0;
                hour = 0;
                minute = 0;
                second = 0;
            }

            //--------------------- Administrator ID
            try
            {
                adminID = Int64.Parse(GetElementValue(doc, "AdminID"));
            }
            catch (System.Exception)
            {
                adminID = -1;
            }

            //---------------------- User ID
            try
            {
                userID = Int64.Parse(GetElementValue(doc, "UserID"));
            }
            catch (System.Exception)
            {
                userID = -1;
            }

            //---------------------- Action
            try
            {
                action = GetElementValue(doc, "Action");
            }
            catch (System.Exception)
            {
                action = "";
            }

            //---------------------- Result
            try
            {
                result = Int32.Parse(GetElementValue(doc, "Stat"));
            }
            catch (System.Exception)
            {
                result = -1;
            }

            Boolean logProcessed = false;
            try
            {
                if (m_AdminLogCallBack != null)
                    logProcessed = m_AdminLogCallBack(termType, termId, serialNumber, transId, new DateTime(year, month, day, hour, minute, second), adminID, userID, action, result);
            }
            catch (Exception)
            {
            }
            if (logProcessed)
                SendReply("UploadedLog", transId);
            else
                SendReply("Error", transId);
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
            catch (System.Exception)
            {
                year = 0;
                month = 0;
                day = 0;
                hour = 0;
                minute = 0;
                second = 0;
            }

            //---------------------- User ID
            try
            {
                userID = Int64.Parse(GetElementValue(doc, "UserID"));
            }
            catch (System.Exception)
            {
                userID = -1;
            }

            //---------------------- Door ID
            try
            {
                doorID = Int32.Parse(GetElementValue(doc, "DoorID"));
            }
            catch (System.Exception)
            {
                doorID = -1;
            }

            //---------------------- Alarm Type
            try
            {
                alarmType = GetElementValue(doc, "Type");
            }
            catch (System.Exception)
            {
                alarmType = "";
            }

            Boolean logProcessed = false;
            try
            {
                if (m_AlarmLogCallBack != null)
                    logProcessed = m_AlarmLogCallBack(termType, termId, serialNumber, transId, new DateTime(year, month, day, hour, minute, second), userID, doorID, alarmType);
            }
            catch (Exception)
            {
            }
            if (logProcessed)
                SendReply("UploadedLog", transId);
            else
                SendReply("Error", transId);
        }

        private void OnPing(XmlDocument doc, String termType, Int32 termId, String serialNumber, Int32 transId)
        {
            SendReply("KeptAlive", transId);
            if (m_PingCallBack != null)
                m_PingCallBack(termType, termId, serialNumber, transId);
        }

        // Parse a message
        private void ParseMessage(string message)
        {
            XmlDocument doc = new XmlDocument();

            String termType;
            Int32 termId;
            String serialNumber;
            String eventType;
            Int32 transId;

            doc.Load(new StringReader(message));

            //----------------- Terminal type
            try
            {
                termType = GetElementValue(doc, "TerminalType");
            }
            catch (System.Exception)
            {
                termType = "";
            }

            //----------------- Terminal ID
            try
            {
                termId = Int32.Parse(GetElementValue(doc, "TerminalID"));
            }
            catch (System.Exception)
            {
                termId = -1;
            }

            //----------------- Serial Number
            try
            {
                serialNumber = GetElementValue(doc, "DeviceSerialNo");
            }
            catch (System.Exception)
            {
                serialNumber = "";
            }

            //----------------- Transaction ID
            try
            {
                transId = Int32.Parse(GetElementValue(doc, "TransID"));
            }
            catch (System.Exception)
            {
                transId = -1;
            }

            //------------------ Event
            try
            {
                eventType = GetElementValue(doc, "Event");
            }
            catch (System.Exception)
            {
                eventType = "";
            }

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
                    OnPing(doc, termType, termId, serialNumber, transId);
                    break;
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
            try
            {
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
            catch
            {
                term.Dispose();
            }
        }
    }

    public  class AttendanceUpSynchronous
    {
        AttendanceUpdateLogServer m_LogServer;          // Log Server
        Boolean m_Running=false;              // Is Running Monitor Thread?
        ManualResetEvent m_StopEvent;   // stop event
       
        public Action<string> ReportUpdataMsg { get; set; }
      
        private UInt16 _portNum = 5005;
        /// <summary>
        /// 扫描端口
        /// </summary>
        public UInt16 PortNum
        {
            set { _portNum = value; }
            get { return _portNum; }
        }
        #region 数据库入存
        /// <summary>
        /// 所有用户列表
        /// </summary>
        private  List<AttendFingerPrintDataInTimeModel> AllUserList = new List<AttendFingerPrintDataInTimeModel>();

        /// <summary>
        /// 刷新所有用户列表
        /// </summary>
        private void RefreshUserList()
        {
            try
            {
                AllUserList.Clear();
                foreach (DataRow dr in DbHelper.Hrm.LoadTable("SELECT  WorkerId,Name,CardID FROM  Archives_EmployeeIdentityInfo WHERE(WorkingStatus = '在职')").Rows)
                {
                    var tem = new AttendFingerPrintDataInTimeModel();
                    if (dr["WorkerId"] != null && dr["WorkerId"].ToString() != "")
                    {
                        tem.WorkerId = dr["WorkerId"].ToString().Trim();
                    }

                    if (dr["Name"] != null && dr["Name"].ToString() != "")
                    {
                        tem.WorkerName  = dr["Name"].ToString().Trim();
                    }
                    if (dr["CardID"] != null && dr["CardID"].ToString() != "")
                    {
                        tem.CardID = dr["CardID"].ToString().Trim();
                    }
                    AllUserList.Add(tem);
                }
            }
            catch
            {
                //使用邮件发送错误信息
            }
        }
        /// <summary>
        /// 存入到数据数库内
        /// </summary>
        /// <param name="UserID"></param>
        /// <param name="anVerifyMode"></param>
        /// <param name="anLogDate"></param>
        /// <param name="astrSerialNo"></param>
        private  bool Add_FingerPrintDataInTime(long UserID, string anVerifyMode, DateTime anLogDate, string astrSerialNo)
        {
           
            try
            {
                bool returnBool = false;
                if (AllUserList == null && AllUserList.Count == 0)
                {
                    RefreshUserList();
                }
                var userInfo = AllUserList.FirstOrDefault(m => m.WorkerId == UserID.ToString("000000"));
                if (userInfo != null)
                {
                    var tem = new AttendFingerPrintDataInTimeModel();
                    tem.WorkerId = userInfo.WorkerId;
                    tem.WorkerName = userInfo.WorkerName;
                    tem.CardID = userInfo.CardID;
                    if (anVerifyMode == "FP")
                        tem.CardType = "指纹";
                    else if (anVerifyMode == "Face")
                        tem.CardType = "脸部";
                    else if (anVerifyMode == "")
                        tem.CardType = "卡片";
                    tem.SlodCardTime = anLogDate;
                    tem.SlodCardDate = anLogDate.Date;
                    string strSql = string.Format("INSERT INTO Attendance_FingerPrintDataInTime VALUES ('{0}', '{1}', '{2}', '{3}', '{4}', '{5}')",
                        tem.WorkerId, tem.WorkerName, tem.CardID, tem.CardType, tem.SlodCardTime, tem.SlodCardDate);
                    returnBool = DbHelper.Hrm.ExecuteNonQuery(strSql.ToString()) > 1 ? true : false;
                    
                   
                }
                else
                {
                    RefreshUserList();
                    userInfo = AllUserList.FirstOrDefault(m => m.WorkerId == UserID.ToString("000000"));
                }
                return returnBool;
            }
            catch (Exception )
            {
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
                msg += " SN=" + serialNumber + "] ";
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
               if(! Add_FingerPrintDataInTime(userID, verifyMode, logTime, serialNumber))
                //BeginInvoke(new delegateAddEvent(OnAddEvent), msg);
               FileOperationExtension.AppendFile(@"C:\Sbx\" + logTime.ToDate().ToString("yyyy-MM-dd") + ".txt", msg);
                //BeginInvoke(new delegateAddEvent(OnAddEvent), _msg);
                //上专数据到服务器上
                if (ReportUpdataMsg != null)
                {
                    ReportUpdataMsg(msg);
                }
                return true;
                
            }
            catch (Exception)
            {
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
                msg += " SN=" + serialNumber + "] ";
                msg += "AdminLog";
                msg += "(" + transactionID.ToString() + ") ";
                msg += logTime.ToString() + ", ";
                msg += "AdminID=" + String.Format("{0}, ", adminID);
                msg += "UserID=" + String.Format("{0}, ", userID);
                msg += "Action=" + action + ", ";
                msg += "Status=" + String.Format("{0:D}", result);
                if (ReportUpdataMsg != null)
                {
                    ReportUpdataMsg(msg);
                }
                //BeginInvoke(new delegateAddEvent(OnAddEvent), msg);
                if (ReportUpdataMsg != null)
                {
                    ReportUpdataMsg(msg);
                }

                return true;
            }
            catch (Exception)
            {
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
                if (ReportUpdataMsg != null)
                {
                    ReportUpdataMsg(msg);
                }
                //BeginInvoke(new delegateAddEvent(OnAddEvent), msg);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public void OnPing(String terminalType, Int32 terminalID, String serialNumber, Int32 transactionID)
        {
            String msg;
            msg = "[" + terminalType + ":";
            msg += terminalID.ToString();
            msg += " SN=" + serialNumber + "] ";
            msg += "KeepAlive";
            msg += "(" + transactionID.ToString() + ") ";
            if (ReportUpdataMsg != null)
            {
                ReportUpdataMsg(msg);
            }
            //BeginInvoke(new delegateAddEvent(OnAddEvent), msg);
        }
        #endregion

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
                Thread.Sleep(1000);  // Simulate some lengthy operations.
            }
            m_LogServer.Dispose();  // Dispose log server
            m_StopEvent.Set();
            // Watch stop signal.
            //Signal the stopped event.
        }
    }
}
