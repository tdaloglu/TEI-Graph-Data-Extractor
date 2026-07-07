using System;
using System.IO;

namespace TEIGraphDataExtractor.Services.Database
{
    public sealed class DbConnectionManager
    {
        private static readonly Lazy<DbConnectionManager> _instance = 
            new Lazy<DbConnectionManager>(() => new DbConnectionManager());

        public static DbConnectionManager Instance => _instance.Value;

        public string DbPath {get; private set; }
        public string ConnectionString {get; private set; }

        private DbConnectionManager()
        {
            string baseFolder = AppDomain.CurrentDomain.BaseDirectory;
            DbPath = Path.Combine(baseFolder, "TEIGraphDataExtractor.db");
            ConnectionString = $"Data Source={DbPath}";
        }

        public void SetCustomDbPath(string customPath)
        {
            DbPath = customPath;
            ConnectionString = $"Data Source={DbPath}";
        }
    }
}