using System;
using System.Web;
using System.Web.Caching;
namespace AutoManage.Sql
{
    internal class SiteCache
    {
        private static readonly Cache _cache;
        private static object readLockObj;
        private SiteCache()
        {
        }
        static SiteCache()
        {
            SiteCache.readLockObj = new object();
            HttpContext current = HttpContext.Current;
            if (current != null)
            {
                SiteCache._cache = current.Cache;
                return;
            }
            SiteCache._cache = HttpRuntime.Cache;
        }
        public static void Remove(string key)
        {
            SiteCache._cache.Remove(key);
        }
        public static void Set(string key, object obj, string cacheFilePath)
        {
            if (obj != null)
            {
                CacheDependency dependencies = new CacheDependency(cacheFilePath);
                SiteCache._cache.Insert(key, obj, dependencies, DateTime.Now.AddYears(1), TimeSpan.Zero, CacheItemPriority.High, null);
            }
        }
        public static void Set(string key, object obj)
        {
            if (obj != null)
            {
                SiteCache.Set(key, obj, DateTime.Now.AddYears(1));
            }
        }
        public static void Set(string key, object obj, DateTime passDate)
        {
            if (obj != null)
            {
                SiteCache._cache.Insert(key, obj, null, passDate, TimeSpan.Zero);
            }
        }
        public static void Set(string key, object obj, long minutes)
        {
            if (obj != null)
            {
                SiteCache._cache.Insert(key, obj, null, DateTime.MaxValue, TimeSpan.FromMinutes((double)minutes));
            }
        }
        public static object Get(string key)
        {
            object result;
            lock (SiteCache.readLockObj)
            {
                result = SiteCache._cache[key];
            }
            return result;
        }
    }
}
