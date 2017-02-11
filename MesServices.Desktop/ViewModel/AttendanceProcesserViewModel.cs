﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Lm.Eic.AutoWorkProcess.Attendance;
using Lm.Eic.Uti.Common.YleeTimer;
using Lm.Eic.Uti.Common.YleeMessage.Windows;

namespace MesServices.Desktop.ViewModel
{
    /// <summary>
    /// 考勤数据自动处理模型
    /// </summary>
    public class AttendanceProcesserViewModel:ViewModelBase
    {
        #region property 
        HandleAttendanceDataTimer timer = null;

        DateTime  _SlodCardDate=DateTime.Now;
        /// <summary>
        /// 刷卡日期
        /// </summary>
        public DateTime  SlodCardDate
        {
            get
            {
                return _SlodCardDate;
            }
            set
            {
                if (_SlodCardDate != value)
                {
                    _SlodCardDate = value;
                    OnPropertyChanged("SlodCardDate");
                }
            }
        }

        string _AutoHandleCommandText="启动自动处理";
        public string AutoHandleCommandText
        {
            get
            {
                return _AutoHandleCommandText;
            }
            set
            {
                if (_AutoHandleCommandText != value)
                {
                    _AutoHandleCommandText = value;
                    OnPropertyChanged("AutoHandleCommandText");
                }
            }
        }


        string _ProcessMessage;
        public string ProcessMessage
        {
            get
            {
                return _ProcessMessage;
            }
            set
            {
                if (_ProcessMessage != value)
                {
                    _ProcessMessage = value;
                    OnPropertyChanged("ProcessMessage");
                }
            }
        }
        #endregion
        
        public AttendanceProcesserViewModel()
        {
            this.timer = new ViewModel.HandleAttendanceDataTimer() { ReportProcessMsg=msg=> { this.ProcessMessage = msg; } };
        }

        #region command
        
        /// <summary>
        /// 自动处理考勤数据命令
        /// </summary>
        public RelayCommand AutoProcessAttendanceDataCmd
        {
            get
            {
                return new RelayCommand(ProcessAttendanceData);
            }
        }
        
        private void ProcessAttendanceData(object o)
        {
            timer.SlodCardDate = this.SlodCardDate;
            if (this.AutoHandleCommandText == "启动自动处理")
            {
                this.AutoHandleCommandText = "停止自动处理";
                timer.Start();
            }
            else
            {
                this.AutoHandleCommandText = "启动自动处理";
                timer.Stop();

            }
        }
        #endregion

    }
    /// <summary>
    /// 考勤处理计时器
    /// </summary>
    public class HandleAttendanceDataTimer:LeeTimerBase
    {
        #region property 
        /// <summary>
        /// 刷卡日期
        /// </summary>
        public DateTime SlodCardDate { get; set; }
        AttendanceDataManger attendmanceDataManager = null;
        TimerTarget ttgt = null;
        //处理进度汇报句柄
        public Action<string> ReportProcessMsg { get; set; }
        #endregion

        public HandleAttendanceDataTimer()
        {
            this.InitTimer(1000);
            this.attendmanceDataManager = new AttendanceDataManger();
            this.ttgt = new ViewModel.TimerTarget() { THour = 0, TEndSecond = 13, TMinute = 30, TStartSecond = 10 };
        }

        #region method
        protected override void TimerWatcherHandler()
        {
            DateTime d = DateTime.Now;
            int m = d.Minute, h = d.Hour, s = d.Second;
            if (h == ttgt.THour && m == ttgt.TMinute && s > ttgt.TStartSecond && s < ttgt.TEndSecond)
            {
                if (ReportProcessMsg != null)
                    ReportProcessMsg("开始汇总");
                this.attendmanceDataManager.AutoProcessAttendanceDatas(this.SlodCardDate.AddDays(-1));
                if (ReportProcessMsg != null)
                    ReportProcessMsg("汇总结束");
            }
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