using System.Data.SqlClient;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
namespace AutoManage.Sql
{
    [Serializable]
    public class SqlServerClient<M> where M : new()
    {
        static Dictionary<string, int> _sqlKeywords = null;
        private string autoIncrement = string.Empty;
        private string unique = string.Empty;
        private SqlServer DBbase;
        private Type type;
        public IQueryable<M> Where
        {
            get
            {
                return new QueryableData<M>
                {
                    Provider = new QueryProvider(new CallBackEvent.CallBackEventHandler(this.ProviderCallBack))
                };
            }
        }
        public SqlServerClient()
        {
            this.type = typeof(M);
            this.DBbase = new SqlServer(SqlServerConfig.GetConn(this.type.FullName));
            this.autoIncrement = TableHelper.GetAutoIncrement(this.DBbase, this.type);
            this.unique = TableHelper.GetIdentity(this.DBbase, this.type);
        }
        private object ProviderCallBack(Expression expression, Type returnType)
        {
            MethodCallExpression methodCallExpression = (MethodCallExpression)expression;
            ConditionBuilder conditionBuilder = new ConditionBuilder();
            conditionBuilder.Build(expression, this.type, true);
            object obj = null;
            string name;
            if ((name = methodCallExpression.Method.Name) != null)
            {
                if (_sqlKeywords == null)
                {
                    _sqlKeywords = new Dictionary<string, int>(8)
					{

						{
							"FirstOrDefault",
							0
						},

						{
							"First",
							1
						},

						{
							"Select",
							2
						},

						{
							"Skip",
							3
						},

						{
							"Take",
							4
						},

						{
							"OrderByDescending",
							5
						},

						{
							"OrderBy",
							6
						},

						{
							"Count",
							7
						}
					};
                }
                int num;
                if (_sqlKeywords.TryGetValue(name, out num))
                {
                    DataTable dataTable;
                    switch (num)
                    {
                        case 0:
                        case 1:
                            {
                                dataTable = this.DBbase.GetDataTable(conditionBuilder.Condition, conditionBuilder.Arguments.ToArray());
                                object obj2;
                                if (AnonymouseHelper.IsAnonyousType(returnType))
                                {
                                    obj2 = ConvertHelper.GetAnonymous(dataTable, returnType);
                                }
                                else
                                {
                                    obj2 = ConvertHelper.GetList(dataTable, returnType);
                                }
                                IEnumerable enumerable = (IEnumerable)obj2;
                                IEnumerator enumerator = enumerable.GetEnumerator();
                                try
                                {
                                    if (enumerator.MoveNext())
                                    {
                                        object current = enumerator.Current;
                                        obj = current;
                                    }
                                    return obj;
                                }
                                finally
                                {
                                    IDisposable disposable = enumerator as IDisposable;
                                    if (disposable != null)
                                    {
                                        disposable.Dispose();
                                    }
                                }
                                break;
                            }
                        case 2:
                        case 3:
                        case 4:
                        case 5:
                        case 6:
                            break;
                        case 7:
                            obj = this.DBbase.GetSingle(conditionBuilder.Condition, conditionBuilder.Arguments.ToArray());
                            obj = Convert.ToInt32(obj);
                            return obj;
                        default:
                            return obj;
                    }
                    Type[] genericArguments = returnType.GetGenericArguments();
                    returnType = ((genericArguments.Length == 0) ? this.type : genericArguments[0]);
                    dataTable = this.DBbase.GetDataTable(conditionBuilder.Condition, conditionBuilder.Arguments.ToArray());
                    if (AnonymouseHelper.IsAnonyousType(returnType))
                    {
                        obj = ConvertHelper.GetAnonymous(dataTable, returnType);
                    }
                    else
                    {
                        obj = ConvertHelper.GetList(dataTable, returnType);
                    }
                    obj = (IEnumerable)obj;
                }
            }
            return obj;
        }
        public int GetCount()
        {
            string sQLString = "select count(*) from " + this.type.Name;
            object single = this.DBbase.GetSingle(sQLString, new SqlParameter[0]);
            return Convert.ToInt32(single);
        }
        public int GetCount(Expression<Func<M, bool>> expression)
        {
            ConditionBuilder builder = this.GetBuilder(expression);
            string text = "select count(*) from " + this.type.Name + " {0}";
            text = string.Format(text, builder.Condition);
            object single = this.DBbase.GetSingle(text, builder.Arguments.ToArray());
            return Convert.ToInt32(single);
        }
        public object GetSum<F>(Expression<Func<M, bool>> expression, Expression<Func<M, F>> fields)
        {
            ConditionBuilder builder = this.GetBuilder(expression);
            string text = this.FieldsByNew<F>(fields);
            string text2 = string.Concat(new string[]
			{
				"select sum(",
				text,
				") from ",
				this.type.Name,
				" {0}"
			});
            text2 = string.Format(text2, builder.Condition);
            object obj = this.DBbase.GetSingle(text2, builder.Arguments.ToArray());
            if (obj != null)
            {
                obj = Convert.ToDecimal(obj).ToString("G0");
            }
            else
            {
                obj = 0;
            }
            return obj;
        }
        public object GetSingle<F>(Expression<Func<M, bool>> expression, Expression<Func<M, F>> fields)
        {
            ConditionBuilder builder = this.GetBuilder(expression);
            string text = this.FieldsByNew<F>(fields);
            string text2 = string.Concat(new string[]
			{
				"select ",
				text,
				" from ",
				this.type.Name,
				" {0}"
			});
            text2 = string.Format(text2, builder.Condition);
            return this.DBbase.GetSingle(text2, builder.Arguments.ToArray());
        }
        public int Delete()
        {
            string sQLString = "truncate table  " + this.type.Name;
            return this.DBbase.ExecuteSql(sQLString, new SqlParameter[0]);
        }
        public int Delete(Expression<Func<M, bool>> expression)
        {
            string text = "delete from " + this.type.Name + " {0}";
            ConditionBuilder builder = this.GetBuilder(expression);
            text = string.Format(text, builder.Condition);
            return this.DBbase.ExecuteSql(text, builder.Arguments.ToArray());
        }
        public M Select(Expression<Func<M, bool>> expression)
        {
            ConditionBuilder builder = this.GetBuilder(expression);
            string text = "select top 1 * from " + this.type.Name + " {0} ";
            text = string.Format(text, builder.Condition);
            DataTable dataTable = this.DBbase.GetDataTable(text, builder.Arguments.ToArray());
            IList<M> list = ConvertHelper.GetList<M>(dataTable);
            M result = default(M);
            if (list != null && list.Count > 0)
            {
                result = list[0];
            }
            return result;
        }
        public M Select<TKey, F>(Expression<Func<M, bool>> expression, Expression<Func<M, TKey>> orderby, Expression<Func<M, F>> fields)
        {
            List<M> list = this.GetList<TKey, F>(expression, orderby, 1, 1, fields);
            if (list.Count == 1)
            {
                return list[0];
            }
            return default(M);
        }
        public List<M> GetList<TKey, F>(Expression<Func<M, TKey>> orderby, Expression<Func<M, F>> fields, int pSize)
        {
            return this.GetListRow<TKey, F>(null, orderby, true, 1, pSize, fields);
        }
        public List<M> GetList<TKey, F>(Expression<Func<M, bool>> expression, Expression<Func<M, TKey>> orderby, Expression<Func<M, F>> fields)
        {
            return this.GetListRow<TKey, F>(expression, orderby, true, 0, 0, fields);
        }
        public List<M> GetList<TKey, F>(Expression<Func<M, bool>> expression, Expression<Func<M, TKey>> orderby, int pStart, int pSize, Expression<Func<M, F>> fields)
        {
            return this.GetListRow<TKey, F>(expression, orderby, true, pStart, pSize, fields);
        }
        public List<M> GetList<TKey, F>(Expression<Func<M, bool>> expression, Expression<Func<M, TKey>> orderby, bool IsDesc, int pStart, int pSize, Expression<Func<M, F>> fields)
        {
            return this.GetListRow<TKey, F>(expression, orderby, IsDesc, pStart, pSize, fields);
        }
        public List<M> GetListRow<TKey, F>(Expression<Func<M, bool>> expression, Expression<Func<M, TKey>> orderby, bool IsDesc, int pStart, int pSize, Expression<Func<M, F>> fields)
        {
            ConditionBuilder builder = this.GetBuilder(expression);
            string str = string.Empty;
            if (orderby != null)
            {
                str = " order by " + ((MemberExpression)orderby.Body).Member.Name + (IsDesc ? " desc " : " asc ");
            }
            string str2 = this.FieldsByNew<F>(fields);

            string sql = string.Format("select " + str2 + ", ROW_NUMBER() OVER({1}) AS RowNumber  from " + this.type.Name + " {0} ", builder.Condition, str);
            if (pSize > 0)
            {
                sql = string.Concat("select * from (", sql, ") tttx where RowNumber Between ", (pStart - 1) * pSize + 1, " and ", pStart * pSize);
            }
            DataTable dataTable = this.DBbase.GetDataTable(sql, builder.Arguments.ToArray());
            builder.Arguments = null;
            return ConvertHelper.GetList<M>(dataTable);

        }
        public long Insert(M entity)
        {
            PropertyInfo[] propertys = ConvertHelper.GetPropertys(this.type);
            string text = "insert into " + this.type.Name + "({0}) values ({1});";
            List<SqlParameter> list = new List<SqlParameter>();
            if (this.autoIncrement != string.Empty)
            {
                text += "select  @@IDENTITY;";
            }
            StringBuilder stringBuilder = new StringBuilder();
            StringBuilder stringBuilder2 = new StringBuilder();
            PropertyInfo[] array = propertys;
            for (int i = 0; i < array.Length; i++)
            {
                PropertyInfo propertyInfo = array[i];
                if (propertyInfo.Name.ToLower() != this.autoIncrement.ToLower())
                {
                    list.Add(new SqlParameter("@" + propertyInfo.Name, TypeConvert.ConvertDbType(propertyInfo.PropertyType.Name))
                    {
                        Value = propertyInfo.GetValue(entity, null)
                    });
                    stringBuilder.Append(propertyInfo.Name + ",");
                    stringBuilder2.Append("@" + propertyInfo.Name + ",");
                }
            }
            text = string.Format(text, stringBuilder.ToString().TrimEnd(new char[]
			{
				','
			}), stringBuilder2.ToString().TrimEnd(new char[]
			{
				','
			}));
            object single = this.DBbase.GetSingle(text, list.ToArray());
            if (single != null)
            {
                return Convert.ToInt64(single);
            }
            return 0L;
        }
        public bool Update<V>(V value, Expression<Func<M, bool>> expression)
        {
            int num = 0;
            string text = "update " + this.type.Name + "{0} {1};";
            ConditionBuilder builder = this.GetBuilder(expression);
            StringBuilder stringBuilder = new StringBuilder(" set ");
            List<SqlParameter> arguments = builder.Arguments;
            PropertyInfo[] propertys = ConvertHelper.GetPropertys(value.GetType());
            PropertyInfo[] array = propertys;
            for (int i = 0; i < array.Length; i++)
            {
                PropertyInfo propertyInfo = array[i];
                num++;
                string text2 = "@U" + propertyInfo.Name + num;
                stringBuilder.Append(propertyInfo.Name + "=" + text2 + ",");
                arguments.Add(new SqlParameter(text2, propertyInfo.GetValue(value, null)));
            }
            text = string.Format(text, stringBuilder.ToString().TrimEnd(new char[]
			{
				','
			}), builder.Condition);
            return this.DBbase.ExecuteSql(text, arguments.ToArray()) != 0;
        }
        public bool Update(M entity)
        {
            PropertyInfo[] propertys = ConvertHelper.GetPropertys(this.type);
            string text = "update " + this.type.Name + "{0} {1};";
            List<SqlParameter> list = new List<SqlParameter>();
            if (this.unique == string.Empty)
            {
                throw new ArgumentException("没有找到主键列");
            }
            StringBuilder stringBuilder = new StringBuilder(" set ");
            string arg = string.Empty;
            PropertyInfo[] array = propertys;
            for (int i = 0; i < array.Length; i++)
            {
                PropertyInfo propertyInfo = array[i];
                list.Add(new SqlParameter("@" + propertyInfo.Name, TypeConvert.ConvertDbType(propertyInfo.PropertyType.Name))
                {
                    Value = propertyInfo.GetValue(entity, null)
                });
                if (propertyInfo.Name.ToLower() != this.unique.ToLower())
                {
                    stringBuilder.Append(propertyInfo.Name + "=@" + propertyInfo.Name + ",");
                }
                else
                {
                    arg = " where " + propertyInfo.Name + "=@" + propertyInfo.Name;
                }
            }
            text = string.Format(text, stringBuilder.ToString().TrimEnd(new char[]
			{
				','
			}), arg);
            return this.DBbase.ExecuteSql(text, list.ToArray()) != 0;
        }
        private ConditionBuilder GetBuilder(Expression<Func<M, bool>> expression)
        {
            ConditionBuilder conditionBuilder = new ConditionBuilder();
            if (expression == null)
            {
                conditionBuilder.Condition = string.Empty;
            }
            else
            {
                conditionBuilder.Build(expression.Body, this.type, false);
            }
            return conditionBuilder;
        }
        private string FieldsByNew<F>(Expression<Func<M, F>> fields)
        {
            MemberExpression memberExpression = null;
            if (fields.Body.NodeType == ExpressionType.Convert)
            {
                UnaryExpression body = (UnaryExpression)fields.Body;
                memberExpression = body.Operand as MemberExpression;

                return memberExpression.Member.Name;
            }
            else if (fields.Body.NodeType == ExpressionType.MemberAccess)
            {
                memberExpression = fields.Body as MemberExpression;

                return memberExpression.Member.Name;
            }
            else if (fields.Body.NodeType == ExpressionType.Parameter)
            {
                StringBuilder stringBuilder = new StringBuilder();
                PropertyInfo[] propertys = ConvertHelper.GetPropertys(this.type);
                PropertyInfo[] array = propertys;
                for (int i = 0; i < array.Length; i++)
                {
                    PropertyInfo propertyInfo = array[i];
                    stringBuilder.Append(propertyInfo.Name + ",");
                }
                stringBuilder = stringBuilder.Remove(stringBuilder.Length - 1, 1);
                return stringBuilder.ToString();
            }
            throw new ArgumentException("无法解析出字段");
        }


        public DataTable ExecuteTable(string sql, params SqlParameter[] param)
        {
            return this.DBbase.GetDataTable(sql, param);
        }

        public int ExecuteSql(string sql, params SqlParameter[] param)
        {
            return this.DBbase.ExecuteSql(sql, param);
        }
        public object ExecuteScalar(string sql, params SqlParameter[] param)
        {
            return this.DBbase.ExecuteScalar(sql, param);
        }
    }
}
