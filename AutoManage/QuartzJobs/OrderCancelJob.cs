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
    /// 未付款超过30分钟自动取消订单
    /// </summary>
    public sealed class OrderCancelJob : IJob
    {
        private readonly ILog _errLog = LogManager.GetLogger("Com.Foo");
        private readonly ILog _logger = LogManager.GetLogger(typeof(OrderCancelJob));
        public void Execute(IJobExecutionContext context)
        {
            try
            {
                Sql.SqlServerClient<Orders> db = Sql.SqlServerClientSingleton<Orders>.Instance;

                //处理业务逻辑 收花时间2天后订单状态改成待评价状态
                var day = 30;//判断条件几分钟后修改
                var orderState = OrderStatusEnum.未付款.GetHashCode();
                var sql = $"select o.OrderId,o.Type,o.OrderState,t.Orders_OrderId,t.Id from Orders o left join Ticket t on o.OrderId=t.Orders_OrderId where o.OrderState = {orderState} and DATEDIFF(MI, o.OrderAddTime, GETDATE()) > {day}";
                var orderIdTable = db.ExecuteTable(sql);
                if (orderIdTable.Rows.Count > 0)
                {
                    var ids = new List<int>();
                    var orderIdStr = string.Empty;
                    var orderStatusSql = string.Empty;//需要插入到主订单状态表的sql
                    int orderid;
                    int Type;
                    int state;
                    object ticketOrderId;
                    var ticketIds = string.Empty;
                    int j = 0;
                    for (int i = 0; i < orderIdTable.Rows.Count; i++)
                    {
                        j++;
                        orderid = orderIdTable.Rows[i]["OrderId"].ToString().ToInt32();
                        Type = orderIdTable.Rows[i]["Type"].ToString().ToInt32();
                        state = orderIdTable.Rows[i]["OrderState"].ToString().ToInt32();
                        ticketOrderId= orderIdTable.Rows[i]["Orders_OrderId"];
                        if (!string.IsNullOrEmpty(ticketOrderId.ToString()))
                        {
                            ticketIds = ticketIds == string.Empty ? orderIdTable.Rows[i]["Id"].ToString() : $"{ticketIds},{orderIdTable.Rows[i]["Id"].ToString()}";
                        }
                        orderStatusSql += $"insert into OrderStatus (LastStatus,CurrentStatus,ChangeTime,Reason,Orders_OrderId)values({state},{OrderStatusEnum.订单关闭.GetHashCode()},GETDATE(),'自动任务修改',{orderid})";
                        ids.Add(orderid);
                        orderIdStr = orderIdStr == "" ? orderid.ToString() : $"{orderIdStr},{orderid}";
                        //一次执行50个
                        if (j >= 50)
                        {
                            j = 1;
                            db.ExecuteSql(orderStatusSql);
                            orderStatusSql = string.Empty;
                        }
                    }

                    //修改主订单状态为待评价
                    var orderUpdateSql = $"update Orders set OrderState={OrderStatusEnum.订单关闭.GetHashCode()} where OrderId in ({orderIdStr})";
                    if (!string.IsNullOrEmpty(orderStatusSql))
                    {
                        db.ExecuteSql(orderStatusSql);//插入主订单状态改变记录
                    }
                    if (ticketIds!=string.Empty)
                    {
                        var ticketSql = $"update Ticket set Orders_OrderId=null,Status=2 where Id in ({ticketIds})";
                        db.ExecuteSql(ticketSql);//执行返回已使用的优惠券
                    }
                    var orderCount = 0;
                    if (orderIdTable.Rows.Count > 0)
                    {
                        orderCount = db.ExecuteSql(orderUpdateSql);
                    }
                    _logger.InfoFormat($"批量修改未付款订单状态为用户取消成功,本次修改了{orderCount}个主订单.");
                }
                else
                {
                    _logger.InfoFormat("未付款超过30分钟后取消订单-没有需要处理的订单");
                }
            }
            catch (Exception ex)
            {
                var fullMesage = ErrorHelper.FullException(ex);
                _errLog.ErrorFormat($"OrderCancelJob错误信息;{fullMesage}");
                _logger.InfoFormat($"未付款超过30分钟后取消订单报错-{ex.Message}");
            }
        }
    }
}
