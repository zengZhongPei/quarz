using System;
using System.Collections.Generic;
using System.Web.Script.Serialization;
namespace AutoManage.Sql
{
    [Serializable]
    public class JsonList
    {
        private static JavaScriptSerializer serializer = new JavaScriptSerializer();
        public List<object> list = new List<object>();
        public new string ToString()
        {
            return JsonList.serializer.Serialize(this.list);
        }
        public void FromString(string json)
        {
            if (json != string.Empty)
            {
                this.list = JsonList.serializer.Deserialize<List<object>>(json);
            }
        }
        public void Set(object value)
        {
            this.list.Add(value);
        }
    }
}
