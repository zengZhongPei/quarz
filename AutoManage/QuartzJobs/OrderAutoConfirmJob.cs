using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tinghua.Management.Model;
using Tinghua.Management.Utility.Extensions;
using Tinghua.Management.Enum;
using log4net;
using Quartz;
using AutoManage.Helper;

namespace AutoManage.QuartzJobs
{
    /// <summary>
    /// 自动确认订单
    /// </summary>
    public sealed class OrderAutoConfirmJob : IJob
    {
        private readonly ILog _errLog = LogManager.GetLogger("Com.Foo");
        private readonly ILog _logger = LogManager.GetLogger(typeof(OrderAutoConfirmJob));
        public void Execute(IJobExecutionContext context)
        {
            try
            {
                Sql.SqlServerClient<Orders> db = Sql.SqlServerClientSingleton<Orders>.Instance;
                Sql.SqlServerClient<OrderStatus> OrderStatusDB = Sql.SqlServerClientSingleton<OrderStatus>.Instance;
                Sql.SqlServerClient<OrderChildStatus> OrderChildStatusDB = Sql.SqlServerClientSingleton<OrderChildStatus>.Instance;
                var sqlTxt = $"select OrderId,OrderAddTime,Type from Orders where OrderState={OrderStatusEnum.已付款.GetHashCode()} and (Person!='' and Person is not null) and (Phone!='' and Phone is not null)and (Province!='' and Province is not null)and (City!='' and City is not null)and (Area!='' and Area is not null)and (AddressLongLat!='' and AddressLongLat is not null and AddressLongLat not like '%测试%') and (ReciveTime!='' and ReciveTime is not null or Type in ({OrderTypeEnum.普通商品.GetHashCode()},{OrderTypeEnum.单束鲜花.GetHashCode()},{OrderTypeEnum.绿植出售.GetHashCode()},{OrderTypeEnum.绿植租赁.GetHashCode()}))";
                var orderTable = db.ExecuteTable(sqlTxt);
                if (orderTable.Rows.Count > 0)
                {
                    var ids = new List<int>();
                    var orderIdStr = string.Empty;
                    var OrderChildStatusSql = string.Empty;//需要插入到子订单状态表的sql
                    var orderStatusSql = string.Empty;//需要插入到主订单状态表的sql
                    int orderid;
                    int Type;
                    int state=OrderStatusEnum.已付款.GetHashCode();
                    int j = 0;
                    for (int i = 0; i < orderTable.Rows.Count; i++)
                    {
                        j++;
                        orderid = orderTable.Rows[i]["OrderId"].ToString().ToInt32();
                        Type = orderTable.Rows[i]["Type"].ToString().ToInt32();
                        orderStatusSql += $"insert into OrderStatus (LastStatus,CurrentStatus,ChangeTime,Reason,Orders_OrderId)values({state},{OrderStatusEnum.已确认.GetHashCode()},GETDATE(),'自动任务修改',{orderid})";
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
                            OrderChildStatusSql += $"insert into OrderChildStatus (LastStatus,CurrentStatus,ChangeTime,OrderChild_Id)values({state},{OrderChildStatusEnum.已确认.GetHashCode()},GETDATE(),{orderid})";
                            //一次执行50个
                            if (j >= 50)
                            {
                                j = 1;
                                db.ExecuteSql(OrderChildStatusSql);
                                OrderChildStatusSql = string.Empty;
                            }
                        }
                    }

                    //修改主订单状态为已确认
                    var orderUpdateSql = $"update Orders set OrderState={OrderStatusEnum.已确认.GetHashCode()} where OrderId in ({orderIdStr})";
                    var orderChildUpdateSql = $"update OrderChild set Status={OrderChildStatusEnum.已确认.GetHashCode()} where Orders_OrderId in ({orderIdStr})";
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
                    if (orderTable.Rows.Count > 0)
                    {
                        orderCount = db.ExecuteSql(orderUpdateSql);
                        orderChildCount = db.ExecuteSql(orderChildUpdateSql);
                    }
                    _logger.InfoFormat($"批量修改订单状态为已确认成功,本次修改了{orderCount}个主订单,{orderChildCount}个子订单.订单号:{orderIdStr}");
                }
                else
                {
                    _logger.InfoFormat("修改订单状态为已确认-没有需要处理的订单");
                }
            }
            catch (Exception ex)
            {
                _logger.InfoFormat($"修改订单状态为已确认报错-{ex.Message}");
                var fullMesage = ErrorHelper.FullException(ex);
                _errLog.ErrorFormat($"OrderAutoConfirmJob错误信息;{fullMesage}");
            }
        }
    }
}
