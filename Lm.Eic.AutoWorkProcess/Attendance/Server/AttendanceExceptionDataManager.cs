using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lm.Eic.AutoWorkProcess.Attendance.DbAccess;

namespace Lm.Eic.AutoWorkProcess.Attendance.Server
{
    public class AttendanceExceptionDataManager
    {
        /// <summary>
        /// 自动处理考勤异常数据
        /// </summary>
        public static void AutoHandleAttendanceExceptionData()
        {
            ExceptionDataDbHandler.PersistanceDataToServer();
        }
    }
}
