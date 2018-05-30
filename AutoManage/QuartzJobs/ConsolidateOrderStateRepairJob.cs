using AutoManage.Helper;
using log4net;
using Quartz;
using System;
using Tinghua.Management.Model;

namespace AutoManage.QuartzJobs
{
    /// <summary>
    /// 修复合并订单打印后订单的那些订单状态没有改变
    /// </summary>
   public sealed class ConsolidateOrderStateRepairJob : IJob
    {
        private readonly ILog _errLog = LogManager.GetLogger("Com.Foo");
        private readonly ILog _logger = LogManager.GetLogger(typeof(ConsolidateOrderStateRepairJob));

        public void Execute(IJobExecutionContext context)
        {
            try
            {
                Sql.SqlServerClient<Orders> db = Sql.SqlServerClientSingleton<Orders>.Instance;
                _logger.InfoFormat($"ConsolidateOrderStateRepair-修复合并订单打印后订单状态修改失败的订单.开始运行");
                var sql = "select NoteStr,cpnumber,cpcode,PrintTime,ManagerId from printorderset where notestr is not null and MergeType is null and PrintTime is not null and DATEDIFF(DD, PrintTime, GETDATE()) < 2";
                var errorTable=db.ExecuteTable(sql);
                var printOrderIds = string.Empty;
                var cpnumber = string.Empty;
                var cpcode = string.Empty;
                var PrintTime = string.Empty;
                var ManagerId = string.Empty;
                var printSql = string.Empty;
                var ordersql = string.Empty;
                var orderIds = string.Empty;
                var updatePrintSql = string.Empty;//保存需要修改打印订单表的sql
                var updateOrderSql = string.Empty;//保存需要修改订单表的sql
                for (int i = 0; i < errorTable.Rows.Count; i++)
                {
                    printOrderIds= errorTable.Rows[i]["NoteStr"].ToString().Trim();//打印订单表合单后。保存合并了哪些数据的此表主键ID
                    cpnumber = errorTable.Rows[i]["cpnumber"].ToString().Trim();//快递单号
                    cpcode = errorTable.Rows[i]["cpcode"].ToString().Trim();//快递编码
                    PrintTime = errorTable.Rows[i]["PrintTime"].ToString().Trim();
                    ManagerId = errorTable.Rows[i]["ManagerId"].ToString().Trim();
                    printSql = $"select ID,CPCode,Orders_OrderId from PrintOrderSet where Id in ({printOrderIds})";
                    var printTable = db.ExecuteTable(printSql);
                    var printIds = string.Empty;//需要修改打印订单表的数据
                    var updateOrderIds = string.Empty;//保存需要修改订单表数据的订单ID
                    for (int j = 0; j < printTable.Rows.Count; j++)
                    {
                        if (j == 0)
                            orderIds = printTable.Rows[j]["Orders_OrderId"].ToString();
                        else
                            orderIds += $",{printTable.Rows[j]["Orders_OrderId"].ToString()}";
                        if (string.IsNullOrWhiteSpace(printTable.Rows[j]["CPCode"].ToString()))
                        {
                            if (string.IsNullOrWhiteSpace(printIds))
                                printIds = printTable.Rows[j]["ID"].ToString();
                            else
                                printIds += $",{printTable.Rows[j]["ID"].ToString()}";
                        }
                    }
                    if (!string.IsNullOrWhiteSpace(printIds))
                        updatePrintSql += $"UPDATE PrintOrderSet SET CPCode='{cpcode}',CPNumber='{cpnumber}',ManagerId={ManagerId},PrintTime='{PrintTime}' where Id in ({printIds});";
                    ordersql = $"select OrderState,OrderId from orders where orderid in ({orderIds})";
                    var orderTable = db.ExecuteTable(ordersql);
                    for (int j = 0; j < orderTable.Rows.Count; j++)
                    {
                        if (orderTable.Rows[j]["OrderState"].ToString() == "3")//如果订单状态为已确认。那么就修改订单状态为待收货
                        {
                            if (string.IsNullOrWhiteSpace(updateOrderIds))
                                updateOrderIds = orderTable.Rows[j]["OrderId"].ToString();
                            else
                                updateOrderIds +=$",{orderTable.Rows[j]["OrderId"].ToString()}";
                            updateOrderSql += $"insert LogSet (type,Content,OrderId,createTime) values (4,'自动任务在修复合单的时候修改还修改了快递信息',{orderTable.Rows[j]["OrderId"].ToString()},getdate())";
                        }
                    }
                    if (!string.IsNullOrWhiteSpace(updateOrderIds))
                        updateOrderSql += $"update orders set expressName='{cpcode}',expressNumber='{cpnumber}',OrderState=4 where orderid in ({updateOrderIds});";
                    if ( i%50 == 0)
                    {
                        if (!string.IsNullOrWhiteSpace(updatePrintSql))
                            db.ExecuteSql(updatePrintSql);
                        if (!string.IsNullOrWhiteSpace(updateOrderSql))
                            db.ExecuteSql(updateOrderSql);
                        updateOrderSql = string.Empty;
                        updatePrintSql = string.Empty;
                    }   
                }
                if (!string.IsNullOrWhiteSpace(updatePrintSql))
                    db.ExecuteSql(updatePrintSql);
                if (!string.IsNullOrWhiteSpace(updateOrderSql))
                    db.ExecuteSql(updateOrderSql);
                _logger.InfoFormat($"ConsolidateOrderStateRepair-修复合并订单打印后订单状态修改失败的订单运行结束.");

            }
            catch (Exception ex)
            {
                _logger.InfoFormat($"ConsolidateOrderStateRepair-读取订单数据到redis出现异常{ex.Message}");
                var fullMesage = ErrorHelper.FullException(ex);
                _errLog.ErrorFormat($"ConsolidateOrderStateRepair错误信息;{fullMesage}");
            }
        }
    }
}
