using System;
using System.Collections;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Quantumart.QPublishing.Helpers;
using Quantumart.QPublishing.Info;
using Microsoft.AspNetCore.Http;

using Quantumart.QP8.Assembling;

// ReSharper disable once CheckNamespace
namespace Quantumart.QPublishing.Database
{
    // ReSharper disable once InconsistentNaming
    public partial class DBConnector
    {

        public DbConnectorSettings DbConnectorSettings { get; }

        internal static readonly int LegacyNotFound = -1;

        public HttpContext HttpContext { get; }

        public static string Version => Assembly.GetExecutingAssembly().GetName().Version.ToString();


        #region live and stage
        private bool? _isStage;

        public bool IsStage
        {
            get
            {
                if (_isStage.HasValue)
                {
                    return _isStage.Value;
                }


                return !CheckIsLive();
            }
            set => _isStage = value;
        }

        public bool CheckIsLive() => DbConnectorSettings.IsLive;

        #endregion


        #region formatters

        internal string UploadPlaceHolder => "<%=upload_url%>";

        internal string SitePlaceHolder => "<%=site_url%>";

        internal string UploadBindingPlaceHolder => "<%#upload_url%>";

        internal string SiteBindingPlaceHolder => "<%#site_url%>";


        public static string GetString(object obj, string defaultValue)
        {
            var result = Convert.ToString(obj);
            return string.IsNullOrEmpty(result) ? defaultValue : result;
        }

        public static bool GetNumBool(object obj) => Convert.ToBoolean(obj);

        public static int GetNumInt(object obj) => Convert.ToInt32(obj);

        public string FormatField(string input, int siteId, bool isLive)
        {
            var result = input;
            var uploadUrl = GetImagesUploadUrl(siteId, true);
            result = result.Replace(UploadPlaceHolder, uploadUrl);
            result = result.Replace(UploadBindingPlaceHolder, uploadUrl);

            var siteUrl = DbConnectorSettings.UseAbsoluteSiteUrl ? GetSiteUrl(siteId, isLive) : GetSiteUrlRel(siteId, isLive);


            result = result.Replace(SitePlaceHolder, siteUrl);
            result = result.Replace(SiteBindingPlaceHolder, siteUrl);

            return result;
        }

        public string FormatField(string input, int siteId) => FormatField(input, siteId, !IsStage);

        public string ReplaceCommas(string str)
        {
            var sb = new StringBuilder(str);
            sb.Replace(",", ".");
            return sb.ToString();
        }

        #endregion


        #region site props

        public bool IsLive(int siteId) => GetSite(siteId)?.IsLive ?? true;

        public bool ForceLive(int siteId) => GetSite(siteId)?.AssembleFormatsInLive ?? false;

        public bool GetAllowUserSessions(int siteId) => GetSite(siteId)?.AllowUserSessions ?? true;

        public bool GetEnableOnScreen(int siteId) => GetSite(siteId)?.EnableOnScreen ?? false;

        public bool GetReplaceUrlsInDB(int siteId) => GetSite(siteId)?.ReplaceUrlsInDB ?? false;

        #endregion


        #region misc queries

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

        #endregion


        #region workflow and statuses

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

        #endregion


        #region search
        public DataTable GetSearchResults(string expression, bool useMorphology, int startPos, long recordCount, string tabname, int minRank, ref long totalRecords)
        {
            const string searchSp = "qp_fulltextSiteSearch";
            const string strSql = "SELECT count(*) from sysobjects where name = '" + searchSp + "'";

            var dt = new DataTable();
            var ds = new DataSet();
            var useMorphologyInt = useMorphology ? 1 : 0;
            if (GetCachedData(strSql).Rows.Count > 0)
            {
                var adapter = CreateDbAdapter();
                var connection = GetActualConnection();
                try
                {
                    if (connection.State == ConnectionState.Closed)
                    {
                        connection.Open();
                    }

                    adapter.SelectCommand = CreateDbCommand(searchSp, connection);
                    adapter.SelectCommand.Transaction = GetActualTransaction();
                    adapter.SelectCommand.CommandType = CommandType.StoredProcedure;
                    adapter.SelectCommand.Parameters.AddWithValue("@tabname", tabname);
                    adapter.SelectCommand.Parameters.AddWithValue("@use_morphology", useMorphologyInt);
                    adapter.SelectCommand.Parameters.AddWithValue("@expression", expression.ToLower());
                    adapter.SelectCommand.Parameters.AddWithValue("@minrank", minRank);
                    adapter.SelectCommand.Parameters.AddWithValue("@startpos", startPos);
                    adapter.SelectCommand.Parameters.AddWithValue("@count", recordCount);
                    adapter.AcceptChangesDuringFill = false;
                    adapter.Fill(ds);
                }
                finally
                {
                    if (NeedToDisposeActualConnection)
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
                var adapter = CreateDbAdapter();
                var connection = GetActualConnection();
                try
                {
                    if (connection.State == ConnectionState.Closed)
                    {
                        connection.Open();
                    }

                    adapter.SelectCommand = CreateDbCommand(searchSp, connection);
                    adapter.SelectCommand.Transaction = GetActualTransaction();
                    adapter.SelectCommand.CommandType = CommandType.StoredProcedure;
                    adapter.SelectCommand.Parameters.AddWithValue("@tabname", tabname);
                    adapter.SelectCommand.Parameters.AddWithValue("@use_morphology", useMorphologyInt);
                    adapter.SelectCommand.Parameters.AddWithValue("@expression",  expression.ToLower());
                    adapter.SelectCommand.Parameters.AddWithValue("@minrank",  minRank);
                    adapter.SelectCommand.Parameters.AddWithValue("@startpos", startPos);
                    adapter.SelectCommand.Parameters.AddWithValue("@count", recordCount);
                    adapter.SelectCommand.Parameters.AddWithValue("@startDate", startDate);
                    adapter.SelectCommand.Parameters.AddWithValue("@endDate", endDate);
                    adapter.SelectCommand.Parameters.AddWithValue("@showStatic", showStaticContent);
                    adapter.AcceptChangesDuringFill = false;
                    adapter.Fill(ds);
                }
                finally
                {
                    if (NeedToDisposeActualConnection)
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

        #endregion


        #region mapping

        private bool _throwLoadFileExceptions = true;

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

            var mapping = new AssembleContentsController(siteId, InstanceConnectionString, DatabaseType) { IsLive = isLive }.GetMapping(contextName);
            if (string.IsNullOrEmpty(mapping))
            {
                throw new ApplicationException($"Cannot receive mapping for context '{contextName}' from site (ID={siteId})");
            }

            return mapping;
        }

        public string GetMapFileContents(int siteId, bool isLive, string fileName) => GetCachedFileContents(Path.Combine(GetAppDataDirectory(siteId, isLive), fileName));

        public string GetDefaultMapFileContents(int siteId, string contextName = null) => GetDefaultMapFileContents(siteId, !IsStage, contextName);

        public string GetMapFileContents(int siteId, string fileName) => GetMapFileContents(siteId, !IsStage, fileName);


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

        #endregion
    }
}
