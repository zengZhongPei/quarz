using System;
using System.Collections.Generic;
using System.Linq.Expressions;
namespace AutoManage.Sql
{
    internal class PartialEvaluator : ExpressionVisitor
    {
        private class Nominator : ExpressionVisitor
        {
            private Func<Expression, bool> m_fnCanBeEvaluated;
            private HashSet<Expression> m_candidates;
            private bool m_cannotBeEvaluated;
            internal Nominator(Func<Expression, bool> fnCanBeEvaluated)
            {
                this.m_fnCanBeEvaluated = fnCanBeEvaluated;
            }
            internal HashSet<Expression> Nominate(Expression expression)
            {
                this.m_candidates = new HashSet<Expression>();
                this.Visit(expression);
                return this.m_candidates;
            }
            protected override Expression Visit(Expression expression)
            {
                if (expression != null)
                {
                    bool cannotBeEvaluated = this.m_cannotBeEvaluated;
                    this.m_cannotBeEvaluated = false;
                    base.Visit(expression);
                    if (!this.m_cannotBeEvaluated)
                    {
                        if (this.m_fnCanBeEvaluated(expression))
                        {
                            this.m_candidates.Add(expression);
                        }
                        else
                        {
                            this.m_cannotBeEvaluated = true;
                        }
                    }
                    this.m_cannotBeEvaluated |= cannotBeEvaluated;
                }
                return expression;
            }
        }
        private Func<Expression, bool> m_fnCanBeEvaluated;
        private HashSet<Expression> m_candidates;
        public PartialEvaluator()
            : this(new Func<Expression, bool>(PartialEvaluator.CanBeEvaluatedLocally))
        {
        }
        public PartialEvaluator(Func<Expression, bool> fnCanBeEvaluated)
        {
            this.m_fnCanBeEvaluated = fnCanBeEvaluated;
        }
        public Expression Eval(Expression exp)
        {
            this.m_candidates = new PartialEvaluator.Nominator(this.m_fnCanBeEvaluated).Nominate(exp);
            return this.Visit(exp);
        }
        protected override Expression Visit(Expression exp)
        {
            if (exp == null)
            {
                return null;
            }
            if (this.m_candidates.Contains(exp))
            {
                return this.Evaluate(exp);
            }
            return base.Visit(exp);
        }
        private Expression Evaluate(Expression e)
        {
            if (e.NodeType == ExpressionType.Constant)
            {
                return e;
            }
            LambdaExpression lambdaExpression = Expression.Lambda(e, new ParameterExpression[0]);
            Delegate @delegate = lambdaExpression.Compile();
            return Expression.Constant(@delegate.DynamicInvoke(null), e.Type);
        }
        private static bool CanBeEvaluatedLocally(Expression exp)
        {
            return exp.NodeType != ExpressionType.Parameter;
        }
    }
}
