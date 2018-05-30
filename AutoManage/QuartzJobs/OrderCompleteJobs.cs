using log4net;
using Quartz;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tinghua.Management.Model;
using Tinghua.Management.Utility.Extensions;
using Tinghua.Management.Enum;
using AutoManage.Helper;

namespace AutoManage.QuartzJobs
{
    /// <summary>
    /// 主订单待评价修改为完成
    /// </summary>
    public sealed class OrderCompleteJobs : IJob
    {
        private readonly ILog _errLog = LogManager.GetLogger("Com.Foo");
        private readonly ILog _logger = LogManager.GetLogger(typeof(OrderCompleteJobs));
        public void Execute(IJobExecutionContext context)
        {
            try
            {
                Sql.SqlServerClient<Orders> db = Sql.SqlServerClientSingleton<Orders>.Instance;

                //处理业务逻辑 收花时间7天后订单状态改成已完成状态
                var day = 15;//判断条件几天后修改
                var orderState = OrderStatusEnum.待评价.GetHashCode();
                var sql = $"select o.OrderId,o.Type,o.OrderState from Orders o where o.OrderState = {orderState} and DATEDIFF(DAY, case when o.ReciveTime is null then DATEADD(DAY,2,o.OrderAddTime) ELSE o.ReciveTime END, GETDATE()) >= {day}";
                var orderIdTable = db.ExecuteTable(sql);
                if (orderIdTable.Rows.Count > 0)
                {
                    var ids = new List<int>();
                    var orderIdStr = string.Empty;
                    var MonthOrderIdStr = string.Empty;//包月订单ID
                    var orderStatusSql = string.Empty;//需要插入到主订单状态表的sql
                    int orderid;
                    int Type;
                    int state;
                    int j = 0;
                    for (int i = 0; i < orderIdTable.Rows.Count; i++)
                    {
                        j++;
                        orderid = orderIdTable.Rows[i]["OrderId"].ToString().ToInt32();
                        Type = orderIdTable.Rows[i]["Type"].ToString().ToInt32();
                        state = orderIdTable.Rows[i]["OrderState"].ToString().ToInt32();
                        orderStatusSql += $"insert into OrderStatus (LastStatus,CurrentStatus,ChangeTime,Reason,Orders_OrderId)values({state},{OrderStatusEnum.已完成.GetHashCode()},GETDATE(),'自动任务修改',{orderid});";
                        ids.Add(orderid);
                        if (Type != OrderTypeEnum.包月.GetHashCode())
                        {
                            orderIdStr = orderIdStr == "" ? orderid.ToString() : $"{orderIdStr},{orderid}";
                        }
                        else
                        {
                            MonthOrderIdStr = MonthOrderIdStr == "" ? orderid.ToString() : $"{MonthOrderIdStr},{orderid}";
                        }

                        if (Type != OrderTypeEnum.绿植补发.GetHashCode())
                        {
                            var orderSQl = $@"select OrderLeaseId from OrderLeaseSerialNumberSet a
                                            left join Orders b on a.OrderSerialNumber = b.OrderSerialNumber
                                            where b.OrderId = {orderid}";
                            var orderTalbe = db.ExecuteTable(orderSQl);
                            for (int k = 0; k < orderTalbe.Rows.Count; k++)
                            {
                                orderStatusSql += $"insert OrderRecordSet(Content,CreateTime,IsState,OrderLeaseId,RecordState,OperationUserId) values ('您的补发商品已签收，感谢您对听花的支持。',getdate(),1,{orderTalbe.Rows[k]["OrderLeaseId"]},4,9476);";
                            }
                        }
                        else if (Type != OrderTypeEnum.绿植换货.GetHashCode())
                        {
                            var orderSQl = $@"select OrderLeaseId from OrderLeaseSerialNumberSet a
                                            left join Orders b on a.OrderSerialNumber = b.OrderSerialNumber
                                            where b.OrderId = {orderid}";
                            var orderTalbe = db.ExecuteTable(orderSQl);
                            for (int k = 0; k < orderTalbe.Rows.Count; k++)
                            {
                                orderStatusSql += $"insert OrderRecordSet(Content,CreateTime,IsState,OrderLeaseId,RecordState,OperationUserId) values ('您申请的需要更换的商品已签收，感谢您对听花的支持。',getdate(),1,{orderTalbe.Rows[k]["OrderLeaseId"]},4,9476);";
                            }
                        }

                        //一次执行50个
                        if (j >= 50)
                        {
                            j = 1;
                            db.ExecuteSql(orderStatusSql);
                            orderStatusSql = string.Empty;
                        }
                    }

                    //修改主订单状态为完成并评价
                    var orderUpdateSql = $"update Orders set OrderState={OrderStatusEnum.已完成.GetHashCode()},efficiencyNumber=5,courtesyNumber=5,DescriptionNumber=5 where OrderId in ({orderIdStr});update OrderGoodss set evaluateText='默认好评' where OrderId in ({orderIdStr})";
                    var orderMonthUpdateSql = $"update Orders set OrderState={OrderStatusEnum.已完成.GetHashCode()} where OrderId in ({MonthOrderIdStr});update OrderChild set EvaluateNum = 5,SpeedNum = 5,ExpressNum = 5,EvaluateText = '默认好评' where Orders_OrderId in ({MonthOrderIdStr})";
                    if (!string.IsNullOrEmpty(orderStatusSql))
                    {
                        db.ExecuteSql(orderStatusSql);//插入主订单状态改变记录
                    }

                    var orderCount = 0;
                    if (orderIdTable.Rows.Count > 0)
                    {
                        if (orderIdStr!=string.Empty)
                        {
                            orderCount = db.ExecuteSql(orderUpdateSql);
                        }
                        if (MonthOrderIdStr!=string.Empty)
                        {
                            orderCount += db.ExecuteSql(orderMonthUpdateSql);
                        }
                    }
                    _logger.InfoFormat($"批量修改订单状态为完成订单成功,本次修改了{orderCount}个主订单.订单号:");
                }
                else
                {
                    _logger.InfoFormat("修改订单状态为完成订单-没有需要处理的订单");

                }
            }
            catch (Exception ex)
            {
                _logger.InfoFormat($"修改订单状态为完成订单报错-{ex.InnerException}");
                var fullMesage = ErrorHelper.FullException(ex);
                _errLog.ErrorFormat($"OrderCompleteJobs错误信息;{fullMesage}");
            }
        }
    }
}
