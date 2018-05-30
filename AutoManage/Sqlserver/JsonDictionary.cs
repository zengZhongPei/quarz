using System;
using System.Collections.Generic;
using System.Web.Script.Serialization;
namespace AutoManage.Sql
{
    public class JsonDictionary
    {
        private static JavaScriptSerializer serializer = new JavaScriptSerializer();
        public Dictionary<string, object> Dict = new Dictionary<string, object>();
        public object Get(string key)
        {
            object result = null;
            this.Dict.TryGetValue(key, out result);
            return result;
        }
        public new string ToString()
        {
            return JsonDictionary.serializer.Serialize(this.Dict);
        }
        public void FromString(string json)
        {
            if (json != string.Empty)
            {
                this.Dict = JsonDictionary.serializer.Deserialize<Dictionary<string, object>>(json);
            }
        }
        public void Set(string key, object value)
        {
            this.Dict[key] = value;
        }
    }
}
