using Lm.Eic.Uti.Common.YleeDbHandler;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lm.Eic.AutoWorkProcess.Attendance
{
    public static class WorkerManager
    {
        public static List<ArWorkerInfo> GetWorkerInfos()
        {
            string sql = "Select IdentityID, WorkerId,Name,CardID,Post, PostNature,Organizetion, Department,ClassType,PersonalPicture from Archives_EmployeeIdentityInfo ";
            return DbHelper.Hrm.LoadEntities<ArWorkerInfo>(sql);
        }
    }
}
