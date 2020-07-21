using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using QP.ConfigurationService.Models;
using Quantumart.QPublishing.Helpers;
using Quantumart.QPublishing.Info;
using Content = Quantumart.QPublishing.Info.Content;
#if ASPNETCORE || NETCORE
using Microsoft.Extensions.Caching.Memory;
#else
using System.IO;
using System.Web;
using System.Web.Caching;
using System.Web.UI.WebControls;
#endif

// ReSharper disable once CheckNamespace
namespace Quantumart.QPublishing.Database
{
    public class DbCacheManager
    {
        private static readonly object NullObject = new object();

        private readonly Dictionary<string, string> _queries = new Dictionary<string, string>();

        private readonly Dictionary<string, string[]> _fieldsToValidate = new Dictionary<string, string[]>();

        private readonly Dictionary<string, DataTable> _dataTables = new Dictionary<string, DataTable>();

        private static readonly Hashtable LockObjects = new Hashtable();

        private static readonly object Locklock = new object();

        private bool _storeInDictionary;

#if !ASPNETCORE && !NETCORE
        private const string WebSpecificString = "DbConnector should be provided with page-specific information.";
#endif

        private readonly Hashtable _localCache = new Hashtable();

        internal const int DefaultLongExpirationTime = 60;

        internal const int DefaultExpirationTime = 10;

        internal const int DefaultShortExpirationTime = 1;

        internal const int MinExpirationTime = 1;

        internal const int DefaultPrefetchLimit = 1000;

        public bool StoreInDictionary
        {
            get => _storeInDictionary;
            set
            {
                _storeInDictionary = value;
                _storeInDictionary = true;
            }
        }

        public DBConnector DbConnector { get; set; }


#if ASPNETCORE || NETCORE
        private readonly IMemoryCache _cache;

