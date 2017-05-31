using System;
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
        /// 加载模板
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
                reader = new StreamReader(templatePath);
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
            catch
            {

            }
            finally
            {
                if (reader != null)
                    reader.Close();
            }
            return template;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="templetpath"></param>
        /// <param name="emailToname"></param>
        /// <param name="tableString"></param>
        /// <returns></returns>
        private string SendTemplateMail(string templetPath, string emailToname,string tableString)
        {
            string mailBody = string.Empty;
            bool isExist = File.Exists(templetPath);
            if (!string.IsNullOrEmpty(emailToname)&& isExist)
            {
                try
                {
                    NameValueCollection myCol = new NameValueCollection();
                    myCol.Add("Name", emailToname);
                    myCol.Add("Table", tableString);
                    mailBody =BulidByFile(templetPath, myCol);
                    return mailBody;
                }
                catch (Exception ex)
                {
                    ErrorMessageTracer.LogErrorMsgToFile("SendTemplateMail", ex);
                }
            }
            return mailBody;


        }
    }
}
    

