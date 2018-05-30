using System;
namespace AutoManage.Sql
{
    internal interface IMemberAccessor
    {
        object GetValue(object instance, string memberName);
        void SetValue(object instance, string memberName, object newValue);
    }
}
