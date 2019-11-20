using System;
using System.Collections;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using Microsoft.Win32;
using Quantumart.QPublishing.FileSystem;
using Quantumart.QPublishing.Helpers;
using Quantumart.QPublishing.Info;
using Quantumart.QPublishing.Resizer;

#if ASPNETCORE || NETSTANDARD
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



#if NET4 && !ASPNETCORE
using Quantumart.QP8.Assembling;
#endif


// ReSharper disable once CheckNamespace
namespace Quantumart.QPublishing.Database
{
    // ReSharper disable once InconsistentNaming
    public partial class DBConnector
    {
        private const string IdentityParamString = "@itemId";

        private bool _throwLoadFileExceptions = true;

        public static readonly string LastModifiedByKey = "QP_LAST_MODIFIED_BY_KEY";

        internal static readonly int LegacyNotFound = -1;

        internal static readonly string RegistryPath = @"Software\Quantum Art\Q-Publishing";

        public DbCacheManager CacheManager { get; internal set; }

        public bool ForceLocalCache { get; set; }

        private int? _lastModifiedBy;

#if ASPNETCORE || NETSTANDARD
        public int LastModifiedBy
        {
            get
            {
                var result = 1;
                if (_lastModifiedBy.HasValue)
                {
                    result = _lastModifiedBy.Value;
                }
#if ASPNETCORE
                else if (HttpContext?.Items != null && HttpContext.Items.ContainsKey(LastModifiedByKey))
                {
                    result = (int)HttpContext.Items[LastModifiedByKey];
                }
#endif

                return result;
            }
            set => _lastModifiedBy = value;
        }
#else
        public int LastModifiedBy
        {
            get
            {
                var result = 1;
                if (_lastModifiedBy.HasValue)
                {
                    result = _lastModifiedBy.Value;
                }
                else if (HttpContext.Current != null && HttpContext.Current.Items.Contains(LastModifiedByKey))
                {
                    result = (int)HttpContext.Current.Items[LastModifiedByKey];
                }

                return result;
            }
            set => _lastModifiedBy = value;
        }
#endif

        public bool UseLocalCache => ForceLocalCache;

        public bool CacheData { get; set; }

        public bool UpdateManyToMany { get; set; }

        public bool UpdateManyToOne { get; set; }

        public bool ThrowNotificationExceptions { get; set; }

        public static string ConnectionString { get; set; }

        public string CustomConnectionString { get; set; }

        public string InstanceConnectionString => !string.IsNullOrEmpty(CustomConnectionString) ? CustomConnectionString : ConnectionString;

        public IDbConnection ExternalConnection { get; set; }

        public IDbTransaction ExternalTransaction { get; set; }

        private IDbConnection InternalConnection { get; set; }

        private IDbTransaction InternalTransaction { get; set; }

#if ASPNETCORE
        public HttpContext HttpContext { get; }
#endif

#if ASPNETCORE || NETSTANDARD
        public DbConnectorSettings DbConnectorSettings { get; }
#else
        public NameValueCollection AppSettings => ConfigurationManager.AppSettings;
#endif

#if !ASPNETCORE && !NETSTANDARD
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
        }

        public DBConnector(IDbConnection connection, IDbTransaction transaction, DbConnectorSettings dbConnectorSettings, IMemoryCache cache, IHttpContextAccessor httpContextAccessor)
            : this(connection, dbConnectorSettings, cache, httpContextAccessor)
        {
            ExternalTransaction = transaction;
        }

        public DBConnector(string connectionString):
            this(connectionString, new DbConnectorSettings(), new MemoryCache(new MemoryCacheOptions()), null)
        {

        }

         public DBConnector(IDbConnection connection):
         this(connection, new DbConnectorSettings(), new MemoryCache(new MemoryCacheOptions()), null)
        {

        }

