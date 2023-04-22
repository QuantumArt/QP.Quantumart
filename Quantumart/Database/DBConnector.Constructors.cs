using System.Data;
using Npgsql;
using QP.ConfigurationService.Models;
using Quantumart.QPublishing.FileSystem;
using Quantumart.QPublishing.Resizer;
using System.Data.SqlClient;
using System.Collections.Generic;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.Http;





namespace Quantumart.QPublishing.Database
{
    public partial class DBConnector
    {


        public DBConnector(DbConnectorSettings dbConnectorSettings, IMemoryCache cache, IHttpContextAccessor httpContextAccessor)
            : this(dbConnectorSettings.ConnectionString, dbConnectorSettings, cache, httpContextAccessor)
        {
        }


        public DBConnector(IDbConnection connection, DbConnectorSettings dbConnectorSettings, IMemoryCache cache, IHttpContextAccessor httpContextAccessor)
            : this(connection.ConnectionString, dbConnectorSettings, cache, httpContextAccessor)
        {
            ExternalConnection = connection;
            if (ExternalConnection != null)
            {
                DbConnectorSettings.DbType = ResolveDbType(ExternalConnection);
                CacheManager.SetInitialQueries();
            }
        }

        public DBConnector(IDbConnection connection, IDbTransaction transaction, DbConnectorSettings dbConnectorSettings, IMemoryCache cache, IHttpContextAccessor httpContextAccessor)
            : this(connection, dbConnectorSettings, cache, httpContextAccessor)
        {
            ExternalTransaction = transaction;
        }

        public DBConnector(string connectionString, DatabaseType dbType = DatabaseType.SqlServer):
            this(connectionString, new DbConnectorSettings() {DbType = dbType}, DefaultMemoryCache, null)
        {
        }


         public DBConnector(IDbConnection connection):
         this(connection, new DbConnectorSettings(), DefaultMemoryCache, null)
        {

        }

        public DBConnector(string strConnectionString, DbConnectorSettings dbConnectorSettings, IMemoryCache cache, IHttpContextAccessor httpContextAccessor)
        {
            ThrowNotificationExceptions = true;
            ForceLocalCache = false;
            UpdateManyToMany = true;
            UpdateManyToOne = true;
            CacheData = true;
            CustomConnectionString = strConnectionString;
            DbConnectorSettings = dbConnectorSettings;
            CacheManager = new DbCacheManager(this, cache);
            FileSystem = new RealFileSystem();
            DynamicImageCreator = new DynamicImageCreator(FileSystem);
            HttpContext = httpContextAccessor?.HttpContext;
            WithTransaction = true;
        }

        private static DatabaseType ResolveDbType(IDbConnection cnn) => cnn is NpgsqlConnection ? DatabaseType.Postgres : DatabaseType.SqlServer;

        private static readonly MemoryCache DefaultMemoryCache = new MemoryCache(new MemoryCacheOptions());

    }
}
