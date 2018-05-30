using log4net;
using Quartz;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ServiceStack;
using Tinghua.Management.BLL;
using Tinghua.Management.Model;
using AutoManage.Helper;

namespace AutoManage.QuartzJobs
{
    /// <summary>
    /// 此自动任务已停止运行
    /// 限购商品购买后发送短信优惠码
    /// </summary>
    class LimitShopSendMsgJob :IJob
    {
        private readonly ILog _errLog = LogManager.GetLogger("Com.Foo");
        private readonly ILog _logger = LogManager.GetLogger(typeof(LimitShopSendMsgJob));

        public void Execute(IJobExecutionContext context)
        {
            try
            {
                #region lgq
                var datetime = DateTime.Now.AddDays(-1).ToShortDateString();
                var et = DateTime.Now.ToShortDateString();
                Sql.SqlServerClient<Orders> db = Sql.SqlServerClientSingleton<Orders>.Instance;
                var sql = $"select og.GoodsId,o.Phone,og.OrderGoodsID from orders o,ordergoodss og where o.orderid=og.orderid and og.GoodsId in (415,416) and o.OrderState >1 and OrderAddTime >= '{datetime}' and OrderAddTime < '{et}' order by OrderGoodsID asc ";
                //var sql = $"select og.GoodsId,o.Phone from orders o,ordergoodss og where o.orderid=og.orderid and og.GoodsId in (415,416) and o.OrderState >1 and OrderAddTime < '2017-04-23'";
                var orderIdTable = db.ExecuteTable(sql);
                sql = "select o.Phone into #TmpPhoneLin  from orders o,ordergoodss og where o.orderid=og.orderid "
                + "  and og.GoodsId in (415,416) and o.OrderState > 1 and OrderAddTime >='"+datetime+ "' and OrderAddTime < '"+et+"' group by  o.Phone;"
                + " select count(0) num, o.Phone from orders o  inner join #TmpPhoneLin s on o.Phone=s.Phone"
                + " inner join ordergoodss og on o.orderid = og.orderid"
                + " where OrderAddTime < '"+ datetime + "' and og.GoodsId in (415, 416)"
                + " group by o.Phone; drop table #TmpPhoneLin ";
                var historyTable = db.ExecuteTable(sql);
                var phoneList = new List<string>();
                var historyList = new Dictionary<string,int>();
                var orderListOne5 = new List<string>();
                var orderListOne6 = new List<string>();
                var orderListTwo5 = new List<string>();
                var orderListTwo6 = new List<string>();
                if (orderIdTable != null && orderIdTable.Rows.Count > 0)
                {
                    var phone = "";
                    int num = 0;
                    int OrderGoodsID = 0;
                    if (historyTable != null && historyTable.Rows.Count > 0)
                    {

                        phone = "";
                        foreach (DataRow item in historyTable.Rows)
                        {
                            phone = item["Phone"].ToString();
                            if (string.IsNullOrEmpty(phone))
                            {
                                continue;
                            }
                            historyList.Add(phone, int.Parse(item["num"].ToString()));
                        }
                    }
                    foreach (DataRow item in orderIdTable.Rows)
                    {
                        phone = item["Phone"].ToString();
                        OrderGoodsID = int.Parse(item["OrderGoodsID"].ToString());
                        if (string.IsNullOrEmpty(phone) || (historyList.ContainsKey(phone)&& historyList[phone]>1))
                        {
                            continue;
                        }
                        else if (historyList.ContainsKey(phone) && historyList[phone] ==1)
                        {
                            num =
                              orderIdTable.Select()
                                  .Where(
                                      o =>
                                          o["phone"].ToString() == phone && int.Parse(o["OrderGoodsID"].ToString()) < OrderGoodsID)
                                  .Count();
                            switch (num)
                            {
                                case 0:
                                    if (item["GoodsId"].ToString() == "415")
                                    {
                                        orderListTwo5.Add(phone);
                                    }
                                    else
                                    {
                                        orderListTwo5.Add(phone);
                                    }
                                    break;
                               
                            }
                        }
                        else
                        {
                            num =
                               orderIdTable.Select()
                                   .Where(
                                       o =>
                                           o["phone"].ToString() == phone && int.Parse(o["OrderGoodsID"].ToString()) < OrderGoodsID)
                                   .Count();
                            switch (num)
                            {
                                case 0:
                                    if (item["GoodsId"].ToString() == "415")
                                    {
                                        orderListOne5.Add(phone);
                                    }
                                    else
                                    {
                                        orderListOne6.Add(phone);
                                    }
                                    break;
                                case 1:
                                    if (item["GoodsId"].ToString() == "415")
                                    {
                                        orderListTwo5.Add(phone);
                                    }
                                    else
                                    {
                                        orderListTwo6.Add(phone);
                                    }
                                    break;
                            }

                        }

                    }
                    orderListOne5.ToList().ForEach(l =>
                    {
                        //发送短信
                        new SendMessageService().SendSmsMessage(phone, "170058", null);
                    });
                    orderListOne6.ToList().ForEach(l =>
                    {
                        //发送短信
                        new SendMessageService().SendSmsMessage(phone, "170059", null);
                    });
                    orderListTwo5.ToList().ForEach(l =>
                    {
                        //发送短信
                        new SendMessageService().SendSmsMessage(phone, "169770", null);
                    });
                    orderListTwo6.ToList().ForEach(l =>
                    {
                        //发送短信
                        new SendMessageService().SendSmsMessage(phone, "169768", null);
                    });
                    _logger.InfoFormat($"自动任务LimitShopSendMsgJob_{orderIdTable.Rows.Count}个订单,排除重复短信发送成功{orderListOne5.Count + orderListOne6.Count+ orderListTwo6.Count+ orderListTwo5.Count}");
                }
                else
                {
                    _logger.InfoFormat("自动任务LimitShopSendMsgJob_没有需要发送优惠码的订单");
                }

                #endregion
                #region
                //var datetime = DateTime.Now.AddDays(-1).ToShortDateString();
                //var et = DateTime.Now.ToShortDateString();
                //Sql.SqlServerClient<Orders> db = Sql.SqlServerClientSingleton<Orders>.Instance;
                //var sql = $"select og.GoodsId,o.Phone from orders o,ordergoodss og where o.orderid=og.orderid and og.GoodsId in (415,416) and o.OrderState >1 and OrderAddTime >= '{datetime}' and OrderAddTime < '{et}'";
                ////var sql = $"select og.GoodsId,o.Phone from orders o,ordergoodss og where o.orderid=og.orderid and og.GoodsId in (415,416) and o.OrderState >1 and OrderAddTime < '2017-04-23'";
                //var orderIdTable = db.ExecuteTable(sql);
                //var orderList5 = new List<string>();
                //var orderList6 = new List<string>();
                //if (orderIdTable.Rows.Count >0)
                //{
                //    var phone = "";
                //    for (int i = 0; i < orderIdTable.Rows.Count; i++)
                //    {
                //        phone = orderIdTable.Rows[i]["Phone"].ToString();
                //        if (string.IsNullOrEmpty(phone))
                //        {
                //            continue;
                //        }
                //        if (orderIdTable.Rows[i]["GoodsId"].ToString()== "415")
                //        {
                //            if (!(orderList5.Where(l=>l==phone).Any()))
                //            {
                //                orderList5.Add(phone);
                //            }
                //        }
                //        else if (orderIdTable.Rows[i]["GoodsId"].ToString() == "416")
                //        {
                //            if (!(orderList6.Where(l => l == phone).Any()))
                //            {
                //                orderList6.Add(phone);
                //            }
                //        }
                //    }
                //    orderList5.Distinct().ToList().ForEach(l => {
                //        //发送短信
                //        new SendMessageService().SendSmsMessage(phone, "170058", null);
                //    });
                //    orderList6.Distinct().ToList().ForEach(l => {
                //        //发送短信
                //        new SendMessageService().SendSmsMessage(phone, "170059", null);
                //    });
                //    _logger.InfoFormat($"自动任务LimitShopSendMsgJob_{orderIdTable.Rows.Count}个订单,排除重复短信发送成功{orderList5.Count+orderList6.Count}");
                //}
                //else
                //{
                //    _logger.InfoFormat("自动任务LimitShopSendMsgJob_没有需要发送优惠码的订单");
                //}
                #endregion
            }
            catch (Exception ex)
            {
                _logger.InfoFormat($"自动任务LimitShopSendMsgJob_出现异常{ex.Message}");
                var fullMesage = ErrorHelper.FullException(ex);
                _errLog.ErrorFormat($"BugJobs错误信息;{fullMesage}");
            }
        }
    }
}
