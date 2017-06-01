using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Threading.Tasks;
using Lm.Eic.Uti.Common.YleeDbHandler;
using Lm.Eic.Uti.Common.YleeExtension.Conversion;
using Lm.Eic.Uti.Common.YleeMessage.Email;
using System.IO;
using Lm.Eic.AutoWorkProcess;
using Lm.Eic.AutoWorkProcess.Attendance.DbAccess;

namespace Lm.Eic.AutoWorkProcess.Attendance.Server
{
    /// <summary>
    /// 考勤数据处理器
    /// </summary>
    public class AttendanceDataManger : AttendanceBase
    {
        /// <summary>
        /// 自动处理考勤数据
        /// </summary>
        public int AutoProcessAttendanceDatas(DateTime slotCardDate)
        {
            Log("开始汇总考勤数据...");
            slotCardDate = slotCardDate.ToDate();
            //处理总记录数
            int totalRecord = 0, record = 0;
            //载入实时指纹考勤中的数据
            Log("载入实时指纹考勤中的数据...");
            var fingerPrintDatas = AttendFingerPrintDataDbHandler.LoadDatas(slotCardDate);
            if (fingerPrintDatas == null || fingerPrintDatas.Count == 0) return totalRecord;
            Log("备份考勤数据...");
            BackupFingerDataDbHandler.BackupData(fingerPrintDatas);
            //载入汇总的考勤数据
            Log("载入汇总的考勤数据...");
            var dayAttendDatas = AttendSlodFingerDataCurrentMonthDbHandler.LoadAttendanceDatas(slotCardDate);
            //获取所有人员信息
            Log("获取所有人员信息...");
            var workers = WorkerDbManager.GetWorkerInfos();
            var departments = AttendSlodFingerDataCurrentMonthDbHandler.GetDepartmentDatas();
            //中间时间
            DateTime middleTime = new DateTime(slotCardDate.Year, slotCardDate.Month, slotCardDate.Day, 13, 0, 0);
            ArWorkerInfo worker = null;
            //将考勤中数据中的人进行分组
            Log("将考勤中数据中的人进行分组...");
            List<string> attendWorkerIdList = fingerPrintDatas.Select(e => e.WorkerId).Distinct().ToList();
            List<AttendFingerPrintDataInTimeModel> attendDataPerWorker = null;
            AttendSlodFingerDataCurrentMonthModel currentAttendData = null;
            ClassTypeModel ctm = null;
            DepartmentModel depm = null;
            int progressIndex = 0;
            Log("开始处理每个人的考勤数据...");
            attendWorkerIdList.ForEach(workerId =>
            {
                progressIndex++;
                record = 0;
                //获取每个人的信息
                worker = workers.FirstOrDefault(w => w.WorkerId == workerId);
                if (worker == null)
                {
                    var m = this.GetWorkerChangeInfo(workerId);
                    if (m != null)
                        worker = workers.FirstOrDefault(w => w.WorkerId == m.NewWorkerId);
                }
                LogProgressUser(progressIndex, "正在处理考勤数据，员工工号：", workerId);
                //从实时考勤数据表中获取该员工的考勤数据
                attendDataPerWorker = fingerPrintDatas.FindAll(f => f.WorkerId == workerId).OrderBy(o => o.SlodCardTime).ToList();
                //从考勤中获取该员工的考勤数据
                currentAttendData = dayAttendDatas.FirstOrDefault(e => e.WorkerId == workerId);//从内存中进行查找
                if (worker != null)
                {
                    ctm = AttendSlodFingerDataCurrentMonthDbHandler.GetClassType(workerId, slotCardDate);
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
                    LogProgress(string.Format("处理进度:len:{0}Vs record:{1}", len, record));
                    if (record == len)//如果处理记录与目标数量一致则进行备份数据
                    {
                        LogProgressUser(progressIndex, "处理完毕，进行数据备份，员工工号：", workerId);
                        AttendFingerPrintDataDbHandler.BackupData(attendDataPerWorker);
                        //从内存中移除数据，减少查询时间
                        //workers.Remove(worker);
                        //从内存中移除该人员的考勤数据，减少查询时间
                        attendDataPerWorker.ForEach(m => fingerPrintDatas.Remove(m));
                        totalRecord += record;
                        record = 0;
                    }
                }
                else
                {
                    AttendFingerPrintDataDbHandler.StoreNoIdentityWorkerInfo(attendDataPerWorker[0]);
                }
            });
            Log("汇总完毕!");
            return totalRecord;
        }
        private WorkerChangeModel GetWorkerChangeInfo(string oldWorkerId)
        {
            string sql = string.Format("Select Top 1 OldWorkerId, WorkerName, NewWorkerId from Archives_WorkerIdChanged where OldWorkerId='{0}'", oldWorkerId);
            var datas = DbHelper.Hrm.LoadEntities<WorkerChangeModel>(sql);
            if (datas != null && datas.Count > 0) return datas[0];
            return null;
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
            return AttendSlodFingerDataCurrentMonthDbHandler.UpdateSlotCardTime2(uSlotCardTime, uSlotCardTime2, currentAttendData.WorkerId, currentAttendData.AttendanceDate);
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
                return AttendSlodFingerDataCurrentMonthDbHandler.UpdateSlotCardTime2(uSlotCardTime, uSlotCardTime2, currentAttendData.WorkerId, currentAttendData.AttendanceDate);
            }
            else
            {
                string uSlotCardTime1 = currentAttendData.SlotCardTime1 == null || currentAttendData.SlotCardTime1.Length == 0 ? slodCardTime.ToString("yyyy-MM-dd HH:mm") : currentAttendData.SlotCardTime1;
                string uSlotCardTime = currentAttendData.SlotCardTime == null || currentAttendData.SlotCardTime.Length == 0 ? slodCardTime.ToString("HH:mm") : cardtime;
                return AttendSlodFingerDataCurrentMonthDbHandler.UpdateSlotCardTime1(uSlotCardTime, uSlotCardTime1, currentAttendData.WorkerId, currentAttendData.AttendanceDate);
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
            record = AttendSlodFingerDataCurrentMonthDbHandler.Insert(mdl);
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
               new XAttribute(XNamespace.Xmlns + "xsi", "lm"),
               new XAttribute(XNamespace.Xmlns + "eic", "eic"),
               new XAttribute("lmschemaLocation", "http://www.ieee.org/ATML/2007/TestResults TestResults.xsd"));
            XElement timeSet = new XElement("TimeSetConfig", new XElement("TimeSetter", new XAttribute("THour", ttgt.THour)
                , new XAttribute("TMinute", ttgt.TMinute), new XAttribute("TStartSecond", ttgt.TStartSecond), new XAttribute("TEndSecond", ttgt.TEndSecond)));

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
            TimerTarget tt = new TimerTarget()
            {
                THour = timeSet.Attribute("THour").Value.ToInt(),
                TMinute = timeSet.Attribute("TMinute").Value.ToInt(),
                TStartSecond = timeSet.Attribute("TStartSecond").Value.ToInt(),
                TEndSecond = timeSet.Attribute("TEndSecond").Value.ToInt(),
            };
            return tt;
        }
        #endregion


