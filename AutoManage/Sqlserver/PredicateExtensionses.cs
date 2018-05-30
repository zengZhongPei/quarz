using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace AutoManage.Sqlserver
{
    public static class PredicateExtensionses
    {
        public static Expression<Func<T, bool>> And<T>(this Expression<Func<T, bool>> exp_left, Expression<Func<T, bool>> exp_right)
        {
            ParameterExpression expression;
            ParameterReplacer replacer = new ParameterReplacer(expression = Expression.Parameter(typeof(T), "candidate"));
            Expression left = replacer.Replace(exp_left.Body);
            Expression right = replacer.Replace(exp_right.Body);
            return Expression.Lambda<Func<T, bool>>(Expression.AndAlso(left, right), new ParameterExpression[] { expression });
        }

        public static Expression<Func<T, bool>> False<T>()
        {
            return f => false;
        }

        public static Expression<Func<T, bool>> Or<T>(this Expression<Func<T, bool>> exp_left, Expression<Func<T, bool>> exp_right)
        {
            ParameterExpression expression;
            ParameterReplacer replacer = new ParameterReplacer(expression = Expression.Parameter(typeof(T), "candidate"));
            Expression left = replacer.Replace(exp_left.Body);
            Expression right = replacer.Replace(exp_right.Body);
            return Expression.Lambda<Func<T, bool>>(Expression.Or(left, right), new ParameterExpression[] { expression });
        }

        public static Expression<Func<T, bool>> True<T>(Expression<Func<T, bool>> x)
        {
            return x;
        }
    }

    internal class ParameterReplacer : ExpressionVisitor
    {
        public ParameterReplacer(ParameterExpression paramExpr)
        {
            this.ParameterExpression = paramExpr;
        }

        public Expression Replace(Expression expr)
        {
            return this.Visit(expr);
        }

        protected override Expression VisitParameter(ParameterExpression p)
        {
            return this.ParameterExpression;
        }

        public System.Linq.Expressions.ParameterExpression ParameterExpression { get; private set; }
    }
}
