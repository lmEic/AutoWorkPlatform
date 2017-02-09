using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Lm.Eic.Uti.Common.YleeDbHandler;
using Lm.Eic.AutoWorkProcess.Attendance;
using System.Data;

namespace Lm.Eic.AutoWorkProcess.AttendanceMachineUpdataServer
{
   public  class AttendanceUpSynchronous
    {

        public delegate void delegateAddEvent(string msg);
        AttendanceUpdateLogServer m_LogServer;          // Log Server
        Boolean m_Running;              // Is Running Monitor Thread?
        ManualResetEvent m_StopEvent;   //

        string _msg;
        public string Msg
        {
            set
            {  _msg=value ; }
            get
            {  return _msg; }
        }
        /// <summary>
        /// 所有用户列表
        /// </summary>
        private  List<AttendFingerPrintDataInTimeModel> AllUserList { get; set; } = new List<AttendFingerPrintDataInTimeModel>();

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

        public  void Add_FingerPrintDataInTime(long UserID, string anVerifyMode, DateTime anLogDate, string astrSerialNo)
        {
           
            try
            {
                if (AllUserList == null && AllUserList.Count == 0)
                {
                    RefreshUserList();
                }
                var userInfo = AllUserList.FirstOrDefault(m => m.WorkerId == UserID.ToString("000000"));
                if (userInfo == null)
                {
                    RefreshUserList();
                    userInfo = AllUserList.FirstOrDefault(m => m.WorkerId == UserID.ToString("000000"));
                }
                else
                {
                    var tem = new AttendFingerPrintDataInTimeModel(); 
                    tem.WorkerId = userInfo.WorkerId;
                    tem.WorkerName = userInfo.WorkerName ;
                    tem.CardID = userInfo.CardID;
                    if (anVerifyMode == "FP")
                        tem.CardType = "指纹";
                    else if (anVerifyMode == "Face")
                        tem.CardType = "脸部";
                    else if (anVerifyMode == "")
                        tem.CardType = "卡片";
                    tem.SlodCardTime = anLogDate;
                    tem.SlodCardDate = anLogDate.Date;
                    string strSql = $"INSERT INTO Attendance_FingerPrintDataInTime VALUES ('{tem.WorkerId}', '{tem.WorkerName}', '{tem.CardID}', '{tem.CardType}', '{tem.SlodCardTime}', '{tem.SlodCardDate}')";
                    DbHelper.Hrm.ExecuteNonQuery(strSql.ToString());
                  
                }
            }
            catch (Exception )
            {
                
            }
        }

        public Boolean OnTimeLog(String terminalType, Int32 terminalID, String serialNumber, Int32 transactionID, DateTime logTime,
            Int64 userID, Int32 doorID, String attendanceStatus, String verifyMode, Int32 jobCode, String antipass, Byte[] photo)
        {
            try
            {
                _msg = "[" + terminalType + ":";
                _msg += terminalID.ToString();
                _msg += " SN=" + serialNumber + "] ";
                _msg += "TimeLog";
                _msg += "(" + transactionID.ToString() + ") ";
                _msg += logTime.ToString() + ", ";
                _msg += "UserID=" + String.Format("{0}, ", userID);
                _msg += "Door=" + doorID.ToString() + ", ";
                _msg += "AttendStat=" + attendanceStatus + ", ";
                _msg += "VMode=" + verifyMode + ", ";
                _msg += "JobCode=" + jobCode.ToString() + ", ";
                _msg += "Antipass=" + antipass + ", ";
                if (photo == null)
                    _msg += "Photo=No";
                else
                {
                    _msg += "Photo=Yes ";
                    _msg += "(" + Convert.ToString(photo.Length) + "bytes)";
                }
                // BeginInvoke(new delegateAddEvent(OnAddEvent), msg);
               //上专数据到服务器上
                Add_FingerPrintDataInTime(userID, verifyMode, logTime, serialNumber);
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
            try
            {
                _msg = "[" + terminalType + ":";
                _msg += terminalID.ToString();
                _msg += " SN=" + serialNumber + "] ";
                _msg += "AdminLog";
                _msg += "(" + transactionID.ToString() + ") ";
                _msg += logTime.ToString() + ", ";
                _msg += "AdminID=" + String.Format("{0}, ", adminID);
                _msg += "UserID=" + String.Format("{0}, ", userID);
                _msg += "Action=" + action + ", ";
                _msg += "Status=" + String.Format("{0:D}", result);

                // BeginInvoke(new delegateAddEvent(OnAddEvent), msg);

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
            try
            {
                _msg = "[" + terminalType + ":";
                _msg += terminalID.ToString();
                _msg += " SN=" + serialNumber + "] ";
                _msg += "AlarmLog";
                _msg += "(" + transactionID.ToString() + ") ";
                _msg += logTime.ToString() + ", ";//'at'
                _msg += "UserID=" + String.Format("{0}, ", userID);
                _msg += "Door=" + doorID.ToString() + ", ";
                _msg += "Type=" + alarmType;
                // BeginInvoke(new delegateAddEvent(OnAddEvent), msg);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public void OnPing(String terminalType, Int32 terminalID, String serialNumber, Int32 transactionID)
        {
            _msg = "[" + terminalType + ":";
            _msg += terminalID.ToString();
            _msg += " SN=" + serialNumber + "] ";
            _msg += "KeepAlive";
            _msg += "(" + transactionID.ToString() + ") ";
          

            //BeginInvoke(new delegateAddEvent(OnAddEvent), msg);
        }


        private void MonitoringThread(object state)
        {
            UInt16 portNum = 5005; // Get port number.

            m_LogServer = new AttendanceUpdateLogServer(portNum, OnTimeLog, OnAdminLog, OnAlarmLog, OnPing);   // Create and start log server.

            // Watch stop signal.
            while (m_Running)
            {
                Thread.Sleep(1000);  // Simulate some lengthy operations.
            }

            m_LogServer.Dispose();  // Dispose log server
            m_StopEvent.Set();
            // Watch stop signal.
            // Signal the stopped event.
        }
    }
}
