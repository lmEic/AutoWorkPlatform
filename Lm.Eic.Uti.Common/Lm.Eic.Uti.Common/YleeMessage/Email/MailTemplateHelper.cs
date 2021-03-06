﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;

namespace Lm.Eic.Uti.Common.YleeMessage.Email
{
 public    class MailTemplateHelper
    {
        /// <summary>
        /// 加载模板 [$   ] 所为标识
        /// </summary>
        /// <param name="templatePath">模板路经</param>
        /// <param name="values">变化的值</param>
        /// <returns></returns>
        public static string BulidByFile(string templatePath, NameValueCollection values)
        {
            return BulidByFile(templatePath, values, "[$", "]");
        }
        /// <summary>
        /// 加载数据
        /// </summary>
        /// <param name="template">模板加入字符串</param>
        /// <param name="values">变化的值</param>
        /// <param name="prefix">固定参数</param>
        /// <param name="postfix">固定参数的标识</param>
        /// <returns></returns>
        public static string Build(string template, NameValueCollection values, string prefix, string postfix)
        {
            if (values != null)
            {
                foreach (DictionaryEntry entry in values)
                {
                    template = template.Replace(string.Format("{0}{1}{2}", prefix, entry.Key, postfix), entry.Value.ToString());
                }
            }
            return template;
        }
        /// <summary>
        /// 邮件模板导入
        /// </summary>
        /// <param name="templatePath">模板路经</param>
        /// <param name="values">变化的值</param>
        /// <param name="prefix">固定参数</param>
        /// <param name="postfix">固定参数的标识</param>
        /// <returns></returns>
        public static string BulidByFile(string templatePath, NameValueCollection values, string prefix, string postfix)
        {
            StreamReader reader = null;
            string template = string.Empty;
            try
            {
                FileStream fs = new FileStream(templatePath, FileMode.Open, FileAccess.Read);
                reader = new StreamReader(fs, Encoding.Default);
                template = reader.ReadToEnd();
                reader.Close();
                if (values != null)
                {
                    foreach (string key in values.AllKeys)
                    {
                        template = template.Replace(string.Format("{0}{1}{2}", prefix, key, postfix), values[key]);
                    }
                }
            }
            catch (Exception ex)
            { ErrorMessageTracer.LogErrorMsgToFile("SendTemplateMail Reader Template", ex); }
            finally
            {
                if (reader != null)
                    reader.Close();
            }
            return template;
        }

        /// <summary>
        ///   发送人表格  关键字 Name Table
        /// </summary>
        /// <param name="templetpath">模板路劲</param>
        /// <param name="toName">发送人昵称</param>
        /// <param name="tableString">表格</param>
        /// <returns></returns>
       public static string SendTemplateMail(string templetPath, string mailBody)
        {
            string returnmailBody = mailBody;
            bool isExist = File.Exists(templetPath);
            if (isExist)
            {
                try
                {
                    NameValueCollection myCol = new NameValueCollection();
                    myCol.Add("Name", "各部门主管:");
                    myCol.Add("Table", mailBody);
                    returnmailBody = BulidByFile(templetPath, myCol);
                    return returnmailBody;
                }
                catch (Exception ex)
                {
                    ErrorMessageTracer.LogErrorMsgToFile("SendTemplateMail", ex);
                }
            }
            return returnmailBody;


        }
    }
}
    

