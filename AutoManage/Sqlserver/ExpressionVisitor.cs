using System.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
namespace AutoManage.Sql
{
    internal abstract class ExpressionVisitor
    {
        static Dictionary<string, int> _sqlKeywords;
        public Dictionary<string, StringBuilder> ConditionPars;
        public List<SqlParameter> Arguments;
        protected virtual Expression Visit(Expression exp)
        {
            if (exp == null)
            {
                return exp;
            }
            switch (exp.NodeType)
            {
                case ExpressionType.Add:
                case ExpressionType.AddChecked:
                case ExpressionType.And:
                case ExpressionType.AndAlso:
                case ExpressionType.ArrayIndex:
                case ExpressionType.Coalesce:
                case ExpressionType.Divide:
                case ExpressionType.Equal:
                case ExpressionType.ExclusiveOr:
                case ExpressionType.GreaterThan:
                case ExpressionType.GreaterThanOrEqual:
                case ExpressionType.LeftShift:
                case ExpressionType.LessThan:
                case ExpressionType.LessThanOrEqual:
                case ExpressionType.Modulo:
                case ExpressionType.Multiply:
                case ExpressionType.MultiplyChecked:
                case ExpressionType.NotEqual:
                case ExpressionType.Or:
                case ExpressionType.OrElse:
                case ExpressionType.RightShift:
                case ExpressionType.Subtract:
                case ExpressionType.SubtractChecked:
                    return this.VisitBinary((BinaryExpression)exp);
                case ExpressionType.ArrayLength:
                case ExpressionType.Convert:
                case ExpressionType.ConvertChecked:
                case ExpressionType.Negate:
                case ExpressionType.NegateChecked:
                case ExpressionType.Not:
                case ExpressionType.Quote:
                case ExpressionType.TypeAs:
                    return this.VisitUnary((UnaryExpression)exp);
                case ExpressionType.Call:
                    return this.VisitMethodCall((MethodCallExpression)exp);
                case ExpressionType.Conditional:
                    return this.VisitConditional((ConditionalExpression)exp);
                case ExpressionType.Constant:
                    return this.VisitConstant((ConstantExpression)exp);
                case ExpressionType.Invoke:
                    return this.VisitInvocation((InvocationExpression)exp);
                case ExpressionType.Lambda:
                    return this.VisitLambda((LambdaExpression)exp);
                case ExpressionType.ListInit:
                    return this.VisitListInit((ListInitExpression)exp);
                case ExpressionType.MemberAccess:
                    return this.VisitMemberAccess((MemberExpression)exp);
                case ExpressionType.MemberInit:
                    return this.VisitMemberInit((MemberInitExpression)exp);
                case ExpressionType.New:
                    return this.VisitNew((NewExpression)exp);
                case ExpressionType.NewArrayInit:
                case ExpressionType.NewArrayBounds:
                    return this.VisitNewArray((NewArrayExpression)exp);
                case ExpressionType.Parameter:
                    return this.VisitParameter((ParameterExpression)exp);
                case ExpressionType.TypeIs:
                    return this.VisitTypeIs((TypeBinaryExpression)exp);
            }
            throw new Exception(string.Format("Unhandled expression type: '{0}'", exp.NodeType));
        }
        protected virtual MemberBinding VisitBinding(MemberBinding binding)
        {
            switch (binding.BindingType)
            {
                case MemberBindingType.Assignment:
                    return this.VisitMemberAssignment((MemberAssignment)binding);
                case MemberBindingType.MemberBinding:
                    return this.VisitMemberMemberBinding((MemberMemberBinding)binding);
                case MemberBindingType.ListBinding:
                    return this.VisitMemberListBinding((MemberListBinding)binding);
                default:
                    throw new Exception(string.Format("Unhandled binding type '{0}'", binding.BindingType));
            }
        }
        protected virtual ElementInit VisitElementInitializer(ElementInit initializer)
        {
            ReadOnlyCollection<Expression> readOnlyCollection = this.VisitExpressionList(initializer.Arguments);
            if (readOnlyCollection != initializer.Arguments)
            {
                return Expression.ElementInit(initializer.AddMethod, readOnlyCollection);
            }
            return initializer;
        }
        protected virtual Expression VisitUnary(UnaryExpression u)
        {
            Expression expression = this.Visit(u.Operand);
            if (expression != u.Operand)
            {
                return Expression.MakeUnary(u.NodeType, expression, u.Type, u.Method);
            }
            return u;
        }
        protected virtual Expression VisitBinary(BinaryExpression b)
        {
            Expression expression = this.Visit(b.Left);
            Expression expression2 = this.Visit(b.Right);
            Expression expression3 = this.Visit(b.Conversion);
            if (expression == b.Left && expression2 == b.Right && expression3 == b.Conversion)
            {
                return b;
            }
            if (b.NodeType == ExpressionType.Coalesce && b.Conversion != null)
            {
                return Expression.Coalesce(expression, expression2, expression3 as LambdaExpression);
            }
            return Expression.MakeBinary(b.NodeType, expression, expression2, b.IsLiftedToNull, b.Method);
        }
        protected virtual Expression VisitTypeIs(TypeBinaryExpression b)
        {
            Expression expression = this.Visit(b.Expression);
            if (expression != b.Expression)
            {
                return Expression.TypeIs(expression, b.TypeOperand);
            }
            return b;
        }
        protected virtual Expression VisitConstant(ConstantExpression c)
        {
            return c;
        }
        protected virtual Expression VisitConditional(ConditionalExpression c)
        {
            Expression expression = this.Visit(c.Test);
            Expression expression2 = this.Visit(c.IfTrue);
            Expression expression3 = this.Visit(c.IfFalse);
            if (expression != c.Test || expression2 != c.IfTrue || expression3 != c.IfFalse)
            {
                return Expression.Condition(expression, expression2, expression3);
            }
            return c;
        }
        protected virtual Expression VisitParameter(ParameterExpression p)
        {
            return p;
        }
        protected virtual Expression VisitMemberAccess(MemberExpression m)
        {
            Expression expression = this.Visit(m.Expression);
            if (expression != m.Expression)
            {
                return Expression.MakeMemberAccess(expression, m.Member);
            }
            return m;
        }
        protected virtual Expression VisitMethodCall(MethodCallExpression m)
        {
            if (this.ConditionPars == null)
            {
                Expression expression = this.Visit(m.Object);
                IEnumerable<Expression> enumerable = this.VisitExpressionList(m.Arguments);
                if (expression != m.Object || enumerable != m.Arguments)
                {
                    return Expression.Call(expression, m.Method, enumerable);
                }
            }
            else
            {
                if (m == null)
                {
                    return m;
                }
                string arg_4F_0 = string.Empty;
                string value = string.Empty;
                string name;
                if ((name = m.Method.Name) != null)
                {
                    if (_sqlKeywords == null)
                    {
                        _sqlKeywords = new Dictionary<string, int>(12)
						{

							{
								"Contains",
								0
							},

							{
								"FirstOrDefault",
								1
							},

							{
								"First",
								2
							},

							{
								"Select",
								3
							},

							{
								"Count",
								4
							},

							{
								"Skip",
								5
							},

							{
								"Take",
								6
							},

							{
								"OrderBy",
								7
							},

							{
								"OrderByDescending",
								8
							},

							{
								"Where",
								9
							},

							{
								"WhereAnd",
								10
							},

							{
								"WhereOr",
								11
							}
						};
                    }
                    int num;
                    if (_sqlKeywords.TryGetValue(name, out num))
                    {
                        switch (num)
                        {
                            case 0:
                                {
                                    string name2;
                                    if ((name2 = m.Arguments[0].Type.Name) != null && (name2 == "String[]" || name2 == "Int64[]" || name2 == "Int32[]" || name2 == "List`1"))
                                    {
                                        this.Visit(m.Arguments[1]);
                                        this.ConditionPars["where"].Append(" in (");
                                        this.Visit(m.Arguments[0]);
                                        this.ConditionPars["where"].Append(") ");
                                        return m;
                                    }
                                    this.Visit(m.Object);
                                    this.ConditionPars["where"].Append(" like ");
                                    this.Visit(m.Arguments[0]);
                                    return m;
                                }
                            case 1:
                            case 2:
                                foreach (Expression current in m.Arguments)
                                {
                                    if (current.NodeType.Equals(ExpressionType.Call))
                                    {
                                        this.Visit(current);
                                    }
                                }
                                this.ConditionPars["top"] = new StringBuilder("1");
                                return m;
                            case 3:
                                {
                                    foreach (Expression current2 in m.Arguments)
                                    {
                                        if (current2.NodeType.Equals(ExpressionType.Call))
                                        {
                                            this.Visit(current2);
                                        }
                                    }
                                    UnaryExpression unaryExpression = (UnaryExpression)m.Arguments[1];
                                    LambdaExpression lambdaExpression = (LambdaExpression)unaryExpression.Operand;
                                    StringBuilder stringBuilder = new StringBuilder();
                                    if (lambdaExpression.Body.Type.Equals(ExpressionType.New))
                                    {
                                        NewExpression newExpression = (NewExpression)lambdaExpression.Body;
                                        using (IEnumerator<MemberInfo> enumerator3 = newExpression.Members.GetEnumerator())
                                        {
                                            while (enumerator3.MoveNext())
                                            {
                                                MemberInfo current3 = enumerator3.Current;
                                                stringBuilder.Append(current3.Name + ",");
                                            }
                                            goto IL_3FF;
                                        }
                                    }
                                    PropertyInfo[] propertys = ConvertHelper.GetPropertys(lambdaExpression.Body.Type);
                                    PropertyInfo[] array = propertys;
                                    for (int i = 0; i < array.Length; i++)
                                    {
                                        PropertyInfo propertyInfo = array[i];
                                        stringBuilder.Append(propertyInfo.Name + ",");
                                    }
                                IL_3FF:
                                    stringBuilder.Remove(stringBuilder.Length - 1, 1);
                                    this.ConditionPars["select"] = stringBuilder;
                                    this.ConditionPars["from"].Append(lambdaExpression.Type.GetGenericArguments()[0].Name);
                                    return m;
                                }
                            case 4:
                                foreach (Expression current4 in m.Arguments)
                                {
                                    if (current4.NodeType.Equals(ExpressionType.Call))
                                    {
                                        this.Visit(current4);
                                    }
                                }
                                this.ConditionPars["select"] = new StringBuilder("count(*)");
                                this.ConditionPars["orderby"] = new StringBuilder();
                                return m;
                            case 5:
                                foreach (Expression current5 in m.Arguments)
                                {
                                    if (current5.NodeType.Equals(ExpressionType.Call))
                                    {
                                        this.Visit(current5);
                                    }
                                }
                                this.ConditionPars["top"].Append(m.Arguments[1].ToString());
                                return m;
                            case 6:
                                foreach (Expression current6 in m.Arguments)
                                {
                                    if (current6.NodeType.Equals(ExpressionType.Call))
                                    {
                                        this.Visit(current6);
                                    }
                                }
                                this.ConditionPars["top"].Append(m.Arguments[1].ToString());
                                return m;
                            case 7:
                                foreach (Expression current7 in m.Arguments)
                                {
                                    if (current7.NodeType.Equals(ExpressionType.Call))
                                    {
                                        this.Visit(current7);
                                    }
                                }
                                value = this.GetMemName(m) + ",";
                                this.ConditionPars["orderby"].Append(value);
                                return m;
                            case 8:
                                foreach (Expression current8 in m.Arguments)
                                {
                                    if (current8.NodeType.Equals(ExpressionType.Call))
                                    {
                                        this.Visit(current8);
                                    }
                                }
                                value = this.GetMemName(m) + " desc ,";
                                this.ConditionPars["orderby"].Append(value);
                                return m;
                            case 9:
                                using (IEnumerator<Expression> enumerator9 = m.Arguments.GetEnumerator())
                                {
                                    while (enumerator9.MoveNext())
                                    {
                                        Expression current9 = enumerator9.Current;
                                        if (current9.NodeType.Equals(ExpressionType.Quote) || current9.NodeType.Equals(ExpressionType.Call))
                                        {
                                            this.Visit(current9);
                                        }
                                    }
                                    return m;
                                }
                                break;
                            case 10:
                                break;
                            case 11:
                                goto IL_7D8;
                            default:
                                goto IL_867;
                        }
                        using (IEnumerator<Expression> enumerator10 = m.Arguments.GetEnumerator())
                        {
                            while (enumerator10.MoveNext())
                            {
                                Expression current10 = enumerator10.Current;
                                if (current10.NodeType.Equals(ExpressionType.Quote))
                                {
                                    this.Visit(current10);
                                }
                                if (current10.NodeType.Equals(ExpressionType.Call))
                                {
                                    this.Visit(current10);
                                    this.ConditionPars["where"].Append(" and ");
                                }
                            }
                            return m;
                        }
                    IL_7D8:
                        using (IEnumerator<Expression> enumerator11 = m.Arguments.GetEnumerator())
                        {
                            while (enumerator11.MoveNext())
                            {
                                Expression current11 = enumerator11.Current;
                                if (current11.NodeType.Equals(ExpressionType.Quote))
                                {
                                    this.Visit(current11);
                                }
                                if (current11.NodeType.Equals(ExpressionType.Call))
                                {
                                    this.Visit(current11);
                                    this.ConditionPars["where"].Append(" or ");
                                }
                            }
                            return m;
                        }
                    }
                }
            IL_867:
                throw new NotSupportedException(m.NodeType + " is not supported!");
            }
            return m;
        }
        private string GetMemName(MethodCallExpression m)
        {
            UnaryExpression unaryExpression = (UnaryExpression)m.Arguments[1];
            LambdaExpression lambdaExpression = (LambdaExpression)unaryExpression.Operand;
            MemberExpression memberExpression = (MemberExpression)lambdaExpression.Body;
            return memberExpression.Member.Name;
        }
        protected virtual ReadOnlyCollection<Expression> VisitExpressionList(ReadOnlyCollection<Expression> original)
        {
            List<Expression> list = null;
            int i = 0;
            int count = original.Count;
            while (i < count)
            {
                Expression expression = this.Visit(original[i]);
                if (list != null)
                {
                    list.Add(expression);
                }
                else
                {
                    if (expression != original[i])
                    {
                        list = new List<Expression>(count);
                        for (int j = 0; j < i; j++)
                        {
                            list.Add(original[j]);
                        }
                        list.Add(expression);
                    }
                }
                i++;
            }
            if (list != null)
            {
                return list.AsReadOnly();
            }
            return original;
        }
        protected virtual MemberAssignment VisitMemberAssignment(MemberAssignment assignment)
        {
            Expression expression = this.Visit(assignment.Expression);
            if (expression != assignment.Expression)
            {
                return Expression.Bind(assignment.Member, expression);
            }
            return assignment;
        }
        protected virtual MemberMemberBinding VisitMemberMemberBinding(MemberMemberBinding binding)
        {
            IEnumerable<MemberBinding> enumerable = this.VisitBindingList(binding.Bindings);
            if (enumerable != binding.Bindings)
            {
                return Expression.MemberBind(binding.Member, enumerable);
            }
            return binding;
        }
        protected virtual MemberListBinding VisitMemberListBinding(MemberListBinding binding)
        {
            IEnumerable<ElementInit> enumerable = this.VisitElementInitializerList(binding.Initializers);
            if (enumerable != binding.Initializers)
            {
                return Expression.ListBind(binding.Member, enumerable);
            }
            return binding;
        }
        protected virtual IEnumerable<MemberBinding> VisitBindingList(ReadOnlyCollection<MemberBinding> original)
        {
            List<MemberBinding> list = null;
            int i = 0;
            int count = original.Count;
            while (i < count)
            {
                MemberBinding memberBinding = this.VisitBinding(original[i]);
                if (list != null)
                {
                    list.Add(memberBinding);
                }
                else
                {
                    if (memberBinding != original[i])
                    {
                        list = new List<MemberBinding>(count);
                        for (int j = 0; j < i; j++)
                        {
                            list.Add(original[j]);
                        }
                        list.Add(memberBinding);
                    }
                }
                i++;
            }
            if (list != null)
            {
                return list;
            }
            return original;
        }
        protected virtual IEnumerable<ElementInit> VisitElementInitializerList(ReadOnlyCollection<ElementInit> original)
        {
            List<ElementInit> list = null;
            int i = 0;
            int count = original.Count;
            while (i < count)
            {
                ElementInit elementInit = this.VisitElementInitializer(original[i]);
                if (list != null)
                {
                    list.Add(elementInit);
                }
                else
                {
                    if (elementInit != original[i])
                    {
                        list = new List<ElementInit>(count);
                        for (int j = 0; j < i; j++)
                        {
                            list.Add(original[j]);
                        }
                        list.Add(elementInit);
                    }
                }
                i++;
            }
            if (list != null)
            {
                return list;
            }
            return original;
        }
        protected virtual Expression VisitLambda(LambdaExpression lambda)
        {
            Expression expression = this.Visit(lambda.Body);
            if (expression != lambda.Body)
            {
                return Expression.Lambda(lambda.Type, expression, lambda.Parameters);
            }
            return lambda;
        }
        protected virtual NewExpression VisitNew(NewExpression nex)
        {
            IEnumerable<Expression> enumerable = this.VisitExpressionList(nex.Arguments);
            if (enumerable == nex.Arguments)
            {
                return nex;
            }
            if (nex.Members != null)
            {
                return Expression.New(nex.Constructor, enumerable, nex.Members);
            }
            return Expression.New(nex.Constructor, enumerable);
        }
        protected virtual Expression VisitMemberInit(MemberInitExpression init)
        {
            NewExpression newExpression = this.VisitNew(init.NewExpression);
            IEnumerable<MemberBinding> enumerable = this.VisitBindingList(init.Bindings);
            if (newExpression != init.NewExpression || enumerable != init.Bindings)
            {
                return Expression.MemberInit(newExpression, enumerable);
            }
            return init;
        }
        protected virtual Expression VisitListInit(ListInitExpression init)
        {
            NewExpression newExpression = this.VisitNew(init.NewExpression);
            IEnumerable<ElementInit> enumerable = this.VisitElementInitializerList(init.Initializers);
            if (newExpression != init.NewExpression || enumerable != init.Initializers)
            {
                return Expression.ListInit(newExpression, enumerable);
            }
            return init;
        }
        protected virtual Expression VisitNewArray(NewArrayExpression na)
        {
            IEnumerable<Expression> enumerable = this.VisitExpressionList(na.Expressions);
            if (enumerable == na.Expressions)
            {
                return na;
            }
            if (na.NodeType == ExpressionType.NewArrayInit)
            {
                return Expression.NewArrayInit(na.Type.GetElementType(), enumerable);
            }
            return Expression.NewArrayBounds(na.Type.GetElementType(), enumerable);
        }
        protected virtual Expression VisitInvocation(InvocationExpression iv)
        {
            IEnumerable<Expression> enumerable = this.VisitExpressionList(iv.Arguments);
            Expression expression = this.Visit(iv.Expression);
            if (enumerable != iv.Arguments || expression != iv.Expression)
            {
                return Expression.Invoke(expression, enumerable);
            }
            return iv;
        }
    }
}
