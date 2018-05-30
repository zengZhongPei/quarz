using log4net;
using Quartz;
using System;
using Tinghua.Management.Utility.Helper;
using Tinghua.Management.Model;
using AutoManage.Helper;

namespace AutoManage.QuartzJobs
{
    public sealed class WangdianGuanJiaSendOrderJob : IJob
    {
        private readonly ILog _errLog = LogManager.GetLogger("Com.Foo");
        private readonly ILog _logger = LogManager.GetLogger(typeof(WangdianGuanJiaSendOrderJob));

        public void Execute(IJobExecutionContext context)
        {
            _logger.InfoFormat($"WangdianGuanJiaSendOrderJob-网店管家发货同步开始...");
            try
            {
                Sql.SqlServerClient<Orders> db = Sql.SqlServerClientSingleton<Orders>.Instance;
                var datetime = DateTime.Now.AddDays(-1).ToShortDateString();
                var et = DateTime.Now.AddDays(2).ToShortDateString();
                var sql = $@"select * from (
select oc.Times,type,o.lat,expressNumber,expressName,oce.Number,oce.Company from Orders o left join orderchild oc on o.orderid = oc.orders_orderid left join OrderChildExpress oce on oc.Id = oce.OrderChild_Id where ShopId in(4) and orderstate > 2 and OrderState <7 and o.ReciveTime >='{datetime}' and o.ReciveTime <'{et}')
tempTab where (times = 1 or Times is null)";
                var table=db.ExecuteTable(sql);
                var order = string.Empty;
                for (int i = 0; i < table.Rows.Count; i++)
                {
                    var lat = string.IsNullOrEmpty(table.Rows[i]["lat"].ToString()) ? "" : table.Rows[i]["lat"].ToString();
                    var expressNumber = string.IsNullOrEmpty(table.Rows[i]["expressNumber"].ToString()) ? table.Rows[i]["Number"].ToString() : table.Rows[i]["expressNumber"].ToString();
                    var expressName = string.IsNullOrEmpty(table.Rows[i]["expressName"].ToString()) ? table.Rows[i]["Company"].ToString() : table.Rows[i]["expressName"].ToString();
                    if (string.IsNullOrEmpty(lat))
                    {
                        continue;
                    }
                    if (string.IsNullOrEmpty(expressNumber))
                    {
                        continue;
                    }
                    if (string.IsNullOrEmpty(expressName))
                    {
                        continue;
                    }
                    if (string.IsNullOrEmpty(order))
                    {
                        order = $"{lat}_{expressNumber}_{expressName}";
                    }
                    else
                    {
                        order += $"&{lat}_{expressNumber}_{expressName}";
                    }
                }
                TaoBaoOperHelper.sendConfim(order);//网店管家发货同步
                _logger.InfoFormat($"WangdianGuanJiaSendOrderJob-网店管家发货同步成功。。。");
            }
            catch (Exception ex)
            {
                _logger.InfoFormat($"WangdianGuanJiaSendOrderJob-网店管家发货同步异常...");
                var fullMesage = ErrorHelper.FullException(ex);
                _errLog.ErrorFormat($"WangdianGuanJiaSendOrderJob错误信息;{fullMesage}");
            }
        }
    }
}
