using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Lm.Eic.AutoWorkProcess.Attendance;
using Lm.Eic.Uti.Common.YleeTimer;
using Lm.Eic.Uti.Common.YleeMessage.Windows;
using Lm.Eic.Uti.Common.YleeExtension.Conversion;
using System.Threading;
using Lm.Eic.AutoWorkProcess;
using System.Windows;
using System.Collections.ObjectModel;
using Lm.Eic.AutoWorkProcess.Attendance.Server;

namespace MesServices.Desktop.ViewModel
{
    /// <summary>
    /// 考勤数据自动处理模型
    /// </summary>
    public class AttendanceProcesserViewModel : ViewModelBase
    {
        #region property
        HandleAttendanceDataTimer timer = null;
        HandleAttendanceExceptionDataTimer exceptionTimer = null;
        AttendanceUpSynchronous attendmanceMachineDataManager = null;
        string _AutoHandleCommandText = "启动自动处理";
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

        string _AttendanceMachineUpDataText = "考勤机服务器启动";
        public string AttendanceMachineUpDataText
        {
            get
            {
                return _AttendanceMachineUpDataText;
            }
            set
            {
                if (_AttendanceMachineUpDataText != value)
                {
                    _AttendanceMachineUpDataText = value;
                    OnPropertyChanged("AttendanceMachineUpDataText");
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


        ObservableCollection<string> _machineUpdateMsg = new ObservableCollection<string>() { "等待读取考勤数据..." };
        /// <summary>
        /// 考勤机上传数据
        /// </summary>
        public ObservableCollection<string> MachineUpdateMsg
        {
            get
            {
                return _machineUpdateMsg;
            }
            set
            {
                if (_machineUpdateMsg != value)
                {
                    _machineUpdateMsg = value;
                    OnPropertyChanged("MachineUpdateMsg");
                }
            }
        }
        #endregion

        /// <summary>
        /// 创建应用程序
        /// </summary>
        private void CreateApp()
        {
            if (Application.Current == null)
            {
                Application app = new Application();
                app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            }
        }

        public AttendanceProcesserViewModel()
        {
            CreateApp();
            this.timer = new HandleAttendanceDataTimer();
            this.timer.AttendmanceDataManager.MessageReportHandler = msg =>
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    this.ProcessMessage = msg;
                }));
            };
            this.exceptionTimer = new HandleAttendanceExceptionDataTimer();

            this.attendmanceMachineDataManager = new AttendanceUpSynchronous()
            {
                ReportUpdataMsg = msgList =>
                {
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        this.MachineUpdateMsg = new ObservableCollection<string>(msgList);
                    }));
                }
            };
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
            if (this.AutoHandleCommandText == "启动自动处理")
            {
                this.AutoHandleCommandText = "停止自动处理";
                timer.Start();

                exceptionTimer.Start();
            }
            else
            {
                this.AutoHandleCommandText = "启动自动处理";
                timer.Stop();
                exceptionTimer.Stop();

            }
        }
        /// <summary>
        /// 考勤机械上传数据
        /// </summary>
        public RelayCommand AutoProcessAttendanceMachineUpDataCmd
        {
            get
            {
                return new RelayCommand(ProcessAttendanceMachineData);
            }
        }


        private void ProcessAttendanceMachineData(object o)
        {
            try
            {
                if (this.AttendanceMachineUpDataText == "考勤机服务器启动")
                {
                    this.AttendanceMachineUpDataText = "考勤机服务器停止";
                    attendmanceMachineDataManager.OpenAttendanceUpSynchronous();
                }
                else
                {
                    this.AttendanceMachineUpDataText = "考勤机服务器启动";
                    attendmanceMachineDataManager.ClosingAttendanceUpSynchronous();

                }
            }
            catch (System.Exception ex)
            {
                ErrorMessageTracer.LogErrorMsgToFile("", ex);
            }
        }

        #endregion

    }
    /// <summary>
    /// 考勤处理计时器
    /// </summary>
    public class HandleAttendanceDataTimer : LeeTimerBase
    {
        #region property
        public AttendanceDataManger AttendmanceDataManager { get; private set; }
        TimerTarget ttgt = null;
        #endregion

        public HandleAttendanceDataTimer()
        {
            this.InitTimer(10000);
            this.AttendmanceDataManager = new AttendanceDataManger();
            this.ttgt = this.AttendmanceDataManager.LoadTimerSetConfigInfo();
        }

        #region method
        private void AutoHandleAttendanceData()
        {
            isStart = false;
            DateTime slodCardDate = DateTime.Now.AddDays(-1);
            this.AttendmanceDataManager.AutoProcessAttendanceDatas(slodCardDate);
            isStart = true;
        }

        protected override void TimerWatcherHandler()
        {

            DateTime d = DateTime.Now;
            int m = d.Minute, h = d.Hour, s = d.Second;
            if (!isStart) return;
            if (h == ttgt.THour && m == ttgt.TMinute && s > ttgt.TStartSecond && s < ttgt.TEndSecond && isStart)
            {
                try
                {
                    AutoHandleAttendanceData();
                }
                catch (System.Exception ex)
                {
                    ErrorMessageTracer.LogErrorMsgToFile("TimerWatcherHandler", ex);
                }
            }
        }
        #endregion
    }
    /// <summary>
    /// 考勤数据定时处理器
    /// </summary>
    public class HandleAttendanceExceptionDataTimer : LeeTimerBase
    {
        public HandleAttendanceExceptionDataTimer()
        {
            this.InitTimer(1000);
        }
        protected override void TimerWatcherHandler()
        {
            DateTime d = DateTime.Now;
            int m = d.Minute, s = d.Second;
            if (!isStart) return;
            if (m == 30 && s > 1 && s < 19 && isStart)
            {
                try
                {
                    isStart = false;
                    AttendanceExceptionDataManager.AutoHandleAttendanceExceptionData();
                    isStart = true;
                }
                catch (System.Exception ex)
                {
                    ErrorMessageTracer.LogErrorMsgToFile("TimerWatcherHandler", ex);
                }
            }
        }
    }
}
