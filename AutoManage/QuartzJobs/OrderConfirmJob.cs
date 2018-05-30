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
    /// 修改主订单状态为待评价
    /// </summary>
    public sealed class OrderConfirmJob : IJob
    {
        private readonly ILog _errLog = LogManager.GetLogger("Com.Foo");
        private readonly ILog _logger = LogManager.GetLogger(typeof(OrderConfirmJob));
        public void Execute(IJobExecutionContext context)
        {
            try
            {
                Sql.SqlServerClient<Orders> db = Sql.SqlServerClientSingleton<Orders>.Instance;
                Sql.SqlServerClient<OrderStatus> OrderStatusDB = Sql.SqlServerClientSingleton<OrderStatus>.Instance;
                Sql.SqlServerClient<OrderChildStatus> OrderChildStatusDB = Sql.SqlServerClientSingleton<OrderChildStatus>.Instance;
                //处理业务逻辑 收花时间2天后订单状态改成待评价状态
                var day = 30;//判断条件几天后修改
                var orderState = OrderStatusEnum.待收货.GetHashCode();
                var childState = OrderChildStatusEnum.完成.GetHashCode();

                var sql = $"select o.OrderId,o.Type,o.OrderState from Orders o left join OrderChild oc on o.OrderId=oc.Orders_OrderId where((select COUNT(*) times from OrderChild where Orders_OrderId = o.OrderId) = (select COUNT(*) times from OrderChild where Orders_OrderId = o.OrderId and Status={childState})) and o.OrderState = {orderState} and ((o.Type!={OrderTypeEnum.包月.GetHashCode()} and DATEDIFF(DAY, case when o.ReciveTime is null then DATEADD(DAY,2,o.OrderAddTime) ELSE o.ReciveTime END, GETDATE()) >= {day}) or (oc.Times = (select top 1 times from OrderChild where Orders_OrderId = o.OrderId order by SendTime desc) and oc.Status = {childState} and DATEDIFF(DAY, oc.SendTime, GETDATE()) >= {day} and o.Type={OrderTypeEnum.包月.GetHashCode()}))";
                var orderIdTable = db.ExecuteTable(sql);
                if (orderIdTable.Rows.Count > 0)
                {
                    var ids = new List<int>();
                    var orderIdStr = string.Empty;
                    var OrderChildStatusSql = string.Empty;//需要插入到子订单状态表的sql
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
                        orderStatusSql += $"insert into OrderStatus (LastStatus,CurrentStatus,ChangeTime,Reason,Orders_OrderId)values({state},{OrderStatusEnum.待评价.GetHashCode()},GETDATE(),'自动任务修改',{orderid})";
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
                    j = 0;
                    //获取所有子订单，用于插入状态改变记录
                    var childSql = $"select Orders_OrderId,Id,Status from OrderChild where Orders_OrderId in ({orderIdStr}) ";
                    if (!string.IsNullOrEmpty(orderIdStr))
                    {
                        var childTable = db.ExecuteTable(childSql);
                        for (int i = 0; i < childTable.Rows.Count; i++)
                        {
                            j++;
                            orderid = childTable.Rows[i]["Id"].ToString().ToInt32();
                            state = childTable.Rows[i]["Status"].ToString().ToInt32();
                            OrderChildStatusSql += $"insert into OrderChildStatus (LastStatus,CurrentStatus,ChangeTime,OrderChild_Id)values({state},{OrderChildStatusEnum.完成.GetHashCode()},GETDATE(),{orderid})";
                            //一次执行50个
                            if (j >= 50)
                            {
                                j = 1;
                                db.ExecuteSql(OrderChildStatusSql);
                                OrderChildStatusSql = string.Empty;
                            }
                        }
                    }

                    //修改主订单状态为待评价
                    var orderUpdateSql = $"update Orders set OrderState={OrderStatusEnum.待评价.GetHashCode()} where OrderId in ({orderIdStr})";
                    var orderChildUpdateSql = $"update OrderChild set Status={OrderChildStatusEnum.完成.GetHashCode()} where Orders_OrderId in ({orderIdStr})";
                    if (!string.IsNullOrEmpty(orderStatusSql))
                    {
                        db.ExecuteSql(orderStatusSql);//插入主订单状态改变记录
                    }
                    if (!string.IsNullOrEmpty(OrderChildStatusSql))
                    {
                        db.ExecuteSql(OrderChildStatusSql);//插入子订单状态改变记录
                    }
                    var orderCount = 0;
                    var orderChildCount = 0;
                    if (orderIdTable.Rows.Count > 0)
                    {
                        orderCount = db.ExecuteSql(orderUpdateSql);
                        orderChildCount = db.ExecuteSql(orderChildUpdateSql);
                    }
                    _logger.InfoFormat($"批量修改订单状态为待评价成功,本次修改了{orderCount}个主订单,{orderChildCount}个子订单.");
                }
                else
                {
                    _logger.InfoFormat("修改订单状态为待评价-没有需要处理的订单");

                }
            }
            catch (Exception ex)
            {
                _logger.InfoFormat($"修改订单状态为待评价报错-{ex.Message}");
                var fullMesage = ErrorHelper.FullException(ex);
                _errLog.ErrorFormat($"OrderConfirmJob错误信息;{fullMesage}");
            }
        }
    }
}
