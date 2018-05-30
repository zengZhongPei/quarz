using log4net;
using Newtonsoft.Json.Linq;
using Quartz;
using ServiceStack;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Tinghua.Management.BLL;
using Tinghua.Management.Model;
using Tinghua.Management.Utility;

namespace AutoManage.QuartzJobs
{
    /// <summary>
    /// 此自动任务已停止运行
    /// 发货和收货短信提醒
    /// </summary>
    public sealed class ReminderJob : IJob
    {
        private readonly ILog _logger = LogManager.GetLogger(typeof(ReminderJob));

        public void Execute(IJobExecutionContext context)
        {
            Sql.SqlServerClient<Orders> db = Sql.SqlServerClientSingleton<Orders>.Instance;
            var DeliveryCount = 0;
            //需要发送发货提醒
            var OrderTable = db.ExecuteTable("select ocp.Number,oc.phone,oc.id from OrderChild oc,OrderChildExpress ocp where oc.Id=ocp.OrderChild_Id and SendTime>='2017-03-29'and SendTime<CONVERT(varchar(100), getdate()+2, 23) and ocp.Number is not null and (oc.DeliveryReminder is null or oc.DeliveryReminder='')");
            //需要发送收货提醒的
            var ReceivingReminderOrder = db.ExecuteTable("select ocp.Number,oc.phone,oc.id,o.UserAgent from OrderChild oc,OrderChildExpress ocp,Orders o where oc.Id=ocp.OrderChild_Id and oc.Orders_OrderId=o.OrderId and ocp.Number is not null and oc.DeliveryReminder='已发' and (oc.ReceivingReminder is null or oc.ReceivingReminder='')");
         
            #region 处理需要发送发货提醒的订单
                if (OrderTable.Rows.Count > 0)
                {
                    var orderIdStr = string.Empty;//需要修改已发发货提醒的订单
                    for (int i = 0; i < OrderTable.Rows.Count; i++)
                    {
                        var Number = OrderTable.Rows[i]["Number"].ToString();
                        var phone = OrderTable.Rows[i]["phone"].ToString();
                        var id = OrderTable.Rows[i]["id"].ToString();
                        if (id.ToInt() % 2 != 0) continue; //执行偶数
                        var url = string.Format("http://m.kuaidi100.com/query?type=shunfeng&postid={0}&id=1&valicode=1&temp=0.6158711992580131", Number);
                         var obj = (JObject)JsonHelper.Deserialize(HttpHelper.Get(url, new { }));
                        //var obj = (JObject)JsonHelper.Deserialize(sendMsg(Number));
                        if (obj["status"].ToString() == "200" && (obj["state"].ToString() == "0" || obj["state"].ToString() == "1"))//说明已签收
                        {
                            orderIdStr = orderIdStr == "" ? id.ToString() : $"{orderIdStr},{id}";
                           //发送短信
                           new SendMessageService().SendSmsMessage(phone, "162029", null);
                        }
                        else
                        {
                           Console.WriteLine($"{i}/{OrderTable.Rows.Count}:子订单ID{id}-运单号:{Number}--查询快递返回结果message{obj["message"].ToString()}");
                        }
                    //if (obj["message"].ToString().Trim()=="ok")//说明已揽件
                    //{
                    //    if (obj["status"].ToString() == "200" && (obj["state"].ToString() == "0" || obj["state"].ToString() == "1"))
                    //    {
                    //        orderIdStr = orderIdStr == "" ? id.ToString() : $"{orderIdStr},{id}";
                    //        //发送短信
                    //        new SendMessageService().SendSmsMessage(phone, "162029", null);
                    //    }

                    //}
                    //else
                    //{
                    //    Console.WriteLine($"{i}/{OrderTable.Rows.Count}:子订单ID{id}-运单号:{Number}--查询快递返回结果message{obj["message"].ToString()}");
                    //}
                    System.Threading.Thread.Sleep(6);//程序休眠6S然后在查询 防止IP被封
                }
                if (!string.IsNullOrEmpty(orderIdStr))
                {
                    DeliveryCount = db.ExecuteSql($"update OrderChild set DeliveryReminder='已发' where id in ({orderIdStr})");
                    _logger.InfoFormat($"共查询到{OrderTable.Rows.Count}个需要发发货提醒,发送短信的子订单ID为{orderIdStr}");
                }
                else
                {
                    _logger.InfoFormat($"共查询到{OrderTable.Rows.Count}个需要发收货提醒但没有符合发送短信条件的");
                }
            }
            #endregion

            #region 处理需要发送收货提醒的
            if (ReceivingReminderOrder.Rows.Count > 0)
            {
                var orderIdStr = string.Empty;//需要修改已发发货提醒的订单
                for (int i = 0; i < ReceivingReminderOrder.Rows.Count; i++)
                {
                    var Number = ReceivingReminderOrder.Rows[i]["Number"].ToString();
                    var phone = ReceivingReminderOrder.Rows[i]["phone"].ToString();
                    var id = ReceivingReminderOrder.Rows[i]["id"].ToString();
                    var UserAgent = ReceivingReminderOrder.Rows[i]["UserAgent"].ToString();
                    if (id.ToInt() % 2 != 0) continue; //执行偶数
                    var url = string.Format("http://m.kuaidi100.com/query?type=shunfeng&postid={0}&id=1&valicode=1&temp=0.6158711992580131", Number);
                    var obj = (JObject)JsonHelper.Deserialize(HttpHelper.Get(url, new { }));
                   // var obj = (JObject)JsonHelper.Deserialize(sendMsg(Number));
                    if (obj["status"].ToString() == "200" && obj["state"].ToString() == "3")//说明已签收
                    {
                        orderIdStr = orderIdStr == "" ? id.ToString() : $"{orderIdStr},{id}";
                        //发送短信
                        switch (UserAgent)
                        {
                            case "淘宝":
                                new SendMessageService().SendSmsMessage(phone, "162498", null);
                                break;
                            case "iOS":
                                new SendMessageService().SendSmsMessage(phone, "162031", null);
                                break;
                            case "Android":
                                new SendMessageService().SendSmsMessage(phone, "162031", null);
                                break;
                            case "微信":
                                new SendMessageService().SendSmsMessage(phone, "162032", null);
                                break;
                            case "":
                                new SendMessageService().SendSmsMessage(phone, "162032", null);
                                break; 
                            default:
                                new SendMessageService().SendSmsMessage(phone, "162032", null);
                                break;
                        }
                        Console.WriteLine($"{i}/{ReceivingReminderOrder.Rows.Count}:子订单ID{id}-运单号:{Number}短信已发送成功");
                    }
                    else
                    {
                        Console.WriteLine($"{i}/{ReceivingReminderOrder.Rows.Count}:子订单ID{id}-运单号:{Number}--查询快递返回结果status:{obj["status"].ToString()}-state:{obj["state"].ToString()}");
                    }
                    System.Threading.Thread.Sleep(6);//程序休眠6S然后在查询 防止IP被封
                }
                if (!string.IsNullOrEmpty(orderIdStr))
                {
                    DeliveryCount = db.ExecuteSql($"update OrderChild set ReceivingReminder='已发' where id in ({orderIdStr})");
                    _logger.InfoFormat($"共查询到{ReceivingReminderOrder.Rows.Count}个需要发收货提,发送短信的子订单ID为{orderIdStr}");
                }
                else{
                    _logger.InfoFormat($"共查询到{ReceivingReminderOrder.Rows.Count}个需要发收货提醒但没有符合发送短信条件的");
                }
            }

            #endregion


            if (OrderTable.Rows.Count <= 0 && ReceivingReminderOrder.Rows.Count <= 0)
            {
                _logger.InfoFormat("没有需要处理的发货和收货短信提醒");
            }
        }

        public string sendMsg(string num)
        {
            string url = "http://poll.kuaidi100.com/poll/query.do";
            Encoding encoding = Encoding.GetEncoding("utf-8");
            
            String param = "{\"com\":\"shunfeng\",\"num\":\"" + num + "\",\"from\":\"\",\"to\":\"\"}";
            String customer = "37B64AFE463A17ED5B8F76B84A3E4985";
            String key = "aOkmTsXL4434";
            MD5CryptoServiceProvider md5 = new MD5CryptoServiceProvider();
            byte[] InBytes = Encoding.GetEncoding("UTF-8").GetBytes(param + key + customer);
            byte[] OutBytes = md5.ComputeHash(InBytes);
            string OutString = "";
            for (int i = 0; i < OutBytes.Length; i++)
            {
                OutString += OutBytes[i].ToString("x2");
            }
            String sign = OutString.ToUpper();
            IDictionary parameters = new Dictionary<string, string>();
            parameters.Add("param", param);
            parameters.Add("customer", customer);
            parameters.Add("sign", sign);
            return HttpHelper.Post(url, new { param= param, customer= customer, sign= sign }, encoding);

        }
    }
}
