using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Threading.Tasks;
using Lm.Eic.Uti.Common.YleeDbHandler;
using Lm.Eic.Uti.Common.YleeExtension.Conversion;
using System.IO;
namespace Lm.Eic.AutoWorkProcess.Attendance
{
    /// <summary>
    /// 考勤数据处理器
    /// </summary>
    public class AttendanceDataManger
    {
        /// <summary>
        /// 自动处理考勤数据
        /// </summary>
        public int AutoProcessAttendanceDatas(DateTime slotCardDate)
        {
            slotCardDate = slotCardDate.ToDate();
            //处理总记录数
            int totalRecord = 0, record = 0;
            //载入实时指纹考勤中的数据
            var fingerPrintDatas = AttendFingerPrintDataHandler.LoadDatas(slotCardDate);
            if (fingerPrintDatas == null || fingerPrintDatas.Count == 0) return totalRecord;
            //载入汇总的考勤数据
            var dayAttendDatas = AttendSlodFingerDataCurrentMonthHandler.LoadAttendanceDatas(slotCardDate);
            //获取所有人员信息
            var workers = GetWorkerInfos();
            var departments = AttendSlodFingerDataCurrentMonthHandler.GetDepartmentDatas();
            //中间时间
            DateTime middleTime = new DateTime(slotCardDate.Year, slotCardDate.Month, slotCardDate.Day, 13, 0, 0);
            ArWorkerInfo worker = null;
            //将考勤中数据中的人进行分组
            List<string> attendWorkerIdList = fingerPrintDatas.Select(e => e.WorkerId).Distinct().ToList();
            List<AttendFingerPrintDataInTimeModel> attendDataPerWorker = null;
            AttendSlodFingerDataCurrentMonthModel currentAttendData = null;
            ClassTypeModel ctm = null;
            DepartmentModel depm = null;
            attendWorkerIdList.ForEach(workerId => {
                record = 0;
                //获取每个人的信息
                worker = workers.FirstOrDefault(w => w.WorkerId == workerId);
                //从实时考勤数据表中获取该员工的考勤数据
                attendDataPerWorker = fingerPrintDatas.FindAll(f => f.WorkerId == workerId).OrderBy(o => o.SlodCardTime).ToList();
                //从考勤中获取该员工的考勤数据
                currentAttendData = dayAttendDatas.FirstOrDefault(e => e.WorkerId == workerId);//从内存中进行查找
                if (worker != null)
                {
                    ctm = AttendSlodFingerDataCurrentMonthHandler.GetClassType(workerId, slotCardDate);
                    depm = departments.FirstOrDefault(d => d.DataNodeName == worker.Department);
                    worker.ClassType = ctm == null ? "白班" : ctm.ClassType;
                    worker.Department = depm == null ? worker.Department : depm.DataNodeText;

                    int len = attendDataPerWorker.Count;
                    for (int i = 0; i < len; i++)
                    {
                        var attendance = attendDataPerWorker[i];

                        //如果考勤数据表没有该人员的考勤数据
                        if (currentAttendData == null)
                        {
                            //则初始化考勤数据
                            record += InitAttendData(attendance, worker, attendance.SlodCardTime, out currentAttendData, middleTime);
                        }
                        else
                        {
                            //反之则合并数据
                            record += MergeAttendTime(currentAttendData, attendance.SlodCardTime);
                        }
                    }
                    if (record == len)//如果处理记录与目标数量一致则进行备份数据
                    {
                        AttendFingerPrintDataHandler.BackupData(attendDataPerWorker, record);
                        //从内存中移除数据，减少查询时间
                        workers.Remove(worker);
                        //从内存中移除该人员的考勤数据，减少查询时间
                        attendDataPerWorker.ForEach(m => fingerPrintDatas.Remove(m));
                        totalRecord += record;
                        record = 0;
                    }
                }
                else
                {
                   AttendFingerPrintDataHandler.StoreNoIdentityWorkerInfo(attendDataPerWorker[0]);
                }
            });
            return totalRecord;
        }

