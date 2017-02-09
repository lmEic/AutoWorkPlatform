using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lm.Eic.AutoWorkProcess.AttendanceMachineUpdataServer
{
   public  class AttendanceUpSynchronous
    {

        public delegate void delegateAddEvent(string msg);
        public void OnAddEvent(String msg)
        { }

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
            String msg;
            msg = "[" + terminalType + ":";
            msg += terminalID.ToString();
            msg += " SN=" + serialNumber + "] ";
            msg += "KeepAlive";
            msg += "(" + transactionID.ToString() + ") ";
            // Application.Current.Dispatcher.BeginInvoke(new delegateAddEvent(OnAddEvent), msg);

            //BeginInvoke(new delegateAddEvent(OnAddEvent), msg);
        }
    }
}