        #region handle exception method
        /// <summary>
        /// 获取异常数据
        /// </summary>
        /// <param name="attendanceDatas"></param>
        /// <returns></returns>
        private List<AttendSlodFingerDataCurrentMonthModel> GetAttendanceExceptionData(List<AttendSlodFingerDataCurrentMonthModel> attendanceDatas)
        {
            List<AttendSlodFingerDataCurrentMonthModel> attendanceExceptionDatas = new List<AttendSlodFingerDataCurrentMonthModel>();
            if (attendanceDatas == null || attendanceDatas.Count == 0) return attendanceExceptionDatas;
            attendanceDatas.ForEach(d =>
            {
                if (string.IsNullOrEmpty(d.SlotCardTime1) || string.IsNullOrEmpty(d.SlotCardTime2))
                    attendanceExceptionDatas.Add(d);
            });
            return attendanceExceptionDatas;
        }
        /// <summary>
        /// 输出异常信息
        /// </summary>
        /// <returns></returns>
        private string OutputAttendanceExceptionMessage(List<AttendSlodFingerDataCurrentMonthModel> attendanceExceptionDatas)
        {
            if (attendanceExceptionDatas == null || attendanceExceptionDatas.Count == 0) return string.Empty;
            StringBuilder sbMessage = new StringBuilder();
            sbMessage.Append("<table border=\"1\">")
                .AppendFormat("<caption>{0}日异常考勤数据汇总</caption>", DateTime.Now.ToDateStr())
                .Append("<thead><tr>")
                .Append("<th>部门</th>").Append("<th>工号</th>").Append("<th>姓名</th>")
                .Append("<th>时间1</th>").Append("<th>时间2</th>").Append("</tr></th>")
                .Append("<tbody>");
            attendanceExceptionDatas.ForEach(aed =>
            {
                sbMessage.Append("<tr>")
                         .AppendFormat("<td>{0}</td>", aed.Department)
                         .AppendFormat("<td>{0}</td>", aed.WorkerId)
                         .AppendFormat("<td>{0}</td>", aed.WorkerName)
                         .AppendFormat("<td>{0}</td>", string.IsNullOrEmpty(aed.SlotCardTime1) ? "" : aed.SlotCardTime1)
                         .AppendFormat("<td>{0}</td>", string.IsNullOrEmpty(aed.SlotCardTime1) ? "" : aed.SlotCardTime2)
                         .Append("</tr>").AppendLine();
            });
            sbMessage.AppendLine("</tbody>")
                     .AppendLine("</table>");
            return sbMessage.ToString();
        }
        /// <summary>
        /// 将异常数据信息通知给各单位主管
        /// </summary>
        private void NotifyToManager(DateTime attendanceDate, List<AttendSlodFingerDataCurrentMonthModel> attendanceExceptionDatas)
        {
            StringBuilder sbMessage = new StringBuilder();
            try
            {
                sbMessage.Append(OutputAttendanceExceptionMessage(attendanceExceptionDatas)).AppendLine()
               .AppendLine("说明：此邮件为系统发送邮件，请勿回复！！");
                MailMsg mailMsg = new MailMsg("wxq520@ezconn.cn", new List<string>() { "ylei@ezconn.cn" });
                mailMsg.Subject = $"{attendanceDate.ToDateStr()}日异常考勤数据汇总数据";
                mailMsg.Body = sbMessage.ToString();
                EmailMessageNotification.EmailNotifier.SendMail(mailMsg);
            }
            catch (System.Exception ex)
            {
                ErrorMessageTracer.LogErrorMsgToFile("NotifyToManager", ex);
            }
        }
        #endregion

