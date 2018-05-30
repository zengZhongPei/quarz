using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
namespace AutoManage.Sql
{
    internal class MyQueryable<T> : IQueryable<T>, IEnumerable<T>, IQueryable, IEnumerable
    {
        public Expression expression;
        public Type ElementType
        {
            get
            {
                return typeof(T);
            }
        }
        public Expression Expression
        {
            get
            {
                return this.expression;
            }
            set
            {
                this.expression = value;
            }
        }
        public IQueryProvider Provider
        {
            get;
            set;
        }
        public virtual IEnumerator<T> GetEnumerator()
        {
            throw new NotImplementedException();
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException();
        }
    }
}