        public DBConnector(string strConnectionString, DbConnectorSettings dbConnectorSettings, IMemoryCache cache, IHttpContextAccessor httpContextAccessor)
        {
            if (dbConnectorSettings.ConnectionStrings == null)
            {
                dbConnectorSettings.ConnectionStrings = new Dictionary<string, string>();
            }

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
            CacheManager = new DbCacheManager(this, cache);
            FileSystem = new RealFileSystem();
            DynamicImageCreator = new DynamicImageCreator(FileSystem);
            DbConnectorSettings = dbConnectorSettings;
            HttpContext = httpContextAccessor?.HttpContext;
        }
#elif NETSTANDARD
        public DBConnector(DbConnectorSettings dbConnectorSettings, IMemoryCache cache)
            : this(dbConnectorSettings.ConnectionString, dbConnectorSettings, cache)
        {
        }


        public DBConnector(IDbConnection connection, DbConnectorSettings dbConnectorSettings, IMemoryCache cache)
            : this(connection.ConnectionString, dbConnectorSettings, cache)
        {
            ExternalConnection = connection;
        }

        public DBConnector(IDbConnection connection, IDbTransaction transaction, DbConnectorSettings dbConnectorSettings, IMemoryCache cache)
            : this(connection, dbConnectorSettings, cache)
        {
            ExternalTransaction = transaction;
        }

        public DBConnector(string connectionString):
            this(connectionString, new DbConnectorSettings(), new MemoryCache(new MemoryCacheOptions()))
        {

        }

         public DBConnector(IDbConnection connection):
         this(connection, new DbConnectorSettings(), new MemoryCache(new MemoryCacheOptions()))
        {

        }

        public DBConnector(string strConnectionString, DbConnectorSettings dbConnectorSettings, IMemoryCache cache)
        {
            if (dbConnectorSettings.ConnectionStrings == null)
            {
                dbConnectorSettings.ConnectionStrings = new Dictionary<string, string>();
            }

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
            CacheManager = new DbCacheManager(this, cache);
            FileSystem = new RealFileSystem();
            DynamicImageCreator = new DynamicImageCreator(FileSystem);
            DbConnectorSettings = dbConnectorSettings;
        }
#else
        public DBConnector()
            : this(ConnectionString)
        {
        }

        public DBConnector(string strConnectionString)
        {
            ForceLocalCache = false;
            CacheData = true;
            UpdateManyToMany = true;
            UpdateManyToOne = true;
            ThrowNotificationExceptions = true;

            CustomConnectionString = strConnectionString;
            CacheManager = new DbCacheManager(this);
            FileSystem = new RealFileSystem();
            DynamicImageCreator = new DynamicImageCreator(FileSystem);
        }

        public DBConnector(IDbConnection connection)
            : this(connection.ConnectionString)
        {
            ExternalConnection = connection;
        }

        public DBConnector(IDbConnection connection, IDbTransaction transaction)
            : this(connection)
        {
            ExternalTransaction = transaction;
        }
#endif

        private void CreateInternalConnection(bool withTransaction)
        {
            InternalConnection = GetActualSqlConnection();
            if (InternalConnection.State == ConnectionState.Closed)
            {
                InternalConnection.Open();
            }

            if (withTransaction)
            {
                var extTr = GetActualSqlTransaction();
                InternalTransaction = extTr ?? InternalConnection.BeginTransaction();
            }
        }

        private void CommitInternalTransaction()
        {
            if (ExternalTransaction == null)
            {
                InternalTransaction.Commit();
            }
        }

        private void DisposeInternalConnection()
        {
            if (ExternalConnection == null)
            {
                InternalConnection.Dispose();
                InternalConnection = null;
                InternalTransaction = null;
            }
        }

        private bool NeedToDisposeActualSqlConnection => ExternalConnection == null && InternalConnection == null;

        private string _instanceCachePrefix;

        public string InstanceCachePrefix => _instanceCachePrefix ?? (_instanceCachePrefix = ExtractCachePrefix(InstanceConnectionString));

        public static string Version => Assembly.GetExecutingAssembly().GetName().Version.ToString();

        public IDynamicImageCreator DynamicImageCreator { get; set; }

        private bool? _isStage;

        public bool IsStage
        {
            get
            {
                if (_isStage.HasValue)
                {
                    return _isStage.Value;
                }

#if !ASPNETCORE && !NETSTANDARD
                if (CacheManager.Page != null)
                {
                    return CacheManager.Page.IsStage;
                }
#endif

                return !CheckIsLive();
            }
            set => _isStage = value;
        }

