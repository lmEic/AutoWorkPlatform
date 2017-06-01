using Lm.Eic.Uti.Common.YleeDbHandler;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.Data.SqlClient;

namespace Lm.Eic.AutoWorkProcess.Attendance.DbAccess
{
    public static class WorkerDbManager
    {
        public static List<ArWorkerInfo> GetWorkerInfos()
        {
            string sql = "Select IdentityID, WorkerId,Name,CardID,Post, PostNature,Organizetion, Department,ClassType,PersonalPicture from Archives_EmployeeIdentityInfo ";
            return DbHelper.Hrm.LoadEntities<ArWorkerInfo>(sql);
        }
        public static List<ArEnrollUser> GetEnrollUsers()
        {
            string sql = "Select EnrollNumber as WorkerId,UserName as WorkerName,EMachineNumber as CardID from  Attendance_UserEnrollData ";
            return DbHelper.Hrm.LoadEntities<ArEnrollUser>(sql);
        }
        /// <summary>
        /// 从变动工号中获取员工信息
        /// </summary>
        /// <param name="enrollNum"></param>
        /// <returns></returns>
        public static ArEnrollUser GetUserFromChangeInfo(int enrollNum)
        {
            string workerId = enrollNum.ToString().PadLeft(6, '0');
            DataTable dt = DbHelper.Hrm.LoadTable(string.Format("Select OldWorkerId, WorkerName from Archives_WorkerIdChanged where OldWorkerId='{0}'", workerId));
            if (dt.Rows.Count > 0)
            {
                return new ArEnrollUser()
                {
                    WorkerId = enrollNum,
                    WorkerName = dt.Rows[0]["WorkerName"].ToString().Trim()
                };
            }
            return null;
        }


        public static List<string> GetUserEmails()
        {
            return DbHelper.LmProductMaster.LoadList("SELECT Distinct Email from  Config_MailInfo WHERE (ReceiveGrade <= 3)", "Email");
        }
    }
}
