﻿using System.Collections.Generic;
using System.Text.RegularExpressions;
using System;


namespace Lm.Eic.Uti.Common.YleeExtension.Validation
{
    //
    public static class BaseValidationExtension
    {
        #region string

        /// <summary>
        /// 字符串是否为Null
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static bool IsNullOrEmpty(this string s)
        {
            return string.IsNullOrEmpty(s);
        }
        
        /// <summary>
        /// 字符串是否为数字
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static bool IsNumber(this string s)
        {
            string pattern = "^[0-9]*$";
            Regex rx = new Regex(pattern);
            return rx.IsMatch(s);
        }

        /// <summary>
        /// 是否为Int类型
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static bool IsInt(this string s)
        {
            int i;
            return int.TryParse(s, out i);
        }

        /// <summary>
        ///  是否为double类型
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static bool Isdouble(this string s)
        {
            double i;
            return double.TryParse(s, out i);
        }

        #endregion string

        #region Int

        /// <summary>
        /// 是否为奇数
        /// </summary>
        public static bool IsEven(this int value)
        {
            return (value % 2 == 0) ? true : false;
        }

        #endregion Int

        #region List

        /// <summary>
        /// 判断此集合是否 不为空 且 Item大于0
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="dt"></param>
        /// <returns></returns>
        public static bool IsNullOrEmpty<T>(this List<T> dt)
        {
            return dt != null && dt.Count > 0;
        }

        //
        // 摘要:
        //     确定string[] 中的所有元素是否都在List中
        //
        // 参数:
        //   stList:
        //     该值不能
        //
        // 返回结果:
        //     如果在List中全部找到，则为 true，否则为 false。
        public static bool Contains(this List<string> mystList,string[] stList )
        {
            if (mystList == null || stList == null)
                return false;

            //物料是否都存在与工单物料中
            foreach (var str in stList)
            {
                if (!mystList.Contains(str))
                    return false;
            }
            return true;
        }


        /// <summary>
        /// 年度、季度格式yyyyMM 转为时间段
        /// </summary>
        /// <param name="seasonDateNum">格式yyyyMM</param>
        /// <param name="stardate">格式yyyyMMdd</param>
        /// <param name="enddate">格式yyyyMMdd</param>
        public static void SeasonNumConvertStartDateAndEndDate(this string seasonDateNum, out string  stardate, out string enddate)
        {
            try
            {

                string year = string.Empty ;
                int DateNum = 0;
                //
                if (seasonDateNum == string.Empty)
                {
                    stardate = string.Empty;
                    enddate = string.Empty;
                    return;
                }
                if (seasonDateNum.Length != 6)
                {
                    DateNum = int.Parse(seasonDateNum.Substring(seasonDateNum.Length - 1, 1));
                    year =DateTime.Now .Year .ToString() ;
                }
                else
                {
                    year = seasonDateNum.Substring(0, 4);
                    DateNum = int.Parse(seasonDateNum.Substring(4, 2));
                }
                switch (DateNum)
                {
                    case 1:
                        stardate = year + "0101";
                        enddate = year + "0331";
                        break;
                    case 2:
                        stardate = year + "0401";
                        enddate = year + "0630";
                        break;
                    case 3:
                        stardate = year + "0701";
                        enddate = year + "0931";
                        break;
                    case 4:
                        stardate = year + "1001";
                        enddate = year + "1231";
                        break;
                    default:
                        stardate = string.Empty;
                        enddate = string.Empty;
                        break;
                }

            }
            catch (System.Exception ex) { throw new System.Exception(ex.InnerException.Message); }

        }
        #endregion List
    }
}