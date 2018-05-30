using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using log4net;
using Quartz;
using Tinghua.Management.Enum;
using Tinghua.Management.Model;
using Tinghua.Management.Utility.Extensions;
using Tinghua.Management.Utility.Helper;

using Tinghua.Management.Utility.IoC;
using System.Data;
using Tinghua.Management.Utility;
using AutoManage.Helper;

namespace AutoManage.QuartzJobs
{
    public sealed class YouZanJob : IJob
    {
        private readonly ILog _errLog = LogManager.GetLogger("Com.Foo");
        private readonly ILog _logger = LogManager.GetLogger(typeof(YouZanJob));
        private List<OrderStatus> OrderStatusList = new List<OrderStatus>();
        public void Execute(IJobExecutionContext context)
        {
            _logger.InfoFormat($"自动任务YouZanJob-读取订单数据开始运行...");

            try
            {
                #region 有赞发货同步

                HttpHelper.Get("http://admin.listenflower.com/open/youzansend", "");

                #endregion

                Sql.SqlServerClient<Orders> db = Sql.SqlServerClientSingleton<Orders>.Instance;
                int num = 0;
                bool isdo;
                int page = 1;
                int pageSize = 20;
                var begTime = DateTime.Now.AddDays(-2);
                var endTime = DateTime.Now.AddDays(1);
                string sql = string.Empty;
                Dictionary<string, string> DicsInsert = new Dictionary<string, string>();
                Dictionary<string, string> DicsUpdate = new Dictionary<string, string>();
                object isOrder = null;
                var newYouZanModelList = YouZanHelper.listModel;
                foreach (NewYouZanModel YouZanConfig in newYouZanModelList)
                {
                    num = 0;
                    page = 1;
                    while (num >= page * pageSize || page == 1)//总条数是否大于当前页*每页数量
                    {
                        //循环调取带发货订单
                        var list = YouZanHelper.GetOrders(YouZanConfig.Name, out num, out isdo, status: "WAIT_SELLER_SEND_GOODS", use_has_next: null, page_size: pageSize, page_no: page, fields: "orders,has_next,total_results,outer_sku_id,outer_item_id,buyer_message,created,feedback,feedback_num,payment,receiver_address,receiver_city,receiver_district,receiver_mobile,trade_memo,created,status,receiver_name,total_fee,receiver_state,trade_memo,tid", start_update: begTime, end_update: endTime);
                        //var list = YouZanHelper.GetOrders(YouZanConfig.Name, out num, out isdo, status: "WAIT_SELLER_SEND_GOODS", use_has_next: null, page_size: pageSize, page_no: page, fields: "orders,has_next,total_results,outer_sku_id,outer_item_id,buyer_message,created,feedback,feedback_num,payment,receiver_address,receiver_city,receiver_district,receiver_mobile,trade_memo,created,status,receiver_name,total_fee,receiver_state,trade_memo,tid", start_update: "2017-10-28".ToDateTime(), end_update: "2017-11-2".ToDateTime());
                        foreach (var model in list)
                        {
                            isOrder = db.ExecuteScalar($"select top 1 OrderState from orders where  orderSerialNumber  like '%{model.ID}%'");
                            if (isOrder == null)
                            {
                                if (InserOrder(model, db, YouZanConfig, OrderStatusEnum.已确认.GetHashCode(), OrderChildStatusEnum.已确认.GetHashCode(), "FuKuan"))
                                {
                                    DicsInsert.Add(model.ID, "true");
                                }
                                else
                                {
                                    DicsInsert.Add(model.ID, "false");
                                }
                            }
                            else
                            {
                                continue;
                            }
                            //else {
                            //    if (UpdateOrderStatus(model.ID, db, YouZanConfig, OrderStatusEnum.已确认.GetHashCode(), OrderChildStatusEnum.已确认.GetHashCode(), "FuKuan"))
                            //    {
                            //        DicsInsert.Add(model.ID, "true");
                            //    }
                            //    else
                            //    {
                            //        DicsInsert.Add(model.ID, "false");
                            //    }
                            //}
                        }
                        page++;
                    }
                    page = 1;
                    while (num >= page * pageSize || page == 1)
                    {
                        //循环调取关闭状态订单
                        var list = YouZanHelper.GetOrders(YouZanConfig.Name, out num, out isdo, status: "TRADE_CLOSED", use_has_next: null, page_size: pageSize, page_no: page, fields: "orders,has_next,total_results,outer_sku_id,outer_item_id,buyer_message,created,feedback,feedback_num,payment,receiver_address,receiver_city,receiver_district,receiver_mobile,trade_memo,created,status,receiver_name,total_fee,receiver_state,trade_memo,tid", start_update: DateTime.Parse(DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd" + " 00:00:00")), end_update: DateTime.Parse(DateTime.Now.ToString("yyyy-MM-dd") + " 00:00:00"));
                        foreach (var model in list)
                        {
                            isOrder = db.ExecuteScalar($"select top 1 OrderState from orders where  orderSerialNumber  like '%{model.ID}%'");

                            if (isOrder == null)
                            {
                                if (InserOrder(model, db, YouZanConfig, OrderStatusEnum.退款成功.GetHashCode(), OrderChildStatusEnum.客服取消.GetHashCode(), "TuiKuan"))
                                {
                                    DicsUpdate.Add(model.ID, "true");
                                }
                                else
                                {
                                    DicsUpdate.Add(model.ID, "false");
                                }

                            }
                            else if (isOrder.ToString() == "12")
                            {
                                continue;
                            }
                            else
                            {
                                if (UpdateOrderStatus(model.ID, db, YouZanConfig, OrderStatusEnum.退款成功.GetHashCode(), OrderChildStatusEnum.客服取消.GetHashCode(), "TuiKuan"))
                                {
                                    DicsUpdate.Add(model.ID, "true");
                                }
                                else
                                {
                                    DicsUpdate.Add(model.ID, "false");
                                }
                            }

                        }
                        page++;
                    }
                }
                int InsertNum = DicsInsert.Where(o => o.Value == "true").Count();
                int UpdateNum = DicsUpdate.Where(o => o.Value == "true").Count();
                _logger.InfoFormat($"自动任务YouZanJob-读取订单数据结束,总共同步了订单：{DicsInsert.Count + DicsUpdate.Count}。待发货订单同步成功：{InsertNum},待发货订单同步失败：{DicsInsert.Count - InsertNum}。关闭订单同步成功：{UpdateNum},关闭订单同步失败：{DicsUpdate.Count - UpdateNum}。同步待发货订单号：{ String.Join(",", DicsInsert.Keys)}。。同步关闭订单号：{ String.Join(",", DicsUpdate.Keys)},找不到商品:{String.Join(",", OrderStatusList.GroupBy(o => o.GoodsID).Select(g => g.First().GoodsID))}...");
                OrderStatusList.Clear();
            }
            catch (Exception ex)
            {

                var fullMesage = ErrorHelper.FullException(ex);
                _errLog.ErrorFormat($"YouZanJob错误信息;{fullMesage}");
            }

           
        }


        public int GetDayOfWeek(DateTime time)
        {
            string dt = time.DayOfWeek.ToString();
            int week = 0;
            switch (dt)
            {
                case "Monday":
                    week = 1;
                    break;
                case "Tuesday":
                    week = 2;
                    break;
                case "Wednesday":
                    week = 3;
                    break;
                case "Thursday":
                    week = 4;
                    break;
                case "Friday":
                    week = 5;
                    break;
                case "Saturday":
                    week = 6;
                    break;
                case "Sunday":
                    week = 7;
                    break;
            }
            return week;
        }

        #region 同步一个订单到数据库
        public bool InserOrder(NewTradeModel model, Sql.SqlServerClient<Orders> db, NewYouZanModel YouZanConfig, int OrderStatus, int OrderChildStatus, string ZhuangTai)
        {

            string sql = string.Empty;
            DateTime ReciveTime = DateTime.Now;
            int OrderID = 0;
            StringBuilder sqls = new StringBuilder();
            for (int j = 0; j < model.Ordes.Count; j++)
            {
                if (string.IsNullOrEmpty(model.Ordes[j].ID))
                {
                    continue;
                }
                var nids = new List<int>();
                var goodsDaylist_IDAttr = model.Ordes[j].ID.Split('&');
                var dicAttr = new Dictionary<int, int>();//用来保存skuID后面数量的键值对 比如 342*2&342*3_1,
                for (int kl = 0; kl < goodsDaylist_IDAttr.Length; kl++)
                {
                    var goodsDaylist_IDAttrs = goodsDaylist_IDAttr[kl].Split('*');
                    nids.Add(goodsDaylist_IDAttrs[0].ToInt32());
                    if (goodsDaylist_IDAttrs.Length > 1)
                    {
                        dicAttr.Add(goodsDaylist_IDAttrs[0].ToInt32(), goodsDaylist_IDAttrs[1].ToInt32());
                    }
                    else
                    {
                        dicAttr.Add(goodsDaylist_IDAttrs[0].ToInt32(), 1);
                    }
                }
                string nid = string.Join(",", nids);
                sql = $"SELECT gs.Id,bs.Name,bs.AttributeShow_Id,gs.type,GoodsId,ShopId,CategoryId,CategoryFatherId,GoodsTitle,GoodsTitleSub,GoodsImageUrl,GoodsPrice"
                      + " FROM  GoodsAttributeSet AS gs INNER JOIN AttributeSet as bs ON gs.Attribute_Id = bs.Id "
                      + " inner join AttributeShowSet ss on ss.Id = bs.AttributeShow_Id INNER JOIN    Goodss AS g ON ss.Goodss_GoodsId = g.GoodsId WHERE gs.Id in (" + nid + ")";
                var goodInfoTable = db.ExecuteTable(sql);
                if (goodInfoTable == null || goodInfoTable.Rows.Count < 1)
                {
                    OrderStatusList.Add(new OrderStatus
                    {
                        GoodsID = nid,
                        OrderID = model.ID,
                        Status = false
                    });
                    continue;
                }

                //数据库读取商品信息
                int type = 0;
                var orderType = OrderTypeEnum.单束鲜花.GetHashCode();
                try
                {
                    DataRow goodItem = null;// goodInfoTable.Rows[0];
                    if (goodInfoTable.Select("CategoryFatherId in (25)").Count() > 0)
                    {
                        type = 25;
                        goodItem = goodInfoTable.Select("CategoryFatherId in (25)")[0];
                    }
                    else if (goodInfoTable.Select("CategoryFatherId in (1)").Count() > 0)
                    {
                        type = 1;
                        goodItem = goodInfoTable.Select("CategoryFatherId in (1)")[0];
                    }
                    else if (goodInfoTable.Select("CategoryFatherId in (3)").Count() > 0)
                    {
                        type = 3;
                        goodItem = goodInfoTable.Select("CategoryFatherId in (3)")[0];
                    }
                    else
                    {
                        goodItem = goodInfoTable.Rows[0];
                        type = int.Parse(goodItem["CategoryFatherId"].ToString());
                    }
                    int WeekDay = GetDayOfWeek(model.Created);
                    int AddDay = 0;
                    //获取母亲节特定商品。然后这些特定商品可以开放购买。
                    var MothersDayGoodss = RedisHelper.Get("TingHua_MothersDayGoodss");
                    if (string.IsNullOrEmpty(MothersDayGoodss))
                    {
                        RedisHelper.Set("TingHua_MothersDayGoodss", "441");
                        MothersDayGoodss = "441";
                    }
                    int GoodsNumber = model.Ordes[j].Num;
                    switch (type)
                    {
                        case 25:
                            orderType = OrderTypeEnum.包月.GetHashCode();
                            break;
                        case 1:
                            orderType = OrderTypeEnum.单束鲜花.GetHashCode();
                            break;
                        case 3:
                            orderType = OrderTypeEnum.绿植出售.GetHashCode();
                            break;
                        default:
                            orderType = OrderTypeEnum.普通商品.GetHashCode();
                            break;
                    }
                    bool isShouCode = false;
                    if (model.Ordes[j].Day != 0 && model.Ordes[j].Day.ToString().Length == 1)//只有当是这种格式(341_1)才能进入这个里面收花时间
                    {
                        isShouCode = true;
                        AddDay = model.Ordes[j].Day - WeekDay; //假如不是包月商品
                        if (AddDay <= 0)
                        {
                            if (orderType == OrderTypeEnum.包月.GetHashCode())
                            {
                                switch (AddDay)
                                {
                                    case -6: AddDay = 8; break;
                                    //case -5: AddDay = 9; break;
                                    //case -4: AddDay = 10; break;
                                    //case -3: AddDay = 11; break;//提前3天下单把这个删掉
                                    default:
                                        AddDay = 7 - Math.Abs(AddDay);
                                        break;
                                }
                            }
                            else
                            {
                                AddDay = 7 - Math.Abs(AddDay);
                            }

                        }
                        else if (AddDay < 2 && MothersDayGoodss.Contains(nid.ToString()) && orderType == 2)
                        {
                            //母亲节特定商品开放提前一天购买。不用+7天
                        }
                        //else if (AddDay < 4 && orderType == 2)  提前3天下单用这个
                        else if (AddDay < 2 && orderType == 2)
                        {
                            AddDay = 7 + AddDay;//提前3天下单
                                                //if (AddDay ==3)
                                                //{
                                                //    var userCreateTime=DateTime.Parse(tradeJObject["created"].ToString()).Hour;
                                                //    if (userCreateTime>=16)
                                                //    {
                                                //        AddDay = 7 + AddDay;
                                                //    }
                                                //}
                                                //else
                                                //{
                                                //    AddDay = 7 + AddDay;
                                                //}
                        }
                    }

                    var dayStr = model.Ordes[j].Day.ToString();
                    if (dayStr.Length > 2 && dayStr.Length < 5)
                    {
                        var month = "01";
                        var recDay = "01";
                        if (dayStr.Length == 3)
                        {
                            month = $"0{dayStr.Substring(0, 1)}";
                            recDay = dayStr.Substring(1, 2);
                        }
                        else
                        {
                            month = dayStr.Substring(0, 2);
                            recDay = dayStr.Substring(2, 2);
                        }
                        ReciveTime = new DateTime(DateTime.Now.Year, month.ToInt32(), recDay.ToInt32());
                    }
                    else
                    {
                        ReciveTime = (AddDay > 0 ? model.Created.AddDays(AddDay) : orderType == 3 ? model.Ordes[j].ReceiveDate : model.Ordes[j].ReceiveDate.AddDays(1));
                    }

                    //收花时间顺延,概述：收花期间，为了确保用户在家能收到花（国庆，元旦出去玩），所以，这里在导入订单的时候，需要动态的修改收花时间。 因为用户每月的收花时间是固定的，定在每周几，所以顺延--一周--，7天是固定的。
                    //因为国庆，元旦或这劳动节，放假时长不一样，假如有一个20天的假期，我在这20天之内肯定不可以给客户送货，所以可能会顺延若干次，直到不在该假期之内，但是仅限于 包月鲜花，因为只有--包月鲜花--才有这个需求。（但是后期可能会有修改的可能）
                    if (!string.IsNullOrEmpty(RedisHelper.Get("Tinghua_limitEndReciveTime")) && !string.IsNullOrEmpty(RedisHelper.Get("Tinghua_limitBeginReciveTime")) && !string.IsNullOrEmpty(RedisHelper.Get("Tinghua_limitReciveTime")))
                    {
                        #region 2017年双十一收货日期更改逻辑：1某些固定类型可以不用延期；2：包月的延期跟之前的逻辑一样不用更改；3：不是包月的：如果goods_code里面有带“_”的要跟包月的延期一样，如果没有则更之前逻辑一样

                        var TingHuaOpenApiOpenController_OrderType = RedisHelper.Get("TingHuaOpenApiOpenController_OrderType");//某些类型可以不用延期
                        bool isDay = false; //当前orderType是不是属于某些可以不用延期固定类型；是:true
                        if (TingHuaOpenApiOpenController_OrderType != null)
                        {
                            List<string> notTypeList = new List<string>();
                            notTypeList = TingHuaOpenApiOpenController_OrderType.Replace('，', ',').Split(',').ToList();
                            if (notTypeList.Contains(orderType.ToString())) { isDay = true; }
                        }
                        #region 之前原来基础延期逻辑
                        if (orderType == OrderTypeEnum.包月.GetHashCode() && !isDay)
                        {
                            //是包月鲜花。
                            DateTime orderReciveTime = System.Convert.ToDateTime(ReciveTime);
                            //开始时间。
                            DateTime beginDate = System.Convert.ToDateTime(RedisHelper.Get("Tinghua_limitBeginReciveTime"));
                            //结束时间。
                            DateTime endDate = System.Convert.ToDateTime(RedisHelper.Get("Tinghua_limitEndReciveTime"));
                            //顺延日期。
                            delaySomeDate(ref orderReciveTime, 7, beginDate, endDate);
                            //重新设置订单的时间。
                            ReciveTime = orderReciveTime;
                        }
                        else
                        {
                            if (!isDay)
                            {
                                if (isShouCode)
                                {
                                    //是包月鲜花。
                                    DateTime orderReciveTime = System.Convert.ToDateTime(ReciveTime);
                                    //开始时间。
                                    DateTime beginDate = System.Convert.ToDateTime(RedisHelper.Get("Tinghua_limitBeginReciveTime"));
                                    //结束时间。
                                    DateTime endDate = System.Convert.ToDateTime(RedisHelper.Get("Tinghua_limitEndReciveTime"));
                                    //顺延日期。
                                    delaySomeDate(ref orderReciveTime, 7, beginDate, endDate);
                                    //重新设置订单的时间。
                                    ReciveTime = orderReciveTime;
                                }
                                else
                                {
                                    DateTime beginDate = System.Convert.ToDateTime(RedisHelper.Get("Tinghua_limitBeginReciveTime"));
                                    DateTime endDate = System.Convert.ToDateTime(RedisHelper.Get("Tinghua_limitEndReciveTime"));
                                    //不是包月鲜花，但是收花日期正好落在了假期内。
                                    if (beginDate < ReciveTime && ReciveTime < endDate)
                                    {
                                        ReciveTime = System.Convert.ToDateTime(RedisHelper.Get("Tinghua_limitReciveTime"));
                                    }
                                }
                            }
                        }
                        #endregion
                        //}
                        #endregion
                        #region 原来的延期代码
                        //if (orderType == OrderTypeEnum.包月.GetHashCode())
                        //{
                        //    //是包月鲜花。
                        //    DateTime orderReciveTime = System.Convert.ToDateTime(order.ReciveTime);
                        //    //开始时间。
                        //    DateTime beginDate = System.Convert.ToDateTime(RedisHelper.Get("Tinghua_limitBeginReciveTime"));
                        //    //结束时间。
                        //    DateTime endDate = System.Convert.ToDateTime(RedisHelper.Get("Tinghua_limitEndReciveTime"));
                        //    //顺延日期。
                        //    YouzanControllerTools.delaySomeDate(ref orderReciveTime, 7, beginDate, endDate);
                        //    //重新设置订单的时间。
                        //    order.ReciveTime = orderReciveTime;
                        //}
                        //else
                        //{
                        //    DateTime beginDate = System.Convert.ToDateTime(RedisHelper.Get("Tinghua_limitBeginReciveTime"));
                        //    DateTime endDate = System.Convert.ToDateTime(RedisHelper.Get("Tinghua_limitEndReciveTime"));
                        //    //不是包月鲜花，但是收花日期正好落在了假期内。
                        //    if (beginDate < order.ReciveTime && order.ReciveTime < endDate)
                        //    {
                        //        order.ReciveTime = System.Convert.ToDateTime(RedisHelper.Get("Tinghua_limitReciveTime"));
                        //    }
                        //}
                        #endregion

                    }


                    #region 情人节获取  固定收花时间  玫瑰单品+单品包月
                    var activeGoodssid = RedisHelper.Get($"TingHua_activeGoodssid{nid}");
                    //if (!string.IsNullOrEmpty(activeGoodssid) && !string.IsNullOrEmpty(model.Ordes[j].shopCode))
                    //{
                    //    if (model.Ordes[j].shopCode.Split('&').Length >= 5)
                    //    {
                    //        ReciveTime = new DateTime(DateTime.Now.Year, 8, 28);
                    //    }
                    //}
                    #endregion

                    object ret = db.ExecuteScalar("INSERT Orders(UserId, OrderSerialNumber, OrderState, Person, Phone, Province, City, Area, AddressLongLat, AddressNumber, Long, Lat, ShopId, OrderAddTime, PayType, PayMent, OrderRemark, MoneyPayable, MoneyPack, MoneyExpress, CouponName, MoneyCoupon, MoneyReality, efficiencyNumber, courtesyNumber, expressName, expressNumber, UserAgent, Type, ReciveTime, Mobi, DonotWant, ContactsName, ContactMobile, Remark, expressAddressCode, DescriptionNumber, ChargeMoney, DeliveryReminder, ReceivingReminder, RoyaltyRatio, IsGroup, GoodsId, GroupId, FirstScale, TwoScale, IsPresent)"
+ $"VALUES(9476,'{(model.ID + (model.Ordes.Count > 1 ? "_" + (j + 1) : ""))}',{OrderStatus}, '{model.Receiver_Name}', '{model.Receiver_Mobile}', '{model.Receiver_State}', '{model.Receiver_City}', '{model.Receiver_District}', '{model.Receiver_Address}', NULL, NULL, NULL, {YouZanHelper.ConvertShop(YouZanConfig)}, '{model.Created}',"
+ $" { PayTypeEnum.有赞.GetHashCode()}, { PaymentEnum.有赞.GetHashCode()}, NULL, {model.Ordes[j].Total_Fee}, NULL, NULL, NULL, {Math.Round(model.Ordes[j].Total_Fee - (model.Total_Fee > 0 ? model.Payment / model.Total_Fee * model.Ordes[j].Total_Fee : 0), 2)}, { Math.Round((model.Payment / model.Ordes.Count), 2)}, NULL, NULL, NULL, NULL, '{ "有赞_" + YouZanConfig.Name}', {orderType.ToShort()}, '{ReciveTime}', NULL, '', NULL, NULL, '{(!string.IsNullOrEmpty(model.Buyer_Message) ? "买家留言：" + model.Buyer_Message : "") + (!string.IsNullOrEmpty(model.Trade_Memo) ? ";卖家备注：" + model.Trade_Memo : "")}', NULL, NULL, 0, NULL, NULL, 0.1, 0, NULL, NULL, 0.4, 0.6, 0);SELECT OrderId FROM Orders WHERE @@ROWCOUNT > 0 AND[OrderId] = scope_identity()");
                    OrderID = ret != null && ret.ToString() != "" ? int.Parse(ret.ToString()) : 0;
                    if (orderType == OrderTypeEnum.包月.GetHashCode())
                    {
                        var orderChild = new List<OrderChild>();
                        var flowerNumber = 0; //购买次数（每月4次  月*4）
                        switch ((GoodsTypeEnum)int.Parse(goodItem["type"].ToString()))
                        {
                            case GoodsTypeEnum.半月:
                                flowerNumber = 2;
                                break;
                            case GoodsTypeEnum.一个月:
                                flowerNumber = 4;
                                break;
                            case GoodsTypeEnum.三个月:
                                flowerNumber = 12;
                                break;
                            case GoodsTypeEnum.半年:
                                flowerNumber = 24;
                                break;
                            case GoodsTypeEnum.全年:
                                flowerNumber = 48;
                                break;
                            case GoodsTypeEnum.单次:
                                flowerNumber = 1;
                                break;
                        }
                        //获取活动特定商品。然后这些特定商品可以赠送不同的次数
                        if (!string.IsNullOrEmpty(activeGoodssid) && !string.IsNullOrEmpty(model.Ordes[j].shopCode))
                        {
                            flowerNumber = activeGoodssid.ToInt32();
                        }
                        flowerNumber = flowerNumber * model.Ordes[j].Num;
                        var bl = false;//是不是mini包月
                        var miniGoodssid = RedisHelper.Get($"TingHua_miniGoodssid");//mini商品ID。配送方式需要改变一下。一周送2束  隔周配送
                        if (!string.IsNullOrEmpty(miniGoodssid))
                        {
                            var miniGoodsssAttr = miniGoodssid.Split(',').ToArray();
                            for (int i = 0; i < nid.Count(); i++)
                            {
                                if (miniGoodsssAttr.Contains(nid[i].ToString()))
                                {
                                    bl = true;
                                }
                            }
                            //错误代码
                            //if (miniGoodssid.Split(',').ToArray().Contains(nid.ToString()))
                            //{
                            //	flowerNumber = flowerNumber / 2;
                            //}
                        }
                        if (bl)
                        {
                            flowerNumber = flowerNumber / 2;
                        }

                        var shopCode = "";
                        for (int i = 1; i <= flowerNumber; i++) //乘上购买数量
                        {
                            if (!string.IsNullOrEmpty(model.Ordes[j].shopCode))
                            {
                                var shopCodeArray = model.Ordes[j].shopCode.Split('&');

                                switch (shopCodeArray.Length)
                                {
                                    case 2:
                                        if ((i % 4) == 1 || (i % 4) == 2)
                                        {
                                            shopCode = shopCodeArray[0];
                                        }
                                        else
                                        {
                                            shopCode = shopCodeArray[1];
                                        }
                                        break;
                                    case 3:
                                        if ((i % 4) == 1 || (i % 4) == 2)
                                        {
                                            shopCode = shopCodeArray[0];
                                        }
                                        else if ((i % 4) == 3)
                                        {
                                            shopCode = shopCodeArray[1];
                                        }
                                        else
                                        {
                                            shopCode = shopCodeArray[2];
                                        }
                                        break;
                                    case 4:
                                        if ((i % 4) == 1)
                                        {
                                            shopCode = shopCodeArray[0];
                                        }
                                        else if ((i % 4) == 2)
                                        {
                                            shopCode = shopCodeArray[1];
                                        }
                                        else if ((i % 4) == 3)
                                        {
                                            shopCode = shopCodeArray[3];
                                        }
                                        else
                                        {
                                            shopCode = shopCodeArray[4];
                                        }
                                        break;
                                    case 5:
                                        switch (i)
                                        {
                                            case 1:
                                                shopCode = shopCodeArray[0];
                                                break;
                                            case 2:
                                                shopCode = shopCodeArray[1];
                                                break;
                                            case 3:
                                                shopCode = shopCodeArray[2];
                                                break;
                                            case 4:
                                                shopCode = shopCodeArray[3];
                                                break;
                                            case 5:
                                                shopCode = shopCodeArray[4];
                                                break;
                                            default:
                                                break;
                                        }
                                        break;
                                    default:
                                        break;
                                }
                            }
                            ReciveTime = ReciveTime.AddDays((i - 1) * 7);
                            if (bl)
                            {
                                ReciveTime = ReciveTime.AddDays(7);
                            }
                            sqls.Append("INSERT OrderChild(SendTime, Person, Phone, Province, City, Area, AddressLongLat, Status, CompleteTime, EvaluateText, EvaluateNum, SpeedNum, ExpressNum, Times, Long, Lat, AddressNumber, EvaluationPicture, DeliveryReminder, ReceivingReminder, Orders_OrderId, Courier_Id,shopCode)"
                                    +
                                    $"VALUES('{ReciveTime}', '{model.Receiver_Name}', '{model.Receiver_Mobile}', '{model.Receiver_State}', '{model.Receiver_City}', '{model.Receiver_District}', '{model.Receiver_Address}', {OrderChildStatus}, NULL, NULL, NULL, NULL, NULL, {i}, NULL, NULL, NULL, NULL, NULL, NULL, {OrderID}, NULL,'{shopCode}');");
                        }
                    }
                    for (int u = 0; u < goodInfoTable.Rows.Count; u++)
                    {
                        var goodsid = int.Parse(goodItem["id"].ToString());
                        goodItem = goodInfoTable.Rows[u];
                        sqls.Append("INSERT OrderGoodss(OrderId, GoodsId, GoodsImageUrl, GoodsTitle, GoodsTitleSub, GoodsNumber, GoodsPrice, EvaluateNumber, evaluateText, AttributeName, EvaluationPicture)"
+ $"VALUES({OrderID}, {goodsid},'{goodItem["GoodsImageUrl"].ToString()}','{goodItem["GoodsTitle"].ToString()}','{ goodItem["GoodsTitleSub"].ToString()}', {GoodsNumber*dicAttr[goodsid]}, {model.Ordes[j].Price}, NULL, NULL, '{goodItem["Name"].ToString()}', NULL);");
                    }
                }
                catch (Exception e)
                {
                    return false;
                }
            }
            int retNum = 0;
            if (sqls.Length < 1)
            {
                YouZanHelper.UpdateMemo(YouZanConfig.Name, model.ID, "tongbu:" + ZhuangTai + "_Fail");
                return false;
            }
            else
            {
                retNum = db.ExecuteSql(sqls.ToString());
                sqls.Clear();
                if (retNum > 0)
                {
                    YouZanHelper.UpdateMemo(YouZanConfig.Name, model.ID, "tongbu:" + ZhuangTai + "_OK");
                }
                else
                {
                    YouZanHelper.UpdateMemo(YouZanConfig.Name, model.ID, "tongbu:" + ZhuangTai + "_Fail");
                }
            }
            return retNum > 0;
        }
        #endregion

        #region 修改一个订单状态

        public bool UpdateOrderStatus(string YouZanOrderID, Sql.SqlServerClient<Orders> db, NewYouZanModel YouZanConfig, int OrderStatus, int OrderChildStatus, string ZhuangTai)
        {
            StringBuilder sqls = new StringBuilder();
            int retNum = db.ExecuteSql($"update orders set OrderState={OrderStatus} where   orderserialNumber like '%{YouZanOrderID}%';update OrderChild set Status = {OrderChildStatus} where Orders_OrderId in (select orderid from orders where orderserialNumber like '%{YouZanOrderID}%')");
            sqls.Clear();
            if (retNum > 0)
            {
                YouZanHelper.UpdateMemo(YouZanConfig.Name, YouZanOrderID, "tongbu:" + ZhuangTai + "_OK");
            }
            else
            {
                YouZanHelper.UpdateMemo(YouZanConfig.Name, YouZanOrderID, "tongbu:" + ZhuangTai + "_Fail");
            }
            return retNum > 0;
        }


        #endregion

        #region 公共方法
        /// <summary>
        /// 递归判断某个日期是否在某个日期之间，如果是的话，顺延x天，直到不在这个时间范围之内。  如果不是的话，直接返回。
        /// </summary>
        /// <returns></returns>
        public static DateTime delaySomeDate(ref DateTime currentDate, int delayDays, DateTime beginDate, DateTime endDate)
        {
            if (beginDate == null || endDate == null)
            {
                //没有假期，不用顺眼。
                return currentDate;
            }


            if (beginDate.Date < currentDate.Date && currentDate.Date < endDate.Date)
            {
                //顺延。
                currentDate = currentDate.AddDays(delayDays);
                if (beginDate.Date < currentDate.Date && currentDate.Date < endDate.Date)
                {
                    //顺延后仍然在区间内。
                    delaySomeDate(ref currentDate, delayDays, beginDate, endDate);
                }
                else
                {
                    //顺延后--不在--区间内。
                    return currentDate;
                }
            }
            else
            {
                //不在期间之内。
                return currentDate;
            }

            return currentDate;
        }
        #endregion
    }
    public class OrderStatus
    {
        public string OrderID { get; set; }
        public bool Status { get; set; }
        public string GoodsID { get; set; }

        //public string dd { get; set }
    }
}