        private List<ArWorkerInfo> GetWorkerInfos()
        {
            string sql = "Select IdentityID, WorkerId,Name,Post, PostNature,Organizetion, Department,ClassType,PersonalPicture from Archives_EmployeeIdentityInfo ";
            return DbHelper.Hrm.LoadEntities<ArWorkerInfo>(sql);
        }
        /// <summary>
        /// 重新排序连接的时间字符串
        /// </summary>
        /// <param name="slotCardTime"></param>
        /// <param name="attendTime"></param>
        /// <returns></returns>
        private string ConcatSlotCardTime(string slotCardTime, DateTime attendTime)
        {
            List<string> timeStrList = null;
            var cardTimeList = slotCardTime.Split(',');
            if (cardTimeList.Length == 0)
            {
                slotCardTime = string.Format("{0},{1}", slotCardTime, attendTime.ToString("HH:mm"));
                cardTimeList = slotCardTime.Split(',');
                timeStrList = cardTimeList.ToList();
            }
            else
            {
                timeStrList = cardTimeList.ToList();
                timeStrList.Add(attendTime.ToString("HH:mm"));
            }
            timeStrList.Sort();
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < timeStrList.Count; i++)
            {
                sb.Append(timeStrList[i] + ",");
            }
            return sb.ToString().Trim(',');
        }
        /// <summary>
        /// 合并考勤时间
        /// </summary>
        /// <param name="currentAttendData"></param>
        /// <param name="slodCardTime"></param>
        /// <returns></returns>
        private int MergeAttendTime(AttendSlodFingerDataCurrentMonthModel currentAttendData, DateTime slodCardTime)
        {
            int record = 0;
            //若请假流程在前，则会先有考勤数据记录，但没有考勤时间，所以从刷卡时间1开始填写
            currentAttendData.SlotCardTime = ConcatSlotCardTime(currentAttendData.SlotCardTime, slodCardTime);

            if (currentAttendData.SlotCardTime1 == null || currentAttendData.SlotCardTime1.Length == 0)
            {
                record = UpdateSlotCardTime1(currentAttendData, slodCardTime, currentAttendData.SlotCardTime);
            }
            else
            {
                record = UpdateSlotCardTime2(currentAttendData, slodCardTime, currentAttendData.SlotCardTime);
            }
            return record;
        }
        /// <summary>
        /// 更新刷卡时间2
        /// </summary>
        /// <param name="currentAttendData"></param>
        /// <param name="slodCardTime"></param>
        /// <param name="cardtime"></param>
        /// <returns></returns>
        private int UpdateSlotCardTime2(AttendSlodFingerDataCurrentMonthModel currentAttendData, DateTime slodCardTime, string cardtime)
        {
            //直接进行更新替代
            string uSlotCardTime2 = slodCardTime.ToString("yyyy-MM-dd HH:mm");
            string uSlotCardTime = currentAttendData.SlotCardTime == null || currentAttendData.SlotCardTime.Length == 0 ? slodCardTime.ToString("HH:mm") : cardtime;
            return AttendSlodFingerDataCurrentMonthHandler.UpdateSlotCardTime2(uSlotCardTime, uSlotCardTime2, currentAttendData.WorkerId,currentAttendData.AttendanceDate);
        }
        /// <summary>
        /// 更新刷卡时间1
        /// </summary>
        /// <param name="currentAttendData"></param>
        /// <param name="slodCardTime"></param>
        /// <param name="cardtime"></param>
        /// <returns></returns>
        private int UpdateSlotCardTime1(AttendSlodFingerDataCurrentMonthModel currentAttendData, DateTime slodCardTime, string cardtime)
        {
            var ctimes = cardtime.Split(',').ToList();
            if (ctimes.Count >= 2)
            {
                ctimes.Sort();
                string cardTimeStr = currentAttendData.AttendanceDate.ToDateStr() + " " + ctimes[ctimes.Count - 1];
                string uSlotCardTime2 = cardTimeStr;
                string uSlotCardTime = currentAttendData.SlotCardTime == null || currentAttendData.SlotCardTime.Length == 0 ? slodCardTime.ToString("HH:mm") : cardtime;
                return AttendSlodFingerDataCurrentMonthHandler.UpdateSlotCardTime2(uSlotCardTime, uSlotCardTime2, currentAttendData.WorkerId,currentAttendData.AttendanceDate);
            }
            else
            {
                string uSlotCardTime1 = currentAttendData.SlotCardTime1 == null || currentAttendData.SlotCardTime1.Length == 0 ? slodCardTime.ToString("yyyy-MM-dd HH:mm") : currentAttendData.SlotCardTime1;
                string uSlotCardTime = currentAttendData.SlotCardTime == null || currentAttendData.SlotCardTime.Length == 0 ? slodCardTime.ToString("HH:mm") : cardtime;
                return AttendSlodFingerDataCurrentMonthHandler.UpdateSlotCardTime1(uSlotCardTime, uSlotCardTime1, currentAttendData.WorkerId,currentAttendData.AttendanceDate);
            }
        }

