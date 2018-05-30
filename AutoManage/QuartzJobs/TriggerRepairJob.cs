using AutoManage.Helper;
using log4net;
using Quartz;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tinghua.Management.Utility.Extensions;

namespace AutoManage.QuartzJobs
{
    /// <summary>
    /// 修复触发器同步订单状态异常数据
    /// </summary>
    class TriggerRepairJob : IJob
    {
        private readonly ILog _errLog = LogManager.GetLogger("Com.Foo");
        private readonly ILog _logger = LogManager.GetLogger(typeof(TriggerRepairJob));

        void IJob.Execute(IJobExecutionContext context)
        {
            _logger.InfoFormat($"TriggerRepairJob-修复触发器异常数据开始运行...");
            #region 非包月订单修复逻辑(排除了合并订单)
            Sql.SqlServerClient<Tinghua.Management.Model.Orders> db = Sql.SqlServerClientSingleton<Tinghua.Management.Model.Orders>.Instance;
            var SQL = @"select p.id,(case o.orderstate when 4 then 2 when 5 then 2 when 6 then 2 when 2 then 1 when 3 then 1 when 14 then 1 else -1 end) as State,o.Person,o.Phone,o.Province,o.City,o.Area,o.AddressLongLat,CONVERT(varchar(100),(case when o.ReciveTime is null then DATEADD(DAY,2,o.OrderAddTime) else o.ReciveTime end), 120) as ReciveTime from printorderset p,Orders o 
                        where o.orderid=p.orders_orderid  and o.type != 2 and MergeType is null and NoteStr is null and o.orderstate=3
                        and(o.Person != p.Person or o.Phone != p.Phone or o.Province != p.Province or o.City != p.City or o.Area != p.Area or RTRIM(replace(o.AddressLongLat, CHAR(10), '')) != RTRIM(replace(o.AddressLongLat, CHAR(10), ''))
                        or(CONVERT(varchar(100), p.ReciveTime, 120) != CONVERT(varchar(100), (case when o.ReciveTime is null then DATEADD(DAY, 2, o.OrderAddTime) else o.ReciveTime end), 120))
                        or p.State != (case o.orderstate when 4 then 2 when 5 then 2 when 6 then 2 when 2 then 1 when 3 then 1 when 14 then 1 else -1 end)
                        )";
            var orderIdTable = db.ExecuteTable(SQL);
            try
            {
                if (orderIdTable.Rows.Count > 0)
                {
                    var orderStatusSql = string.Empty;//需要插入到主订单状态表的sql
                    var Person = string.Empty;
                    var Phone = string.Empty;
                    var Province = string.Empty;
                    var City = string.Empty;
                    var Area = string.Empty;
                    var AddressLongLat = string.Empty;
                    var State = string.Empty;
                    var ReciveTime = string.Empty;
                    var id = 0;
                    int j = 0;
                    for (int i = 0; i < orderIdTable.Rows.Count; i++)
                    {
                        j++;
                        id = orderIdTable.Rows[i]["id"].ToString().ToInt32();
                        Person = orderIdTable.Rows[i]["Person"].ToString();
                        Phone = orderIdTable.Rows[i]["Phone"].ToString();
                        Province = orderIdTable.Rows[i]["Province"].ToString();
                        City = orderIdTable.Rows[i]["City"].ToString();
                        Area = orderIdTable.Rows[i]["Area"].ToString();
                        AddressLongLat = orderIdTable.Rows[i]["AddressLongLat"].ToString();
                        State = orderIdTable.Rows[i]["State"].ToString();
                        ReciveTime = orderIdTable.Rows[i]["ReciveTime"].ToString();
                        orderStatusSql += $"update printorderset set Person='{Person}',Phone='{Phone}',Province='{Province}',City='{City}',Area='{Area}',AddressLongLat='{AddressLongLat}',State={State},ReciveTime='{ReciveTime}' where id={id}";
                        //一次执行50个
                        if (j >= 50)
                        {
                            j = 1;
                            db.ExecuteSql(orderStatusSql);
                            orderStatusSql = string.Empty;
                        }
                    }
                    //修改主订单状态为待评价

                    if (!string.IsNullOrEmpty(orderStatusSql))
                    {
                        db.ExecuteSql(orderStatusSql);//插入主订单状态改变记录
                    }
                    _logger.InfoFormat($"TriggerRepairJob-修复触发器异常数据之非包月订单修复,本次修改了{orderIdTable.Rows.Count}个.");
                }
                else
                {
                    _logger.InfoFormat("TriggerRepairJob-修复触发器异常数据之非包月订单修复-没有需要处理的订单");
                }
            }
            catch (Exception ex)
            {
                _logger.InfoFormat($"TriggerRepairJob-修复触发器异常数据修复之非包月订单修复报错-{ex.Message}");
                var fullMesage = ErrorHelper.FullException(ex);
                _errLog.ErrorFormat($"TriggerRepairJob错误信息;{fullMesage}");
            }
            #endregion

            #region 包月订单修复逻辑

            var MonthSQL = @"select p.id,(case o.Status when 3 then 2 when 4 then 2 when 2 then 1 when 1 then 1 else -1 end) as State,o.Person,o.Phone,o.Province,o.City,o.Area,o.AddressLongLat,o.SendTime ReciveTime from printorderset p,orderchild o 
                        where o.Id=p.OrderChild_Id  and o.Status=1
                        and(o.Person != p.Person or o.Phone != p.Phone or o.Province != p.Province or o.City != p.City or o.Area != p.Area or RTRIM(replace(o.AddressLongLat, CHAR(10), '')) != RTRIM(replace(o.AddressLongLat, CHAR(10), ''))
                        or(CONVERT(varchar(100), p.ReciveTime, 120) != CONVERT(varchar(100), SendTime, 120))
                        or p.State != (case o.Status when 3 then 2 when 4 then 2 when 2 then 1 when 1 then 1 else -1 end)
                        )";
            orderIdTable = db.ExecuteTable(MonthSQL);
            try
            {
                if (orderIdTable.Rows.Count > 0)
                {
                    var orderStatusSql = string.Empty;//需要插入到主订单状态表的sql
                    var Person = string.Empty;
                    var Phone = string.Empty;
                    var Province = string.Empty;
                    var City = string.Empty;
                    var Area = string.Empty;
                    var AddressLongLat = string.Empty;
                    var State = string.Empty;
                    var ReciveTime = string.Empty;
                    var id = 0;
                    int j = 0;
                    for (int i = 0; i < orderIdTable.Rows.Count; i++)
                    { 
                        j++;
                        id = orderIdTable.Rows[i]["id"].ToString().ToInt32();
                        Person = orderIdTable.Rows[i]["Person"].ToString();
                        Phone = orderIdTable.Rows[i]["Phone"].ToString();
                        Province = orderIdTable.Rows[i]["Province"].ToString();
                        City = orderIdTable.Rows[i]["City"].ToString();
                        Area = orderIdTable.Rows[i]["Area"].ToString();
                        AddressLongLat = orderIdTable.Rows[i]["AddressLongLat"].ToString();
                        State = orderIdTable.Rows[i]["State"].ToString();
                        ReciveTime = orderIdTable.Rows[i]["ReciveTime"].ToString();
                        orderStatusSql += $"update printorderset set Person='{Person}',Phone='{Phone}',Province='{Province}',City='{City}',Area='{Area}',AddressLongLat='{AddressLongLat}',State={State},ReciveTime='{ReciveTime}' where id={id}";
                        //一次执行50个
                        if (j >= 50)
                        {
                            j = 1;
                            db.ExecuteSql(orderStatusSql);
                            orderStatusSql = string.Empty;
                        }
                    }
                    //修改主订单状态为待评价

                    if (!string.IsNullOrEmpty(orderStatusSql))
                    {
                        db.ExecuteSql(orderStatusSql);//插入主订单状态改变记录
                    }
                    _logger.InfoFormat($"TriggerRepairJob-修复触发器异常数据之包月订单修复,本次修改了{orderIdTable.Rows.Count}个.");
                }
                else
                {
                    _logger.InfoFormat("TriggerRepairJob-修复触发器异常数据之包月订单修复-没有需要处理的订单");
                }
            }
            catch (Exception ex)
            {
                _logger.InfoFormat($"TriggerRepairJob-修复触发器异常数据修复之包月订单修复报错-{ex.Message}");
                var fullMesage = ErrorHelper.FullException(ex);
                _errLog.ErrorFormat($"TriggerRepairJob错误信息;{fullMesage}");
            }

            #endregion
        }
    }
}