        public IFileSystem FileSystem { get; set; }

        internal string UploadPlaceHolder => "<%=upload_url%>";

        internal string SitePlaceHolder => "<%=site_url%>";

        internal string UploadBindingPlaceHolder => "<%#upload_url%>";

        internal string SiteBindingPlaceHolder => "<%#site_url%>";

#if NET4
        public static string GetConnectionString(string customerCode)
        {
            var doc = GetQpConfig();
            var node = doc.SelectSingleNode("configuration/customers/customer[@customer_name='" + customerCode + "']/db/text()");
            if (node != null)
            {
                return node.Value.Replace("Provider=SQLOLEDB;", "");
            }

            throw new InvalidOperationException("Cannot load connection string for Asp.NET in QP7 configuration file");
        }

        public static XmlDocument GetQpConfig()
        {
            var localKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
            var qKey = localKey.OpenSubKey(RegistryPath);
            if (qKey != null)
            {
                var regValue = qKey.GetValue("Configuration File");
                if (regValue != null)
                {
                    var doc = new XmlDocument();
                    doc.Load(regValue.ToString());
                    return doc;
                }

                throw new InvalidOperationException("QP7 records in the registry are inconsistent or damaged");
            }

            throw new InvalidOperationException("QP7 is not installed");
        }

        public static string GetQpTempDirectory()
        {
            var doc = GetQpConfig();
            var node = doc.SelectSingleNode("configuration/app_vars/app_var[@app_var_name='TempDirectory']/text()");
            if (node != null)
            {
                return node.Value;
            }

            throw new InvalidOperationException("Cannot load TempDirectory parameter from QP7 configuration file");
        }
#endif

        public static string GetString(object obj, string defaultValue)
        {
            var result = Convert.ToString(obj);
            return string.IsNullOrEmpty(result) ? defaultValue : result;
        }

        public static bool GetNumBool(object obj) => Convert.ToBoolean((decimal)obj);

        public static int GetNumInt(object obj) => (int)(decimal)obj;

        private static string ExtractCachePrefix(string cnnString)
        {
            var result = string.Empty;
            if (cnnString != null)
            {
                var cnnParams = cnnString.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).AsEnumerable().Select(s => new { Key = s.Split('=')[0], Value = s.Split('=')[1] }).ToArray();
                var dbName = cnnParams
                    .Where(n => string.Equals(n.Key, "Initial Catalog", StringComparison.InvariantCultureIgnoreCase) || string.Equals(n.Key, "Database", StringComparison.InvariantCultureIgnoreCase))
                    .Select(n => n.Value)
                    .FirstOrDefault();

                if (dbName == null)
                {
                    throw new ArgumentException("The connection supplied string should contain at least 'Initial Catalog' or 'Database' keyword.");
                }

                var serverName = cnnParams
                    .Where(n => string.Equals(n.Key, "Data Source", StringComparison.InvariantCultureIgnoreCase) || string.Equals(n.Key, "Server", StringComparison.InvariantCultureIgnoreCase))
                    .Select(n => n.Value)
                    .FirstOrDefault();

                if (serverName == null)
                {
                    throw new ArgumentException("The connection string supplied should contain at least 'Data Source' or 'Server' keyword.");
                }

                result = $"{dbName}.{serverName}.";
            }

