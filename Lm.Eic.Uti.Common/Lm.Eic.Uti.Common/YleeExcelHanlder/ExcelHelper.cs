﻿
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using System.Web;

namespace Lm.Eic.Uti.Common.YleeExcelHanlder
{
    /// <summary>
    /// Excel操作助手
    /// Microsoft.Excel插件，
    /// 支持小批量数据导入导出与打印功能
    /// 大量数据导入导出时速度比较慢，导入
    /// 导出大量数据时，尽量不要使用此模块
    /// </summary>
    public static class ExcelHelper
    {
        /// <summary>
        /// 返回模板文件中的指定的Sheet表
        /// </summary>
        /// <param name="templateFilePath">模板文件路径</param>
        /// <param name="sheetIndex">Sheet索引</param>
        /// <param name="setDataToSheetHandler">赋值数据给Excel</param>
        public static Excel.Worksheet CreateXlsSheet(string templateFilePath, int sheetIndex, ref Excel.Application xlsApp, ref Excel.Workbook xlsWorkbook, bool isvisible = false)
        {
            xlsApp = new Excel.Application();
            Excel.Workbooks workbook = xlsApp.Workbooks;
            xlsWorkbook = workbook.Add(templateFilePath);
            Excel.Worksheet xst = xlsWorkbook.Worksheets[sheetIndex] as Excel.Worksheet;
            xlsApp.Visible = isvisible;
            xlsApp.DisplayAlerts = false;
            return xst;
        }

        /// <summary>
        /// 关闭Excel内存进程
        /// </summary>
        /// <param name="xlsApp">应用程序</param>
        /// <param name="xlsWorkbook">工作簿</param>
        /// <param name="xlsSheet">工作表</param>
        public static void CloseXls(Excel.Application xlsApp, Excel.Workbook xlsWorkbook, Excel.Worksheet xlsSheet)
        {
            xlsWorkbook.Close();
            xlsApp.Workbooks.Close();
            xlsApp.Quit();
            //关闭EXCEL进程
            System.Runtime.InteropServices.Marshal.ReleaseComObject(xlsApp);
            System.Runtime.InteropServices.Marshal.ReleaseComObject(xlsWorkbook);
            System.Runtime.InteropServices.Marshal.ReleaseComObject(xlsSheet);
            xlsWorkbook = null;
            xlsApp = null;
        }



        #region Excel导入

        /// <summary>
        /// 从Excel取数据并记录到List集合 ：从三行开始，前二行是英与中文映射
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="filePath">保存文件绝对路径</param>
        /// <param name="sheetColumn">列</param>
        /// <param name="errorMsg">错误信息</param>
        /// <returns>转换后的List对象集合<</returns>
        public static List<T> ExcelToEntityList<T>(string filePath, out StringBuilder errorMsg) where T : new()
        {
            List<T> enlist = new List<T>();
            errorMsg = new StringBuilder();
            try
            {
                if (Regex.IsMatch(filePath, ".xls$")) // 2003
                {
                    enlist = Excel2003ToEntityList<T>(filePath, out errorMsg);
                }
                else if (Regex.IsMatch(filePath, ".xlsx$")) // 2007
                {
                    //return FailureResultMsg("请选择Excel文件"); // 未设计
                    // enlist = Excel2007ToEntityList<T>(filePath, sheetColumn, out errorMsg);
                }
                return enlist;
            }
            catch (Exception ex) { throw new Exception(ex.InnerException.Message); }
        }

