using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
namespace AutoManage.Sql
{
    internal class DynamicMethod : IMemberAccessor
    {
        private static Dictionary<Type, IMemberAccessor> classAccessors = new Dictionary<Type, IMemberAccessor>();
        public object GetValue(object instance, string memberName)
        {
            return this.FindClassAccessor(instance).GetValue(instance, memberName);
        }
        public void SetValue(object instance, string memberName, object newValue)
        {
            this.FindClassAccessor(instance).SetValue(instance, memberName, newValue);
        }
        private IMemberAccessor FindClassAccessor(object instance)
        {
            Type type = instance.GetType();
            IMemberAccessor memberAccessor;
            DynamicMethod.classAccessors.TryGetValue(type, out memberAccessor);
            if (memberAccessor == null)
            {
                memberAccessor = (Activator.CreateInstance(typeof(DynamicMethod<>).MakeGenericType(new Type[]
				{
					instance.GetType()
				})) as IMemberAccessor);
                DynamicMethod.classAccessors.Add(type, memberAccessor);
            }
            return memberAccessor;
        }
    }

    internal class DynamicMethod<T> : IMemberAccessor
    {
        internal static Func<object, string, object> GetValueDelegate;
        internal static Action<object, string, object> SetValueDelegate;
        public object GetValue(T instance, string memberName)
        {
            return DynamicMethod<T>.GetValueDelegate(instance, memberName);
        }
        public void SetValue(T instance, string memberName, object newValue)
        {
            DynamicMethod<T>.SetValueDelegate(instance, memberName, newValue);
        }
        public object GetValue(object instance, string memberName)
        {
            return DynamicMethod<T>.GetValueDelegate(instance, memberName);
        }
        public void SetValue(object instance, string memberName, object newValue)
        {
            DynamicMethod<T>.SetValueDelegate(instance, memberName, newValue);
        }
        static DynamicMethod()
        {
            DynamicMethod<T>.GetValueDelegate = DynamicMethod<T>.GenerateGetValue();
            DynamicMethod<T>.SetValueDelegate = DynamicMethod<T>.GenerateSetValue();
        }
        private static Func<object, string, object> GenerateGetValue()
        {
            Type typeFromHandle = typeof(T);
            ParameterExpression parameterExpression = Expression.Parameter(typeof(object), "instance");
            ParameterExpression parameterExpression2 = Expression.Parameter(typeof(string), "memberName");
            ParameterExpression parameterExpression3 = Expression.Variable(typeof(int), "nameHash");
            BinaryExpression binaryExpression = Expression.Assign(parameterExpression3, Expression.Call(parameterExpression2, typeof(object).GetMethod("GetHashCode")));
            List<SwitchCase> list = new List<SwitchCase>();
            PropertyInfo[] properties = typeFromHandle.GetProperties();
            for (int i = 0; i < properties.Length; i++)
            {
                PropertyInfo propertyInfo = properties[i];
                MemberExpression expression = Expression.Property(Expression.Convert(parameterExpression, typeof(T)), propertyInfo.Name);
                ConstantExpression constantExpression = Expression.Constant(propertyInfo.Name.GetHashCode(), typeof(int));
                list.Add(Expression.SwitchCase(Expression.Convert(expression, typeof(object)), new Expression[]
				{
					constantExpression
				}));
            }
            SwitchExpression switchExpression = Expression.Switch(parameterExpression3, Expression.Constant(null), list.ToArray());
            BlockExpression body = Expression.Block(typeof(object), new ParameterExpression[]
			{
				parameterExpression3
			}, new Expression[]
			{
				binaryExpression,
				switchExpression
			});
            return Expression.Lambda<Func<object, string, object>>(body, new ParameterExpression[]
			{
				parameterExpression,
				parameterExpression2
			}).Compile();
        }
        private static Action<object, string, object> GenerateSetValue()
        {
            Type typeFromHandle = typeof(T);
            ParameterExpression parameterExpression = Expression.Parameter(typeof(object), "instance");
            ParameterExpression parameterExpression2 = Expression.Parameter(typeof(string), "memberName");
            ParameterExpression parameterExpression3 = Expression.Parameter(typeof(object), "newValue");
            ParameterExpression parameterExpression4 = Expression.Variable(typeof(int), "nameHash");
            BinaryExpression binaryExpression = Expression.Assign(parameterExpression4, Expression.Call(parameterExpression2, typeof(object).GetMethod("GetHashCode")));
            List<SwitchCase> list = new List<SwitchCase>();
            PropertyInfo[] properties = typeFromHandle.GetProperties();
            for (int i = 0; i < properties.Length; i++)
            {
                PropertyInfo propertyInfo = properties[i];
                MemberExpression left = Expression.Property(Expression.Convert(parameterExpression, typeof(T)), propertyInfo.Name);
                BinaryExpression expression = Expression.Assign(left, Expression.Convert(parameterExpression3, propertyInfo.PropertyType));
                ConstantExpression constantExpression = Expression.Constant(propertyInfo.Name.GetHashCode(), typeof(int));
                list.Add(Expression.SwitchCase(Expression.Convert(expression, typeof(object)), new Expression[]
				{
					constantExpression
				}));
            }
            SwitchExpression switchExpression = Expression.Switch(parameterExpression4, Expression.Constant(null), list.ToArray());
            BlockExpression body = Expression.Block(typeof(object), new ParameterExpression[]
			{
				parameterExpression4
			}, new Expression[]
			{
				binaryExpression,
				switchExpression
			});
            return Expression.Lambda<Action<object, string, object>>(body, new ParameterExpression[]
			{
				parameterExpression,
				parameterExpression2,
				parameterExpression3
			}).Compile();
        }
    }
}
