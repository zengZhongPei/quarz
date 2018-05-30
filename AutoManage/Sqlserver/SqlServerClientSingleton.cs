using System;
namespace AutoManage.Sql
{
    public abstract class SqlServerClientSingleton<T> where T : new()
    {
        private class SingletonCreator
        {
            internal static readonly SqlServerClient<T> instance;
            static SingletonCreator()
            {
                SqlServerClientSingleton<T>.SingletonCreator.instance = new SqlServerClient<T>();
            }
        }
        public static SqlServerClient<T> Instance
        {
            get
            {
                return SqlServerClientSingleton<T>.SingletonCreator.instance;
            }
        }
    }
}
