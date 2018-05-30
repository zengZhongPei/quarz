using AutoManage.Helper;
using log4net;
using Quartz;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Tinghua.Management.Enum;
using Tinghua.Management.Model;
using Tinghua.Management.Utility.Helper;

namespace AutoManage.QuartzJobs
{
    /// <summary>
    /// 此自动任务已停止运行，已迁移到其他任务(InsertPrintOrderJob)中
    /// 处理的业务逻辑:把下单并且发货的用户的手机号放入redis里面去，用于后续做是否首次下单判断。每天凌晨12点运行一次
    /// </summary>
    public sealed class InsertFirstOrderJob : IJob
    {
        private readonly ILog _errLog = LogManager.GetLogger("Com.Foo");
        private readonly ILog _logger = LogManager.GetLogger(typeof(BugJobs));
        public void Execute(IJobExecutionContext context)
        {
            _logger.InfoFormat($"自动任务InsertFirstOrderJob-读取订单数据到redis开始运行...");
            try
            {
                Sql.SqlServerClient<Orders> db = Sql.SqlServerClientSingleton<Orders>.Instance;
                var datetime = DateTime.Now.ToShortDateString();
                var et = DateTime.Now.AddDays(1).ToShortDateString();
                var state = $"{ OrderStatusEnum.待评价.GetHashCode()},{OrderStatusEnum.已付款.GetHashCode()},{OrderStatusEnum.已确认.GetHashCode()}, {OrderStatusEnum.已完成.GetHashCode()},{ OrderStatusEnum.待收货.GetHashCode()}";
                var sql = $"select OrderId,Phone from Orders where OrderState in ({state}) and OrderAddTime>='{datetime}' and OrderAddTime <'{et}' and type=2";
                var idval = RedisHelper.Get("FirstOrderPhone_proint");
                if (string.IsNullOrEmpty(idval))
                {
                    sql = $"select OrderId,Phone from Orders where OrderState in ({state}) and type=2";
                }
                var orderTable = db.ExecuteTable(sql);
                var temp = "";
                var phone = "";
                var list = new List<string>();
                if (orderTable.Rows.Count > 0)
                {
                    if (!string.IsNullOrEmpty(idval))
                    {
                        list.AddRange(idval.Split(','));
                    }
                    for (int i = 0; i < orderTable.Rows.Count; i++)
                    {
                        phone = orderTable.Rows[i]["Phone"].ToString();
                        if (string.IsNullOrEmpty(phone))
                        {
                            continue;
                        }
                        temp = $"{phone}_{orderTable.Rows[i]["OrderId"].ToString()}";
                        //检查是否已经把改手机号放到redis了，如果放到了就更新后面的ID值
                        if (list.Where(l => l== temp).Any())
                        {
                            list.RemoveAll(l => l== temp);
                            list.Add(temp);
                        }
                        else
                        {
                            list.Add(temp);
                        }

                    }
                    idval = string.Join(",", list.ToArray());
                    RedisHelper.Set("FirstOrderPhone_proint", idval);
                    _logger.InfoFormat($"自动任务InsertFirstOrderJob-读取订单数据到redis成功,一共插入{list.Count},当前插入{orderTable.Rows.Count}");
                }
                else
                {
                    _logger.InfoFormat($"自动任务InsertFirstOrderJob-读取订单数据到redis无数据");

                }
            }
            catch (Exception ex)
            {
                _logger.InfoFormat($"自动任务InsertFirstOrderJob-读取订单数据到redis出现异常{ex.Message}");
                var fullMesage = ErrorHelper.FullException(ex);
                _errLog.ErrorFormat($"InsertFirstOrderJob错误信息;{fullMesage}");
            }
            
        }
    }
}
