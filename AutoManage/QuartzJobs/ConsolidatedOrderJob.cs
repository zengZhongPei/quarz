using AutoManage.Helper;
using log4net;
using Quartz;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Tinghua.Management.Enum;
using Tinghua.Management.Model;
using Tinghua.Management.Utility.Helper;


namespace AutoManage.QuartzJobs
{
    /// <summary>
    /// 合并绿植订单,鲜花订单不做处理.此自动任务已停止运行,已迁移到其他任务(InsertPrintOrderJob)中
    /// </summary>
    public sealed class ConsolidatedOrderJob : IJob
    {
        private readonly ILog _logger = LogManager.GetLogger(typeof(ConsolidatedOrderJob));
        private readonly ILog _errLog = LogManager.GetLogger("Com.Foo");

        public void Execute(IJobExecutionContext context)
        {
            _logger.InfoFormat($"自动任务ConsolidatedOrderJob-组合订单数据开始运行...");
            try
            {
                Sql.SqlServerClient<Orders> db = Sql.SqlServerClientSingleton<Orders>.Instance;
                var sql = $"select * from (select addresslonglat, count(1) b, value = stuff((SELECT ',' + convert(varchar, Id) FROM[tinghua].[dbo].[PrintOrderSet] AS t WHERE t.addresslonglat = p.addresslonglat and t.State = 1 and t.type in (6,7) and t.Person = p.Person and t.Phone = p.Phone and t.Province = p.Province and t.City = p.City and t.Area = p.Area FOR xml path('')), 1, 1, '') from[tinghua].[dbo].[PrintOrderSet] p where type in (6,7) and state = 1 group by Person,Phone,Province,City,Area,addresslonglat) a where a.b > 1";
                var resultTable = db.ExecuteTable(sql);


                if (resultTable.Rows.Count > 0)
                {
                    List<PrintOrderSet> printOrderSetList = new List<PrintOrderSet>();  //除开第一条数据之外的数据集合
                    var ids = "";   //存放非第一条数据的订单ID
                    var count = 0;
                    for (int i = 0; i < resultTable.Rows.Count; i++)
                    {
                        PrintOrderSet newOrder = new PrintOrderSet();//第一条数据
                        var value = Convert.ToString(resultTable.Rows[i]["value"]);
                        var sonSql = $"select * from PrintOrderSet where Id in({value})";
                        var orderTable = db.ExecuteTable(sonSql);
                        for (int j = 0; j < orderTable.Rows.Count; j++)
                        {
                            if (j > 0)
                            {
                                PrintOrderSet order = new PrintOrderSet()
                                {
                                    Id = Convert.ToInt32(orderTable.Rows[j]["Id"]),
                                    State = Convert.ToInt32(orderTable.Rows[j]["State"]),
                                    GoodssTitle = Convert.ToString(orderTable.Rows[j]["GoodssTitle"]),
                                    NoteStr = Convert.ToString(orderTable.Rows[j]["NoteStr"])
                                };
                                printOrderSetList.Add(order);
                            }
                            else
                            {
                                newOrder.Id = Convert.ToInt32(orderTable.Rows[j]["Id"]);
                                newOrder.State = Convert.ToInt32(orderTable.Rows[j]["State"]);
                                newOrder.GoodssTitle = Convert.ToString(orderTable.Rows[j]["GoodssTitle"]);
                                newOrder.NoteStr = Convert.ToString(orderTable.Rows[j]["NoteStr"]);
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
                            else {
                                newOrder.NoteStr = newOrder.NoteStr+","+ ids;
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
                            db.ExecuteSql(updateSql);
                        }
                        printOrderSetList.Clear();
                        ids = string.Empty;
                        count++;
                    }
                    _logger.InfoFormat($"ConsolidatedOrderJob-组合订单数据到打单记录表执行完毕共修改{count}条数据...");
                }
                else
                {
                    _logger.InfoFormat($"自动任务ConsolidatedOrderJob-组合订单数据读取数据时无数据");
                }
            }
            catch (Exception e)
            {
                _logger.InfoFormat($"ConsolidatedOrderJob-读取订单数据到打单记录表出现异常...{e.Message}");
                var fullMesage = ErrorHelper.FullException(e);
                _errLog.ErrorFormat($"ConsolidatedOrderJob错误信息;{fullMesage}");
            }
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
                    Number = Convert.ToInt32(arryList.Length>=2?arryList[1]:"0")
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
                            l.Number = l.Number + Convert.ToInt32(arryList.Length >= 2 ? arryList[1] : "0");
                        }
                    });
                }
                else
                {
                    productList.Add(new Product
                    {
                        Name = arryList[0],
                        Number = Convert.ToInt32(arryList.Length >= 2 ? arryList[1] : "0")
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

    public class PrintOrderSet
    {
        /// <summary>
        /// 订单表（PrintOrder）id
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 订单表（PrintOrder）State
        /// </summary>
        public int State { get; set; }

        /// <summary>
        /// 订单表（PrintOrder）GoodssTitle
        /// </summary>
        public string GoodssTitle { get; set; }

        /// <summary>
        /// 订单表（PrintOrder）NoteStr
        /// </summary>
        public string NoteStr { get; set; }

    }

    public class Product
    {
        /// <summary>
        /// 产品名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 数量
        /// </summary>
        public int Number { get; set; }

        public string NoteStr { get; set; }
    }

    //public class Auxiliary
    //{
    //    public string GoodsTitle { get; set; }
    //    public string Ids { get; set; }
    //}
}
