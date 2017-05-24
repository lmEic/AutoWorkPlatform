using Microsoft.VisualStudio.TestTools.UnitTesting;
using Lm.Eic.AutoWorkProcess.Attendance;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lm.Eic.AutoWorkProcess.Attendance.Server;

namespace Lm.Eic.AutoWorkProcess.Attendance.Tests
{
    [TestClass()]
    public class AttendanceDataMangerTests
    {
        [TestMethod()]
        public void AutoProcessAttendanceDatasTest()
        {
            AttendanceDataManger d = new AttendanceDataManger();
            //d.InitPostDatas();
            //am.InitDatas();

            d.InitDatas();

            //d.TestInsert();
            Assert.Fail();
        }

        [TestMethod()]
        public void InitConfigurationFileTest()
        {
            AttendanceDataManger am = new AttendanceDataManger();
            am.InitConfigurationFile();
            Assert.Fail();
        }
    }
}