        public DbCacheManager(DBConnector dbConnector, IMemoryCache cache)
        {
            _cache = cache;
#else
        public DbCacheManager(DBConnector dbConnector)
        {
#endif
            DbConnector = dbConnector;
            SetInitialQueries();

        }

        public void SetInitialQueries()
        {
            _queries[ConstraintKey] = $@"
                SELECT CCR.CONSTRAINT_ID, CCR.ATTRIBUTE_ID, CC.CONTENT_ID FROM CONTENT_CONSTRAINT_RULE CCR {DbConnector.WithNoLock}
                INNER JOIN CONTENT_CONSTRAINT CC {DbConnector.WithNoLock} ON CC.CONSTRAINT_ID = CCR.CONSTRAINT_ID ";

            _queries[StatusKey] = $@" SELECT C.SITE_ID, C.STATUS_TYPE_ID, C.STATUS_TYPE_NAME, C.WEIGHT, C.DESCRIPTION FROM STATUS_TYPE AS C {DbConnector.WithNoLock}";

            _fieldsToValidate[ConstraintKey] = new [] { "attribute_id" };
            _fieldsToValidate[StatusKey] = new [] { "status_type_id" };
        }


        public void ResetCacheItem(string key)
        {
            if (_dataTables.ContainsKey(key))
            {
                _dataTables.Remove(key);
            }

            if (GetDataFromCache(key) != null)
            {
                RemoveDataFromCache(key);
            }
        }

        public void ClearLocalCache()
        {
            _localCache.Clear();
        }

#if !ASPNETCORE && !NETCORE
        private static bool IsPageSpecificKey(string key) => string.IsNullOrEmpty(key);
#endif

        private T GetDataFromCache<T>(string key) => (T)GetDataFromCache(key);

#if ASPNETCORE || NETCORE
        private object GetDataFromCache(string key) => DbConnector.UseLocalCache ? _localCache[key] : _cache.Get(key);
#else
        private object GetDataFromCache(string key) => DbConnector.UseLocalCache ? _localCache[key] : HttpRuntime.Cache[key];
#endif

#if ASPNETCORE || NETCORE
        private void AddEntityToCache<T>(string cacheKey, T ht, double cacheInterval)
            where T : class
        {
            object obj = ht;
            if (ht == null)
            {
                obj = NullObject;
            }

            if (DbConnector.UseLocalCache)
            {
                _localCache[cacheKey] = obj;
            }
            else
            {
                _cache.GetOrCreate(cacheKey, entry =>
                {
                    entry.Value = obj;
                    entry.AbsoluteExpiration = DateTime.UtcNow.AddMinutes(cacheInterval);
                    return obj;
                });
            }
        }
#else
        private void AddEntityToCache<T>(string cacheKey, T ht, double cacheInterval, SqlCacheDependency extDep)
            where T : class
        {
            object obj = ht;
            if (ht == null)
            {
                obj = NullObject;
            }

            if (DbConnector.UseLocalCache)
            {
                _localCache[cacheKey] = obj;
            }
            else
            {
                if (extDep != null)
                {
                    HttpRuntime.Cache.Insert(cacheKey, obj, extDep);
                }
                else
                {
                    HttpRuntime.Cache.Insert(cacheKey, obj, null, DateTime.UtcNow.AddMinutes(cacheInterval), Cache.NoSlidingExpiration);
                }
            }
        }
#endif

        private void RemoveDataFromCache(string key)
        {
            if (DbConnector.UseLocalCache)
            {
                _localCache.Remove(key);
            }
            else
            {
#if ASPNETCORE || NETCORE
                _cache.Remove(key);
#else
                HttpRuntime.Cache.Remove(key);
#endif
            }
        }

        private static object GetLockObject(string key)
        {
            if (LockObjects.ContainsKey(key))
            {
                return LockObjects[key];
            }

            lock (Locklock)
            {
                if (!LockObjects.ContainsKey(key))
                {
                    var obj = new object();
                    LockObjects.Add(key, obj);
                }

                return LockObjects[key];
            }
        }


        internal DataTable GetCachedTable(string key) => GetCachedTable(key, GetInternalExpirationTime(key));

        internal DataTable GetCachedTable(string key, double cacheInterval) => GetCachedEntity(key, cacheInterval, GetRealData);

#if !ASPNETCORE && !NETCORE
        internal DataTable GetCachedTable(string key, double cacheInterval, bool useDependency) => useDependency
            ? GetCachedTableWithDependency(key)
            : GetCachedTable(key, cacheInterval);
#endif

        private DataTable GetRealData(string key) => DbConnector.GetRealData(GetQuery(key));

#if !ASPNETCORE && !NETCORE
        public DataTable GetCachedTableWithDependency(string key)
        {
            DataTable ht;
            if (!DbConnector.CacheData)
            {
                SqlCacheDependency dep = null;
                ht = DbConnector.GetRealDataWithDependency(GetQuery(key), ref dep);
            }
            else
            {
                var obj = GetDataFromCache(key);
                if (obj == NullObject)
                {
                    return null;
                }

                ht = (DataTable)obj;
                if (ht == null)
                {
                    lock (GetLockObject(key))
                    {
                        ht = GetDataFromCache<DataTable>(key);
                        if (ht == null)
                        {
                            SqlCacheDependency dep = null;
                            ht = DbConnector.GetRealDataWithDependency(GetQuery(key), ref dep);
                            AddEntityToCache(key, ht, 0, dep);
                        }
                    }
                }
            }

            return ht;
        }
#endif

        internal DataTable GetDataTable(string key)
        {
            if (_dataTables.ContainsKey(key))
            {
                return _dataTables[key];
            }

            var obj = GetCachedTable(key);
            _dataTables.Add(key, obj);
            return obj;
        }

        internal DataView GetDataView(string key, string rowFilter)
        {
            var dv = new DataView(GetDataTable(key));
            try
            {
                dv.RowFilter = rowFilter;
            }
            catch (EvaluateException)
            {
#if NET4
                Dump.DumpDataTable(dv.ToTable(), key);
#endif
                dv = ReGetDataView(key, rowFilter);
            }

            if (!ValidateView(dv, key))
            {
#if NET4
                Dump.DumpDataTable(dv.ToTable(), key);
#endif
                dv = ReGetDataView(key, rowFilter);
            }

            return dv;
        }

        private DataView ReGetDataView(string key, string rowFilter)
        {
            ResetCacheItem(key);
            var dv = new DataView(GetDataTable(key)) { RowFilter = rowFilter };
            return dv;
        }

        private bool ValidateView(DataView view, string key)
        {
            var fieldsArray = GetFieldsToValidate(key);
            return view.Count == 0 || fieldsArray.All(field => view.Table.Columns.Contains(field));
        }

        private IEnumerable<string> GetFieldsToValidate(string key)
        {
            var newKey = key.Replace(GetDataKeyPrefix, string.Empty);
            if (string.Equals(key, newKey))
            {
                Debug.Assert(_fieldsToValidate.ContainsKey(key), "_fieldsToValidate.ContainsKey(key):" + key);
                return _fieldsToValidate[key];
            }

            return new string[0];
        }

#if !ASPNETCORE && !NETCORE
        private static string GetBaseObjectsQuery() => "SELECT PT.TEMPLATE_NAME, PT.NET_TEMPLATE_NAME, PT.SITE_ID, PT.PAGE_TEMPLATE_ID, P.PAGE_ID, OBJ.[OBJECT_NAME], OBJ.[OBJECT_ID], OBJ.NET_OBJECT_NAME, OBJF.FORMAT_NAME, OBJF.NET_FORMAT_NAME, OBJ.OBJECT_FORMAT_ID AS DEFAULT_FORMAT_ID, P.PAGE_FOLDER, OBJF.OBJECT_FORMAT_ID AS CURRENT_FORMAT_ID FROM OBJECT AS OBJ INNER JOIN OBJECT_FORMAT AS OBJF ON OBJ.OBJECT_ID = OBJF.OBJECT_ID INNER JOIN PAGE_TEMPLATE PT ON OBJ.PAGE_TEMPLATE_ID = PT.PAGE_TEMPLATE_ID LEFT JOIN PAGE AS P ON P.PAGE_ID = OBJ.PAGE_ID";

        private static string GetPageQuery() => "SELECT P.PAGE_ID, P.PAGE_TEMPLATE_ID, P.PAGE_NAME, PAGE_FILENAME, P.PROXY_CACHE, P.CACHE_HOURS, P.CHARSET, P.GENERATE_TRACE, P.PAGE_FOLDER, P.DISABLE_BROWSE_SERVER, P.SET_LAST_MODIFIED_HEADER, P.SEND_NOCACHE_HEADERS FROM PAGE P INNER JOIN PAGE_TEMPLATE PT ON P.PAGE_TEMPLATE_ID = PT.PAGE_TEMPLATE_ID";

        private static string GetPageTemplateQuery() => "SELECT PT.SITE_ID, PT.PAGE_TEMPLATE_ID, PT.TEMPLATE_FOLDER, PT.NET_TEMPLATE_NAME, PT.TEMPLATE_NAME, PT.CHARSET, PT.SEND_NOCACHE_HEADERS FROM PAGE_TEMPLATE PT";
#endif

        private string GetContentQuery() => $@"
            SELECT C.CONTENT_ID, C.CONTENT_NAME, C.NET_CONTENT_NAME, C.VIRTUAL_TYPE, C.SITE_ID, C.MAX_NUM_OF_STORED_VERSIONS, S.SITE_NAME, CWB.WORKFLOW_ID
            FROM CONTENT AS C {DbConnector.WithNoLock} INNER JOIN SITE AS S {DbConnector.WithNoLock} ON C.SITE_ID = S.SITE_ID
            LEFT JOIN CONTENT_WORKFLOW_BIND CWB on CWB.CONTENT_ID = C.CONTENT_ID ";

        private string GetAttributeQuery() => $@"
            SELECT C.SITE_ID, AT.TYPE_NAME, AT.DATABASE_TYPE, AT.INPUT_TYPE, CA.ATTRIBUTE_ID, CA.CONTENT_ID, CA.ATTRIBUTE_NAME, CA.NET_ATTRIBUTE_NAME,
            CA.INPUT_MASK, CA.ATTRIBUTE_SIZE,
            CASE WHEN ca.link_id is not null THEN {DbConnector.Schema}.qp_default_link_ids(CA.ATTRIBUTE_ID) ELSE coalesce(ca.DEFAULT_BLOB_VALUE, ca.DEFAULT_VALUE) END as DEFAULT_VALUE,
            CA.ATTRIBUTE_TYPE_ID, CA.INDEX_FLAG, CA.ATTRIBUTE_ORDER, CA.DESCRIPTION,
            CA.REQUIRED, CA.IS_CLASSIFIER, CA.AGGREGATED, CA.READONLY_FLAG, CA.RELATED_IMAGE_ATTRIBUTE_ID, CA.PERSISTENT_ATTR_ID, CA.JOIN_ATTR_ID, CA.LINK_ID, CA.USE_SITE_LIBRARY, CA.SUBFOLDER, CA.DISABLE_VERSION_CONTROL,
            DIA.WIDTH, DIA.HEIGHT, DIA.TYPE, DIA.QUALITY, DIA.MAX_SIZE, DIA.ATTRIBUTE_ID AS DYNAMIC_IMAGE_ATTRIBUTE_ID,
            S.USE_SITE_LIBRARY AS SOURCE_USE_SITE_LIBRARY, S.CONTENT_ID AS SOURCE_CONTENT_ID,
            CA.BACK_RELATED_ATTRIBUTE_ID AS BASE_RELATION_ATTRIBUTE_ID, RCA.CONTENT_ID AS BASE_RELATION_CONTENT_ID, RCA.ATTRIBUTE_NAME AS BASE_RELATION_ATTRIBUTE_NAME,
            CL.R_CONTENT_ID, COALESCE(RA.CONTENT_ID, CL.R_CONTENT_ID, RCA.CONTENT_ID) AS RELATED_CONTENT_ID, CCR.CONSTRAINT_ID
            FROM CONTENT_ATTRIBUTE AS CA {DbConnector.WithNoLock}
            INNER JOIN ATTRIBUTE_TYPE AS AT {DbConnector.WithNoLock} ON AT.ATTRIBUTE_TYPE_ID = CA.ATTRIBUTE_TYPE_ID
            INNER JOIN CONTENT AS C {DbConnector.WithNoLock} ON C.CONTENT_ID = CA.CONTENT_ID
            LEFT JOIN DYNAMIC_IMAGE_ATTRIBUTE AS DIA {DbConnector.WithNoLock} ON CA.ATTRIBUTE_ID = DIA.ATTRIBUTE_ID
            LEFT JOIN CONTENT_ATTRIBUTE S ON CA.PERSISTENT_ATTR_ID = S.ATTRIBUTE_ID
            LEFT JOIN CONTENT_ATTRIBUTE RA ON CA.RELATED_ATTRIBUTE_ID = RA.ATTRIBUTE_ID
            LEFT JOIN CONTENT_ATTRIBUTE RCA ON CA.BACK_RELATED_ATTRIBUTE_ID = RCA.ATTRIBUTE_ID
            LEFT JOIN CONTENT_CONSTRAINT_RULE CCR ON CA.ATTRIBUTE_ID = CCR.ATTRIBUTE_ID
            LEFT JOIN CONTENT_TO_CONTENT CL ON CA.LINK_ID = CL.LINK_ID AND CA.CONTENT_ID = CL.L_CONTENT_ID ";

        public string GetQuery(string key)
        {
            var newKey = key.Replace(GetDataKeyPrefix, string.Empty);
            if (string.Equals(key, newKey))
            {
                Debug.Assert(_queries.ContainsKey(key), "_queries.ContainsKey(key):" + key);
                return _queries[key];
            }

            return newKey;
        }

        internal Hashtable GetCachedHashTable(string key) => GetCachedHashTable(key, GetInternalExpirationTime(key));

        internal Hashtable GetCachedHashTable(string key, double cacheInterval)
        {
            return GetCachedEntity(key, cacheInterval, FillHashTable);
        }

        internal DualHashTable GetCachedDualHashTable(string key) => GetCachedDualHashTable(key, GetInternalExpirationTime(key));

        internal DualHashTable GetCachedDualHashTable(string key, double cacheInterval)
        {

            return GetCachedEntity(key, cacheInterval, FillDualHashTable);
        }

        internal Hashtable GetAttributeIdForLinqHashTable(int contentId)
        {
            var key = contentId.ToString();
            var attributeIdForLinqHash = GetCachedHashTable(AttributeIdForLinqHashKey);
            if (attributeIdForLinqHash.ContainsKey(key))
            {
                return (Hashtable)attributeIdForLinqHash[key];
            }

            var dualHash = GetCachedDualHashTable(AttributeHashKey);
            var resultHash = new Hashtable();
            var idHash = dualHash.Ids.ContainsKey(key) ? (ArrayList)dualHash.Ids[key] : AddAttributeIdHashEntry(key);
            foreach (var item in idHash)
            {
                if (item != null)
                {
                    var itemKey = ((int)item).ToString();
                    var attr = (ContentAttribute)dualHash.Items[itemKey];
                    if (attr?.LinqName != null)
                    {
                        var linqKey = attr.LinqName.ToLowerInvariant();
                        resultHash[linqKey] = attr.Id;
                    }
                    else if (attr == null)
                    {
#if NET4
                        Dump.DumpStr($"{DateTime.Now}: Attribute returned for key '{key}' is null");
#endif
                    }
                }
                else
                {
#if NET4
                    Dump.DumpStr($"{DateTime.Now}: Item in arraylist for key '{key}' is null");
#endif
                }
            }

            attributeIdForLinqHash[key] = resultHash;
            return resultHash;
        }

        internal Hashtable GetLinkForLinqHashTable(int siteId)
        {
            var key = siteId.ToString();
            var linkForLinqHash = GetCachedHashTable(LinkForLinqHashKey);
            if (linkForLinqHash.ContainsKey(key))
            {
                return (Hashtable)linkForLinqHash[key];
            }

            var localHash = new Hashtable();

            var cmd = DbConnector.CreateDbCommand($"SELECT LINK_ID, NET_LINK_NAME FROM CONTENT_TO_CONTENT CC INNER JOIN CONTENT C ON CC.L_CONTENT_ID = C.CONTENT_ID WHERE SITE_ID = @Id");
            cmd.Parameters.AddWithValue("@Id", siteId);
            var dt2 = DbConnector.GetRealData(cmd);
            foreach (DataRow row in dt2.Rows)
            {
                var itemKey = Convert.ToString(row["NET_LINK_NAME"]);
                if (!string.IsNullOrEmpty(itemKey))
                {
                    itemKey = itemKey.ToLowerInvariant();
                    var linkId = (int)(decimal)row["LINK_ID"];
                    localHash[itemKey] = linkId;
                }
            }

            linkForLinqHash[key] = localHash;
            return localHash;
        }

        internal Hashtable GetContentIdForLinqHashTable(int siteId)
        {
            var key = siteId.ToString();
            var contentIdForLinqHash = GetCachedHashTable(ContentIdForLinqHashKey);
            if (contentIdForLinqHash.ContainsKey(key))
            {
                return (Hashtable)contentIdForLinqHash[key];
            }

            var resultHash = new Hashtable();
            var dualHash = GetCachedDualHashTable(ContentHashKey);
            var idHash = dualHash.Ids.ContainsKey(key) ? (Hashtable)dualHash.Ids[key] : AddContentIdHashEntry(key);
            foreach (var item in idHash.Values)
            {
                var itemKey = ((int)item).ToString();
                var content = (Content)dualHash.Items[itemKey];
                if (content?.LinqName != null)
                {
                    var linqKey = content.LinqName.ToLowerInvariant();
                    resultHash[linqKey] = content.Id;
                }
            }

            contentIdForLinqHash[key] = resultHash;
            return resultHash;
        }

        internal ContentAttribute AddAttributeHashEntry(string itemKey)
        {
            var cmd = DbConnector.CreateDbCommand($@"SELECT CONTENT_ID FROM CONTENT_ATTRIBUTE {DbConnector.WithNoLock} WHERE ATTRIBUTE_ID = @attrId");
            cmd.Parameters.AddWithValue("@attrId", int.Parse(itemKey));
            var dt = DbConnector.GetRealData(cmd);
            var contentId = dt.Rows.Count == 0 ? 0 : int.Parse(dt.Rows[0]["CONTENT_ID"].ToString());

            ContentAttribute result = null;
            AddAttributeIdHashEntry(contentId.ToString(), itemKey, ref result);
            return result;
        }

        internal RelationInfo AddLinkHashEntry(string contentKey, string nameKey)
        {
            RelationInfo result = null;
            var linkHash = GetCachedHashTable(LinkHashKey);
            var attributeHash = new Hashtable();
            var cmd = DbConnector.CreateDbCommand($@"
                SELECT ATTRIBUTE_NAME, LINK_ID, BACK_RELATED_ATTRIBUTE_ID FROM CONTENT_ATTRIBUTE
                WHERE (LINK_ID IS NOT NULL OR BACK_RELATED_ATTRIBUTE_ID IS NOT NULL) AND CONTENT_ID = @Id"
            );
            cmd.Parameters.AddWithValue("@Id", int.Parse(contentKey));
            var dt = DbConnector.GetRealData(cmd);
            foreach (DataRow row in dt.Rows)
            {
                var key = row["ATTRIBUTE_NAME"].ToString().ToLowerInvariant();
                var linkId = (int?)CastDbNull.To<decimal?>(row["LINK_ID"]);
                var info = new RelationInfo
                {
                    LinkId = linkId ?? (int)(decimal)row["BACK_RELATED_ATTRIBUTE_ID"],
                    IsManyToMany = linkId.HasValue
                };

                attributeHash[key] = info;
                if (key == nameKey)
                {
                    result = info;
                }
            }

            lock (GetLockObject(LinkHashKey))
            {
                linkHash[contentKey] = attributeHash;
            }

            return result;
        }

        internal int AddItemHashEntry(string itemKey)
        {
            var cmd = DbConnector.CreateDbCommand($@"SELECT CONTENT_ID FROM CONTENT_ITEM {DbConnector.WithNoLock} WHERE content_item_id = @itemId");
            cmd.Parameters.AddWithValue("@itemId", int.Parse(itemKey));
            var dt = DbConnector.GetRealData(cmd);
            var contentId = dt.Rows.Count == 0 ? 0 : int.Parse(dt.Rows[0]["CONTENT_ID"].ToString());
            var contentPrefetchKey = "content" + contentId;
            var hash = GetCachedHashTable(ItemHashKey);
            lock (GetLockObject(ItemHashKey))
            {
                hash[itemKey] = contentId;
                if (!hash.ContainsKey(contentPrefetchKey))
                {
                    var prefetchLimit = GetPrefetchLimit().ToString();
                    var top = SqlQuerySyntaxHelper.Top(DbConnector.DatabaseType, prefetchLimit);
                    var limit = SqlQuerySyntaxHelper.Limit(DbConnector.DatabaseType, prefetchLimit);
                    var cmd2 = DbConnector.CreateDbCommand($@"SELECT {top} content_item_id FROM CONTENT_ITEM {DbConnector.WithNoLock} WHERE CONTENT_ID = @Id ORDER BY content_item_id DESC {limit}");
                    cmd2.Parameters.AddWithValue("@Id", contentId);
                    var dt2 = DbConnector.GetRealData(cmd2);
                    foreach (DataRow row in dt2.Rows)
                    {
                        hash[row["content_item_id"].ToString()] = contentId;
                    }

                    hash[contentPrefetchKey] = 1;
                }
            }

            return contentId;
        }

        internal Content AddContentHashEntry(string itemKey)
        {
            var cmd = DbConnector.CreateDbCommand($@"SELECT SITE_ID FROM CONTENT {DbConnector.WithNoLock} WHERE CONTENT_ID = @contentId");
            cmd.Parameters.AddWithValue("@contentId", int.Parse(itemKey));
            var dt = DbConnector.GetRealData(cmd);
            var siteId = dt.Rows.Count == 0 ? 0 : int.Parse(dt.Rows[0]["SITE_ID"].ToString());
            Content content = null;
            AddContentIdHashEntry(siteId.ToString(), itemKey, ref content);
            return content;
        }

        internal Hashtable AddContentIdHashEntry(string siteKey)
        {
            Content content = null;
            return AddContentIdHashEntry(siteKey, string.Empty, ref content);
        }

        internal Hashtable AddContentIdHashEntry(string siteKey, string itemKey, ref Content result)
        {
            var dualHash = GetCachedDualHashTable(ContentHashKey);
            var localHash = new Hashtable();
            lock (GetLockObject(ContentHashKey))
            {
                var cmd = DbConnector.CreateDbCommand($@"{GetContentQuery()} WHERE C.SITE_ID = @Id");
                cmd.Parameters.AddWithValue("@Id", int.Parse(siteKey));
                var dt2 = DbConnector.GetRealData(cmd);
                foreach (DataRow row in dt2.Rows)
                {
                    var current = new Content
                    {
                        Id = (int)(decimal)row["CONTENT_ID"],
                        Name = row["CONTENT_NAME"].ToString(),
                        SiteId = (int)(decimal)row["SITE_ID"],
                        VirtualType = (int)(decimal)row["VIRTUAL_TYPE"]
                    };

                    var linqName = Convert.ToString(row["NET_CONTENT_NAME"]);
                    current.LinqName = !string.IsNullOrEmpty(linqName) ? linqName : DefaultLinqNameGenerator.GetMappedName(current.Name, current.Id, true) + "Article";
                    if (DbConnector.DatabaseType == DatabaseType.SqlServer)
                    {
                        current.MaxVersionNumber = (byte)row["MAX_NUM_OF_STORED_VERSIONS"];
                    }
                    else
                    {
                        current.MaxVersionNumber = (short)row["MAX_NUM_OF_STORED_VERSIONS"];
                    }
                    current.WorkflowId = (int?)CastDbNull.To<decimal?>(row["WORKFLOW_ID"]);

                    var idKey = current.Id.ToString();
                    var nameKey = current.Name.ToLowerInvariant();
                    dualHash.Items[idKey] = current;

                    if (idKey == itemKey)
                    {
                        result = current;
                    }

                    localHash[nameKey] = current.Id;
                }

                dualHash.Ids[siteKey] = localHash;
            }

            return localHash;
        }

        internal ArrayList AddAttributeIdHashEntry(string contentKey)
        {
            ContentAttribute fake = null;
            return AddAttributeIdHashEntry(contentKey, string.Empty, ref fake);
        }

        internal ArrayList AddAttributeIdHashEntry(string contentKey, string itemKey, ref ContentAttribute result)
        {
            lock (GetLockObject(AttributeHashKey))
            {
                var dualHash = GetCachedDualHashTable(AttributeHashKey);
                var cmd = DbConnector.CreateDbCommand($@"{GetAttributeQuery()}WHERE CA.CONTENT_ID = @Id");
                cmd.Parameters.AddWithValue("@Id", int.Parse(contentKey));
                var dt2 = DbConnector.GetRealData(cmd);
                var attrs = new ArrayList(dt2.Rows.Count);
                foreach (DataRow row in dt2.Rows)
                {
                    var id = (int)(decimal)row["attribute_id"];
                    attrs.Add(id);
                    var key = id.ToString();

                    var current = new ContentAttribute
                    {
                        Id = id,
                        Name = row["ATTRIBUTE_NAME"].ToString(),
                        Description = row["DESCRIPTION"].ToString(),
                        Type = (AttributeType)(int)(decimal)row["ATTRIBUTE_TYPE_ID"],
                        Size = (int)(decimal)row["ATTRIBUTE_SIZE"],
                        ContentId = (int)(decimal)row["CONTENT_ID"],
                        SiteId = (int)(decimal)row["SITE_ID"],
                        SubFolder = row["SUBFOLDER"].ToString(),
                        DisableVersionControl = (bool)row["DISABLE_VERSION_CONTROL"],
                        UseSiteLibrary = (bool)row["USE_SITE_LIBRARY"],
                        DbTypeName = row["DATABASE_TYPE"].ToString().ToUpperInvariant(),
                        RelatedImageId = (int?)CastDbNull.To<decimal?>(row["RELATED_IMAGE_ATTRIBUTE_ID"]),
                        ConstraintId = (int?)CastDbNull.To<decimal?>(row["CONSTRAINT_ID"]),
                        RelatedContentId = (int?)CastDbNull.To<decimal?>(row["RELATED_CONTENT_ID"]),
                        DefaultValue = row["DEFAULT_VALUE"].ToString(),
                        LinkId = (int?)CastDbNull.To<decimal?>(row["LINK_ID"]),
                        Required = DBConnector.GetNumBool(row["REQUIRED"]),
                        IsClassifier = (bool)row["IS_CLASSIFIER"],
                        Aggregated = (bool)row["AGGREGATED"],
                        InputMask = row["INPUT_MASK"].ToString()
                    };

                    var linqName = Convert.ToString(row["NET_ATTRIBUTE_NAME"]);
                    current.LinqName = !string.IsNullOrEmpty(linqName) ? linqName : DefaultLinqNameGenerator.GetMappedName(current.Name, current.Id, false);

                    if (row["SOURCE_CONTENT_ID"] != DBNull.Value)
                    {
                        current.SourceAttribute = new SourceAttribute
                        {
                            Id = (int)(decimal)row["PERSISTENT_ATTR_ID"],
                            ContentId = (int)(decimal)row["SOURCE_CONTENT_ID"],
                            UseSiteLibrary = (bool)row["SOURCE_USE_SITE_LIBRARY"]
                        };
                    }

                    if (row["DYNAMIC_IMAGE_ATTRIBUTE_ID"] != DBNull.Value)
                    {
                        if (current.RelatedImageId != null)
                        {
                            current.DynamicImage = new DynamicImageAttribute
                            {
                                Id = current.Id,
                                BaseImageId = (int)current.RelatedImageId,
                                Type = row["TYPE"].ToString(),
                                MaxSize = (bool)row["MAX_SIZE"],
                                Quality = CastDbNull.To<short>(row["QUALITY"]),
                                Width = CastDbNull.To<short>(row["WIDTH"]),
                                Height = CastDbNull.To<short>(row["HEIGHT"])
                            };
                        }
                    }

                    if (row["BASE_RELATION_ATTRIBUTE_ID"] != DBNull.Value)
                    {
                        current.BackRelation = new BackRelation
                        {
                            Id = (int)(decimal)row["BASE_RELATION_ATTRIBUTE_ID"],
                            ContentId = (int)(decimal)row["BASE_RELATION_CONTENT_ID"],
                            Name = row["BASE_RELATION_ATTRIBUTE_NAME"].ToString()
                        };
                    }

                    dualHash.Items[key] = current;
                    if (itemKey == key)
                    {
                        result = current;
                    }
                }

                dualHash.Ids[contentKey] = attrs;
                return attrs;
            }
        }

        internal string AddItemLinkHashEntry(int linkId, string itemId, bool isManyToMany)
        {
            var result = DbConnector.GetRealContentItemLinkIDs(linkId, itemId, isManyToMany);
            lock (GetLockObject(ItemLinkHashKey))
            {
                GetCachedHashTable(ItemLinkHashKey)[GetItemLinkElementHashKey(linkId, itemId, isManyToMany)] = result;
            }

            return result;
        }


        private Hashtable FillPageHashTable()
        {
            var dv = DbConnector.GetAllPages(string.Empty);
            var allPages = new Hashtable(dv.Count);
            foreach (DataRowView drv in dv)
            {
                var key = drv["PAGE_ID"].ToString().ToLowerInvariant();
                if (!allPages.Contains(key))
                {
                    allPages.Add(key, drv["PAGE_FOLDER"].ToString());
                }
            }

            return allPages;
        }

        private Hashtable FillTemplateMapping()
        {
            var dv = DbConnector.GetTemplateMapping(string.Empty);
            var templateMapping = new Hashtable(dv.Count);
            foreach (DataRowView drv in dv)
            {
                var key = $"{drv["PAGE_TEMPLATE_ID1"]},{drv["SITE_ID2"]}";
                if (!templateMapping.Contains(key))
                {
                    templateMapping.Add(key, DBConnector.GetNumInt(drv["PAGE_TEMPLATE_ID2"]));
                }
            }

            return templateMapping;
        }

        private Hashtable FillStatusHashTable()
        {
            var weightSql = $"SELECT MAX(WEIGHT) AS MAX_WEIGHT, SITE_ID FROM STATUS_TYPE {DbConnector.WithNoLock} GROUP BY SITE_ID";
            var sql = $"WITH WEIGHTS AS({weightSql}) SELECT ST.SITE_ID, STATUS_TYPE_ID AS ID, STATUS_TYPE_NAME AS NAME FROM STATUS_TYPE ST {DbConnector.WithNoLock} INNER JOIN WEIGHTS W ON ST.SITE_ID = W.SITE_ID AND ST.WEIGHT = W.MAX_WEIGHT";
            var dt = DbConnector.GetRealData(sql);
            var statuses = new Hashtable(dt.Rows.Count);
            foreach (DataRow row in dt.Rows)
            {
                var key = DBConnector.GetNumInt(row["SITE_ID"]);
                if (!statuses.ContainsKey(key))
                {
                    statuses.Add(key, new StatusType { Id = DBConnector.GetNumInt(row["ID"]), Name = row["NAME"].ToString() });
                }
            }

            return statuses;
        }

        private Hashtable FillSiteIdHashTable()
        {
            var dt = DbConnector.GetRealData("SELECT SITE_ID, SITE_NAME FROM SITE");
            var siteIds = new Hashtable(dt.Rows.Count);
            foreach (DataRow row in dt.Rows)
            {
                siteIds[row["SITE_NAME"].ToString().ToLowerInvariant()] = DBConnector.GetNumInt(row["SITE_ID"]);
            }

            return siteIds;
        }

        private Hashtable FillSiteHashTable()
        {
            var dt = DbConnector.GetRealData("SELECT SITE_NAME, SITE_ID, DNS, STAGE_DNS, LIVE_DIRECTORY, STAGE_DIRECTORY, ASSEMBLY_PATH, STAGE_ASSEMBLY_PATH, UPLOAD_DIR, TEST_DIRECTORY, UPLOAD_URL, UPLOAD_URL_PREFIX, LIVE_VIRTUAL_ROOT, STAGE_VIRTUAL_ROOT, STAGE_EDIT_FIELD_BORDER, ASSEMBLE_FORMATS_IN_LIVE, USE_ABSOLUTE_UPLOAD_URL, ALLOW_USER_SESSIONS, IS_LIVE, SCRIPT_LANGUAGE, CONTEXT_CLASS_NAME, ENABLE_ONSCREEN, REPLACE_URLS_IN_DB FROM SITE");
            var sites = new Hashtable(dt.Rows.Count);
            foreach (DataRow row in dt.Rows)
            {
                var key = DBConnector.GetNumInt(row["SITE_ID"]);
                sites[key] = new Site
                {
                    Name = row["SITE_NAME"].ToString(),
                    Dns = row["DNS"].ToString(),
                    StageDns = row["STAGE_DNS"].ToString(),
                    LiveDirectory = row["LIVE_DIRECTORY"].ToString(),
                    StageDirectory = row["STAGE_DIRECTORY"].ToString(),
                    UploadDir = row["UPLOAD_DIR"].ToString(),
                    TestDirectory = row["TEST_DIRECTORY"].ToString(),
                    AssemblyDirectory = row["ASSEMBLY_PATH"].ToString(),
                    StageAssemblyDirectory = row["STAGE_ASSEMBLY_PATH"].ToString(),
                    UploadUrl = row["UPLOAD_URL"].ToString(),
                    UploadUrlPrefix = row["UPLOAD_URL_PREFIX"].ToString(),
                    LiveVirtualRoot = row["LIVE_VIRTUAL_ROOT"].ToString(),
                    StageVirtualRoot = row["STAGE_VIRTUAL_ROOT"].ToString(),
                    FieldBorderMode = DBConnector.GetNumInt(row["STAGE_EDIT_FIELD_BORDER"]),
                    AssembleFormatsInLive = (bool)row["ASSEMBLE_FORMATS_IN_LIVE"],
                    UseAbsoluteUploadUrl = DBConnector.GetNumBool(row["USE_ABSOLUTE_UPLOAD_URL"]),
                    AllowUserSessions = DBConnector.GetNumBool(row["ALLOW_USER_SESSIONS"]),
                    ContextClassName = DBConnector.GetString(row["CONTEXT_CLASS_NAME"], "QPDataContext"),
                    IsLive = row["IS_LIVE"].ToString() == "1",
                    ScriptLanguage = row["SCRIPT_LANGUAGE"].ToString(),
                    EnableOnScreen = (bool)row["ENABLE_ONSCREEN"],
                    ReplaceUrlsInDB = (bool)row["REPLACE_URLS_IN_DB"]
                };
            }

            return sites;
        }

        private Hashtable FillHashTable(string key)
        {
            if (string.Equals(key, PageHashKey))
            {
                return FillPageHashTable();
            }

            if (string.Equals(key, TemplateMappingHashKey))
            {
                return FillTemplateMapping();
            }

            if (string.Equals(key, ContentIdForLinqHashKey))
            {
                return new Hashtable();
            }

            if (string.Equals(key, StatusHashKey))
            {
                return FillStatusHashTable();
            }

            if (string.Equals(key, SiteHashKey))
            {
                return FillSiteHashTable();
            }

            if (string.Equals(key, SiteIdHashKey))
            {
                return FillSiteIdHashTable();
            }

            if (string.Equals(key, LinkHashKey))
            {
                return new Hashtable();
            }

            if (string.Equals(key, LinkForLinqHashKey))
            {
                return new Hashtable();
            }

            if (string.Equals(key, AttributeIdForLinqHashKey))
            {
                return new Hashtable();
            }

            if (string.Equals(key, ItemLinkHashKey))
            {
                return new Hashtable();
            }

            if (string.Equals(key, ItemHashKey))
            {
                return new Hashtable();
            }

            throw new Exception("Incorrect key for saved hashtable: " + key);
        }

        private static DualHashTable FillDualHashTable(string key) => new DualHashTable();

        public T GetCachedEntity<T>(string key, Func<T> fillAction)
            where T : class =>
            GetCachedEntity(key, GetInternalExpirationTime(key), fillAction);

        public T GetCachedEntity<T>(string key, Func<string, T> fillAction)
            where T : class =>
            GetCachedEntity(key, GetInternalExpirationTime(key), fillAction);

        public T GetCachedEntity<T>(string key, double cacheInterval, Func<string, T> fillAction)
            where T : class
        {
            T ht;
            if (!DbConnector.CacheData)
            {
                ht = fillAction.Invoke(key);
            }
            else
            {
                var obj = GetDataFromCache(key);
                if (obj == NullObject)
                {
                    return null;
                }

                ht = (T)obj;
                if (ht == null)
                {
                    lock (GetLockObject(key))
                    {
                        ht = GetDataFromCache<T>(key);
                        if (ht == null)
                        {
                            ht = fillAction.Invoke(key);
#if ASPNETCORE || NETCORE
                            AddEntityToCache(key, ht, cacheInterval);
#else
                            AddEntityToCache(key, ht, cacheInterval, null);
#endif
                        }
                    }
                }
            }

            return ht;
        }

        public T GetCachedEntity<T>(string key, double cacheInterval, Func<T> fillAction)
            where T : class
        {
            T ht;
            if (!DbConnector.CacheData)
            {
                ht = fillAction.Invoke();
            }
            else
            {
                var obj = GetDataFromCache(key);
                if (obj == NullObject)
                {
                    return null;
                }

                ht = (T)obj;
                if (ht == null)
                {
                    lock (GetLockObject(key))
                    {
                        ht = GetDataFromCache<T>(key);
                        if (ht == null)
                        {
                            ht = fillAction.Invoke();
#if ASPNETCORE || NETCORE
                            AddEntityToCache(key, ht, cacheInterval);
#else
                            AddEntityToCache(key, ht, cacheInterval, null);
#endif
                        }
                    }
                }
            }

            return ht;
        }

        internal DataTable GetQueryResult(IQueryObject obj, out long totalRecords)
        {
            QueryResult qr;
            if (!DbConnector.CacheData || !obj.CacheResult)
            {
                qr = DbConnector.GetFilledDataTable(obj);
            }
            else
            {
                var key = obj.GetKey(CacheKeyPrefix);
                qr = GetDataFromCache<QueryResult>(key);
                if (qr == null || obj.WithReset)
                {
                    lock (GetLockObject(key))
                    {
                        qr = GetDataFromCache<QueryResult>(key);
                        if (qr == null || obj.WithReset)
                        {
                            qr = DbConnector.GetFilledDataTable(obj);
#if ASPNETCORE || NETCORE
                            AddEntityToCache(key, qr, obj.CacheInterval);
#else
                            AddEntityToCache(key, qr, obj.CacheInterval, null);
#endif
                        }
                    }
                }
            }

            totalRecords = qr.TotalRecords;
            return qr.DataTable;
        }

        private double GetInternalExpirationTime(string cacheKey)
        {
            if (cacheKey == ItemLinkHashKey)
            {
                return GetShortExpirationTime();
            }

            return cacheKey == ItemHashKey ? GetLongExpirationTime() : GetExpirationTime(DbConnector.DbConnectorSettings.InternalExpirationTime);
        }

        private double GetShortExpirationTime() => GetExpirationTime(DbConnector.DbConnectorSettings.InternalShortExpirationTime, DefaultShortExpirationTime);

        private double GetLongExpirationTime() => GetExpirationTime(DbConnector.DbConnectorSettings.InternalLongExpirationTime, DefaultLongExpirationTime);

        private double GetExpirationTime(string value, double defaultValue = DefaultExpirationTime)
        {
            if (double.TryParse(value, out var result))
            {
                if (result < MinExpirationTime)
                {
                    result = MinExpirationTime;
                }

                return result;
            }

            return defaultValue;
        }

        private int GetPrefetchLimit()
        {
            var prefetchLimitString = DbConnector.DbConnectorSettings.PrefetchLimit;
            if (int.TryParse(prefetchLimitString, out var result))
            {
                if (result < 1)
                {
                    result = DefaultPrefetchLimit;
                }

                return result;
            }

            return DefaultPrefetchLimit;
        }

        public string CacheKeyPrefix => "QA.dll." + DbConnector.InstanceCachePrefix;

        public string GetDataKeyPrefix => $"{CacheKeyPrefix}GetData.";

        public string FileContentsCacheKeyPrefix => CacheKeyPrefix + ".FileContents.";

        public string ConstraintKey => $"{CacheKeyPrefix}constraintList";

        public string StatusKey => $"{CacheKeyPrefix}statusList";

#if !ASPNETCORE && !NETCORE
        private string GetPageObjectKey(int pageId) => $"{CacheKeyPrefix}pageObjects{pageId}";
#endif

#if !ASPNETCORE && !NETCORE
        private string GetPageObjectHashKey(int pageId) => $"{CacheKeyPrefix}pageObjectsHash{pageId}";
#endif

        public string AllPageObjectsKey => $"{CacheKeyPrefix}allPageObjects";

        public string AllPageObjectsHashKey => $"{CacheKeyPrefix}allPageObjectsHash";

        public string AllTemplateObjectsKey => $"{CacheKeyPrefix}allTemplateObjects";

        public string AllTemplateObjectsHashKey => $"{CacheKeyPrefix}allTemplateObjectsHash";

        public string TemplateObjectKey => $"{CacheKeyPrefix}templateObjects";

#if !ASPNETCORE && !NETCORE
        private string GetTemplateObjectHashKey(int pageTemplateId) => $"{CacheKeyPrefix}templateObjectsHash{pageTemplateId}";
#endif

        public string TemplateKey => $"{CacheKeyPrefix}templates";

        public string TemplateHashKey => $"{CacheKeyPrefix}templatesHash";

        public string AllTemplatesKey => $"{CacheKeyPrefix}allTemplates";

        public string AllTemplatesHashKey => $"{CacheKeyPrefix}allTemplatesHash";

        public string PageKey => $"{CacheKeyPrefix}pages";

        public string PageHashKey => $"{CacheKeyPrefix}pagesHash";

        public string AllPagesKey => $"{CacheKeyPrefix}allPages";

        public string AllPagesHashKey => $"{CacheKeyPrefix}allPagesHash";

        public string TemplateMappingKey => $"{CacheKeyPrefix}templateMapping";

        public string TemplateMappingHashKey => $"{CacheKeyPrefix}templateMappingHash";

        public string PageMappingKey => $"{CacheKeyPrefix}pageMapping";

#if !ASPNETCORE && !NETCORE
        private string GetPageMappingHashKey(int pageId) => $"{CacheKeyPrefix}pageMappingHash{pageId}";
#endif

        public string ContentHashKey => $"{CacheKeyPrefix}contentHash";

        public string ContentIdForLinqHashKey => $"{CacheKeyPrefix}contentIdForLinqHash";

        public string LinkHashKey => $"{CacheKeyPrefix}linkHash";

        public string LinkForLinqHashKey => $"{CacheKeyPrefix}linkForLinqHash";

        public string ItemLinkHashKey => $"{CacheKeyPrefix}itemLinkHash";

        public string GetItemLinkElementHashKey(int linkId, string itemId, bool isManyToMany)
        {
            var hashKeyPart = isManyToMany ? "link" : "attribute";
            return $"item{itemId}{hashKeyPart}{linkId}";
        }

        public string ItemHashKey => $"{CacheKeyPrefix}itemHash";

        public string StatusHashKey => $"{CacheKeyPrefix}statusHash";

        public string SiteHashKey => $"{CacheKeyPrefix}siteHash";

        public string SiteIdHashKey => $"{CacheKeyPrefix}siteIdHash";

        public string AttributeHashKey => $"{CacheKeyPrefix}attributeHash";

        public string AttributeIdForLinqHashKey => $"{CacheKeyPrefix}attributeIdForLinqHash";


    }
}