            return result;
        }

        public string FormatField(string input, int siteId, bool isLive)
        {
            var result = input;
            var uploadUrl = GetImagesUploadUrl(siteId, true);
            result = result.Replace(UploadPlaceHolder, uploadUrl);
            result = result.Replace(UploadBindingPlaceHolder, uploadUrl);

#if ASPNETCORE || NETSTANDARD
            var siteUrl = DbConnectorSettings.UseAbsoluteSiteUrl == 1 ? GetSiteUrl(siteId, isLive) : GetSiteUrlRel(siteId, isLive);
#else
            var siteUrl = AppSettings["UseAbsoluteSiteUrl"] == "1" ? GetSiteUrl(siteId, isLive) : GetSiteUrlRel(siteId, isLive);
#endif


            result = result.Replace(SitePlaceHolder, siteUrl);
            result = result.Replace(SiteBindingPlaceHolder, siteUrl);

            return result;
        }

        public string FormatField(string input, int siteId) => FormatField(input, siteId, !IsStage);

        public int InsertDataWithIdentity(string queryString)
        {
            var command = new SqlCommand(queryString + ";SELECT @Identity = SCOPE_IDENTITY();");
            var idParam = command.Parameters.Add("@Identity", SqlDbType.Decimal);
            idParam.Direction = ParameterDirection.Output;
            ProcessData(command);
            return GetNumInt(idParam.Value);
        }

        public int GetIdentityId(SqlCommand command) => command.Parameters.Contains(IdentityParamString) ? GetNumInt(command.Parameters[IdentityParamString].Value) : 0;

        public string ReplaceCommas(string str)
        {
            var sb = new StringBuilder(str);
            sb.Replace(",", ".");
            return sb.ToString();
        }

        public string GetCachedFileContents(string path)
        {
            var key = CacheManager.FileContentsCacheKeyPrefix + path.ToLowerInvariant();
            return GetCachedEntity(key, LoadFileContents);
        }

        private string LoadFileContents(string key)
        {
            var path = key.Replace(CacheManager.FileContentsCacheKeyPrefix, string.Empty);
            var result = string.Empty;
            try
            {
                result = File.ReadAllText(path);
            }
            catch (Exception)
            {
                if (_throwLoadFileExceptions)
                {
                    throw;
                }
            }

            return result;
        }

        private SqlConnection GetActualSqlConnection(string internalConnectionString) => (InternalConnection ?? ExternalConnection) as SqlConnection ?? new SqlConnection(internalConnectionString);

        private SqlConnection GetActualSqlConnection() => GetActualSqlConnection(InstanceConnectionString);

        private SqlTransaction GetActualSqlTransaction() => (InternalTransaction ?? ExternalTransaction) as SqlTransaction;

#if ASPNETCORE || NETSTANDARD
        public bool CheckIsLive() => DbConnectorSettings.IsLive;
#else
        public bool CheckIsLive() => AppSettings["isLive"] != "false";
