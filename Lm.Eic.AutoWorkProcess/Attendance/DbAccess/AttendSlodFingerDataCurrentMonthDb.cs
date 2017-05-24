using Lm.Eic.Uti.Common.YleeDbHandler;
using Lm.Eic.Uti.Common.YleeExtension.Conversion;
using Lm.Eic.Uti.Common.YleeExtension.FileOperation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lm.Eic.AutoWorkProcess.Attendance.DbAccess
{
    /// <summary>
    /// 考勤指纹数据处理器
    /// </summary>
    internal class AttendFingerPrintDataDbHandler
    {
        /// <summary>
        /// 按日期载入实时考勤数据
        /// </summary>
        /// <param name="slotCardDate"></param>
        /// <returns></returns>
        internal static List<AttendFingerPrintDataInTimeModel> LoadDatas(DateTime slotCardDate)
        {
            StringBuilder sbSql = new StringBuilder();
            sbSql.Append("SELECT  WorkerId, WorkerName, CardID, CardType, SlodCardTime, SlodCardDate ");
            sbSql.AppendFormat(" FROM Attendance_FingerPrintDataInTime where SlodCardDate='{0}'", slotCardDate);
            return DbHelper.Hrm.LoadEntities<AttendFingerPrintDataInTimeModel>(sbSql.ToString());
        }
        /// <summary>
        /// 删除数据
        /// </summary>
        /// <param name="entities"></param>
        internal static int BackupData(List<AttendFingerPrintDataInTimeModel> entities)
        {
            int record = 0;
            entities.ForEach(entity =>
            {
                if (InsertDataToLib(entity) > 0)
                {
                    record += Delete(entity);
                }
            });
            return record;
        }
        private static int InsertDataToLib(AttendFingerPrintDataInTimeModel entity)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("INSERT INTO Attendance_FingerPrintDataInTimeLib  (WorkerId,WorkerName,CardID,CardType,SlodCardTime,SlodCardDate)");
            sb.AppendFormat(" values ('{0}',", entity.WorkerId);
            sb.AppendFormat("'{0}',", entity.WorkerName);
            sb.AppendFormat("'{0}',", entity.CardID);
            sb.AppendFormat("'{0}',", entity.CardType);
            sb.AppendFormat("'{0}',", entity.SlodCardTime);
            sb.AppendFormat("'{0}')", entity.SlodCardDate);
            return DbHelper.Hrm.ExecuteNonQuery(sb.ToString());
        }
        /// <summary>
        /// 向考勤中插入指纹数据
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        internal static int InsertDataTo(AttendFingerPrintDataInTimeModel entity)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("INSERT INTO Attendance_FingerPrintDataInTime (WorkerId,WorkerName,CardID,CardType,SlodCardTime,SlodCardDate)");
            sb.AppendFormat(" values ('{0}',", entity.WorkerId);
            sb.AppendFormat("'{0}',", entity.WorkerName);
            sb.AppendFormat("'{0}',", entity.CardID);
            sb.AppendFormat("'{0}',", entity.CardType);
            sb.AppendFormat("'{0}',", entity.SlodCardTime);
            sb.AppendFormat("'{0}')", entity.SlodCardDate);
            int record = DbHelper.Hrm.ExecuteNonQuery(sb.ToString());
            if (record == 0)
            {
                ExceptionDataDbHandler.BackupAttendanceDataToFile(entity);
            }
            return record;
        }

        private static int Delete(AttendFingerPrintDataInTimeModel entity)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("Delete From Attendance_FingerPrintDataInTime");
            sb.AppendFormat("  where WorkerId='{0}' And SlodCardTime='{1}'", entity.WorkerId, entity.SlodCardTime);
            return DbHelper.Hrm.ExecuteNonQuery(sb.ToString());
        }

        /// <summary>
        /// 存储没有档案信息人员数据
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        internal static int StoreNoIdentityWorkerInfo(AttendFingerPrintDataInTimeModel entity)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("INSERT INTO Archives_ForgetInputWorkerInfo  (WorkerId,WorkerName,OpDate,OpTime)");
            sb.AppendFormat(" values ('{0}',", entity.WorkerId);
            sb.AppendFormat("'{0}',", entity.WorkerName);
            sb.AppendFormat("'{0}',", DateTime.Now.ToDate());
            sb.AppendFormat("'{0}')", DateTime.Now.ToDateTime());
            return DbHelper.Hrm.ExecuteNonQuery(sb.ToString());
        }
    }
    /// <summary>
    /// 总考勤数据处理器
    /// </summary>
    internal class AttendSlodFingerDataCurrentMonthDbHandler
    {
        /// <summary>
        /// 按日期载入当月考勤数据
        /// </summary>
        /// <param name="attendanceDate"></param>
        /// <returns></returns>
        internal static List<AttendSlodFingerDataCurrentMonthModel> LoadAttendanceDatas(DateTime attendanceDate)
        {
            StringBuilder sbSql = new StringBuilder();
            sbSql.Append("SELECT WorkerId, WorkerName, Department, ClassType, AttendanceDate, CardID, CardType, YearMonth, WeekDay,SlotCardTime1, SlotCardTime2, SlotCardTime ");
            sbSql.AppendFormat(" FROM  Attendance_SlodFingerDataCurrentMonth where AttendanceDate='{0}'", attendanceDate);
            return DbHelper.Hrm.LoadEntities<AttendSlodFingerDataCurrentMonthModel>(sbSql.ToString());
        }
        /// <summary>
        /// 更改考勤时间2
        /// </summary>
        /// <param name="slodCardTime"></param>
        /// <param name="slodCardTime2"></param>
        /// <returns></returns>
        internal static int UpdateSlotCardTime2(string slodCardTime, string slodCardTime2, string workerId, DateTime attendanceDate)
        {
            StringBuilder sbSql = new StringBuilder();
            sbSql.AppendFormat("Update Attendance_SlodFingerDataCurrentMonth set SlotCardTime2='{0}', SlotCardTime='{1}' where WorkerId='{2}' And AttendanceDate='{3}'", slodCardTime2, slodCardTime, workerId, attendanceDate);
            return DbHelper.Hrm.ExecuteNonQuery(sbSql.ToString());
        }
        /// <summary>
        /// 更改考勤时间1
        /// </summary>
        /// <param name="slodCardTime"></param>
        /// <param name="slodCardTime1"></param>
        /// <returns></returns>
        internal static int UpdateSlotCardTime1(string slodCardTime, string slodCardTime1, string workerId, DateTime attendanceDate)
        {
            StringBuilder sbSql = new StringBuilder();
            sbSql.AppendFormat("Update Attendance_SlodFingerDataCurrentMonth set SlotCardTime1='{0}', SlotCardTime='{1}' where WorkerId='{2}' And AttendanceDate='{3}'", slodCardTime1, slodCardTime, workerId, attendanceDate);
            return DbHelper.Hrm.ExecuteNonQuery(sbSql.ToString());
        }
        /// <summary>
        /// 插入考勤数据
        /// </summary>
        /// <param name="mdl"></param>
        /// <returns></returns>
        internal static int Insert(AttendSlodFingerDataCurrentMonthModel mdl)
        {
            StringBuilder sbSql = new StringBuilder();
            sbSql.Append("Insert  Attendance_SlodFingerDataCurrentMonth (WorkerId, WorkerName, Department, ClassType, AttendanceDate, CardID, CardType, YearMonth, WeekDay,SlotCardTime1, SlotCardTime2, SlotCardTime) values (");
            sbSql.AppendFormat("'{0}',", mdl.WorkerId);
            sbSql.AppendFormat("'{0}',", mdl.WorkerName);
            sbSql.AppendFormat("'{0}',", mdl.Department);
            sbSql.AppendFormat("'{0}',", mdl.ClassType);
            sbSql.AppendFormat("'{0}',", mdl.AttendanceDate);
            sbSql.AppendFormat("'{0}',", mdl.CardID);
            sbSql.AppendFormat("'{0}',", mdl.CardType);
            sbSql.AppendFormat("'{0}',", mdl.YearMonth);
            sbSql.AppendFormat("'{0}',", mdl.WeekDay);
            sbSql.AppendFormat("'{0}',", mdl.SlotCardTime1);
            sbSql.AppendFormat("'{0}',", mdl.SlotCardTime2);
            sbSql.AppendFormat("'{0}')", mdl.SlotCardTime);
            return DbHelper.Hrm.ExecuteNonQuery(sbSql.ToString());
        }
        /// <summary>
        /// 获取班别信息
        /// </summary>
        /// <param name="workerId"></param>
        /// <param name="attendanceDate"></param>
        /// <returns></returns>
        internal static ClassTypeModel GetClassType(string workerId, DateTime attendanceDate)
        {
            StringBuilder sbSql = new StringBuilder();
            sbSql.Append("SELECT WorkerId,ClassType FROM Attendance_ClassTypeDetail ");
            sbSql.AppendFormat(" where WorkerId='{0}' And DateAt='{1}'", workerId, attendanceDate);
            var datas = DbHelper.Hrm.LoadEntities<ClassTypeModel>(sbSql.ToString());
            if (datas != null && datas.Count > 0) return datas.FirstOrDefault();
            return null;
        }
        /// <summary>
        /// 载入部门信息
        /// </summary>
        /// <returns></returns>
        internal static List<DepartmentModel> GetDepartmentDatas()
        {
            StringBuilder sbSql = new StringBuilder();
            sbSql.Append("SELECT DataNodeName, DataNodeText FROM  Config_DataDictionary where TreeModuleKey='Organization' And AboutCategory='HrDepartmentSet'");
            return DbHelper.LmProductMaster.LoadEntities<DepartmentModel>(sbSql.ToString());
        }
    }

    internal class BackupFingerDataDbHandler
    {
        private static int InsertDataTo(AttendFingerPrintDataInTimeModel entity)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("INSERT INTO Attendance_FingerPrintDataInTimeBackup (WorkerId,WorkerName,CardID,CardType,SlodCardTime,SlodCardDate)");
            sb.AppendFormat(" values ('{0}',", entity.WorkerId);
            sb.AppendFormat("'{0}',", entity.WorkerName);
            sb.AppendFormat("'{0}',", entity.CardID);
            sb.AppendFormat("'{0}',", entity.CardType);
            sb.AppendFormat("'{0}',", entity.SlodCardTime);
            sb.AppendFormat("'{0}')", entity.SlodCardDate);
            return DbHelper.Hrm.ExecuteNonQuery(sb.ToString());
        }
        internal static int BackupData(List<AttendFingerPrintDataInTimeModel> entities)
        {
            int record = 0;
            entities.ForEach(entity =>
            {
                record += InsertDataTo(entity);
            });
            return record;
        }
    }
    /// <summary>
    /// 上传异常数据处理句柄
    /// </summary>
    internal class ExceptionDataDbHandler
    {
        /// <summary>
        /// 上传异常数据存储文件夹
        /// </summary>
        private static string dataLogFilePath = @"C:\AutoProcessWorker\AttendanceData\";

        /// <summary>
        /// 备份异常考勤数据至本机
        /// </summary>
        /// <param name="entity"></param>
        internal static void BackupAttendanceDataToFile(AttendFingerPrintDataInTimeModel entity)
        {
            string fileName = Path.Combine(dataLogFilePath, DateTime.Now.ToString("yyyyMMdd") + ".txt");
            FileDbHelper.AppendFile<AttendFingerPrintDataInTimeModel>(entity, fileName);
        }
        /// <summary>
        /// 将数据持久化到服务器上
        /// </summary>
        internal static void PersistanceDataToServer()
        {
            List<Dictionary<string, object>> datas = new List<Dictionary<string, object>>();
            try
            {
                dataLogFilePath.GetFiles().ForEach(file =>
                {
                    FileDbHelper.GetEntityDicFromTxtFile(file).ForEach(dic =>
                    {
                        DbHelper.Hrm.Insert(dic);
                    });
                    File.Delete(file);
                });
            }
            catch (System.Exception ex)
            {
                ErrorMessageTracer.LogErrorMsgToFile("PersistanceDataToServer", ex);
            }
        }
    }
}
