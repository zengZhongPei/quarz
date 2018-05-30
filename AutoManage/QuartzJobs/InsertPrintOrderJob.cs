using AutoManage.Helper;
using log4net;
using Quartz;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Tinghua.Management.Enum;
using Tinghua.Management.Model;
using Tinghua.Management.Utility;
using Tinghua.Management.Utility.Extensions;
using Tinghua.Management.Utility.Helper;

namespace AutoManage.QuartzJobs
{
    /// <summary>
    /// 自动读取订单插入到打单表。如需停后台更新。需要先调试走到读取是否首次送花瓶后再停掉后台
    /// </summary>
    class InsertPrintOrderJob : IJob
    {
        private readonly ILog _logger = LogManager.GetLogger(typeof(InsertPrintOrderJob));

        private readonly ILog _errorLog = LogManager.GetLogger("Com.Foo");

        public static int onlyNumber = 1;//保持运行状态。如果上一次没有完成就不运行下一次
        public static DateTime phoneTime = DateTime.Now;//短信上次短信通知的时间

        public void Execute(IJobExecutionContext context)
        { 
            if (onlyNumber!=1)
            { 
                _logger.InfoFormat($"InsertPrintOrderJob-读取订单数据到打单记录表上一次任务没有运行完成跳过此次运行");
                if ((DateTime.Now-phoneTime).TotalMinutes >180)//如果相差180分钟就发短信提示
                {
                    Helper.SendMsgHelper.SendSmsMessage("17623028800", "195607", null);
                    phoneTime = DateTime.Now;
                }
                return;
            }
            onlyNumber = 2;
            _logger.InfoFormat($"InsertPrintOrderJob-读取订单数据到打单记录表开始运行...");
            try
            {

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
                catch (Exception ex)
                {
                    _logger.InfoFormat($"自动任务InsertFirstOrderJob-读取订单数据到redis出现异常{ex.Message}");
                    var fullMesage = ErrorHelper.FullException(ex);
                    _errorLog.ErrorFormat($"InsertPrintOrderJob错误信息;{fullMesage}");
                }

                #endregion

                #region 插入打单表数据

                var count = 0;
                var datetime = DateTime.Now.AddDays(-60).ToShortDateString();
                var sql = string.Empty;

                #region 单品查询SQL
                var danPinSql = @"select o.Phone,o.userid,o.OrderId,null as id,o.OrderSerialNumber,o.city,o.Person,'' as childPhone,o.Province,o.Area,o.AddressLongLat,o.AddressNumber,o.DonotWant,o.Type,
                                    case when o.ReciveTime is null then DATEADD(DAY,2,o.OrderAddTime)
									 else o.ReciveTime end ReciveTime,o.ReciveTime as ReciveTime1,
                                    (isnull(o.Remark,'')+isnull(o.OrderRemark,'')) as OrderRemark,o.UserAgent OrderAgent,
                                    STUFF(
		                                    (
		                                      select '#'+notes+'_'+convert(varchar,AddTime,120) from OrderNoteSet where o.orderid=OrdersOrderId FOR XML PATH('')
                                             ),1,1,''
	                                     )AS Remark,0 Times,
                                      STUFF(
		                                    (
		                                      select '#'+ (CASE WHEN g.GoodsName is not null THEN g.GoodsName ELSE og.GoodsTitle END)+'_'+AttributeName+'*'+(case when GoodsNumber is null then '1'
									 else convert(varchar,GoodsNumber,120) end)  from OrderGoodss og,GoodsAttributeSet ga,AttributeSet a,AttributeShowSet ass,Goodss g where ga.Attribute_Id=a.Id and a.AttributeShow_Id=ass.id and ass.Goodss_GoodsId=g.GoodsId and ga.id=og.GoodsId and o.orderid=OrderId FOR XML PATH('')
                                             ),1,1,''
	                                     )AS GoodssTitle,
                                    STUFF(
		                                (
		                                  select '#'+ (convert(varchar,g.GoodsId,120)+'_'+convert(varchar,g.CategoryFatherId,120))  from OrderGoodss og,GoodsAttributeSet ga,AttributeSet a,AttributeShowSet ass,Goodss g where ga.Attribute_Id=a.Id and a.AttributeShow_Id=ass.id and ass.Goodss_GoodsId=g.GoodsId and ga.id=og.GoodsId and o.orderid=OrderId FOR XML PATH('')
                                             ),1,1,'')
									 AS GoodsId,
                                    STUFF(
		                                    (
		                                     select '#'+convert(varchar(20),GoodsId) from OrderGoodss where o.orderid=OrderId FOR XML PATH('')
                                             ),1,1,''
	                                     )AS GoodAttrId,
                                    STUFF(
		                                    (
		                                     select '#'+convert(varchar(20),IsHaveOtherGoods) from GoodsAttributeSet ga where Id in (select GoodsId from orderGoodss where o.orderid=OrderId) FOR XML PATH('')
                                             ),1,1,''
	                                     )AS IsHaveOtherGoods,
                                    (case when (select count(1) from orderGoodss where o.orderid=OrderId) >1 then 1
								      when (select GoodsNumber from orderGoodss where o.orderid=OrderId and GoodsNumber >1) >1  then 1    
									 else 0 end) as IsSpecial,
                                    STUFF(
		                                    (
		                                      select '#'+Content from WishCard where o.orderid=Orders_OrderId FOR XML PATH('')
                                             ),1,1,''
	                                    )AS FlowerGiftCardMsg,
                                    (select g.ShopCode from GoodsAttributeSet ga,AttributeSet a,AttributeShowSet ass,Goodss g where ga.Attribute_Id=a.Id and a.AttributeShow_Id=ass.id and ass.Goodss_GoodsId=g.GoodsId and ga.id=(
                                     select top 1 GoodsId from orderGoodss where o.orderid=OrderId order by GoodsPrice desc)) as GoodssCode,
                                     (select top 1 GoodsPrice from orderGoodss where o.orderid=OrderId order by GoodsPrice desc) as GoodssPrice,
                                    (select COUNT(*) from OrderGoodss where o.orderid=OrderId) as GoodssCount
                                     from Orders o where o.type!=2 and o.AddressLongLat not like '测试%' and o.orderstate in(3)";
                //and o.OrderAddTime >='" + datetime + "'

                #endregion

                #region 包月查询SQL
                var baoyueSql = @"select o.phone,o.userid,o.OrderId,oc.id,o.OrderSerialNumber,oc.city,oc.Person,oc.Phone as childPhone,oc.Province,oc.Area,oc.AddressLongLat,oc.AddressNumber,o.DonotWant,o.Type,oc.SendTime as ReciveTime,
                                (isnull(o.Remark,'')+isnull(o.OrderRemark,'')) as OrderRemark,o.UserAgent OrderAgent,(isnull(oc.Note,'')) as Remark,oc.Times,
                               (
								
                                STUFF(
		                                (
		                                  select '#'+ (CASE WHEN g.GoodsName is not null THEN g.GoodsName ELSE og.GoodsTitle END)+'_'+AttributeName+'*'+(case when GoodsNumber is null then '1'
									 else convert(varchar,GoodsNumber,120) end)  from OrderGoodss og,GoodsAttributeSet ga,AttributeSet a,AttributeShowSet ass,Goodss g where ga.Attribute_Id=a.Id and a.AttributeShow_Id=ass.id and ass.Goodss_GoodsId=g.GoodsId and ga.id=og.GoodsId and o.orderid=OrderId FOR XML PATH('')
                                             ),1,1,'')) 
									 AS GoodssTitle,
									 (case when (oc.shopCode is null or oc.ShopCode='') then '' else
									 (select top 1 (CASE WHEN GoodsName is not null THEN GoodsName ELSE GoodsTitle END) from goodss where shopcode=oc.shopCode order by goodsid desc)
									 end
									 ) as newGoodssTitle,
                                STUFF(
		                                (
		                                 select '#'+convert(varchar(20),GoodsId) from OrderGoodss where o.orderid=OrderId FOR XML PATH('')
                                         ),1,1,''
	                                 )AS GoodAttrId,
                                STUFF(
		                                (
		                                  select '#'+ (convert(varchar,g.GoodsId,120)+'_'+convert(varchar,g.CategoryFatherId,120))  from OrderGoodss og,GoodsAttributeSet ga,AttributeSet a,AttributeShowSet ass,Goodss g where ga.Attribute_Id=a.Id and a.AttributeShow_Id=ass.id and ass.Goodss_GoodsId=g.GoodsId and ga.id=og.GoodsId and o.orderid=OrderId FOR XML PATH('')
                                             ),1,1,'')
									 AS GoodsId,
                                STUFF(
		                                    (
		                                     select '#'+convert(varchar(20),IsHaveOtherGoods) from GoodsAttributeSet ga where Id in (select GoodsId from orderGoodss where o.orderid=OrderId) FOR XML PATH('')
                                             ),1,1,''
	                                     )AS IsHaveOtherGoods,
                                (case when (select count(1) from orderGoodss where o.orderid=OrderId) >1 and oc.Times=1 then 1
								      when (select GoodsNumber from orderGoodss where o.orderid=OrderId and GoodsNumber >1) >1  then 1  
                                      when (select count(1) from ordergoodss og,GoodsAttributeSet ga where og.GoodsId=ga.id and oc.Times=1 and ga.IsHaveOtherGoods=1 and og.orderid=o.orderid) >0 then 1    
									 else 0 end) as IsSpecial,
                                STUFF(
		                                (
		                                  select '#'+Content from WishCard where o.orderid=Orders_OrderId FOR XML PATH('')
                                         ),1,1,''
	                                )AS FlowerGiftCardMsg,
                               (case when (oc.shopCode is not null and oc.shopCode !='') then oc.shopCode else (select g.ShopCode from GoodsAttributeSet ga,AttributeSet a,AttributeShowSet ass,Goodss g where ga.Attribute_Id=a.Id and a.AttributeShow_Id=ass.id and ass.Goodss_GoodsId=g.GoodsId and ga.id=(
                                 select top 1 GoodsId from orderGoodss where o.orderid=OrderId order by GoodsPrice desc)) end) as GoodssCode,
                                (select top 1 GoodsPrice from orderGoodss where o.orderid=OrderId order by GoodsPrice desc) as GoodssPrice,
                                (select COUNT(*) from OrderGoodss where o.orderid=OrderId) as GoodssCount,
								(select top 1 CategoryFatherId from goodss where shopcode=oc.shopCode order by goodsid desc) as CategoryFatherId,
								 (select count(1) from orderchild orderchild where o.orderid=orderchild.Orders_OrderId ) as ordercount
                                 from Orders o,orderchild oc where o.type=2 and o.orderstate in(3,4) and oc.AddressLongLat not like '测试%'
								  and oc.Status in(1,2) and o.orderid=oc.Orders_OrderId";
                //and o.OrderAddTime >='" + datetime + "'
                #endregion

                var danPinTable = db.ExecuteTable(danPinSql);
                var baoyueTable = db.ExecuteTable(baoyueSql);
                var orderidList = new List<string>();//保存所有包月和单品的订单号
                var userIdList = new List<string>();//保存所有包月和单品的用户ID

                #region 读取固定商品到某个仓库的配置数据
                var Warehouse = string.Empty;//订单默认到某个仓库。根据仓库提供的编码来默认到某个仓库
                var CangKu_fixedGoodsHouse97 = RedisHelper.Get("CangKu_fixedGoodsHouse97");//获取成都仓库固定发货商品属性ID
                var CangKu_fixedGoodsHouse92 = RedisHelper.Get("CangKu_fixedGoodsHouse92");//获取杭州绿植仓库固定发货商品属性ID
                var CangKu_fixedGoodsHouse93 = RedisHelper.Get("CangKu_fixedGoodsHouse93");//获取广州绿植仓库固定发货商品属性ID
                if (CangKu_fixedGoodsHouse97 == null)
                {
                    CangKu_fixedGoodsHouse97 = "940,934,939,933,938,932,937,931,1072,1073,1074,820,758,894,889,732,799";
                    RedisHelper.Set("CangKu_fixedGoodsHouse97", "940,934,939,933,938,932,937,931,1072,1073,1074,820,758,894,889,732,799");
                }
                if (CangKu_fixedGoodsHouse92 == null)
                {
                    CangKu_fixedGoodsHouse92 = "760,818,768,833,994,993,992,991,990,989,960,959,988,987,986,985,972,970,981,980,979,976,969,967,958,957,956,955,954,953,950,949,964,948,946,945,944,943,1030,1029,1014,1013,1012,1011,1056,1055,1020,1019,1028,1027,1034,1033,1016,1015,1032,1031,1060,1059,1018,1017,1058,1057,1026,1025,895,890,947";
                    RedisHelper.Set("CangKu_fixedGoodsHouse92", "760,818,768,833,994,993,992,991,990,989,960,959,988,987,986,985,972,970,981,980,979,976,969,967,958,957,956,955,954,953,950,949,964,948,946,945,944,943,1030,1029,1014,1013,1012,1011,1056,1055,1020,1019,1028,1027,1034,1033,1016,1015,1032,1031,1060,1059,1018,1017,1058,1057,1026,1025,895,890,947");
                }
                if (CangKu_fixedGoodsHouse93 == null)
                {
                    CangKu_fixedGoodsHouse93 = "1062,1061,839,737,838,736,828,766,878,832,733,942,936,941,935,920,910,919,909,918,908,917,907,916,906,915,905,914,904,913,903,1003,1002,1062,1061,825,763,730,800,735,840";
                    RedisHelper.Set("CangKu_fixedGoodsHouse93", "1062,1061,839,737,838,736,828,766,878,832,733,942,936,941,935,920,910,919,909,918,908,917,907,916,906,915,905,914,904,913,903,1003,1002,1062,1061,825,763,730,800,735,840");
                }
                #endregion

                //需要延期开始开始时间。
                var beginDate = RedisHelper.Get("Tinghua_limitBeginReciveTime");
                //需要延期结束时间。
                var endDate = RedisHelper.Get("Tinghua_limitEndReciveTime");
                //非包月订单延到固定时间。
                var limitReciveTime = RedisHelper.Get("Tinghua_limitReciveTime");
                if (danPinTable.Rows.Count > 0)
                {
                    for (int i = 0; i < danPinTable.Rows.Count; i++)
                    {
                        orderidList.Add(danPinTable.Rows[i]["OrderId"].ToString());
                        userIdList.Add(danPinTable.Rows[i]["phone"].ToString());
                    }
                }
                if (baoyueTable.Rows.Count > 0)
                {
                    for (int i = 0; i < baoyueTable.Rows.Count; i++)
                    {
                        orderidList.Add(baoyueTable.Rows[i]["OrderId"].ToString());
                        userIdList.Add(baoyueTable.Rows[i]["phone"].ToString());
                    }
                }
                if (orderidList.Any())//如果这个里面有值就说明需要往打单记录表插入数据
                {
                    var index = 0;//一次批量插入50个
                    var danPinInsertSql = "insert PrintOrderSet (Person,Phone,Province,City,Area,AddressLongLat,AddressNumber,OrderRemark,Remark,AddTime,CPCode,State,IsGive,CPNumber,DonotWant,FlowerGiftCardMsg,GoodssCode,GoodssPrice,OrderAgent,GoodssTitle,GoodssCount,Times,GoodAttrId,OrderSerialNumber,ReciveTime,Type,Orders_OrderId,IsSpecial,LvzhiGroup,Warehouse) values";
                    var baoYueInsertSql = "insert PrintOrderSet (Person,Phone,Province,City,Area,AddressLongLat,AddressNumber,OrderRemark,Remark,AddTime,CPCode,State,IsGive,CPNumber,DonotWant,FlowerGiftCardMsg,GoodssCode,GoodssPrice,OrderAgent,GoodssTitle,GoodssCount,Times,GoodAttrId,OrderSerialNumber,ReciveTime,Type,OrderChild_Id,Orders_OrderId,IsSpecial,NoteType,LvzhiGroup,Warehouse) values";
                    var userIds = string.Join(",", userIdList.Distinct());
                    var orderIds = string.Join(",", orderidList.Distinct());
                    //获取所有需要赠送花瓶的订单
                    var GiveOrder = HttpHelper.Post("http://admin.listenflower.com/Open/OrderGive", new { Phone = userIds, orderid = orderIds }).Split(',').ToArray();
                    //查询当前打单记录表里面已存在的订单号
                    var printSql = $"select Orders_OrderId,type,OrderChild_Id from PrintOrderSet where Orders_OrderId in({orderIds})";
                    var printTable = db.ExecuteTable(printSql);
                    var printList = new List<string>();
                    var printListBaoYue = new List<string>();
                    var keys = string.Empty;
                    var isGave = 0;//是否送花瓶(0.不送 1.送)
                    var LvzhiGroup = 0;//订单商品购买的属性中是否包含其他属性 具体参照枚举IsHaveOtherGoodsEnum
                    var lvzhiGroup = new string[] { IsHaveOtherGoodsEnum.A3盆器.GetHashCode().ToString(), IsHaveOtherGoodsEnum.H3盆器.GetHashCode().ToString(), IsHaveOtherGoodsEnum.M3盆器.GetHashCode().ToString(), IsHaveOtherGoodsEnum.U3盆器.GetHashCode().ToString(), IsHaveOtherGoodsEnum.W3盆器.GetHashCode().ToString() };
                    var flowerGroup = new string[] { IsHaveOtherGoodsEnum.可利鲜.GetHashCode().ToString(), IsHaveOtherGoodsEnum.花瓶.GetHashCode().ToString() };
                    if (printTable.Rows.Count > 0)
                    {
                        for (int i = 0; i < printTable.Rows.Count; i++)
                        {
                            if (!string.IsNullOrEmpty(printTable.Rows[i]["Orders_OrderId"].ToString()))
                            {
                                printList.Add(printTable.Rows[i]["Orders_OrderId"].ToString());
                            }
                            if (!string.IsNullOrEmpty(printTable.Rows[i]["OrderChild_Id"].ToString()))
                            {
                                printListBaoYue.Add(printTable.Rows[i]["OrderChild_Id"].ToString());
                            }
                        }
                    }
                    DataRow[] danPinTableRow = null;
                    if (printList.Count > 0)
                    {
                        danPinTableRow = danPinTable.Select("OrderId not in (" + string.Join(",", printList) + ")");
                    }
                    else
                    {
                        danPinTableRow = danPinTable.Select("1=1");
                    }

                    #region 单品的插入到打单表
                    //先插入单品的打单数据到打单表
                    for (int i = 0; i < danPinTableRow.Count(); i++)
                    {
                        //检查是否需要拆单。如果需要拆单就拆单并下次再同步此订单
                        if (splitOrder(danPinTableRow[i]["OrderId"].ToString()))
                        {
                            continue;
                        }
                        count++;
                        #region 读取基本数据
                        var id = string.IsNullOrEmpty(danPinTableRow[i]["id"].ToString()) ? 0 : danPinTableRow[i]["id"].ToString().ToInt32();
                        var IsSpecial = string.IsNullOrEmpty(danPinTableRow[i]["IsSpecial"].ToString()) ? 0 : danPinTableRow[i]["IsSpecial"].ToString().ToInt32();
                        var Types = string.IsNullOrEmpty(danPinTableRow[i]["Type"].ToString()) ? 0 : danPinTableRow[i]["Type"].ToString().ToInt32();
                        var GoodssPrice = string.IsNullOrEmpty(danPinTableRow[i]["GoodssPrice"].ToString()) ? 0 : danPinTableRow[i]["GoodssPrice"].ToString().ToDouble();
                        var GoodssCount = string.IsNullOrEmpty(danPinTableRow[i]["GoodssCount"].ToString()) ? 0 : danPinTableRow[i]["GoodssCount"].ToString().ToInt32();
                        var GoodAttrId = string.IsNullOrEmpty(danPinTableRow[i]["GoodAttrId"].ToString()) ? "" : danPinTableRow[i]["GoodAttrId"].ToString();
                        var IsHaveOtherGoods = string.IsNullOrEmpty(danPinTableRow[i]["IsHaveOtherGoods"].ToString()) ? "0" : danPinTableRow[i]["IsHaveOtherGoods"].ToString();
                        var ReciveTime = ObjToClear(danPinTableRow[i]["ReciveTime"] ?? "");
                        #endregion

                        #region 固定商品默认仓库设置
                        var GoodsId = string.IsNullOrEmpty(danPinTableRow[i]["GoodsId"].ToString()) ? "" : danPinTableRow[i]["GoodsId"].ToString();
                        var GoodsIdList = GoodsId.Split('#').Distinct().ToList();
                        if (fixedGoodss(GoodsIdList, CangKu_fixedGoodsHouse92.Split('#').Distinct().ToList()))
                        {
                            Warehouse = "杭州绿植仓库";
                        }
                        if (fixedGoodss(GoodsIdList, CangKu_fixedGoodsHouse93.Split('#').Distinct().ToList()))
                        {
                            Warehouse = "广州绿植仓库";
                        }
                        if (fixedGoodss(GoodsIdList, CangKu_fixedGoodsHouse97.Split('#').Distinct().ToList()))
                        {
                            Warehouse = "成都绿植仓库";
                        }
                        #endregion

                        #region 收花时间延期处理。条件在配置的时间段之内。并且原始订单没有选择收花日期的非包月订单(需求有变化暂时屏蔽。需要加入固定某些品类不顺延)
                        //if (!string.IsNullOrEmpty(beginDate) && !string.IsNullOrEmpty(endDate) && !string.IsNullOrEmpty(limitReciveTime) && string.IsNullOrEmpty(danPinTableRow[i]["ReciveTime1"].ToString()))
                        //{
                        //    if (ReciveTime.ToDateTime() > beginDate.ToDateTime() && ReciveTime.ToDateTime() < endDate.ToDateTime())
                        //    {
                        //        ReciveTime = limitReciveTime;
                        //    }
                        //}
                        #endregion

                        #region 判断是否首次送花瓶
                        isGave = 0;//默认不送花瓶
                        keys = $"{danPinTableRow[i]["userid"].ToString()}{danPinTableRow[i]["OrderId"].ToString()}";
                        if (GiveOrder.Contains(keys))
                        {
                            isGave = 1;
                        }
                        #endregion

                        #region 设置改订单是否包含其他特殊品种。比如说单独购买了花瓶 特殊的绿植盆器
                        var IsHaveOtherGoodsList = IsHaveOtherGoods.Split('#').Distinct().Where(l => l != "0").ToList();

                        if (IsHaveOtherGoodsList.Count == 1)
                        {
                            LvzhiGroup = IsHaveOtherGoodsList.FirstOrDefault().ToInt32();
                        }
                        else if (IsHaveOtherGoodsList.Where(l => lvzhiGroup.Contains(l)).Count() >= 2)
                        {
                            LvzhiGroup = IsHaveOtherGoodsEnum.订单包含2种特殊盆器或者以上.GetHashCode();
                        }
                        else if (IsHaveOtherGoodsList.Where(l => flowerGroup.Contains(l)).Count() >= 2)
                        {
                            LvzhiGroup = IsHaveOtherGoodsEnum.订单包含2种花瓶可利鲜或者以上.GetHashCode();
                        }
                        else
                        {
                            LvzhiGroup = IsHaveOtherGoodsEnum.默认.GetHashCode();
                        }
                        #endregion

                        #region 重新设置订单类型(已过时)
                        //Types = getNewOrderType(GoodsIdList, Types);
                        #endregion

                        #region 插入数据到打单表
                        if (index == 0)
                        {
                            danPinInsertSql += $"('{ObjToClear(danPinTableRow[i]["Person"].ToString())}','{ObjToClear(danPinTableRow[i]["Phone"].ToString())}','{ObjToClear(danPinTableRow[i]["Province"].ToString())}','{ObjToClear(danPinTableRow[i]["City"].ToString())}','{ObjToClear(danPinTableRow[i]["Area"].ToString())}','{ObjToClear(danPinTableRow[i]["AddressLongLat"].ToString())}','{ObjToClear(danPinTableRow[i]["AddressNumber"] ?? "")}','{ObjToClear(danPinTableRow[i]["OrderRemark"] ?? "")}','{ObjToClear(danPinTableRow[i]["Remark"] ?? "")}','{DateTime.Now}','',{PrintOrderStateEnum.待打印.GetHashCode()},{isGave},'','{ObjToClear(danPinTableRow[i]["DonotWant"] ?? "")}','{ObjToClear(danPinTableRow[i]["FlowerGiftCardMsg"] ?? "")}','{ObjToClear(danPinTableRow[i]["GoodssCode"] ?? "")}',{GoodssPrice},'{ObjToClear(danPinTableRow[i]["OrderAgent"].ToString())}','{ObjToClear(danPinTableRow[i]["GoodssTitle"] ?? "")}',{GoodssCount},{danPinTableRow[i]["Times"].ToString()},'{GoodAttrId}','{ObjToClear(danPinTableRow[i]["OrderSerialNumber"] ?? "")}','{ReciveTime}',{Types},{danPinTableRow[i]["OrderId"]},{IsSpecial},{LvzhiGroup},'{Warehouse}')";
                        }
                        else
                        {
                            danPinInsertSql += $",('{ObjToClear(danPinTableRow[i]["Person"].ToString())}','{ObjToClear(danPinTableRow[i]["Phone"].ToString())}','{ObjToClear(danPinTableRow[i]["Province"].ToString())}','{ObjToClear(danPinTableRow[i]["City"].ToString())}','{ObjToClear(danPinTableRow[i]["Area"].ToString())}','{ObjToClear(danPinTableRow[i]["AddressLongLat"].ToString())}','{ObjToClear(danPinTableRow[i]["AddressNumber"] ?? "")}','{ObjToClear(danPinTableRow[i]["OrderRemark"] ?? "")}','{ObjToClear(danPinTableRow[i]["Remark"] ?? "")}','{DateTime.Now}','',{PrintOrderStateEnum.待打印.GetHashCode()},{isGave},'','{ObjToClear(danPinTableRow[i]["DonotWant"] ?? "")}','{ObjToClear(danPinTableRow[i]["FlowerGiftCardMsg"] ?? "")}','{ObjToClear(danPinTableRow[i]["GoodssCode"] ?? "")}',{GoodssPrice},'{ObjToClear(danPinTableRow[i]["OrderAgent"].ToString())}','{ObjToClear(danPinTableRow[i]["GoodssTitle"] ?? "")}',{GoodssCount},{danPinTableRow[i]["Times"].ToString()},'{GoodAttrId}','{ObjToClear(danPinTableRow[i]["OrderSerialNumber"] ?? "")}','{ReciveTime}',{Types},{danPinTableRow[i]["OrderId"]},{IsSpecial},{LvzhiGroup},'{Warehouse}')";
                        }
                        index++;
                        if (index == 50)
                        {
                            db.ExecuteSql(danPinInsertSql);
                            danPinInsertSql = "insert PrintOrderSet (Person,Phone,Province,City,Area,AddressLongLat,AddressNumber,OrderRemark,Remark,AddTime,CPCode,State,IsGive,CPNumber,DonotWant,FlowerGiftCardMsg,GoodssCode,GoodssPrice,OrderAgent,GoodssTitle,GoodssCount,Times,GoodAttrId,OrderSerialNumber,ReciveTime,Type,Orders_OrderId,IsSpecial,LvzhiGroup,Warehouse) values";
                            index = 0;
                        }
                        #endregion
                    }
                    if (index != 0)
                    {
                        db.ExecuteSql(danPinInsertSql);
                        index = 0;
                    }
                    #endregion

                    #region 包月订单数据插入到打单表
                    #region 排除在打单表一存在的订单数据
                    DataRow[] baoyueTableRow = null;
                    if (printListBaoYue.Count > 0)
                    {
                        baoyueTableRow = baoyueTable.Select("id not in (" + string.Join(",", printListBaoYue) + ")");
                    }
                    else
                    {
                        baoyueTableRow = baoyueTable.Select("1=1");
                    }
                    #endregion
                    var notetype = 0;
                    //再插入包月的打单数据到打单表
                    for (int i = 0; i < baoyueTableRow.Count(); i++)
                    {
                        #region 初始化数据
                        LvzhiGroup = 0;
                        notetype = 0;
                        isGave = 0;
                        #endregion

                        //检查是否需要拆单。如果需要拆单就拆单并下次再同步此订单
                        if (splitOrder(baoyueTableRow[i]["OrderId"].ToString()))
                        {
                            continue;
                        }
                        count++;
                        #region 基础数据读取
                        var id = string.IsNullOrEmpty(baoyueTableRow[i]["id"].ToString()) ? 0 : baoyueTableRow[i]["id"].ToString().ToInt32();
                        var ordercount = string.IsNullOrEmpty(baoyueTableRow[i]["ordercount"].ToString()) ? 0 : baoyueTableRow[i]["ordercount"].ToString().ToInt32();
                        var CategoryFatherId = baoyueTableRow[i]["CategoryFatherId"] == null ? 0 : baoyueTableRow[i]["CategoryFatherId"].ToString().ToInt32();
                        var IsSpecial = string.IsNullOrEmpty(baoyueTableRow[i]["IsSpecial"].ToString()) ? 0 : baoyueTableRow[i]["IsSpecial"].ToString().ToInt32();
                        var times = int.Parse(ObjToClear(baoyueTableRow[i]["Times"].ToString()));
                        var Types = string.IsNullOrEmpty(baoyueTableRow[i]["Type"].ToString()) ? 0 : baoyueTableRow[i]["Type"].ToString().ToInt32();
                        var IsHaveOtherGoods = string.IsNullOrEmpty(baoyueTableRow[i]["IsHaveOtherGoods"].ToString()) ? "0" : baoyueTableRow[i]["IsHaveOtherGoods"].ToString();
                        var IsHaveOtherGoodsList = IsHaveOtherGoods.Split('#').Distinct().Where(l => l != "0").ToList();
                        var GoodssPrice = string.IsNullOrEmpty(baoyueTableRow[i]["GoodssPrice"].ToString()) ? 0 : baoyueTableRow[i]["GoodssPrice"].ToString().ToDouble();
                        var GoodssCount = string.IsNullOrEmpty(baoyueTableRow[i]["GoodssCount"].ToString()) ? 0 : baoyueTableRow[i]["GoodssCount"].ToString().ToInt32();
                        var GoodAttrId = string.IsNullOrEmpty(baoyueTableRow[i]["GoodAttrId"].ToString()) ? "" : baoyueTableRow[i]["GoodAttrId"].ToString();
                        var goodAttrIdArr = Array.ConvertAll<string, int>(GoodAttrId.Split('#'), l => int.Parse(l));
                        var title = ObjToClear(baoyueTableRow[i]["GoodssTitle"] ?? "");
                        #endregion

                        #region 设置商品固定某个仓库发货模块
                        var GoodsId = string.IsNullOrEmpty(baoyueTableRow[i]["GoodsId"].ToString()) ? "" : baoyueTableRow[i]["GoodsId"].ToString();
                        var GoodsIdList = GoodsId.Split('#').Distinct().ToList();
                        if (fixedGoodss(GoodsIdList, CangKu_fixedGoodsHouse92.Split('#').Distinct().ToList()))
                        {
                            Warehouse = "杭州绿植仓库";
                        }
                        if (fixedGoodss(GoodsIdList, CangKu_fixedGoodsHouse93.Split('#').Distinct().ToList()))
                        {
                            Warehouse = "广州绿植仓库";
                        }
                        if (fixedGoodss(GoodsIdList, CangKu_fixedGoodsHouse97.Split('#').Distinct().ToList()))
                        {
                            Warehouse = "成都绿植仓库";
                        }
                        #endregion

                        #region 设置改订单商品是否包含其他特殊品种。比如说单独购买了花瓶 特殊的绿植盆器
                        if (IsHaveOtherGoodsList.Count == 1)
                        {
                            LvzhiGroup = IsHaveOtherGoodsList.FirstOrDefault().ToInt32();
                        }
                        else if (IsHaveOtherGoodsList.Where(l => lvzhiGroup.Contains(l)).Count() >= 2)
                        {
                            LvzhiGroup = IsHaveOtherGoodsEnum.订单包含2种特殊盆器或者以上.GetHashCode();
                        }
                        else if (IsHaveOtherGoodsList.Where(l => flowerGroup.Contains(l)).Count() >= 2)
                        {
                            LvzhiGroup = IsHaveOtherGoodsEnum.订单包含2种花瓶可利鲜或者以上.GetHashCode();
                        }
                        #endregion

                        #region 如果是设置了某次固定配送某个商品。同步标题处理逻辑
                        var newGoodssTitle = string.IsNullOrEmpty(baoyueTableRow[i]["newGoodssTitle"].ToString()) ? "" : baoyueTableRow[i]["newGoodssTitle"].ToString();
                        if (!string.IsNullOrEmpty(newGoodssTitle))
                        {
                            var tempGoodsIdList = GoodsId.Split('#').ToList();
                            var tempString= tempGoodsIdList.Where(l => l.Contains("_25")).FirstOrDefault();//获取需要替换的属性ID字符串
                            if (!string.IsNullOrEmpty(tempString))
                            {
                                var tempIndex = -1;//用来保存刚刚获取到属性字符串在原字符串里面的下标
                                var tempAttr = tempGoodsIdList.ToArray();//转换成数组来取下标
                                for (int ki = 0; ki < tempAttr.Length; ki++)
                                {
                                    if (tempAttr[ki]==tempString)
                                    {
                                        tempIndex = ki;
                                        break;
                                    }
                                }
                                if (tempIndex!=-1)
                                {
                                    var tempTitleAttr = title.Split('#').ToArray();//把原标题转换为数组
                                    try
                                    {
                                       var tempReplaceStr=tempTitleAttr[tempIndex].Split('_')[0];//获取原标题中需要替换为固定配送某个商品的商品名字
                                        if (times==1)
                                        {
                                            title = title.Replace(tempReplaceStr, newGoodssTitle);
                                        }
                                        else
                                        {
                                            title = tempTitleAttr[tempIndex].Replace(tempReplaceStr, newGoodssTitle);
                                        }
                                    }
                                    catch (Exception)
                                    {
                                        
                                    }
                                }
                            }
                        }

                        #endregion

                        #region 重新设置订单类型
                        Types = getNewOrderType(GoodsIdList, Types);
                        #endregion

                        #region 在某种特定的情况下重新获取订单类型
                        if (CategoryFatherId != 0)
                        {
                            //如果该订单是组合商品。就需要重新获取订单类型
                            switch (CategoryFatherId)
                            {
                                case 25:
                                    Types = OrderTypeEnum.包月.GetHashCode();
                                    break;
                                case 1:
                                    Types = OrderTypeEnum.单束鲜花.GetHashCode();
                                    break;
                                case 2:
                                    Types = OrderTypeEnum.多肉.GetHashCode();
                                    break;
                                case 3:
                                    Types = OrderTypeEnum.绿植出售.GetHashCode();
                                    break;
                                default:
                                    Types = OrderTypeEnum.普通商品.GetHashCode();
                                    break;
                            }
                        }
                        #endregion

                        #region 包月的区分第几次配送。用在前台区分显示  每次送赠送的商品不一样
                        if (times == ordercount)
                        {
                            notetype = PrintOrderNoteTypeEnum.最后一次收花.GetHashCode();
                        }
                        else if (times == 1)
                        {
                            notetype = PrintOrderNoteTypeEnum.第一次.GetHashCode();
                        }
                        else
                        {
                            notetype = PrintOrderNoteTypeEnum.其他次数.GetHashCode();
                        }
                        #endregion

                        #region 判断是否送花瓶处理逻辑
                        keys = $"{baoyueTableRow[i]["userid"].ToString()}{baoyueTableRow[i]["OrderId"].ToString()}{id}";
                        if (GiveOrder.Contains(keys))
                        {
                            isGave = 1;
                        }
                        var bl = false;//表示该订单里面是否购买了mini包月商品。

                        if (isGave != 1)
                        {
                            var miniGoodssid = RedisHelper.Get($"TingHua_miniGoodssid");//mini商品ID。配送方式需要改变一下。一周送2束  隔周配送
                            miniGoodssid += $",{RedisHelper.Get($"TingHua_ZhuTiGoodssid")}";
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
                                sql = $"select  count(1) miniCount from Orders o,OrderGoodss og where o.OrderId=og.OrderId and og.goodsid in ({miniGoodssid}) and o.orderstate in({state}) and o.Phone='{baoyueTableRow[i]["Phone"].ToString()}' and o.OrderId <{baoyueTableRow[i]["OrderId"]}";
                                var dS = db.ExecuteTable(sql);
                                if (dS.Rows[0]["miniCount"].ToString().ToInt32() == 0 && baoyueTableRow[i]["Times"].ToString() == "1")
                                {
                                    isGave = 1;
                                }
                            }
                        }
                        #endregion

                        #region 数据插入
                        if (index == 0)
                        {
                            baoYueInsertSql += $"('{ObjToClear(baoyueTableRow[i]["Person"].ToString())}','{ObjToClear(baoyueTableRow[i]["childPhone"].ToString())}','{ObjToClear(baoyueTableRow[i]["Province"].ToString())}','{ObjToClear(baoyueTableRow[i]["City"].ToString())}','{ObjToClear(baoyueTableRow[i]["Area"].ToString())}','{ObjToClear(baoyueTableRow[i]["AddressLongLat"].ToString())}','{ObjToClear(baoyueTableRow[i]["AddressNumber"] ?? "")}','{ObjToClear(baoyueTableRow[i]["OrderRemark"] ?? "")}','{ObjToClear(baoyueTableRow[i]["Remark"] ?? "")}','{DateTime.Now}','',{PrintOrderStateEnum.待打印.GetHashCode()},{isGave},'','{ObjToClear(baoyueTableRow[i]["DonotWant"] ?? "")}','{ObjToClear(baoyueTableRow[i]["FlowerGiftCardMsg"] ?? "")}','{ObjToClear(baoyueTableRow[i]["GoodssCode"] ?? "")}',{GoodssPrice},'{ObjToClear(baoyueTableRow[i]["OrderAgent"].ToString())}','{title}',{GoodssCount},{times},'{ObjToClear(baoyueTableRow[i]["GoodAttrId"] ?? "")}','{ObjToClear(baoyueTableRow[i]["OrderSerialNumber"] ?? "")}','{ObjToClear(baoyueTableRow[i]["ReciveTime"] ?? "")}',{Types},{id},{baoyueTableRow[i]["OrderId"]},{IsSpecial},{notetype},{LvzhiGroup},'{Warehouse}')";
                        }
                        else
                        {
                            baoYueInsertSql += $",('{ObjToClear(baoyueTableRow[i]["Person"].ToString())}','{ObjToClear(baoyueTableRow[i]["childPhone"].ToString())}','{ObjToClear(baoyueTableRow[i]["Province"].ToString())}','{ObjToClear(baoyueTableRow[i]["City"].ToString())}','{ObjToClear(baoyueTableRow[i]["Area"].ToString())}','{ObjToClear(baoyueTableRow[i]["AddressLongLat"].ToString())}','{ObjToClear(baoyueTableRow[i]["AddressNumber"] ?? "")}','{ObjToClear(baoyueTableRow[i]["OrderRemark"] ?? "")}','{ObjToClear(baoyueTableRow[i]["Remark"] ?? "")}','{DateTime.Now}','',{PrintOrderStateEnum.待打印.GetHashCode()},{isGave},'','{ObjToClear(baoyueTableRow[i]["DonotWant"] ?? "")}','{ObjToClear(baoyueTableRow[i]["FlowerGiftCardMsg"] ?? "")}','{ObjToClear(baoyueTableRow[i]["GoodssCode"] ?? "")}',{GoodssPrice},'{ObjToClear(baoyueTableRow[i]["OrderAgent"].ToString())}','{title}',{GoodssCount},{times},'{ObjToClear(baoyueTableRow[i]["GoodAttrId"] ?? "")}','{ObjToClear(baoyueTableRow[i]["OrderSerialNumber"] ?? "")}','{ObjToClear(baoyueTableRow[i]["ReciveTime"] ?? "")}',{Types},{id},{baoyueTableRow[i]["OrderId"]},{IsSpecial},{notetype},{LvzhiGroup},'{Warehouse}')";
                        }
                        index++;
                        if (index == 50)
                        {
                            db.ExecuteSql(baoYueInsertSql);
                            baoYueInsertSql = "insert PrintOrderSet (Person,Phone,Province,City,Area,AddressLongLat,AddressNumber,OrderRemark,Remark,AddTime,CPCode,State,IsGive,CPNumber,DonotWant,FlowerGiftCardMsg,GoodssCode,GoodssPrice,OrderAgent,GoodssTitle,GoodssCount,Times,GoodAttrId,OrderSerialNumber,ReciveTime,Type,OrderChild_Id,Orders_OrderId,IsSpecial,NoteType,LvzhiGroup,Warehouse) values";
                            index = 0;
                        }
                        #endregion

                    }
                    if (index != 0)
                    {
                        db.ExecuteSql(baoYueInsertSql);
                        index = 0;
                    }
                    #endregion

                    _logger.InfoFormat($"InsertPrintOrderJob-读取订单数据到打单记录表执行完毕共插入{count}条数据...");

                }
                else
                {
                    _logger.InfoFormat($"InsertPrintOrderJob-读取订单数据到打单记录表没有数据...");
                }
            }
            catch (Exception ex)
            {
                if ((DateTime.Now - phoneTime).TotalMinutes > 180)//如果相差180分钟就发短信提示
                {
                    Helper.SendMsgHelper.SendSmsMessage("17623028800", "195607", null);
                    phoneTime = DateTime.Now;
                }
                _logger.InfoFormat($"InsertPrintOrderJob-读取订单数据到打单记录表出现异常...{ex.Message}");
                var fullMesage = ErrorHelper.FullException(ex);
                _errorLog.ErrorFormat($"InsertPrintOrderJob错误信息;{fullMesage}");

            }
            #endregion

