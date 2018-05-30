using System;
using System.Linq.Expressions;
namespace AutoManage.Sql
{
    internal class CallBackEvent
    {
        public delegate object CallBackEventHandler(Expression expression, Type returnType);
        public event CallBackEvent.CallBackEventHandler CallBack;
    }
}
