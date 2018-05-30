using System;
using System.Reflection;
using System.Runtime.CompilerServices;
namespace AutoManage.Sql
{
    internal class AnonymouseHelper
    {
        public static object CloneAnonyousObject(object obj)
        {
            Type type = obj.GetType();
            ParameterInfo[] parameters = type.GetConstructors()[0].GetParameters();
            object[] array = new object[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                PropertyInfo property = type.GetProperty(parameters[i].Name);
                object value = property.GetValue(obj, null);
                array[i] = AnonymouseHelper.Clone(value);
            }
            return Activator.CreateInstance(type, array);
        }
        public static object Clone(object obj)
        {
            if (obj == null)
            {
                return null;
            }
            if (AnonymouseHelper.IsAnonyousType(obj.GetType()))
            {
                return AnonymouseHelper.CloneAnonyousObject(obj);
            }
            return AnonymouseHelper.CloneAnonyousObject(obj);
        }
        public static bool IsAnonyousType(Type type)
        {
            if (!type.IsGenericType)
            {
                return false;
            }
            TypeAttributes arg_10_0 = type.Attributes;
            return !false && Attribute.IsDefined(type, typeof(CompilerGeneratedAttribute), false) && type.Name.Contains("AnonymousType");
        }
    }
}