                #region 执行合并订单任务
            _logger.InfoFormat($"自动任务ConsolidatedOrderJob-组合订单数据开始运行...");
                try
            {
                Sql.SqlServerClient<Orders> newDb = Sql.SqlServerClientSingleton<Orders>.Instance;
                var ordersSql = $@"select * from (
                    select addresslonglat, count(1) b, 
                 value = stuff((SELECT ',' + convert(varchar, Id) FROM[tinghua].[dbo].[PrintOrderSet] AS t

                    WHERE t.addresslonglat = p.addresslonglat

                     and t.State = 1 and t.type in (6, 7, 12)

                      and t.Person = p.Person

                       and t.Phone = p.Phone

                       and t.Province = p.Province

                       and t.City = p.City

                       and t.Area = p.Area FOR xml path('')), 1, 1, '')
                    from[tinghua].[dbo].[PrintOrderSet] p
                    where type in (6, 7, 12) and state = 1 group by Person,Phone,Province,City,Area,addresslonglat
                ) a where a.b > 1";
                //var ordersSql = $@"select * from (
                //    select addresslonglat, count(1) b, 
	               // value = stuff((SELECT ',' + convert(varchar, Id) FROM[tinghua].[dbo].[PrintOrderSet] AS t

                //    WHERE t.addresslonglat = p.addresslonglat