#endif

        public bool IsLive(int siteId) => GetSite(siteId)?.IsLive ?? true;

        public bool ForceLive(int siteId) => GetSite(siteId)?.AssembleFormatsInLive ?? false;

        public bool GetAllowUserSessions(int siteId) => GetSite(siteId)?.AllowUserSessions ?? true;

        public bool GetEnableOnScreen(int siteId) => GetSite(siteId)?.EnableOnScreen ?? false;

        public bool GetReplaceUrlsInDB(int siteId) => GetSite(siteId)?.ReplaceUrlsInDB ?? false;

        public void CopyArticleSchedule(int fromArticleId, int toArticleId)
        {
            var testcmd = new SqlCommand("select count(*) From information_schema.columns where column_name = 'use_service' and table_name = 'content_item_schedule'")
            {
                CommandType = CommandType.Text
            };

            var colCount = (int)GetRealScalarData(testcmd);
            var serviceString = colCount == 0 ? string.Empty : ", USE_SERVICE";
            using (var cmd = new SqlCommand())
            {
                var sb = new StringBuilder();
                sb.AppendLine("update content_item_schedule set delete_job = 1 where content_item_id = @newId");
                sb.AppendLine("delete from content_item_schedule where content_item_id = @newId");
                sb.AppendFormatLine("insert into content_item_schedule (CONTENT_ITEM_ID, MAXIMUM_OCCURENCES, CREATED, MODIFIED, LAST_MODIFIED_BY, freq_type, freq_interval, freq_subday_type, freq_subday_interval, freq_relative_interval, freq_recurrence_factor, active_start_date, active_end_date, active_start_time, active_end_time, occurences, use_duration, duration, duration_units, DEACTIVATE, DELETE_JOB{0})", serviceString);
                sb.AppendFormatLine("select @newId, MAXIMUM_OCCURENCES, GETDATE(), GETDATE(), LAST_MODIFIED_BY, freq_type, freq_interval, freq_subday_type, freq_subday_interval, freq_relative_interval, freq_recurrence_factor, active_start_date, active_end_date, active_start_time, active_end_time, occurences, use_duration, duration, duration_units, DEACTIVATE, DELETE_JOB{0}", serviceString);
                sb.AppendLine("from content_item_schedule where content_item_id = @oldId");
                cmd.CommandText = sb.ToString();
                cmd.CommandType = CommandType.Text;
                cmd.Parameters.AddWithValue("@oldId", fromArticleId);
                cmd.Parameters.AddWithValue("@newId", toArticleId);
                ProcessData(cmd);
            }
        }

        public bool IsSplitted(int articleId)
        {
            using (var cmd = new SqlCommand())
            {
                cmd.CommandType = CommandType.Text;
                cmd.CommandText = "select splitted from content_item where content_item_id = @id";
                cmd.Parameters.AddWithValue("@id", articleId);
                return CastDbNull.To(GetRealScalarData(cmd), false);
            }
        }

        public void MergeArticle(int articleId)
        {
            using (var cmd = new SqlCommand("qp_merge_article"))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@item_id", articleId);
                ProcessData(cmd);
            }
        }

        public bool IsTargetTableAsync(int id)
        {
            var cmd = new SqlCommand("qp_is_target_table_async") { CommandType = CommandType.StoredProcedure };
            var idParam = cmd.Parameters.Add("@content_item_id", SqlDbType.Decimal);
            idParam.Value = id;
            var returnParam = cmd.Parameters.Add("@is_target_table_async", SqlDbType.Bit);
            returnParam.Direction = ParameterDirection.Output;
            ProcessData(cmd);
            return (bool)returnParam.Value;
        }

        public int GetMaximumWeightStatusTypeId(int siteId) => ((StatusType)GetStatusHashTable()[siteId]).Id;

        public string GetMaximumWeightStatusTypeName(int siteId) => ((StatusType)GetStatusHashTable()[siteId]).Name;

        public DataRowView GetMaximumWeightStatusRow(int siteId)
        {
            var dv = GetStatuses($"SITE_ID = {siteId}");
            dv.Sort = "WEIGHT DESC";
            return dv[0];
        }

        public int GetStatusTypeId(int siteId, string statusName)
        {
            var filter = $"SITE_ID = {siteId} AND STATUS_TYPE_NAME='{statusName}'";
            var dv = GetStatuses(filter);
            return dv.Count > 0 ? GetNumInt(dv[0]["STATUS_TYPE_ID"]) : LegacyNotFound;
        }

        public DataRow GetPreviousStatusHistoryRecord(int id) => Status.GetPreviousStatusHistoryRecord(id, this);

        public DataTable GetSearchResults(string expression, bool useMorphology, int startPos, long recordCount, string tabname, int minRank, ref long totalRecords)
        {
            const string searchSp = "qp_fulltextSiteSearch";
            const string strSql = "SELECT count(*) from sysobjects where name = '" + searchSp + "'";

            var dt = new DataTable();
            var ds = new DataSet();
            var useMorphologyInt = useMorphology ? 1 : 0;
            if (GetCachedData(strSql).Rows.Count > 0)
            {
                var adapter = new SqlDataAdapter();
                var connection = GetActualSqlConnection();
                try
                {
                    if (connection.State == ConnectionState.Closed)
                    {
                        connection.Open();
                    }

                    adapter.SelectCommand = new SqlCommand(searchSp, connection)
                    {
                        Transaction = GetActualSqlTransaction(),
                        CommandType = CommandType.StoredProcedure
                    };

                    adapter.SelectCommand.Parameters.Add("@tabname", SqlDbType.NVarChar, 255).Value = tabname;
                    adapter.SelectCommand.Parameters.Add("@use_morphology", SqlDbType.Int, 4).Value = useMorphologyInt;
                    adapter.SelectCommand.Parameters.Add("@expression", SqlDbType.NVarChar, 1000).Value = expression.ToLower();
                    adapter.SelectCommand.Parameters.Add("@minrank", SqlDbType.Int, 4).Value = minRank;
                    adapter.SelectCommand.Parameters.Add("@startpos", SqlDbType.Int, 4).Value = startPos;
                    adapter.SelectCommand.Parameters.Add("@count", SqlDbType.Int, 100).Value = recordCount;
                    adapter.AcceptChangesDuringFill = false;
                    adapter.Fill(ds);
                }
                finally
                {
                    if (NeedToDisposeActualSqlConnection)
                    {
                        connection.Dispose();
                    }
                }

                if (ds.Tables[0].Rows.Count > 0)
                {
                    dt = ds.Tables[1];
                    totalRecords = (long)ds.Tables[0].Rows[0]["total"];
                }

                adapter.AcceptChangesDuringFill = true;
            }

            return dt;
        }

        public DataTable GetSearchResults(string expression, bool useMorphology, int startPos, long recordCount, string tabname, int minRank, DateTime startDate, DateTime endDate, int showStaticContent, ref long totalRecords)
        {
            const string searchSp = "qp_fulltextSiteSearchWithDate";
            var dt = new DataTable();
            var ds = new DataSet();

            const string strSql = "SELECT count(*) from sysobjects where name = '" + searchSp + "'";
            var useMorphologyInt = useMorphology ? 1 : 0;
            if (GetCachedData(strSql).Rows.Count > 0)
            {
                var adapter = new SqlDataAdapter();
                var connection = GetActualSqlConnection();
                try
                {
                    if (connection.State == ConnectionState.Closed)
                    {
                        connection.Open();
                    }

                    adapter.SelectCommand = new SqlCommand(searchSp, connection)
                    {
                        Transaction = GetActualSqlTransaction(),
                        CommandType = CommandType.StoredProcedure
                    };

                    adapter.SelectCommand.Parameters.Add("@tabname", SqlDbType.NVarChar, 255).Value = tabname;
                    adapter.SelectCommand.Parameters.Add("@use_morphology", SqlDbType.Int, 4).Value = useMorphologyInt;
                    adapter.SelectCommand.Parameters.Add("@expression", SqlDbType.NVarChar, 1000).Value = expression.ToLower();
                    adapter.SelectCommand.Parameters.Add("@minrank", SqlDbType.Int, 4).Value = minRank;
                    adapter.SelectCommand.Parameters.Add("@startpos", SqlDbType.Int, 4).Value = startPos;
                    adapter.SelectCommand.Parameters.Add("@count", SqlDbType.Int, 100).Value = recordCount;
                    adapter.SelectCommand.Parameters.Add("@startDate", SqlDbType.DateTime).Value = startDate;
                    adapter.SelectCommand.Parameters.Add("@endDate", SqlDbType.DateTime).Value = endDate;
                    adapter.SelectCommand.Parameters.Add("@showStatic", SqlDbType.Bit).Value = showStaticContent;

                    adapter.AcceptChangesDuringFill = false;
                    adapter.Fill(ds);
                }
                finally
                {
                    if (NeedToDisposeActualSqlConnection)
                    {
                        connection.Dispose();
                    }
                }

                if (ds.Tables[0].Rows.Count > 0)
                {
                    dt = ds.Tables[1];
                    totalRecords = (long)ds.Tables[0].Rows[0]["total"];
                }

                adapter.AcceptChangesDuringFill = true;
            }

            return dt;
        }

        public string CorrectStatuses(int srcSiteId, int destSiteId, string where)
        {
            var result = where;
            if (destSiteId != 0 && srcSiteId != destSiteId)
            {
                result = result.ToUpperInvariant();
                const string simpleExpression = "AND C.STATUS_TYPE_ID = ";
                const string complexExpressionBegin = "AND C.STATUS_TYPE_ID IN (";
                const string complexExpressionEnd = ")";

                var statusString = string.Empty;
                var simpleRegex = new Regex(Regex.Escape(simpleExpression) + "([\\d]+)");
                var complexRegex = new Regex(Regex.Escape(complexExpressionBegin) + "([^\\)]+)" + Regex.Escape(complexExpressionEnd));

                if (result.IndexOf(simpleExpression, StringComparison.Ordinal) >= 0)
                {
                    var simpleMatch = simpleRegex.Match(result);
                    if (simpleMatch.Success)
                    {
                        statusString = simpleMatch.Groups[1].Value;
                        result = result.Replace(simpleExpression + statusString, complexExpressionBegin + statusString + complexExpressionEnd);
                    }
                }
                else
                {
                    var complexMatch = complexRegex.Match(result);
                    if (complexMatch.Success)
                    {
                        statusString = complexMatch.Groups[1].Value;
                    }
                }

                if (string.IsNullOrEmpty(statusString))
                {
                    return result;
                }

                var statuses = new ArrayList();
                statuses.AddRange(statusString.Split(','));
                foreach (var status in statusString.Split(','))
                {
                    var statusesView = GetStatuses($"[STATUS_TYPE_ID] = {status}");
                    var statusName = statusesView[0]["STATUS_TYPE_NAME"].ToString();
                    var statusesView2 = GetStatuses($"[STATUS_TYPE_ID] <> {status} AND [STATUS_TYPE_NAME] = '{statusName}'");
                    foreach (DataRowView row in statusesView2)
                    {
                        statuses.Add(row["STATUS_TYPE_ID"].ToString());
                    }
                }

                if (statuses.Count > 0)
                {
                    var newStatusString = string.Join(",", (string[])statuses.ToArray());
                    result = result.Replace(complexExpressionBegin + statusString + complexExpressionEnd, complexExpressionBegin + newStatusString + complexExpressionEnd);
                }
            }

            return result;
        }

        public DataTable GetContainerQueryResultTable(ContainerQueryObject obj, out long totalRecords)
        {
            obj.WithReset = false;
            var result = CacheManager.GetQueryResult(obj, out totalRecords);
            if (!result.Columns.Contains("content_item_id"))
            {
                obj.WithReset = true;
                result = CacheManager.GetQueryResult(obj, out totalRecords);
            }

            return new DataView(result).ToTable();
        }

        public DataTable GetPageData(string select, string from, string where, string orderBy, long startRow, long pageSize, byte getCount, out long totalRecords) =>
            GetContainerQueryResultTable(new ContainerQueryObject(this, select, from, where, orderBy, startRow.ToString(), pageSize.ToString()), out totalRecords);

        public string GetBinDirectory(int siteId, bool isLive)
        {
            var site = GetSite(siteId);
            if (site == null)
            {
                return string.Empty;
            }

            return isLive ? site.AssemblyDirectory : site.StageAssemblyDirectory;
        }

        public string GetAppDataDirectory(int siteId, bool isLive) => GetBinDirectory(siteId, isLive).Replace("bin", "App_Data");

        public string GetDefaultMapFileContents(int siteId, bool isLive, string contextName)
        {
            var saved = _throwLoadFileExceptions;
            _throwLoadFileExceptions = false;

            var result = GetMapFileContents(siteId, isLive, contextName + ".map");
            _throwLoadFileExceptions = saved;

            if (string.IsNullOrEmpty(result))
            {
                var key = $"Quantumart.QP8.OnlineMapping.{siteId}.{isLive}.{contextName}";
                result = GetCachedEntity(key, GetRealDefaultMapFileContents);
            }

            return result;
        }

