namespace PoolAutomation
{
    static class Global
    {

        public static string AquaConnectCache { get { return System.IO.Path.Combine(CACHE_DIR, "AquaConnect.json"); } }

        public static string CACHE_DIR = @"C:\Test\Cache";

    }
}