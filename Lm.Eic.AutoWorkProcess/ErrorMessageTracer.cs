﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lm.Eic.Uti.Common.YleeExtension.FileOperation;
using System.IO;
using Lm.Eic.Uti.Common.YleeMessage.Email;

namespace Lm.Eic.AutoWorkProcess
{
    /// <summary>
    /// 消息跟踪器
    /// </summary>
    public static class ErrorMessageTracer
    {
        #region property
        /// <summary>
        /// 错误发生计数容器
        /// </summary>
        private static Dictionary<string, int> errorOccurCountDocker = new Dictionary<string, int>();
        /// <summary>
        /// 错误日志文件夹路径
        /// </summary>
        private static string errorLogFilePath = @"C:\AutoProcessWorker\ErrorMesage\";
        #endregion
        /// <summary>
        /// 将错误消息记录到文件中
        /// </summary>
        /// <param name="fnName">函数名称</param>
        /// <param name="ex"></param>
        public static void LogErrorMsgToFile(string fnName, Exception ex)
        {

            string fileName = Path.Combine(errorLogFilePath, DateTime.Now.ToString("yyyyMMdd") + ".txt");
            StringBuilder sbMsg = new StringBuilder();
            sbMsg.AppendFormat("函数名称：{0}", fnName).AppendLine();
            sbMsg.AppendFormat("错误信息：{0}", ex.Message).AppendLine();
            sbMsg.AppendFormat("错误描述：{0}", ex.StackTrace).AppendLine();
            sbMsg.AppendFormat("错误源：{0}", ex.Source).AppendLine();
            sbMsg.AppendFormat("发生时间：{0}", DateTime.Now).AppendLine();

            if (CheckErrorOccurTime(fnName)) return;

            fileName.AppendFile(sbMsg.ToString());
        }

        private static bool CheckErrorOccurTime(string key)
        {
            if (errorOccurCountDocker.ContainsKey(key))
            {
                if (errorOccurCountDocker[key] >= 3)
                {
                    return true;
                }
                else
                {
                    errorOccurCountDocker[key]++;
                }
            }
            else
            {
                errorOccurCountDocker.Add(key, 1);
            }
            return false;
        }

    }
    /// <summary>
    /// Email消息通知器
    /// </summary>
    public static class EmailMessageNotification
    {
        /// <summary>
        /// 邮件通知器
        /// </summary>
        public static MailHelper EmailNotifier
        {
            get
            {
                MailHelper mailHelper = new MailHelper(new SmtpConfig("smtp.exmail.qq.com", 25, "softwareadmin@ezconn.cn", "EIc2017"));
                return mailHelper;
            }
        }
    }
}
