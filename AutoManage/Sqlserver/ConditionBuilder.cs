using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace AutoManage.Sql
{
    internal class ConditionBuilder : ExpressionVisitor
    {
        private int ParIndex;
        public string Condition = string.Empty;
        public Type t;
        public void Build(Expression expression, Type type, bool IsBuildSelect = false)
        {
            this.t = type;
            PartialEvaluator partialEvaluator = new PartialEvaluator();
            Expression exp = partialEvaluator.Eval(expression);
            this.Arguments = new List<SqlParameter>();
            this.ConditionPars = new Dictionary<string, StringBuilder>();
            this.ConditionPars.Add("select", new StringBuilder());
            this.ConditionPars.Add("from", new StringBuilder());
            this.ConditionPars.Add("where", new StringBuilder());
            this.ConditionPars.Add("orderby", new StringBuilder());
            this.ConditionPars.Add("top", new StringBuilder());
            this.Visit(exp);
            if (IsBuildSelect)
            {
                this.BuildSelect(expression);
            }
            this.Condition = this.GetSql();
        }
        private string GetSql()
        {
            StringBuilder stringBuilder = new StringBuilder();
            if (this.ConditionPars["select"].Length > 0)
            {
                stringBuilder.Append("select " + this.ConditionPars["select"].ToString().TrimEnd(new char[]
				{
					','
				}));
            }
            if (this.ConditionPars["from"].Length > 0)
            {
                stringBuilder.Append(" from " + this.ConditionPars["from"].ToString());
            }
            if (this.ConditionPars["where"].Length > 0)
            {
                stringBuilder.Append(" where " + this.ConditionPars["where"].ToString().TrimEnd(new char[]
				{
					','
				}));
            }
            if (this.ConditionPars["orderby"].Length > 0)
            {
                stringBuilder.Append(" order by  " + this.ConditionPars["orderby"].ToString().TrimEnd(new char[]
				{
					','
				}));
            }
            if (this.ConditionPars["top"].Length > 0)
            {
                stringBuilder.Append(" top " + this.ConditionPars["top"].ToString().Trim(new char[]
				{
					','
				}));
            }
            return stringBuilder.ToString();
        }
        private void BuildSelect(Expression expression)
        {
            MethodCallExpression methodCallExpression = (MethodCallExpression)expression;
            if (this.ConditionPars["select"].Length == 0)
            {
                if (methodCallExpression.Method.Name == "Count")
                {
                    this.ConditionPars["orderby"] = new StringBuilder();
                    this.ConditionPars["select"] = new StringBuilder("count(*)");
                }
                else
                {
                    StringBuilder stringBuilder = new StringBuilder();
                    Type type = methodCallExpression.Method.ReturnType;
                    if (type.Name == "IQueryable`1")
                    {
                        type = type.GetGenericArguments()[0];
                    }
                    PropertyInfo[] propertys = ConvertHelper.GetPropertys(type);
                    PropertyInfo[] array = propertys;
                    for (int i = 0; i < array.Length; i++)
                    {
                        PropertyInfo propertyInfo = array[i];
                        stringBuilder.Append(propertyInfo.Name + ",");
                    }
                    this.ConditionPars["select"] = stringBuilder;
                }
            }
            if (this.ConditionPars["from"].Length == 0)
            {
                this.ConditionPars["from"] = new StringBuilder(this.t.Name);
            }
        }
        protected override Expression VisitBinary(BinaryExpression b)
        {
            if (b == null)
            {
                return b;
            }
            ExpressionType nodeType = b.NodeType;
            string str;
            if (nodeType <= ExpressionType.LessThanOrEqual)
            {
                if (nodeType == ExpressionType.Add)
                {
                    str = "+";
                    goto IL_F1;
                }
                if (nodeType == ExpressionType.AndAlso)
                {
                    str = "AND";
                    goto IL_F1;
                }
                switch (nodeType)
                {
                    case ExpressionType.Divide:
                        str = "/";
                        goto IL_F1;
                    case ExpressionType.Equal:
                        if (((System.Linq.Expressions.ConstantExpression)b.Right).Value == null)
                        {
                            str = "is  null";
                        }
                        else
                        {
                            str = "=";
                        }
                        goto IL_F1;
                    case ExpressionType.GreaterThan:
                        str = ">";
                        goto IL_F1;
                    case ExpressionType.GreaterThanOrEqual:
                        str = ">=";
                        goto IL_F1;
                    case ExpressionType.LessThan:
                        str = "<";
                        goto IL_F1;
                    case ExpressionType.LessThanOrEqual:
                        str = "<=";
                        goto IL_F1;
                }
            }
            else
            {
                if (nodeType == ExpressionType.Multiply)
                {
                    str = "*";
                    goto IL_F1;
                }
                switch (nodeType)
                {
                    case ExpressionType.NotEqual:
                        if (((System.Linq.Expressions.ConstantExpression)b.Right).Value == null)
                        {
                            str = "is not null";
                        }
                        else
                        {
                            str = "<>";
                        }
                        goto IL_F1;
                    case ExpressionType.Or:
                        break;
                    case ExpressionType.OrElse:
                        str = "OR";
                        goto IL_F1;
                    default:
                        if (nodeType == ExpressionType.Subtract)
                        {
                            str = "-";
                            goto IL_F1;
                        }
                        break;
                }
            }
            throw new NotSupportedException(b.NodeType + "is not supported.");
        IL_F1:
            this.ConditionPars["where"].Append("(");
            this.Visit(b.Left);
            this.ConditionPars["where"].Append(" " + str + " ");
            this.Visit(b.Right);
            this.ConditionPars["where"].Append(")");
            return b;
        }
        protected override Expression VisitConstant(ConstantExpression c)
        {
            if (c == null)
            {
                return c;
            }
            string name;
            if (c.Value != null && (name = c.Value.GetType().Name) != null && (name == "String[]" || name == "Int64[]" || name == "Int32[]" || name == "List`1"))
            {
                IEnumerable enumerable = c.Value as IEnumerable;
                StringBuilder stringBuilder = new StringBuilder();
                foreach (object current in enumerable)
                {
                    this.ParIndex++;
                    string text = "@in" + this.ParIndex.ToString();
                    stringBuilder.Append(text + ",");
                    this.Arguments.Add(new SqlParameter(text, current));
                }
                this.ConditionPars["where"].Append(stringBuilder.ToString().TrimEnd(new char[]
				{
					','
				}));
            }
            else if (c.Value == null)
            {

            }
            else
            {
                this.Arguments.Last<SqlParameter>().Value = c.Value;
                if (this.ConditionPars["where"].ToString().TrimEnd().EndsWith("like"))
                {
                    this.ConditionPars["where"].Append("'%'+" + this.Arguments.Last<SqlParameter>().ParameterName + "+'%'");
                }
                else
                {
                    this.ConditionPars["where"].Append(this.Arguments.Last<SqlParameter>().ParameterName);
                }
            }
            return c;
        }
        protected override Expression VisitMemberAccess(MemberExpression m)
        {
            if (m == null)
            {
                return m;
            }
            PropertyInfo propertyInfo = m.Member as PropertyInfo;
            if (propertyInfo == null)
            {
                return m;
            }
            this.ConditionPars["where"].Append(propertyInfo.Name);
            string text = "@" + propertyInfo.Name;
            text = this.GetParName(text);
            SqlParameter item = new SqlParameter(text, TypeConvert.ConvertDbType(propertyInfo.PropertyType.Name));
            this.Arguments.Add(item);
            return m;
        }
        private string GetParName(string parName)
        {
            foreach (SqlParameter current in this.Arguments)
            {
                if (current.ParameterName == parName)
                {
                    parName += this.ParIndex;
                    this.ParIndex++;
                    break;
                }
            }
            return parName;
        }
    }
}