        #region Init Datas

        public void InitDatas()
        {
            for (int day = 27; day <= 27; day++)
            {
                DateTime dt = new DateTime(2017, 5, day, 0, 0, 0);
                var datas = this.LoadDatas(dt);
                if (datas != null && datas.Count > 0)
                {
                    datas.ForEach(d =>
                    {
                        if (AttendFingerPrintDataDbHandler.InsertDataTo(d) > 0)
                        {
                            this.Delete(d);
                        }
                    });
                }
                this.DeleteAttendanceDatas(dt);
                this.AutoProcessAttendanceDatas(dt);
            }
        }
        public void TestInsert()
        {
            List<AttendSlodFingerDataCurrentMonthModel> attendanceExceptionDatas = new List<AttendSlodFingerDataCurrentMonthModel>() {
                new AttendSlodFingerDataCurrentMonthModel() { Department="EIC", WorkerId="003095",WorkerName="杨磊" },
                new AttendSlodFingerDataCurrentMonthModel() { Department="EIC", WorkerId="001359",WorkerName="万晓桥" },
            };

            string msg = OutputAttendanceExceptionMessage(attendanceExceptionDatas);
            MailMsg mailMsg = new MailMsg("wxq520@ezconn.cn", new List<string>() {"wxq520@ezconn.cn" });
            mailMsg.Subject = $"{DateTime.Now.ToDateStr()}日异常考勤数据汇总数据";
            mailMsg.Body = msg;
            string templatePath = @"C:\LightMasterTemplate.html";
            EmailMessageNotification.EmailNotifier.sendHaveTemplateMail(templatePath, mailMsg);
            //EmailMessageNotification.EmailNotifier.SendMail(mailMsg);
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

        private int Delete(AttendFingerPrintDataInTimeModel entity)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("Delete From Attendance_FingerPrintDataInTimeLib");
            sb.AppendFormat("  where WorkerId='{0}' And SlodCardTime='{1}'", entity.WorkerId, entity.SlodCardTime);
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
    /// 目标时间模型
    /// </summary>
    public class TimerTarget
    {
        public int THour { get; set; }
        public int TMinute { get; set; }
        public int TStartSecond { get; set; }
        public int TEndSecond { get; set; }
    }
}
