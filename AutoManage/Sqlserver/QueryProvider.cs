using System;
using System.Linq;
using System.Linq.Expressions;
namespace AutoManage.Sql
{
    internal class QueryProvider : IQueryProvider
    {
        private CallBackEvent.CallBackEventHandler CallBack;
        public QueryProvider(CallBackEvent.CallBackEventHandler callBack)
        {
            this.CallBack = callBack;
        }
        public IQueryable CreateQuery(Expression expression)
        {
            Type elementType = TypeSystem.GetElementType(expression.Type);
            IQueryable result;
            try
            {
                result = (IQueryable)Activator.CreateInstance(typeof(QueryableData<>).MakeGenericType(new Type[]
				{
					elementType
				}), new object[]
				{
					this,
					expression
				});
            }
            catch
            {
                throw new Exception();
            }
            return result;
        }
        public IQueryable<TResult> CreateQuery<TResult>(Expression expression)
        {
            return new QueryableData<TResult>(this, expression);
        }
        public object Execute(Expression expression)
        {
            return null;
        }
        public TResult Execute<TResult>(Expression expression)
        {
            return (TResult)((object)this.CallBack(expression, typeof(TResult)));
        }
        protected Expression VisitMethodCall(MethodCallExpression m)
        {
            return null;
        }
    }
}
