using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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
                //SbxLib.DbOption.DbOption.Add_FingerPrintDataInTime(userID, verifyMode, logTime, serialNumber);
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