                //     and id in (351261,351297,351260)

                //      and t.Person = p.Person

                //       and t.Phone = p.Phone

                //       and t.Province = p.Province

                //       and t.City = p.City

                //       and t.Area = p.Area FOR xml path('')), 1, 1, '')
	               //    from[tinghua].[dbo].[PrintOrderSet] p
                //    where id in (351261,351297,351260) group by Person,Phone,Province,City,Area,addresslonglat
                //) a where a.b > 1";
                var resultTable = newDb.ExecuteTable(ordersSql);


                if (resultTable.Rows.Count > 0)
                {
                    List<PrintOrderSet> printOrderSetList = new List<PrintOrderSet>();  //除开第一条数据之外的数据集合
                    var ids = "";   //存放非第一条数据的订单ID
                    var ordersCount = 0;
                    for (int i = 0; i < resultTable.Rows.Count; i++)
                    {
                        PrintOrderSet newOrder = new PrintOrderSet();//第一条数据
                        var value = System.Convert.ToString(resultTable.Rows[i]["value"]);
                        var sonSql = $"select * from PrintOrderSet where Id in({value})";
                        var orderTable = newDb.ExecuteTable(sonSql);
                        for (int j = 0; j < orderTable.Rows.Count; j++)
                        {
                            if (j > 0)
                            {
                                PrintOrderSet order = new PrintOrderSet()
                                {
                                    Id = System.Convert.ToInt32(orderTable.Rows[j]["Id"]),
                                    State = System.Convert.ToInt32(orderTable.Rows[j]["State"]),
                                    GoodssTitle = System.Convert.ToString(orderTable.Rows[j]["GoodssTitle"]),
                                    NoteStr = System.Convert.ToString(orderTable.Rows[j]["NoteStr"])
                                };
                                printOrderSetList.Add(order);
                            }
                            else
                            {
                                newOrder.Id = System.Convert.ToInt32(orderTable.Rows[j]["Id"]);
                                newOrder.State = System.Convert.ToInt32(orderTable.Rows[j]["State"]);
                                newOrder.GoodssTitle = System.Convert.ToString(orderTable.Rows[j]["GoodssTitle"]);
                                newOrder.NoteStr = System.Convert.ToString(orderTable.Rows[j]["NoteStr"]);
                            }
                        }
                        if (printOrderSetList.Count > 0)
                        {
                            var noteStr = newOrder.GoodssTitle.Split('#').ToList(); //第一条

                            var newGoodssTitle = "";   //最后结果（商品名称与数量组成的字符串）
                            List<Product> productList = new List<Product>();    //最后得出来的商品和对应商品数量的集合，
                            List<string> str = new List<string>();  //累计叠加的商品名称，用于循环比较商品名称是否再这个集合中从而确定productList集合中是否已经存在了此商品
                            #region 第一条数据 
                            if (noteStr.Count > 0)
                            {
                                noteStr.ForEach(p =>    //第一条
                                {
                                    var od = p.Split('*'); //第一条
                                        productList = Calculation(productList, ref str, od);
                                });
                            }
                            #endregion

                            #region 其它条数据
                            foreach (var mo in printOrderSetList)   //其它条数据组成的集合
                            {
                                ids = ids + $"{mo.Id},";
                                if (!string.IsNullOrEmpty(mo.GoodssTitle))
                                {
                                    var newNoteStr = mo.GoodssTitle.Split('#').ToList();    //第n条商品名称和数据量
                                    newNoteStr.ForEach(o => //第n条
                                    {
                                        var newOd = o.Split('*');   //第n条
                                            List<Product> newProductList = Calculation(productList, ref str, newOd);
                                    });
                                }
                            }
                            #endregion


                            if (productList.Count > 0)
                            {
                                foreach (var model in productList)
                                {
                                    newGoodssTitle = newGoodssTitle + $"{model.Name}*{model.Number}#";
                                }
                            }
                            if (!string.IsNullOrEmpty(newGoodssTitle)) { newOrder.GoodssTitle = newGoodssTitle; }
                        }

                        if (!string.IsNullOrEmpty(newOrder.GoodssTitle) && !string.IsNullOrEmpty(ids))
                        {
                            if (string.IsNullOrEmpty(newOrder.NoteStr))
                            {
                                newOrder.NoteStr = ids;
                            }
                            else
                            {
                                newOrder.NoteStr = newOrder.NoteStr + "," + ids;
                            }
                            if (!string.IsNullOrEmpty(newOrder.NoteStr)) { newOrder.NoteStr = newOrder.NoteStr.TrimEnd(','); }
                            newOrder.GoodssTitle = newOrder.GoodssTitle.TrimEnd('#').TrimEnd(',');
                            var goodssList = newOrder.GoodssTitle.Split('#').ToList();
                            var GoodssCount = goodssList.Count;
                            if (goodssList.Count == 1)
                            {
                                GoodssCount = int.Parse(goodssList[0].Split('*')[1]);
                            }
                            var IsSpecial = 0;
                            if (GoodssCount > 1)
                            {
                                IsSpecial = 1;
                            }
                            var updateSql = $"update [tinghua].[dbo].[PrintOrderSet] set [GoodssTitle]='{newOrder.GoodssTitle}',IsSpecial={IsSpecial},GoodssCount={GoodssCount},[NoteStr]='{newOrder.NoteStr}'where [Id] = {newOrder.Id};";
                            if (!string.IsNullOrEmpty(ids))
                            {
                                ids = ids.TrimEnd(',');
                                updateSql = updateSql + $"update [tinghua].[dbo].[PrintOrderSet] set [state]=-1,[MergeType]=0 where [Id] in ({ids});";
                            }
                            newDb.ExecuteSql(updateSql);
                        }
                        printOrderSetList.Clear();
                        ids = string.Empty;
                        ordersCount++;
                    }
                    _logger.InfoFormat($"ConsolidatedOrderJob-组合订单数据到打单记录表执行完毕共修改{ordersCount}条数据...");
                }
                else
                {
                    _logger.InfoFormat($"自动任务ConsolidatedOrderJob-组合订单数据读取数据时无数据");
                }
            }
            catch (Exception ex)
            {
                _logger.ErrorFormat($"IConsolidatedOrderJob-读取订单数据到打单记录表出现异常...{ex.Message}");
                var fullMesage = ErrorHelper.FullException(ex);
                _errorLog.ErrorFormat($"InsertPrintOrderJob错误信息;{fullMesage}");
            }
            finally {
                onlyNumber = 1;
            }
            #endregion
        }

        public string ObjToClear(object obj)
        {
            return obj.ToString().Replace("'", "").Replace(".", "-").Trim();
        }

        /// <summary>
        /// 是否需要拆单 false不需要 true需要拆单并拆单已完成  
        /// </summary>
        /// <param name="orderid"></param>
        /// <returns></returns>
        public bool splitOrder(string orderid)
        {
            Sql.SqlServerClient<Orders> db = Sql.SqlServerClientSingleton<Orders>.Instance;
            var sql = @"select og.orderGoodsId,g.CategoryFatherId from ordergoodss og,GoodsAttributeSet ga,AttributeSet a,AttributeShowSet ats,Goodss g 
where ga.Attribute_Id = a.id and a.AttributeShow_Id = ats.id and ats.Goodss_GoodsId = g.GoodsId and og.GoodsId = ga.Id and og.OrderId =" + orderid;
            var dt = db.ExecuteTable(sql);
            var newType = OrderTypeEnum.绿植出售.GetHashCode();
            var tempIsLvZhi = 1;//默认没有绿植
            var tempIsOther = 1;//默认除绿植以外的其他商品
            if (dt.Rows.Count > 0)
            {
                var orderIdList = new List<int>();
                var CategoryFatherId = 0;
                for (int i = 0; i < dt.Rows.Count; i++)
                {
                    var nowTypeId = int.Parse(dt.Rows[i]["CategoryFatherId"].ToString());
                    var orderGoodsId = int.Parse(dt.Rows[i]["orderGoodsId"].ToString());
                    if (i == 0)
                    {
                        if (nowTypeId == 3 || nowTypeId == 73 || nowTypeId == 2)
                        {
                            orderIdList.Add(orderGoodsId);
                            tempIsLvZhi = 2;
                        }
                        else
                        {
                            tempIsOther = 2;
                        }
                        CategoryFatherId = nowTypeId;
                    }
                    else
                    {
                        if (nowTypeId != CategoryFatherId)
                        {
                            if (nowTypeId == 3 || nowTypeId == 73 || nowTypeId == 2)
                            {
                                tempIsLvZhi = 2;
                                orderIdList.Add(orderGoodsId);
                            }
                            else
                            {
                                tempIsOther = 2;
                            }
                        }
                        else
                        {
                            if (nowTypeId == 3 || nowTypeId == 73 || nowTypeId == 2)
                            {
                                tempIsLvZhi = 2;
                                orderIdList.Add(orderGoodsId);
                            }
                            else
                            {
                                tempIsOther = 2;
                            }
                        }
                    }
                }
                if (tempIsOther==2 && tempIsLvZhi==2)//是否需要拆单
                {
                    var oldtype = OrderTypeEnum.普通商品.GetHashCode();
                    if (dt.Select("CategoryFatherId in (25)").Count() > 0)
                    {
                        oldtype = OrderTypeEnum.包月.GetHashCode();
                    }
                    else if (dt.Select("CategoryFatherId in (1)").Count() > 0)
                    {
                        oldtype = OrderTypeEnum.单束鲜花.GetHashCode();
                    }
                    else if (dt.Select("CategoryFatherId in (73)").Count() > 0)
                    {
                        newType = OrderTypeEnum.绿植租赁.GetHashCode();
                    }
                    else if (dt.Select("CategoryFatherId in (2)").Count() > 0)
                    {
                        newType = OrderTypeEnum.多肉.GetHashCode();
                    }

                    //进行拆单操作
                    try
                    {
                        if (orderIdList.Count > 0)
                        {
                            var insertSql = @"INSERT INTO orders(userid,OrderSerialNumber,OrderState,Person,Phone,Province,City,Area,AddressLongLat,AddressNumber,Long,Lat,ShopId,OrderAddTime,PayType,PayMent,OrderRemark,MoneyPayable,MoneyPack,MoneyExpress,CouponName,MoneyCoupon,MoneyReality,efficiencyNumber,courtesyNumber,expressName,expressNumber,UserAgent,Type,ReciveTime,Mobi,DonotWant,ContactsName,ContactMobile,Remark,expressAddressCode,DescriptionNumber,ChargeMoney,FlowerGiftChildCard_Id,DeliveryReminder,ReceivingReminder,RoyaltyRatio,IsGroup,GoodsId,GroupId,FirstScale,TwoScale,IsPresent,RefundMoney,StallId) 
SELECT userid, OrderSerialNumber+'_1' as OrderSerialNumber,OrderState,Person,Phone,Province,City,Area,AddressLongLat,AddressNumber,Long,Lat,ShopId,OrderAddTime,PayType,PayMent,OrderRemark,0 as MoneyPayable,0 as MoneyPack,MoneyExpress,CouponName,0 as MoneyCoupon,0 as MoneyReality,efficiencyNumber,courtesyNumber,expressName,expressNumber,UserAgent," + newType + " as Type,ReciveTime,Mobi,DonotWant,ContactsName,ContactMobile,Remark,expressAddressCode,DescriptionNumber,ChargeMoney,FlowerGiftChildCard_Id,DeliveryReminder,ReceivingReminder,RoyaltyRatio,IsGroup,GoodsId,GroupId,FirstScale,TwoScale,IsPresent,RefundMoney,StallId FROM orders WHERE orderid = " + orderid + " select SCOPE_IDENTITY() as newOrderid";
                            var insertOrderid = db.ExecuteTable(insertSql).Rows[0]["newOrderid"].ToString();
                            var updateStr = "update ordergoodss set orderid=" + insertOrderid + " where ordergoodsid in (" + string.Join(",", orderIdList) + ");update orders set type=" + oldtype + " where orderid=" + orderid;
                            updateStr += $";insert LogSet(Type,Content,orderid,createTime)values(5,'系统自动拆单新建订单,拆自订单ID:{orderid}',{insertOrderid},GETDATE())";
                            db.ExecuteSql(updateStr);
                        }
                    }
                    catch (Exception)
                    {
                    }
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// 判断一个集合A里面是否包含另外一个集合B的值。只要包含A包含B里面的一个都返回True
        /// </summary>
        /// <param name="oldList">A集合</param>
        /// <param name="newList">B集合</param>
        /// <returns></returns>
        public bool fixedGoodss(List<string> oldList,List<string> newList)
        {
            var bl = false;
            foreach (var item in newList)
            {
                if (oldList.Where(l => l.Contains(item)).Any())
                {
                    bl = true;
                    break;
                }
            }
            return bl;
        }

        /// <summary>
        /// 根据商品分类ID重新获取订单类型(用原始订单类型可能会出现类型错误导致仓库分仓错误)
        /// </summary>
        /// <param name="oldList">原订单商品分类ID</param>
        /// <param name="def">默认值。在redis里面找到此类型的Key对应的类型默认值</param>
        /// <returns></returns>
        public int getNewOrderType(List<string> oldList,int def)
        {
            var retType = def;
            if (oldList.Where(l=>l.Contains("_25")).Any())
            {
                var temp = RedisHelper.Get("TingHuaAutoManageInsertPrintOrderJob_OrderType25");
                if (string.IsNullOrEmpty(temp))
                {
                    RedisHelper.Set("TingHuaAutoManageInsertPrintOrderJob_OrderType25", OrderTypeEnum.包月.GetHashCode());
                    retType = OrderTypeEnum.包月.GetHashCode();
                }
                else
                {
                    retType = temp.ToInt32();
                }
            }
            else if (oldList.Where(l => l.Contains("_1")).Any())
            {
                var temp = RedisHelper.Get("TingHuaAutoManageInsertPrintOrderJob_OrderType1");
                if (string.IsNullOrEmpty(temp))
                {
                    RedisHelper.Set("TingHuaAutoManageInsertPrintOrderJob_OrderType1", OrderTypeEnum.单束鲜花.GetHashCode());
                    retType = OrderTypeEnum.单束鲜花.GetHashCode();
                }
                else
                {
                    retType = temp.ToInt32();
                }
            }
            else if (oldList.Where(l => l.Contains("_3")).Any())
            {
                var temp = RedisHelper.Get("TingHuaAutoManageInsertPrintOrderJob_OrderType3");
                if (string.IsNullOrEmpty(temp))
                {
                    RedisHelper.Set("TingHuaAutoManageInsertPrintOrderJob_OrderType3", OrderTypeEnum.绿植出售.GetHashCode());
                    retType = OrderTypeEnum.绿植出售.GetHashCode();
                }
                else
                {
                    retType = temp.ToInt32();
                }
            }
            else if (oldList.Where(l => l.Contains("_2")).Any())
            {
                var temp = RedisHelper.Get("TingHuaAutoManageInsertPrintOrderJob_OrderType2");
                if (string.IsNullOrEmpty(temp))
                {
                    RedisHelper.Set("TingHuaAutoManageInsertPrintOrderJob_OrderType2", OrderTypeEnum.多肉.GetHashCode());
                    retType = OrderTypeEnum.多肉.GetHashCode();
                }
                else
                {
                    retType = temp.ToInt32();
                }
            }
            return retType;
        }


        /// <summary>
        /// 合并订单时计算订单里的（GoodssTitle）字段值，
        /// </summary>
        /// <param name="productList">最后得出来的商品和对应商品数量的集合</param>
        /// <param name="str">累计叠加的商品名称，用于循环比较商品名称是否再这个集合中从而确定productList集合中是否已经存在了此商品</param>
        /// <param name="arryList">当前需要计算的(GoodssTitle)字段值（商品名称和商品数量组成的arrylist）</param>
        /// <returns>最后得出来的商品和对应商品数量的集合</returns>
        public List<Product> Calculation(List<Product> productList, ref List<string> str, string[] arryList)
        {
            if (productList.Count <= 0 && str.Count <= 0)
            {
                productList.Add(new Product
                {
                    Name = arryList[0],
                    Number = System.Convert.ToInt32(arryList.Length >= 2 ? arryList[1] : "0")
                });
                str.Add(arryList[0].Replace(" ", ""));
            }
            else if (productList.Count > 0 && str.Count > 0)
            {
                if (str.Contains(arryList[0].Replace(" ", "")))
                {
                    productList.ForEach(l =>
                    {
                        if (l.Name.Contains(arryList[0].Replace(" ", "")))
                        {
                            l.Number = l.Number + System.Convert.ToInt32(arryList.Length >= 2 ? arryList[1] : "0");
                        }
                    });
                }
                else
                {
                    productList.Add(new Product
                    {
                        Name = arryList[0],
                        Number = System.Convert.ToInt32(arryList.Length >= 2 ? arryList[1] : "0")
                    });
                    str.Add(arryList[0].Replace(" ", ""));
                }
            }
            else
            {
                productList.Clear();
                str.Clear();
            }
            return productList;
        }
    }

}
