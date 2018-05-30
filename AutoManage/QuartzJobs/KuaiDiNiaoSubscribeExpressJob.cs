using AutoManage.Helper;
using log4net;
using Quartz;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Tinghua.Management.Model;
using Tinghua.Management.Utility.Helper;

namespace AutoManage.QuartzJobs
{
    /// <summary>
    /// 快递鸟物流订阅接口
    /// </summary>
    public sealed class KuaiDiNiaoSubscribeExpressJob : IJob
    {
        private readonly ILog _errLog = LogManager.GetLogger("Com.Foo");
        private readonly ILog _logger = LogManager.GetLogger(typeof(ConsolidateOrderStateRepairJob));

        public void Execute(IJobExecutionContext context)
        {
            _logger.InfoFormat($"KuaiDiNiaoSubscribeExpressJob-订阅快递跟踪信息开始处理");
             Sql.SqlServerClient<Orders> db = Sql.SqlServerClientSingleton<Orders>.Instance;
            var sql = $@"select Person,Phone,Province,City,Area,AddressLongLat,ManagerId,CPCode,cpnumber from printorderset
                         where CPCode is not null and state = 2 
                         and cpcode not in ('SF','JD','spd','thld','wtp','KSD','QSD','KSDTMS')
                         and (IsSubscribeExpress <> 'true' or IsSubscribeExpress is null)
                         and DATEDIFF(DD, PrintTime, GETDATE()) < 30
                         group by Person,Phone,Province,City,Area,AddressLongLat,ManagerId,CPCode,cpnumber";
            var resultTable = db.ExecuteTable(sql);
            var provinceSql="select Name from provinceset";
            var provinceTable = db.ExecuteTable(provinceSql);
            var provinceList = new List<string>();
            for (int j = 0; j < provinceTable.Rows.Count; j++)
            {
                provinceList.Add(provinceTable.Rows[j]["Name"].ToString());
            }
            var citySql = "select Name from cityset";
            var cityTable = db.ExecuteTable(citySql);
            var cityList = new List<string>();
            var successCount = 0;
            var failCount = 0;
            for (int j = 0; j < cityTable.Rows.Count; j++)
            {
                cityList.Add(cityTable.Rows[j]["Name"].ToString());
            }
            var updatePrintOrderSql = string.Empty;
            try
            {
                if (resultTable.Rows.Count > 0)
                {
                    for (int i = 0; i < resultTable.Rows.Count; i++)
                    {
                        var Person = resultTable.Rows[i]["Person"].ToString().Trim();
                        var Phone = resultTable.Rows[i]["Phone"].ToString().Trim();
                        var Province = resultTable.Rows[i]["Province"].ToString().Trim();
                        if (Province.Length <= 2)
                        {
                            Province = provinceList.Where(l => l.Contains(Province)).FirstOrDefault();
                        }
                        var City = resultTable.Rows[i]["City"].ToString().Trim();
                        if (City.Length <= 2)
                        {
                            City = cityList.Where(l => l.Contains(City)).FirstOrDefault();
                        }
                        var Area = resultTable.Rows[i]["Area"].ToString().Trim();
                        var AddressLongLat = resultTable.Rows[i]["AddressLongLat"].ToString().Trim();
                        var ManagerId = resultTable.Rows[i]["ManagerId"].ToString().Trim();
                        var CPCode = resultTable.Rows[i]["CPCode"].ToString().Trim();
                        var cpnumber = resultTable.Rows[i]["cpnumber"].ToString().Trim();
                        var subscribeResult = KuaiDiHelper.ExpressSubscribe(CPCode, cpnumber, ManagerId, Phone, Person, Province, City, Area, AddressLongLat);
                        if (subscribeResult)
                        {
                            updatePrintOrderSql += $"update printorderset set IsSubscribeExpress='true',SubscribeExpressTime=getdate() where cpnumber='{cpnumber}' and CPCode='{CPCode}'";
                            successCount++;
                        }
                        else
                        {
                            updatePrintOrderSql += $"update printorderset set IsSubscribeExpress='false',SubscribeExpressTime=getdate() where cpnumber='{cpnumber}' and CPCode='{CPCode}'";
                            failCount++;
                        }
                        if (i % 50 == 0 && !string.IsNullOrWhiteSpace(updatePrintOrderSql))
                        {
                            db.ExecuteSql(updatePrintOrderSql);
                            updatePrintOrderSql = string.Empty;
                        }
                        Thread.Sleep(1300);//快递鸟那边接口并发 30/s
                    }
                    if (!string.IsNullOrWhiteSpace(updatePrintOrderSql))
                    {
                        db.ExecuteSql(updatePrintOrderSql);
                        updatePrintOrderSql = string.Empty;
                    }
                    _logger.InfoFormat($"KuaiDiNiaoSubscribeExpressJob-订阅快递跟踪信息处理成功，处理成功:{successCount} 失败{failCount}");

                }
                else
                    _logger.InfoFormat($"KuaiDiNiaoSubscribeExpressJob-订阅快递跟踪信息没有需要处理的信息");

            }
            catch (Exception ex)
            {
                _logger.InfoFormat($"KuaiDiNiaoSubscribeExpressJob-订阅快递跟踪信息错误{ex.Message}");
                var fullMesage = ErrorHelper.FullException(ex);
                _errLog.ErrorFormat($"KuaiDiNiaoSubscribeExpressJob-订阅快递跟踪信息错误;{fullMesage}");

            }
        }
    }
}
