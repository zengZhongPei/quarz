using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
namespace AutoManage.Sql
{
    internal class QueryableData<T> : MyQueryable<T>, IOrderedQueryable<T>, IQueryable<T>, IEnumerable<T>, IOrderedQueryable, IQueryable, IEnumerable
    {
        public QueryableData()
        {
            base.Expression = Expression.Constant(this);
        }
        public QueryableData(QueryProvider provider, Expression expression)
        {
            if (provider == null)
            {
                throw new ArgumentNullException("provider");
            }
            if (expression == null)
            {
                throw new ArgumentNullException("expression");
            }
            if (!typeof(IQueryable<T>).IsAssignableFrom(expression.Type))
            {
                throw new ArgumentOutOfRangeException("expression");
            }
            base.Provider = provider;
            base.Expression = expression;
        }
        public override IEnumerator<T> GetEnumerator()
        {
            return base.Provider.Execute<IEnumerable<T>>(base.Expression).GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return base.Provider.Execute<IEnumerable>(base.Expression).GetEnumerator();
        }
    }
}