#if !ASPNETCORE && NET4
        private string GetRealDefaultMapFileContents(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException(nameof(key));
            }

            var arr = key.Split('.');
            if (key.Length < 6)
            {
                throw new ArgumentException(nameof(key));
            }

            var siteId = int.Parse(arr[3]);
            var isLive = bool.Parse(arr[4]);
            var contextName = arr[5];
            if (string.IsNullOrEmpty(contextName))
            {
                contextName = GetSite(siteId).ContextClassName;
            }

            var mapping = new AssembleContentsController(siteId, InstanceConnectionString) { IsLive = isLive }.GetMapping(contextName);
            if (string.IsNullOrEmpty(mapping))
            {
                throw new ApplicationException($"Cannot receive mapping for context '{contextName}' from site (ID={siteId})");
            }

            return mapping;
        }
#else
        private static string GetRealDefaultMapFileContents(string key) => throw new NotImplementedException("Not implemented at .net core app framework");
#endif

        public string GetMapFileContents(int siteId, bool isLive, string fileName) => GetCachedFileContents(Path.Combine(GetAppDataDirectory(siteId, isLive), fileName));

        public string GetDefaultMapFileContents(int siteId, string contextName = null) => GetDefaultMapFileContents(siteId, !IsStage, contextName);

        public string GetMapFileContents(int siteId, string fileName) => GetMapFileContents(siteId, !IsStage, fileName);
    }
}