        /// <summary>
        /// 初次插入数据
        /// </summary>
        /// <param name="attendTimeMdl"></param>
        /// <param name="worker"></param>
        /// <param name="slodCardTime"></param>
        /// <returns></returns>
        private int InitAttendData(AttendFingerPrintDataInTimeModel attendTimeMdl, ArWorkerInfo worker, DateTime slodCardTime, out AttendSlodFingerDataCurrentMonthModel initMdl, DateTime middleTime)
        {
            initMdl = null;
            int record = 0;
            var mdl = CreateAttendDataModel(attendTimeMdl, worker, slodCardTime);
            //首次赋值需要加中间判定时间
            if (slodCardTime > middleTime)
                mdl.SlotCardTime2 = slodCardTime.ToString("yyyy-MM-dd HH:mm");
            else
                mdl.SlotCardTime1 = slodCardTime.ToString("yyyy-MM-dd HH:mm");
            record = AttendSlodFingerDataCurrentMonthHandler.Insert(mdl);
            if (record == 1)
            {
                initMdl = CreateAttendDataModel(attendTimeMdl, worker, slodCardTime);
                initMdl.SlotCardTime1 = mdl.SlotCardTime1;
            }
            return record;
        }

        private AttendSlodFingerDataCurrentMonthModel CreateAttendDataModel(AttendFingerPrintDataInTimeModel attendTimeMdl, ArWorkerInfo worker, DateTime slodCardTime)
        {
            var mdl = new AttendSlodFingerDataCurrentMonthModel()
            {
                AttendanceDate = attendTimeMdl.SlodCardDate,
                WorkerId = worker.WorkerId,
                CardID = attendTimeMdl.CardID,
                CardType = attendTimeMdl.CardType,
                ClassType = worker.ClassType,
                Department = worker.Department,
                WorkerName = worker.Name,
                WeekDay = attendTimeMdl.SlodCardDate.DayOfWeek.ToString().ToChineseWeekDay(),
                YearMonth = slodCardTime.ToString("yyyyMM"),
                SlotCardTime = slodCardTime.ToString("HH:mm"),
            };
            return mdl;
        }
    
        #region configuration
        /// <summary>
        /// 初始化配置文件
        /// </summary>
        public void InitConfigurationFile()
        {
           var ttgt = new TimerTarget() { THour = 0, TEndSecond = 13, TMinute = 30, TStartSecond = 10 };
            string filePath = @"C:\AutoProcessWorker\Configuration\";
            if (!Directory.Exists(filePath)) Directory.CreateDirectory(filePath);
            string fileName = filePath + "TimeSetCongig.xml";
            XElement root = new XElement("AutoPrecessWork",
               new XAttribute(XNamespace.Xmlns + "xsi","lm"),
               new XAttribute(XNamespace.Xmlns + "eic", "eic"),
               new XAttribute("lmschemaLocation", "http://www.ieee.org/ATML/2007/TestResults TestResults.xsd"));
            XElement timeSet = new XElement("TimeSetConfig", new XElement("TimeSetter", new XAttribute("THour", ttgt.THour)
                ,new XAttribute("TMinute",ttgt.TMinute),new XAttribute("TStartSecond",ttgt.TStartSecond),new XAttribute("TEndSecond",ttgt.TEndSecond)));

            root.Add(timeSet);
            XDocument doc = new XDocument(new XDeclaration("1.0", "gb2312", ""), root);
            doc.Save(fileName, SaveOptions.DisableFormatting);
        }
        //载入时间参数配置信息
        public TimerTarget LoadTimerSetConfigInfo()
        {
            string fileName = @"C:\AutoProcessWorker\Configuration\TimeSetCongig.xml";
            XDocument data = XDocument.Load(fileName);
            XElement root = data.Root;
            XElement timeSet = root.Descendants().FirstOrDefault().Descendants().FirstOrDefault();
            TimerTarget tt = new TimerTarget() {
                THour = timeSet.Attribute("THour").Value.ToInt(),
                TMinute= timeSet.Attribute("TMinute").Value.ToInt(),
                TStartSecond = timeSet.Attribute("TStartSecond").Value.ToInt(),
                TEndSecond = timeSet.Attribute("TEndSecond").Value.ToInt(),
            };
            return tt;
        }
        #endregion

        #region Init Datas

