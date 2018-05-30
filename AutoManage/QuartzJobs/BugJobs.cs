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
    /// 修复包月订单没有子订单。修复子订单个数有问题需要根据商品属性ID去读取子订单配送次数(暂停使用)
    /// </summary>
    public sealed class BugJobs : IJob
    {
        private readonly ILog _logger = LogManager.GetLogger(typeof(BugJobs));
        private readonly ILog _errLog = LogManager.GetLogger("Com.Foo");


        public void Execute(IJobExecutionContext context)
        {
            try
            {
                Sql.SqlServerClient<Orders> db = Sql.SqlServerClientSingleton<Orders>.Instance;
                var sql = $"select o.OrderSerialNumber,o.ReciveTime,o.OrderId,o.OrderState,o.Person,o.Phone,o.Province,o.City,o.Area,o.AddressLongLat from OrderGoodss og join Orders o on og.OrderId=o.OrderId where o.type=2 and o.OrderState in(2,3,4,5) and not exists(select Orders_OrderId from OrderChild where Orders_OrderId=o.OrderId)";
                var orderIdTable = db.ExecuteTable(sql);
                var insertSql = "insert into OrderChild(SendTime,Person,Phone,Province,City,Area,AddressLongLat,Status,Times,Orders_OrderId) values";
                var orderStr = "";
                if (orderIdTable.Rows.Count > 0)
                {
                    for (int i = 0; i < orderIdTable.Rows.Count; i++)
                    {
                        //var SendTime = "2017/2/14 0:00:00";
                        var Person = orderIdTable.Rows[i]["Person"].ToString();
                        var Phone = orderIdTable.Rows[i]["Phone"].ToString();
                        var Province = orderIdTable.Rows[i]["Province"].ToString();
                        var City = orderIdTable.Rows[i]["City"].ToString();
                        var Area = orderIdTable.Rows[i]["Area"].ToString();
                        var AddressLongLat = orderIdTable.Rows[i]["AddressLongLat"].ToString();
                        var Status = 1;
                        var Orders_OrderId = orderIdTable.Rows[i]["OrderId"].ToString().ToInt32();
                        var ReciveTime = orderIdTable.Rows[i]["ReciveTime"].ToString().ToDateTime();
                        orderStr += orderIdTable.Rows[i]["OrderSerialNumber"].ToString() + ",";
                        if (i == 0)
                        {
                            insertSql += $"('{ReciveTime}','{Person}','{Phone}','{Province}','{City}','{Area}','{AddressLongLat}',{Status},1,{Orders_OrderId})";
                        }
                        else
                        {
                            insertSql += $",('{ReciveTime}','{Person}','{Phone}','{Province}','{City}','{Area}','{AddressLongLat}',{Status},1,{Orders_OrderId})";

                        }
                        //for (int j = 2; j < 5; j++)
                        //{
                        //    ReciveTime = ReciveTime.AddDays(7);
                        //    insertSql += $",('{ReciveTime}','{Person}','{Phone}','{Province}','{City}','{Area}','{AddressLongLat}',{Status},{j},{Orders_OrderId})";
                        //}
                    }
                    var count = db.ExecuteSql(insertSql);
                    _logger.InfoFormat($"当前共处理{count}个订单共{orderIdTable.Rows.Count}个异常订单，订单号:{orderStr}");
                }
                else
                {
                    _logger.InfoFormat("没有需要处理的异常订单");
                }
            }
            catch (Exception ex)
            {
                var fullMesage = ErrorHelper.FullException(ex);
                _errLog.ErrorFormat($"BugJobs错误信息;{fullMesage}");
            }
            
        }
    }
}
