using System;
using System.Collections;
using System.Xml;
namespace AutoManage.Sql
{
    internal class SqlServerConfig
    {
        public static string ConfigPath;
        private static Hashtable mysqlHT;
        static SqlServerConfig()
        {
            SqlServerConfig.ConfigPath = AppDomain.CurrentDomain.BaseDirectory + "AutoManage.config";
            SqlServerConfig.mysqlHT = new Hashtable();
            ConfigCheck.Run();
            XmlDocument xmlDocument = new XmlDocument();
            xmlDocument.Load(SqlServerConfig.ConfigPath);
            XmlNodeList xmlNodeList = xmlDocument.SelectNodes("/root/sql/key");
            foreach (XmlNode xmlNode in xmlNodeList)
            {
                string key = xmlNode.Attributes["name"].Value.ToLower();
                if (!SqlServerConfig.mysqlHT.Contains(key))
                {
                    SqlServerConfig.mysqlHT.Add(key, xmlNode.Attributes["value"].Value);
                }
            }
        }
        public static string GetConn(string key)
        {
            if (SqlServerConfig.mysqlHT.ContainsKey(key))
            {
                return SqlServerConfig.mysqlHT[key].ToString();
            }
            return SqlServerConfig.mysqlHT["default"].ToString();
        }
    }
}
