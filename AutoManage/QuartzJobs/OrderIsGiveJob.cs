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
using Tinghua.Management.Utility;
using Tinghua.Management.Utility.Extensions;
using Tinghua.Management.Utility.Helper;

namespace AutoManage.QuartzJobs
{
    /// <summary>
    /// 自动任务处理赠送花瓶异常数据场景(当一个用户下了2个订单。第一个订单送花瓶。第二个订单不送花瓶。第一单退款后。需要把送花瓶添加到第二单上面去.此时需要修改用户在打单表里面的数据)
    /// </summary>
    class OrderIsGiveJob : IJob
    {
        private readonly ILog _errLog = LogManager.GetLogger("Com.Foo");
        private readonly ILog _logger = LogManager.GetLogger(typeof(OrderIsGiveJob));

        public void Execute(IJobExecutionContext context)
        {
            try
            {
                _logger.InfoFormat($"OrderIsGiveJob-处理赠送花瓶开始执行...");
                Sql.SqlServerClient<Orders> db = Sql.SqlServerClientSingleton<Orders>.Instance;
                #region 把下单并且发货的用户的手机号放入redis里面去，用于后续做是否首次下单判断.
                var state = $"{ OrderStatusEnum.待评价.GetHashCode()},{OrderStatusEnum.已付款.GetHashCode()},{OrderStatusEnum.已确认.GetHashCode()}, {OrderStatusEnum.已完成.GetHashCode()},{ OrderStatusEnum.待收货.GetHashCode()}";
                _logger.InfoFormat($"自动任务InsertFirstOrderJob-读取订单数据到redis开始运行...");
                try
                {
                    var datetimes = DateTime.Now.AddDays(-35).ToShortDateString();
                    var et = DateTime.Now.AddDays(1).ToShortDateString();
                    var sqls = $"select OrderId,Phone from Orders where OrderState in ({state}) and type=2";
                    var idval = RedisHelper.Get("FirstOrderPhone_proint");
                    var orderTable = db.ExecuteTable(sqls);
                    var temp = "";
                    var phone = "";
                    var list = new List<string>();
                    if (orderTable.Rows.Count > 0)
                    {
                        for (int i = 0; i < orderTable.Rows.Count; i++)
                        {
                            phone = orderTable.Rows[i]["Phone"].ToString().Trim();
                            if (string.IsNullOrEmpty(phone))
                            {
                                continue;
                            }
                            if (phone.Length > 11)//如果手机号码大于了就提取正确的手机号码。
                            {
                                var ary = System.Text.RegularExpressions.Regex.Matches(phone, @"1[345789]\d{9}").Cast<System.Text.RegularExpressions.Match>().Select(t => t.Value).ToArray();
                                if (ary.Count() > 0)
                                {
                                    phone = ary[0];
                                }
                            }
                            temp = $"{phone}_{orderTable.Rows[i]["OrderId"].ToString()}";
                            //检查是否已经把改手机号放到redis了，如果放到了就更新后面的ID值
                            if (list.Where(l => l == temp).Any())
                            {
                                list.RemoveAll(l => l == temp);
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
                catch (Exception e)
                {
                    _logger.InfoFormat($"自动任务InsertFirstOrderJob-读取订单数据到redis出现异常{e.Message}");
                }

                #endregion
                var orderIsGiveTable = db.ExecuteTable("select p.id,p.GoodAttrId,o.phone,o.userid,o.OrderId,p.OrderChild_Id,o.OrderSerialNumber from printorderset p,Orders o where p.orders_orderid=o.orderid and Times =1 and State=1 and isgive!=1");
                if (orderIsGiveTable.Rows.Count > 0)
                {
                    var orderidList = new List<string>();
                    var userIdList = new List<string>();
                    for (int i = 0; i < orderIsGiveTable.Rows.Count; i++)
                    {
                        orderidList.Add(orderIsGiveTable.Rows[i]["OrderId"].ToString());
                        userIdList.Add(orderIsGiveTable.Rows[i]["phone"].ToString());
                    }
                    var userIds = string.Join(",", userIdList.Distinct());
                    var orderIds = string.Join(",", orderidList.Distinct());
                    //获取所有需要赠送花瓶的订单
                    var GiveOrder = HttpHelper.Post("http://admin.listenflower.com/Open/OrderGive", new { Phone = userIds, orderid = orderIds }).Split(',').ToArray();
                    var giveList = new List<int>();
                    var keys = string.Empty;
                    var sql = string.Empty;
                    for (int i = 0; i < orderIsGiveTable.Rows.Count; i++)
                    {
                        keys = $"{orderIsGiveTable.Rows[i]["userid"].ToString()}{orderIsGiveTable.Rows[i]["OrderId"].ToString()}{orderIsGiveTable.Rows[i]["OrderChild_Id"].ToString()}";
                        if (GiveOrder.Contains(keys))
                        {
                            giveList.Add(orderIsGiveTable.Rows[i]["id"].ToString().ToInt32());
                        }
                        else
                        {
                            var bl = false;//表示该订单里面是否购买了mini包月商品。
                            var miniGoodssid = RedisHelper.Get($"TingHua_miniGoodssid");//mini商品ID。配送方式需要改变一下。一周送2束  隔周配送
                            miniGoodssid += $",{RedisHelper.Get($"TingHua_ZhuTiGoodssid")}";
                            var GoodAttrId = string.IsNullOrEmpty(orderIsGiveTable.Rows[i]["GoodAttrId"].ToString()) ? "" : orderIsGiveTable.Rows[i]["GoodAttrId"].ToString();
                            var goodAttrIdArr = Array.ConvertAll<string, int>(GoodAttrId.Split('#'), l => int.Parse(l));
                            if (!string.IsNullOrEmpty(miniGoodssid))
                            {
                                var arr1 = Array.ConvertAll<string, int>(miniGoodssid.Split(',').ToArray(), l => int.Parse(l));
                                foreach (var item in goodAttrIdArr)
                                {
                                    if (arr1.Contains(item))
                                    {
                                        bl = true;
                                        break;
                                    }
                                }
                            }
                            if (bl)
                            {
                                sql = $"select  count(1) miniCount from Orders o,OrderGoodss og where o.OrderId=og.OrderId and og.goodsid in ({miniGoodssid}) and o.orderstate in({state}) and o.Phone='{orderIsGiveTable.Rows[i]["Phone"].ToString()}' and o.OrderId <{orderIsGiveTable.Rows[i]["OrderId"]}";
                                var dS = db.ExecuteTable(sql);
                                if (dS.Rows[0]["miniCount"].ToString().ToInt32() == 0)
                                {
                                    giveList.Add(orderIsGiveTable.Rows[i]["id"].ToString().ToInt32());
                                }
                            }
                        }
                    }
                    if (giveList.Count>0)
                    {
                        var printorderIds = string.Join(",", giveList);
                        var giveSql = $"update printorderset set IsGive=1 where id in ({printorderIds})";
                        var count=db.ExecuteSql(giveSql);
                        _logger.InfoFormat($"OrderIsGiveJob-处理赠送花瓶成功共修改了{count}数据...");
                    }
                    else
                    {
                        _logger.InfoFormat($"OrderIsGiveJob-处理赠送花瓶暂无数据...");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.InfoFormat($"OrderIsGiveJob-处理赠送花瓶出现异常...");
                var fullMesage = ErrorHelper.FullException(ex);
                _errLog.ErrorFormat($"OrderIsGiveJob错误信息;{fullMesage}");
            }

        }
    }
}
