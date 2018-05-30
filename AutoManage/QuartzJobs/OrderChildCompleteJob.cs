using AutoManage.Helper;
using log4net;
using Quartz;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tinghua.Management.Enum;
using Tinghua.Management.Model;
using Tinghua.Management.Utility.Extensions;

namespace AutoManage.QuartzJobs
{
    /// <summary>
    /// 修改子订单状态为完成
    /// </summary>
    public sealed class OrderChildCompleteJob : IJob
    {
        private readonly ILog _errLog = LogManager.GetLogger("Com.Foo");
        private readonly ILog _logger = LogManager.GetLogger(typeof(OrderChildCompleteJob));
        public void Execute(IJobExecutionContext context)
        {
            try
            {
                Sql.SqlServerClient<Orders> db = Sql.SqlServerClientSingleton<Orders>.Instance;
                //处理业务逻辑 收花时间2天后子订单状态改成完成状态
                var day = 3;//判断条件几天后修改
                var childState = OrderChildStatusEnum.送货中.GetHashCode();
                var sql = $"select Id from OrderChild where Status={childState} and DATEDIFF(DAY, sendtime, GETDATE()) >= {day}";
                var orderIdTable = db.ExecuteTable(sql);
                var OrderChildId = 0;
                var OrderChildIdStr = string.Empty;
                var orderStatusSql = string.Empty;
                if (orderIdTable.Rows.Count > 0)
                {
                    int j = 0;
                    for (int i = 0; i < orderIdTable.Rows.Count; i++)
                    {
                        j++;
                        OrderChildId = orderIdTable.Rows[i]["Id"].ToString().ToInt32();
                        orderStatusSql += $"insert into OrderChildStatus (LastStatus,CurrentStatus,ChangeTime,OrderChild_Id)values(3,{OrderChildStatusEnum.完成.GetHashCode()},GETDATE(),{OrderChildId})";
                        OrderChildIdStr = OrderChildIdStr == "" ? OrderChildId.ToString() : $"{OrderChildIdStr},{OrderChildId}";
                        //一次执行50个
                        if (j >= 50)
                        {
                            j = 1;
                            db.ExecuteSql(orderStatusSql);
                            orderStatusSql = string.Empty;
                        }
                    }
                    

                    //修改主订单状态为待评价
                    var orderChildUpdateSql = $"update OrderChild set Status={OrderChildStatusEnum.完成.GetHashCode()} where Id in ({OrderChildIdStr})";
                   
                    if (!string.IsNullOrEmpty(orderStatusSql))
                    {
                        db.ExecuteSql(orderStatusSql);//插入子订单状态改变记录
                    }
                    var orderChildCount = 0;
                    if (orderIdTable.Rows.Count > 0)
                    {
                        orderChildCount = db.ExecuteSql(orderChildUpdateSql);
                    }
                    _logger.InfoFormat($"任务名:OrderChildCompleteJob-批量修改子订单状态为已完成成功,{orderChildCount}个子订单.");
                }
                else
                {
                    _logger.InfoFormat("任务名:OrderChildCompleteJob-修改订单状态为子订单状态为已完成-没有需要处理的订单");

                }
            }
            catch (Exception ex)
            {
                _logger.InfoFormat($"任务名:OrderChildCompleteJob-修改子订单状态为完成报错-{ex.Message}");
                var fullMesage = ErrorHelper.FullException(ex);
                _errLog.ErrorFormat($"OrderChildCompleteJob错误信息;{fullMesage}");
            }
        }
    }
}
