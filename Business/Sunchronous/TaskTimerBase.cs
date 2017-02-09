using GalaSoft.MvvmLight;
using Microsoft.Practices.Prism.Commands;
using System.Threading;
using System.Timers;
using System.Windows.Input;
using System;

namespace Business
{

    /// <summary>
    /// 倒计时执行任务
    /// </summary>
    public abstract class TaskTimerBase : ViewModelBase
    {
        Thread Td;                                            //实例化一个线程，执行实现的任务
        System.Timers.Timer tim = new System.Timers.Timer();  //实例化Timer类，设置间隔时间为10000毫秒；   

        /// <summary>
        /// 初始化同步
        /// </summary>
        public TaskTimerBase()
        {
            tim.Interval = 1000;                              //设置
            tim.Elapsed += new ElapsedEventHandler(Theout);   //到达时间的时候执行事件；   
            Reset();
            WriteLog("初始化完成！");
        }

        int interval = 5;
        /// <summary>
        /// 同步间隔时间 单位为分钟 默认五分钟
        /// </summary>
        public int Interval
        {
            get { return interval; }
            set { interval = value; Reset(); }
        }

        int countdown;
        /// <summary>
        /// 倒计时 具备UI更新功能可直接绑定 单位为秒
        /// </summary>
        public int CountDown
        {
            get { return countdown; }
            set
            {
                countdown = value;
                this.RaisePropertyChanged("CountDown");
            }
        }

        private string operationLog;
        /// <summary>
        /// 执行日志
        /// </summary>
        public string OperationLog
        {
            get { return operationLog; }
            set
            {
                operationLog = value;
                this.RaisePropertyChanged("OperationLog");
            }
        }

        /// <summary>
        /// 开始同步
        /// </summary>
        public ICommand Start => new DelegateCommand(() =>
        {
            tim.Start();
        });

        /// <summary>
        /// 停止同步
        /// </summary>
        public ICommand Stop => new DelegateCommand(() =>
        {
            tim.Stop();
            CountDown = 0;
        });

        
        /// <summary>
        /// 写日志
        /// </summary>
        /// <param name="Log">日志内容</param>
       public void WriteLog(string log)
        {
            OperationLog += DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\r\n";
            OperationLog += log + "\r\n";
        }
       
        /// <summary>
        /// 重置倒计时
        /// </summary>
        void Reset()
        {
            CountDown = interval * 60;
        }

        /// <summary>
        /// 开始计时
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void Theout(object sender, ElapsedEventArgs e)
        {
            CountDown--;
            if (CountDown == 0) //倒计时完成
            {
                Td = new Thread(new ThreadStart(SynchronousMethod));
                Td.IsBackground = true;
                Td.Start();
                Reset();
            }
        }

        /// <summary>
        /// 到了指定时间要执行的方法
        /// </summary>
        public abstract void SynchronousMethod();
    }
}