        public void InitDatas()
        {
            for (int day = 1; day <=15; day++)
            {
                DateTime dt = new DateTime(2017, 2, day, 0, 0, 0);
                var datas = this.LoadDatas(dt);
                if (datas != null && datas.Count > 0)
                {
                    datas.ForEach(d => {
                        if (this.InsertDataTo(d)>0)
                        {
                            this.Delete(d);
                        }
                    });
                }
                this.DeleteAttendanceDatas(dt);
                this.AutoProcessAttendanceDatas(dt);
            }

        }
        /// <summary>
        /// 按日期载入实时考勤数据
        /// </summary>
        /// <param name="slotCardDate"></param>
        /// <returns></returns>
        private List<AttendFingerPrintDataInTimeModel> LoadDatas(DateTime slotCardDate)
        {
            StringBuilder sbSql = new StringBuilder();
            sbSql.Append("SELECT  WorkerId, WorkerName, CardID, CardType, SlodCardTime, SlodCardDate ");
            sbSql.AppendFormat(" FROM Attendance_FingerPrintDataInTimeLib where SlodCardDate='{0}'", slotCardDate);
            return DbHelper.Hrm.LoadEntities<AttendFingerPrintDataInTimeModel>(sbSql.ToString());
        }
        private int InsertDataTo(AttendFingerPrintDataInTimeModel entity)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("INSERT INTO Attendance_FingerPrintDataInTime (WorkerId,WorkerName,CardID,CardType,SlodCardTime,SlodCardDate)");
            sb.AppendFormat(" values ('{0}',", entity.WorkerId);
            sb.AppendFormat("'{0}',", entity.WorkerName);
            sb.AppendFormat("'{0}',", entity.CardID);
            sb.AppendFormat("'{0}',", entity.CardType);
            sb.AppendFormat("'{0}',", entity.SlodCardTime);
            sb.AppendFormat("'{0}')", entity.SlodCardDate);
            return DbHelper.Hrm.ExecuteNonQuery(sb.ToString());
        }
        private int Delete(AttendFingerPrintDataInTimeModel entity)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("Delete From Attendance_FingerPrintDataInTimeLib");
            sb.AppendFormat("  where WorkerId='{0}' And SlodCardDate='{1}'", entity.WorkerId, entity.SlodCardDate);
            return DbHelper.Hrm.ExecuteNonQuery(sb.ToString());
        }
        /// <summary>
        /// 按日期载入当月考勤数据
        /// </summary>
        /// <param name="attendanceDate"></param>
        /// <returns></returns>
        private int DeleteAttendanceDatas(DateTime attendanceDate)
        {
            StringBuilder sbSql = new StringBuilder();
            sbSql.AppendFormat("Delete FROM  Attendance_SlodFingerDataCurrentMonth where AttendanceDate='{0}'", attendanceDate);
            return DbHelper.Hrm.ExecuteNonQuery(sbSql.ToString());
        }
        #endregion
    }
    /// <summary>
    /// 考勤指纹数据处理器
    /// </summary>
    internal class AttendFingerPrintDataHandler
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
        /// <param name="targetRecord"></param>
        internal static void BackupData(List<AttendFingerPrintDataInTimeModel> entities, int targetRecord)
        {
            int record = 0;
            entities.ForEach(entity => { record += InsertDataToLib(entity); });
            if (record == targetRecord)
            {
                var mdl = entities[0];
                Delete(mdl);
            }
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

        private static int Delete(AttendFingerPrintDataInTimeModel entity)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("Delete From Attendance_FingerPrintDataInTime");
            sb.AppendFormat("  where WorkerId='{0}' And SlodCardDate='{1}'", entity.WorkerId,entity.SlodCardDate);
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
            sb.AppendFormat("'{0}')", entity.WorkerName);
            sb.AppendFormat("'{0}')", DateTime.Now.ToDate());
            sb.AppendFormat("'{0}')", DateTime.Now.ToDateTime());
            return DbHelper.Hrm.ExecuteNonQuery(sb.ToString());
        }
    }
    /// <summary>
    /// 总考勤数据处理器
    /// </summary>
    internal class AttendSlodFingerDataCurrentMonthHandler
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
        internal static int UpdateSlotCardTime2(string slodCardTime,string slodCardTime2,string workerId,DateTime attendanceDate)
        {
            StringBuilder sbSql = new StringBuilder();
            sbSql.AppendFormat("Update Attendance_SlodFingerDataCurrentMonth set SlotCardTime2='{0}', SlotCardTime='{1}' where WorkerId='{2}' And AttendanceDate='{3}'", slodCardTime2, slodCardTime, workerId,attendanceDate);
            return DbHelper.Hrm.ExecuteNonQuery(sbSql.ToString());
        }
        /// <summary>
        /// 更改考勤时间1
        /// </summary>
        /// <param name="slodCardTime"></param>
        /// <param name="slodCardTime1"></param>
        /// <returns></returns>
        internal static int UpdateSlotCardTime1(string slodCardTime, string slodCardTime1,string workerId, DateTime attendanceDate)
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
            sbSql.AppendFormat(" where WorkerId='{0}' And DateAt='{1}'",workerId, attendanceDate);
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

    /// <summary>
    /// 目标时间模型
    /// </summary>
    public class TimerTarget
    {
        public int THour { get; set; }
        public int TMinute { get; set; }
        public int TStartSecond { get; set; }
        public int TEndSecond { get; set; }
    }
    public class DepartmentModel
    {
        /// <summary>
        /// 名称
        /// </summary>
        public string DataNodeName { get; set; }
        /// <summary>
        /// 文本信息
        /// </summary>
        public string DataNodeText { get; set; }
    }
}
