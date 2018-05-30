
using System;
using System.Data;
namespace AutoManage.Sql
{
    internal class TypeConvert
    {
        public static SqlDbType ConvertDbType(string vsType)
        {
            SqlDbType result;
            switch (vsType)
            {
                case "String":
                    result = SqlDbType.NVarChar;
                    return result;
                case "Int32":
                    result = SqlDbType.Int;
                    return result;
                case "Int64":
                    result = SqlDbType.BigInt;
                    return result;
                case "DateTime":
                    result = SqlDbType.DateTime;
                    return result;
                case "Decimal":
                    result = SqlDbType.Decimal;
                    return result;
                case "Byte[]":
                    result = SqlDbType.Binary;
                    return result;
                case "Byte":
                    result = SqlDbType.Binary;
                    return result;
                case "Char":
                    result = SqlDbType.VarChar;
                    return result;
                case "Boolean":
                    result = SqlDbType.Bit;
                    return result;
                case "Hashtable":
                    result = SqlDbType.Text;
                    return result;
            }
            result = SqlDbType.NVarChar;
            return result;
        }
    }
}
