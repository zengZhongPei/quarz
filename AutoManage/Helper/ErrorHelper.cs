using System;


namespace AutoManage.Helper
{
   public static class ErrorHelper
    {
        /// <summary>
        /// 递归获取异常和所有内部异常信息
        /// </summary>
        /// <param name="ex"></param>
        public static string FullException(Exception ex)
        {
            if (ex == null)
                return string.Empty;
            var content = $"Message:{ex.Message}, StackTrace:{ex.StackTrace}";
            var child = FullException(ex.InnerException);
            return $"{content}\r\n{child}";
        }


    }
}
