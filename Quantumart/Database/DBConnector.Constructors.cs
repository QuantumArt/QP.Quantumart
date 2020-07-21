using System.Data;
using Npgsql;
using QP.ConfigurationService.Models;
using Quantumart.QPublishing.FileSystem;
using Quantumart.QPublishing.Resizer;

#if ASPNETCORE || NETCORE
using System.Data.SqlClient;
using System.Collections.Generic;
using Microsoft.Extensions.Caching.Memory;
#else
using System.Collections.Specialized;
using System.Configuration;
using System.Web;
#endif

#if ASPNETCORE
using Microsoft.AspNetCore.Http;

#endif



#if NET4
using Quantumart.QP8.Assembling;
#endif

namespace Quantumart.QPublishing.Database
{
    public partial class DBConnector
    {

#if !ASPNETCORE && !NETCORE
        static DBConnector()
        {
            if (ConfigurationManager.ConnectionStrings["qp_database"] != null)
            {
                ConnectionString = ConfigurationManager.ConnectionStrings["qp_database"].ConnectionString;
                ConfigurationManager.AppSettings["ConnectionString"] = ConnectionString;
            }
        }
#endif

#if ASPNETCORE
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
            this(connectionString, new DbConnectorSettings() {DbType = dbType}, new MemoryCache(new MemoryCacheOptions()), null)
        {
        }


         public DBConnector(IDbConnection connection):
         this(connection, new DbConnectorSettings(), new MemoryCache(new MemoryCacheOptions()), null)
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
#elif NETCORE
        public DBConnector(DbConnectorSettings dbConnectorSettings, IMemoryCache cache)
            : this(dbConnectorSettings.ConnectionString, dbConnectorSettings, cache)
        {
        }


        public DBConnector(IDbConnection connection, DbConnectorSettings dbConnectorSettings, IMemoryCache cache)
            : this(connection.ConnectionString, dbConnectorSettings, cache)
        {
            ExternalConnection = connection;
            if (ExternalConnection != null)
            {
                DbConnectorSettings.DbType = ResolveDbType(ExternalConnection);
                CacheManager.SetInitialQueries();
            }

        }

        public DBConnector(IDbConnection connection, IDbTransaction transaction, DbConnectorSettings dbConnectorSettings, IMemoryCache cache)
            : this(connection, dbConnectorSettings, cache)
        {
            ExternalTransaction = transaction;
        }

        public DBConnector(string connectionString, DatabaseType dbType = DatabaseType.SqlServer):
            this(connectionString, new DbConnectorSettings() {DbType = dbType}, new MemoryCache(new MemoryCacheOptions()))
        {

        }

         public DBConnector(IDbConnection connection):
         this(connection, new DbConnectorSettings(), new MemoryCache(new MemoryCacheOptions()))
        {

        }

        public DBConnector(string strConnectionString, DbConnectorSettings dbConnectorSettings, IMemoryCache cache)
        {

            if (!string.IsNullOrWhiteSpace(dbConnectorSettings.ConnectionString))
            {
                ConnectionString = dbConnectorSettings.ConnectionString;
            }

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
            WithTransaction = true;
        }
#else
        public DBConnector()
            : this(ConnectionString)
        {
        }

        public DBConnector(string strConnectionString, DatabaseType dbType = DatabaseType.SqlServer)
        {
            ForceLocalCache = false;
            CacheData = true;
            UpdateManyToMany = true;
            UpdateManyToOne = true;
            ThrowNotificationExceptions = true;

            CustomConnectionString = strConnectionString;
            DbConnectorSettings = new DbConnectorSettings(ConfigurationManager.AppSettings)
            {
                ConnectionString = strConnectionString,
                DbType = dbType
            };

            CacheManager = new DbCacheManager(this);
            FileSystem = new RealFileSystem();
            DynamicImageCreator = new DynamicImageCreator(FileSystem);
            WithTransaction = true;
        }

        public DBConnector(IDbConnection connection)
            : this(connection.ConnectionString)
        {
            ExternalConnection = connection;
            if (ExternalConnection != null)
            {
                DbConnectorSettings.DbType = ResolveDbType(ExternalConnection);
                CacheManager.SetInitialQueries();
            }

        }

        public DBConnector(IDbConnection connection, IDbTransaction transaction)
            : this(connection)
        {
            ExternalTransaction = transaction;
        }
#endif

        private static DatabaseType ResolveDbType(IDbConnection cnn) => cnn is NpgsqlConnection ? DatabaseType.Postgres : DatabaseType.SqlServer;

    }
}
