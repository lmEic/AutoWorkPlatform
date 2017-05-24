using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lm.Eic.AutoWorkProcess.Attendance.Server
{
    public class AttendanceBase
    {
        /// <summary>
        /// 操作信息
        /// </summary>
        protected StringBuilder OpMessage { get; private set; }

        private List<string> opMessageList = new List<string>();
        public Action<string> MessageReportHandler { get; set; }
        /// <summary>
        /// 记录消息
        /// </summary>
        /// <param name="message"></param>
        protected void Log(string message)
        {
            if (this.opMessageList.Count > 20)
            {
                this.opMessageList.Clear();
                this.OpMessage.Clear();
            }
            this.opMessageList.Add(message);
            this.OpMessage.AppendLine(message);
            if (MessageReportHandler != null) MessageReportHandler(this.OpMessage.ToString());
        }
        protected void LogException(Exception ex)
        {
            Log(ex.Message);
        }
        protected void LogProgress(string message)
        {
            string sourceMsg = this.OpMessage.ToString();
            string msg = sourceMsg + Environment.NewLine + message;
            if (MessageReportHandler != null) MessageReportHandler(msg);
        }

        protected void LogProgressUser(int count, string message, string workerId)
        {
            LogProgress(string.Format("进度:{0},{1}:{2}", count, message, workerId));
        }
        public AttendanceBase()
        {
            this.OpMessage = new StringBuilder();
        }
    }
}
