using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
namespace AutoManage.Sql
{
    internal class ConvertHelper
    {
        private static Dictionary<string, PropertyInfo[]> cache = new Dictionary<string, PropertyInfo[]>();
        public static PropertyInfo[] GetPropertys(Type EntityType)
        {
            PropertyInfo[] array = null;
            string fullName = EntityType.FullName;
            ConvertHelper.cache.TryGetValue(fullName, out array);
            if (array == null)
            {
                array = EntityType.GetProperties();
                List<PropertyInfo> list = new List<PropertyInfo>();
                for (int i = 0; i < array.Length; i++)
                {
                    if (array[i].Name != "Instance" && array[i].Name.ToLower() != "where")
                    {
                        list.Add(array[i]);
                    }
                }
                array = list.ToArray();
                ConvertHelper.cache.Add(fullName, array);
            }
            return array;
        }
        public static IList GetAnonymous(DataTable dataTable, Type GenericType)
        {
            Type typeFromHandle = typeof(List<>);
            Type type = typeFromHandle.MakeGenericType(new Type[]
			{
				GenericType
			});
            IList list = Activator.CreateInstance(type) as IList;
            if (dataTable == null || dataTable.Rows.Count == 0)
            {
                return list;
            }
            ConstructorInfo constructorInfo = (
                from c in GenericType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                orderby c.GetParameters().Length
                select c).First<ConstructorInfo>();
            ParameterInfo[] parameters = constructorInfo.GetParameters();
            object[] array = new object[parameters.Length];
            foreach (DataRow dataRow in dataTable.Rows)
            {
                int num = 0;
                ParameterInfo[] array2 = parameters;
                for (int i = 0; i < array2.Length; i++)
                {
                    ParameterInfo parameterInfo = array2[i];
                    object obj = null;
                    if (dataRow[parameterInfo.Name] != null && dataRow[parameterInfo.Name] != DBNull.Value)
                    {
                        obj = Convert.ChangeType(dataRow[parameterInfo.Name], parameterInfo.ParameterType);
                    }
                    array[num++] = obj;
                }
                list.Add(constructorInfo.Invoke(array));
            }
            return list;
        }
        public static IList GetList(DataTable dt, Type t)
        {
            Type typeFromHandle = typeof(List<>);
            Type type = typeFromHandle.MakeGenericType(new Type[]
			{
				t
			});
            IList list = Activator.CreateInstance(type) as IList;
            PropertyInfo[] propertys = ConvertHelper.GetPropertys(t);
            for (int i = 0; i < dt.Rows.Count; i++)
            {
                object obj = Activator.CreateInstance(t);
                PropertyInfo[] array = propertys;
                for (int j = 0; j < array.Length; j++)
                {
                    PropertyInfo propertyInfo = array[j];
                    object obj2 = dt.Rows[i][propertyInfo.Name];
                    if (obj2 != null && obj2 != DBNull.Value)
                    {
                        if (!propertyInfo.PropertyType.IsGenericType)
                        {
                            propertyInfo.SetValue(obj, Convert.ChangeType(obj2, propertyInfo.PropertyType), null);
                        }
                        else
                        {
                            Type genericTypeDefinition = propertyInfo.PropertyType.GetGenericTypeDefinition();
                            if (genericTypeDefinition == typeof(Nullable<>))
                            {
                                propertyInfo.SetValue(obj, Convert.ChangeType(obj2, Nullable.GetUnderlyingType(propertyInfo.PropertyType)), null);
                            }
                        }
                    }
                }
                list.Add(obj);
            }
            return list;
        }
        public static List<T> GetList<T>(DataTable dt) where T : new()
        {
            List<T> list = new List<T>();
            if (dt == null)
            {
                return list;
            }
            Type typeFromHandle = typeof(T);
            PropertyInfo[] propertys = ConvertHelper.GetPropertys(typeFromHandle);
            for (int i = 0; i < dt.Rows.Count; i++)
            {
                object obj = Activator.CreateInstance(typeFromHandle);
                PropertyInfo[] array = propertys;
                for (int j = 0; j < array.Length; j++)
                {
                    PropertyInfo propertyInfo = array[j];
                    if (dt.Columns.Contains(propertyInfo.Name))
                    {
                        object obj2 = dt.Rows[i][propertyInfo.Name];
                        if (obj2 != null && obj2 != DBNull.Value)
                        {
                            if (!propertyInfo.PropertyType.IsGenericType)
                            {
                                propertyInfo.SetValue(obj, Convert.ChangeType(obj2, propertyInfo.PropertyType), null);
                            }
                            else
                            {
                                Type genericTypeDefinition = propertyInfo.PropertyType.GetGenericTypeDefinition();
                                if (genericTypeDefinition == typeof(Nullable<>))
                                {
                                    propertyInfo.SetValue(obj, Convert.ChangeType(obj2, Nullable.GetUnderlyingType(propertyInfo.PropertyType)), null);
                                }
                            }
                        }
                    }
                }
                list.Add((T)((object)obj));
            }
            return list;
        }
    }
}
