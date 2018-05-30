using AutoManage.Helper;
using log4net;
using Quartz;
using System;
using Tinghua.Management.Model;

namespace AutoManage.QuartzJobs
{
    /// <summary>
    /// 前台自动拼团处理逻辑
    /// </summary>
    public sealed class UpdateGroupEndOrderStateJobs : IJob
    {
        private readonly ILog _logger = LogManager.GetLogger(typeof(UpdateGroupEndOrderStateJobs));
        private readonly ILog _errLog = LogManager.GetLogger("Com.Foo");

        public void Execute(IJobExecutionContext context)
        {
            _logger.InfoFormat($"自动任务UpdateGroupEndOrderStateJobs-自动修改拼团订单状态开始运行...");
            Sql.SqlServerClient<Orders> db = Sql.SqlServerClientSingleton<Orders>.Instance;
            try
            {
                //自动成团
                string sql = @"Update Orders set OrderState=2 where OrderId in(select o1.OrderId from orders as o1 where o1.OrderState =14 and o1.groupid in(
                            select OrderId from orders where OrderId in(select o.OrderId from Orders o where o.OrderState=14 and o.IsGroup=1 and  
		                                DATEDIFF( Second, getdate(), DATEADD(MINUTE,(select GroupEndTime from Goodss 
                                    where (IsAutomaticGroup=1 or IsAutomaticGroup is null) and GoodsId=o.GoodsId),OrderAddTime))<0)));";
                //成团失败
                sql += @"Update Orders set OrderState=16 where OrderId in(select o1.OrderId from orders as o1 where o1.OrderState =14 and o1.groupid in(
                            select OrderId from orders where OrderId in(select o.OrderId from Orders o where o.OrderState =14 and o.IsGroup=1 and  
		                                DATEDIFF( Second, getdate(), DATEADD(MINUTE,(select GroupEndTime from Goodss 
                                    where IsAutomaticGroup=0 and GoodsId=o.GoodsId),OrderAddTime))<0)));";

                sql += @"Update [tinghua].[dbo].[Orders] set [OrderState]=16 where [OrderId] in(
                select[OrderId] as OrderId

    from[tinghua].[dbo].[Orders] where[OrderId]in (select OrderId from(
      select count(*) as cont,LackNum,OrderId from(
          select orders.OrderId, orders.GroupId,
              dateadd(mi, goods.GroupEndTime, orders.OrderAddTime) as times,
				orders.OrderAddTime,
				orders.IsGroup,
				goods.LackNum,goods.GroupEndTime,goods.GoodsId,GETDATE() as nowdate

            from
                (SELECT[OrderId] as OrderId,
					case when IsGroup in (2, 3) then(select[OrderAddTime] from[tinghua].[dbo].[Orders] o where o.[OrderId] = od.GroupId)

                         when IsGroup = 1 then[OrderAddTime]end
                    as OrderAddTime,

                    [GroupId] as GroupId,[GoodsId] as GoodsId,[IsGroup] as IsGroup

                FROM[tinghua].[dbo].[Orders] od where  od.[OrderState] = 14
                ) orders
            left join
                (SELECT[LackNum] as LackNum,[GroupEndTime] as GroupEndTime,[GoodsId] as GoodsId  FROM[tinghua].[dbo].[Goodss]

                where[GoodsId] in
                    (SELECT[GoodsId] as GoodsId FROM[tinghua].[dbo].[Orders] where[OrderState] = 14)
				) goods
            on orders.GoodsId = goods.GoodsId
		) model
            where model.nowdate > times  group by model.GroupId ,LackNum,OrderId
	 ) item where cont < LackNum))";

                var returnInt = db.ExecuteSql(sql, null);
                _logger.InfoFormat($"自动任务UpdateGroupEndOrderStateJobs-自动修改拼团订单状态运行结束返回:{returnInt}");
            }
            catch (Exception ex)
            {
                _logger.InfoFormat($"自动任务UpdateGroupEndOrderStateJobs-自动修改拼团订单状态报错{ex.Message}");
                var fullMesage = ErrorHelper.FullException(ex);
                _errLog.ErrorFormat($"UpdateGroupEndOrderStateJobs错误信息;{fullMesage}");
            }

        }
    }
}