        /// <summary>
        /// 从Excel2003取数据并记录到List集合里
        /// </summary>
        /// <param name="cellHeard">单元头的Key和Value：</param>
        /// <param name="filePath">保存文件绝对路径</param>
        /// <param name="errorMsg">错误信息</param>
        /// <returns>转换好的List对象集合</returns>
        private static List<T> Excel2003ToEntityList<T>(string filePath, out StringBuilder errorMsg) where T : new()
        {
            errorMsg = new StringBuilder(); // 错误信息,Excel转换到实体对象时，会有格式的错误信息
            List<T> enlist = new List<T>(); // 转换后的集合
            Dictionary<string, string> cellHeard = new Dictionary<string, string>();
            //List<string> keys = cellHeard.Keys.ToList(); // 要赋值的实体对象属性名称
            try
            {
                using (FileStream fs = File.OpenRead(filePath))
                {
                    HSSFWorkbook workbook = new HSSFWorkbook(fs);
                    HSSFSheet sheet = (HSSFSheet)workbook.GetSheetAt(0); // 获取此文件第一个Sheet页
                    #region    导出头二行 做为对应的字典
                    try
                    {
                        int cellCount = sheet.GetRow(0).LastCellNum;
                        List<string> EnglishCellHeardGGroup = new List<string>();
                        for (int jj = 0; jj < cellCount; jj++)
                        {
                            if (sheet.LastRowNum >= 2)
                            {
                                string englishCellHeard = sheet.GetRow(0).GetCell(jj).StringCellValue.ToString().Trim();
                                string chineCellHeard = sheet.GetRow(1).GetCell(jj).StringCellValue.ToString().Trim();
                                if (!EnglishCellHeardGGroup.Contains(englishCellHeard))
                                    cellHeard.Add(englishCellHeard, chineCellHeard);
                                else
                                    errorMsg.Append("第" + jj + "列有重复");
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        throw new Exception(e.ToString());
                    }
                    #endregion   导出头二行 做为对应的字典

                    List<string> keys = cellHeard.Keys.ToList(); // 要赋值的实体对象属性名称
                    for (int i = 2; i <= sheet.LastRowNum; i++) // 从2开始，第0，1行为单元头 英 中文对应
                    {
                        // 1.判断当前行是否空行，若空行就不在进行读取下一行操作，结束Excel读取操作
                        if (sheet.GetRow(i) == null)
                        {
                            break;
                        }

                        T en = new T();
                        string errStr = ""; // 当前行转换时，是否有错误信息，格式为：第1行数据转换异常：XXX列；
                        for (int j = 0; j < keys.Count; j++)
                        {
                            // 2.若属性头的名称包含'.',就表示是子类里的属性，那么就要遍历子类，eg：UserEn.TrueName
                            if (keys[j].IndexOf(".") >= 0)
                            {
                                // 2.1解析子类属性
                                string[] properotyArray = keys[j].Split(new string[] { "." }, StringSplitOptions.RemoveEmptyEntries);
                                string subClassName = properotyArray[0]; // '.'前面的为子类的名称
                                string subClassProperotyName = properotyArray[1]; // '.'后面的为子类的属性名称
                                System.Reflection.PropertyInfo subClassInfo = en.GetType().GetProperty(subClassName); // 获取子类的类型
                                if (subClassInfo != null)
                                {
                                    // 2.1.1 获取子类的实例
                                    var subClassEn = en.GetType().GetProperty(subClassName).GetValue(en, null);
                                    // 2.1.2 根据属性名称获取子类里的属性信息
                                    System.Reflection.PropertyInfo properotyInfo = subClassInfo.PropertyType.GetProperty(subClassProperotyName);
                                    if (properotyInfo != null)
                                    {
                                        try
                                        {
                                            // Excel单元格的值转换为对象属性的值，若类型不对，记录出错信息
                                            properotyInfo.SetValue(subClassEn, GetExcelCellToProperty(properotyInfo.PropertyType, sheet.GetRow(i).GetCell(j)), null);
                                        }
                                        catch
                                        {
                                            if (errStr.Length == 0)
                                            {
                                                errStr = "第" + i + "行数据转换异常：";
                                            }
                                            errStr += cellHeard[keys[j]] + "列；";
                                        }

                                    }
                                }
                            }
                            else
                            {
                                // 3.给指定的属性赋值
                                System.Reflection.PropertyInfo properotyInfo = en.GetType().GetProperty(keys[j]);
                                if (properotyInfo != null)
                                {
                                    try
                                    {
                                        // Excel单元格的值转换为对象属性的值，若类型不对，记录出错信息
                                        properotyInfo.SetValue(en, GetExcelCellToProperty(properotyInfo.PropertyType, sheet.GetRow(i).GetCell(j)), null);
                                    }
                                    catch
                                    {
                                        if (errStr.Length == 0)
                                        {
                                            errStr = "第" + i + "行数据转换异常：";
                                        }
                                        errStr += cellHeard[keys[j]] + "列；";
                                    }
                                }
                            }
                        }
                        // 若有错误信息，就添加到错误信息里
                        if (errStr.Length > 0)
                        {
                            errorMsg.AppendLine(errStr);
                        }
                        enlist.Add(en);
                    }
                }
                return enlist;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        #endregion Excel导入

        #region Excel导出

        /// <summary>
        /// 实体类集合导出到EXCLE2003
        /// </summary>
        /// <param name="cellHeard">单元头的Key和Value： { "English", "中文" }</param>
        /// <param name="enList">数据源</param>
        /// <param name="sheetName">工作表名称</param>
        /// <returns>文件的下载地址</returns>
        public static string EntityListToExcel2003(Dictionary<string, string> cellHeard, IList enList, string sheetName)
        {
            try
            {
                string fileName = sheetName + "-" + DateTime.Now.ToString("yyyyMMddHHmmssfff") + ".xls"; // 文件名称
                string urlPath = "UpFiles/ExcelFiles/" + fileName; // 文件下载的URL地址，供给前台下载
                string filePath =System.Web.HttpContext.Current.Server.MapPath("\\" + urlPath); // 文件路径

                // 1.检测是否存在文件夹，若不存在就建立个文件夹
                string directoryName = Path.GetDirectoryName(filePath);
                if (!Directory.Exists(directoryName))
                {
                    Directory.CreateDirectory(directoryName);
                }

                // 2.解析单元格头部，设置单元头的中文名称
                HSSFWorkbook workbook = new HSSFWorkbook(); // 工作簿
                ISheet sheet = workbook.CreateSheet(sheetName); // 工作表
                IRow row = sheet.CreateRow(0);
                List<string> keys = cellHeard.Keys.ToList();
                for (int i = 0; i < keys.Count; i++)
                {
                    row.CreateCell(i).SetCellValue(cellHeard[keys[i]]); // 列名为Key的值
                }

                // 3.List对象的值赋值到Excel的单元格里
                int rowIndex = 1; // 从第二行开始赋值(第一行已设置为单元头)
                foreach (var en in enList)
                {
                    IRow rowTmp = sheet.CreateRow(rowIndex);
                    for (int i = 0; i < keys.Count; i++) // 根据指定的属性名称，获取对象指定属性的值
                    {
                        string cellValue = ""; // 单元格的值
                        object properotyValue = null; // 属性的值
                        System.Reflection.PropertyInfo properotyInfo = null; // 属性的信息

                        // 3.1 若属性头的名称包含'.',就表示是子类里的属性，那么就要遍历子类，eg：UserEn.UserName
                        if (keys[i].IndexOf(".") >= 0)
                        {
                            // 3.1.1 解析子类属性(这里只解析1层子类，多层子类未处理)
                            string[] properotyArray = keys[i].Split(new string[] { "." }, StringSplitOptions.RemoveEmptyEntries);
                            string subClassName = properotyArray[0]; // '.'前面的为子类的名称
                            string subClassProperotyName = properotyArray[1]; // '.'后面的为子类的属性名称
                            System.Reflection.PropertyInfo subClassInfo = en.GetType().GetProperty(subClassName); // 获取子类的类型
                            if (subClassInfo != null)
                            {
                                // 3.1.2 获取子类的实例
                                var subClassEn = en.GetType().GetProperty(subClassName).GetValue(en, null);
                                // 3.1.3 根据属性名称获取子类里的属性类型
                                properotyInfo = subClassInfo.PropertyType.GetProperty(subClassProperotyName);
                                if (properotyInfo != null)
                                {
                                    properotyValue = properotyInfo.GetValue(subClassEn, null); // 获取子类属性的值
                                }
                            }
                        }
                        else
                        {
                            // 3.2 若不是子类的属性，直接根据属性名称获取对象对应的属性
                            properotyInfo = en.GetType().GetProperty(keys[i]);
                            if (properotyInfo != null)
                            {
                                properotyValue = properotyInfo.GetValue(en, null);
                            }
                        }

                        // 3.3 属性值经过转换赋值给单元格值
                        if (properotyValue != null)
                        {
                            cellValue = properotyValue.ToString();
                            // 3.3.1 对时间初始值赋值为空
                            if (cellValue.Trim() == "0001/1/1 0:00:00" || cellValue.Trim() == "0001/1/1 23:59:59")
                            {
                                cellValue = "";
                            }
                        }

                        // 3.4 填充到Excel的单元格里
                        rowTmp.CreateCell(i).SetCellValue(cellValue);
                    }
                    rowIndex++;
                }

                // 4.生成文件
                FileStream file = new FileStream(filePath, FileMode.Create);
                workbook.Write(file);
                file.Close();

                // 5.返回下载路径
                return urlPath;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        #endregion Excel导出

        /// <summary>
        /// 从Excel获取值传递到对象的属性里
        /// </summary>
        /// <param name="distanceType">目标对象类型</param>
        /// <param name="sourceCell">对象属性的值</param>
        private static Object GetExcelCellToProperty(Type distanceType, ICell sourceCell)
        {
            object rs = distanceType.IsValueType ? Activator.CreateInstance(distanceType) : null;

            // 1.判断传递的单元格是否为空
            if (sourceCell == null || string.IsNullOrEmpty(sourceCell.ToString()))
            {
                return rs;
            }

            // 2.Excel文本和数字单元格转换，在Excel里文本和数字是不能进行转换，所以这里预先存值
            object sourceValue = null;
            switch (sourceCell.CellType)
            {
                case CellType.Blank:
                    break;

                case CellType.Boolean:
                    break;

                case CellType.Error:
                    break;

                case CellType.Formula:
                    break;

                case CellType.Numeric: sourceValue = sourceCell.NumericCellValue;
                    break;

                case CellType.String: sourceValue = sourceCell.StringCellValue;
                    break;

                case CellType.Unknown:
                    break;

                default:
                    break;
            }

            string valueDataType = distanceType.Name;

            // 在这里进行特定类型的处理
            switch (valueDataType.ToLower()) // 以防出错，全部小写
            {
                case "string":
                    rs = sourceValue.ToString();
                    break;
                case "int":
                case "int16":
                case "int32":
                    if (sourceCell.CellType == CellType.Numeric)
                    { rs = (int)Convert.ChangeType(sourceCell.NumericCellValue.ToString(), distanceType); }
                    else { rs = (int)Convert.ChangeType(sourceCell.StringCellValue.ToString(), distanceType); }
                    break;
                case "float":
                case "decimal":
                     if (sourceCell.CellType == CellType.Numeric)
                    { rs = (decimal )Convert.ChangeType(sourceCell.NumericCellValue.ToString(), distanceType); }
                    else { rs = (decimal)Convert.ChangeType(sourceCell.StringCellValue.ToString(), distanceType); }
                    break;
                case "single":
                    if (sourceCell.CellType == CellType.Numeric)
                    { rs = (float)Convert.ChangeType(sourceCell.NumericCellValue.ToString(), distanceType); }
                    else { rs = (float)Convert.ChangeType(sourceCell.StringCellValue.ToString(), distanceType); }
                    break;
                case "datetime":
                    if (sourceCell.CellType == CellType.Numeric)
                    { rs = sourceCell.DateCellValue; }
                    else { rs = (DateTime)Convert.ChangeType(sourceCell.StringCellValue.ToString(), distanceType); }
                    break;
                case "guid":
                    if (sourceCell.CellType == CellType.Numeric)
                    { rs = (Guid)Convert.ChangeType(sourceCell.NumericCellValue.ToString(), distanceType); ; }
                    else { rs = (Guid)Convert.ChangeType(sourceCell.StringCellValue.ToString(), distanceType); }

                    return rs;
            }
            return rs;
        }

      
    }
}