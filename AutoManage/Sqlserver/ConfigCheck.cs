using System;
using System.IO;
using System.Text;

namespace AutoManage.Sql
{
    internal class ConfigCheck
    {
        private static bool IsExist;
        public static void Run()
        {
            if (ConfigCheck.IsExist)
            {
                return;
            }
            string text = AppDomain.CurrentDomain.BaseDirectory + "AutoManage.config";
            if (!File.Exists(text))
            {
                ConfigCheck.WriteFile(text);
                throw new ArgumentException("Please configure the connection parameters in the project root path in \"" + text + "\"!");
            }
            ConfigCheck.IsExist = true;
        }
        private static void WriteFile(string filePath)
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\" ?>");
            stringBuilder.AppendLine("<root>");
            stringBuilder.AppendLine("  <memcached>");
            stringBuilder.AppendLine("    <key name=\"default\" ip=\"127.0.0.1\" port=\"11211\" />");
            stringBuilder.AppendLine("  </memcached>");
            stringBuilder.AppendLine("  <!--local|memcached|redis-->");
            stringBuilder.AppendLine("   <mysql cachetype=\"memcached\">");
            stringBuilder.AppendLine("    <key name=\"default\" value=\"server=127.0.0.1; user id=harry; password=harrry123; database=dbname; pooling=true;\" />");
            stringBuilder.AppendLine("    <key name=\"Harry.Service.Model.Test\" value=\"\" />");
            stringBuilder.AppendLine("  </mysql>");
            stringBuilder.AppendLine("</root>");
            StreamWriter streamWriter = File.CreateText(filePath);
            streamWriter.Write(stringBuilder.ToString());
            streamWriter.Flush();
            streamWriter.Close();
        }
    }
}
