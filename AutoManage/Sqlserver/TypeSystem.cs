using System;
using System.Collections.Generic;
namespace AutoManage.Sql
{
    internal static class TypeSystem
    {
        internal static Type GetElementType(Type seqType)
        {
            Type type = TypeSystem.FindIEnumerable(seqType);
            if (type == null)
            {
                return seqType;
            }
            return type.GetGenericArguments()[0];
        }
        private static Type FindIEnumerable(Type seqType)
        {
            if (seqType == null || seqType == typeof(string))
            {
                return null;
            }
            if (seqType.IsArray)
            {
                return typeof(IEnumerable<>).MakeGenericType(new Type[]
				{
					seqType.GetElementType()
				});
            }
            if (seqType.IsGenericType)
            {
                Type[] genericArguments = seqType.GetGenericArguments();
                for (int i = 0; i < genericArguments.Length; i++)
                {
                    Type type = genericArguments[i];
                    Type type2 = typeof(IEnumerable<>).MakeGenericType(new Type[]
					{
						type
					});
                    if (type2.IsAssignableFrom(seqType))
                    {
                        Type result = type2;
                        return result;
                    }
                }
            }
            Type[] interfaces = seqType.GetInterfaces();
            if (interfaces != null && interfaces.Length > 0)
            {
                Type[] array = interfaces;
                for (int j = 0; j < array.Length; j++)
                {
                    Type seqType2 = array[j];
                    Type type3 = TypeSystem.FindIEnumerable(seqType2);
                    if (type3 != null)
                    {
                        Type result = type3;
                        return result;
                    }
                }
            }
            if (seqType.BaseType != null && seqType.BaseType != typeof(object))
            {
                return TypeSystem.FindIEnumerable(seqType.BaseType);
            }
            return null;
        }
    }
}
