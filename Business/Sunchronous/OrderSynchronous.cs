using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Business
{
    public class OrderSynchronous : TaskTimerBase
    {

        private List<MES.Server.Model.BPM_Order> orderlist = new List<MES.Server.Model.BPM_Order>();

        public List<MES.Server.Model.BPM_Order> OrderList
        {
            get { return orderlist; }
            set
            {
                orderlist = value;
                this.RaisePropertyChanged("OrderList");
            }
        }

        /// <summary>
        /// 到了指定时间要执行的方法
        /// </summary>
        public override void SynchronousMethod()
        {
            WriteLog("开始同步！");
            int tryCount = 0;
            //获得生产中工单
            var lsErp = MES.Business.BpmHelper.Order_ERP.GetModelList(" (TA021 = 'MS2') AND (TA011 IN ('1', '2', '3'))");
            //获取本地服务器中的生产中的工单
            var lsLoac = MES.Business.BpmHelper.Order.GetModelList("  (State IN ('生产中', '未生产', '已发料'))");
            //遍历ERP中的待生产工单 如果本地没有 则添加至本地
            foreach (var erpOrder in lsErp)
            {
                var temOrder = lsLoac.FirstOrDefault(p => p.OrderID == erpOrder.OrderID);
                if (temOrder == null)
                {
                    bool tem = MES.Business.BpmHelper.Order.Add(erpOrder);
                    if (!tem) { tryCount++; WriteLog("失败：" + erpOrder.OrderID); }
                }
            }

            //遍历本地的生产中工单 生成SQL Where语句
            var lsOrderID = new List<string>();
            foreach (var loacOrder in lsLoac)
            {
                lsOrderID.Add("'" + loacOrder.OrderID.Split('-')[1] + "'");
                OrderList.Add(loacOrder);
            }
            var strWhere = "TA021 = 'MS2' AND TA025 IN (" + string.Join(",", lsOrderID) + ")";

            //从ERP中获取在本地服务器为未完工的工单 检查其状态是否已经变更为已完工  并更新本地工单
            var lsErp_OverOrder = MES.Business.BpmHelper.Order_ERP.GetModelList(strWhere);
            var yetOverOrderList = lsErp_OverOrder.Where(p => p.State == "已完工" || p.State == "指定完工");
            foreach (var yetOverOrder in yetOverOrderList)
            {

                bool tem = MES.Business.BpmHelper.Order.Add(yetOverOrder);
                if (!tem) { tryCount++; WriteLog("失败：" + yetOverOrder.OrderID); }
            }
            WriteLog("同步完成！" + tryCount);
        }
    }
}
