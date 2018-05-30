using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoManage.Helper
{
   public static class SendMsgHelper
    {
        private static CCPRestSDK.CCPRestSDK GetCCPRestSDKApi()
        {
            var api = new CCPRestSDK.CCPRestSDK();
            api.init("sandboxapp.cloopen.com", "8883");
            api.setAccount("8a216da854e1a37a0154e1e6034700a9", "9fa6b4026ca84b9cb0462ab1333e4934");
            api.setAppId("8a216da854ebfcf70154f04180df0441");
            return api;
        }
        public static void SendSmsMessage(string mobi, string templateid, string[] data)
        {
            var api = GetCCPRestSDKApi();
            string ret = null;
            try
            {
                Dictionary<string, object> retData = api.SendTemplateSMS(mobi, templateid, data);
            }
            catch (System.Exception exc)
            {
                ret = exc.Message;
            }
            finally
            {
                //Response.Write(ret);
            }
        }
    }
}
