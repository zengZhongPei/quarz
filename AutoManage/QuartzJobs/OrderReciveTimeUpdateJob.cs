using Quartz;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tinghua.Management.Model;
using Tinghua.Management.Utility.Extensions;
using Tinghua.Management.Enum;
using log4net;
using AutoManage.Helper;

namespace AutoManage.QuartzJobs
{
    /// <summary>
    /// 此自动任务已停止运行
    /// </summary>
    public sealed class OrderReciveTimeUpdateJob : IJob
    {
        private readonly ILog _errLog = LogManager.GetLogger("Com.Foo");
        private readonly ILog _logger = LogManager.GetLogger(typeof(OrderReciveTimeUpdateJob));

        public void Execute(IJobExecutionContext context)
        {
            try
            {
                Sql.SqlServerClient<OrderChild> db = Sql.SqlServerClientSingleton<OrderChild>.Instance;

                //处理业务逻辑 修改固定4个商品的收花时间为情人节当天
                var SQL = "update OrderChild set SendTime='2017-2-14' where Id in (select oc.Id from Orders o inner join OrderChild oc on o.OrderId=oc.Orders_OrderId right join OrderGoodss og on oc.Orders_OrderId=og.OrderId where o.OrderState in(2,3) and og.GoodsId in (334,335,336,337) and oc.Times=1 and o.OrderAddTime <'2017-2-15 00:00:00' and oc.SendTime !='2017-2-14 00:00:00')";
                var count = db.ExecuteSql(SQL);
                _logger.InfoFormat($"批量修改固定4个商品的收花时间为情人节当天,共修改{count}个");
            }
            catch (Exception ex)
            {
                _logger.InfoFormat($"修改订单状态为待评价报错-{ex.Message}");
                var fullMesage = ErrorHelper.FullException(ex);
                _errLog.ErrorFormat($"OrderReciveTimeUpdateJob错误信息;{fullMesage}");
            }
          
        }
    }
}
