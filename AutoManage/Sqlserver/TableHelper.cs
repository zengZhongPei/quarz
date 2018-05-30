using System;
using System.Collections.Generic;
using System.Data;
namespace AutoManage.Sql
{
    internal class TableHelper
    {
        private static Dictionary<string, string> keyValues = new Dictionary<string, string>();
        public static string GetAutoIncrement(SqlServer db, Type typeEntity)
        {
            string text = string.Empty;
            string key = typeEntity.Name + "primary";
            if (!TableHelper.keyValues.TryGetValue(key, out text))
            {
                DataTable schemaTable = db.GetSchemaTable(typeEntity.Name);
                foreach (DataRow dataRow in schemaTable.Rows)
                {
                    if ((bool)dataRow["IsAutoIncrement"])
                    {
                        text = dataRow["ColumnName"].ToString();
                        TableHelper.keyValues[key] = text;
                        break;
                    }
                }
            }
            return text;
        }
        public static string GetIdentity(SqlServer db, Type typeEntity)
        {
            string text = string.Empty;
            string key = typeEntity.Name + "identity";
            if (!TableHelper.keyValues.TryGetValue(key, out text))
            {
                DataTable schemaTable = db.GetSchemaTable(typeEntity.Name);
                foreach (DataRow dataRow in schemaTable.Rows)
                {
                    if ((bool)dataRow["IsIdentity"])
                    {
                        text = dataRow["ColumnName"].ToString();
                        TableHelper.keyValues[key] = text;
                        break;
                    }
                }
            }
            return text;
        }
    }
}
