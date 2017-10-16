using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Quantumart.QPublishing.Database;

// ReSharper disable once CheckNamespace
namespace Quantumart.QPublishing.Info
{
    public class ContentDataQueryObject : IQueryObject
    {
        public DBConnector DbConnector { get; set; }

        public bool GetCount { get; set; }

        public bool GetCountInTable => true;

        public int ContentId { get; set; }

        public string SiteName { get; set; }

        public string ContentName { get; set; }

        public string Fields { get; set; }

        public string Where { get; set; }

        public string OrderBy { get; set; }

        public string ExtraFrom { get; set; }

        public long StartRow { get; set; }

        public long PageSize { get; set; }

        public byte UseSchedule { get; set; }

        public string StatusName { get; set; }

        public byte ShowSplittedArticle { get; set; }

        public byte IncludeArchive { get; set; }

        public long UserId { get; set; }

        public long GroupId { get; set; }

        public long StartLevel { get; set; }

        public long EndLevel { get; set; }

        public bool FilterRecords { get; set; }

        public bool UseClientSelection { get; set; }

        public bool UseSecurity { get; set; }

        public bool CacheResult { get; set; }

        public double CacheInterval { get; set; }

        public bool WithReset { get; set; }

        public List<SqlParameter> Parameters { get; set; }

        public string CountSql { get; private set; }

        private const string InsertKey = "<$_security_insert_$>";
        private const string DefaultStatusName = "Published";
        private const string DefaultOrderBy = "c.content_item_id";
        private static readonly Regex DescRegex = new Regex(@"desc$", RegexOptions.IgnoreCase);
        private static readonly Regex AscRegex = new Regex(@"asc$", RegexOptions.IgnoreCase);
        private static readonly Regex CRegex = new Regex(@"^c\.", RegexOptions.IgnoreCase);

        public string WhereExpression => UseClientSelection ? string.Empty : Where;

        public string OrderByExpression => UseClientSelection ? "CONTENT_ITEM_ID ASC" : OrderBy;

        public long StartRowExpression => UseClientSelection ? 1 : StartRow;

        public long PageSizeExpression => UseClientSelection ? 0 : PageSize;

        public bool IsFirstPage => StartRowExpression <= 1;

        public ContentDataQueryObject(DBConnector dbConnector, int contentId, string fields, string whereExpression, string orderExpression, long startRow, long pageSize, byte useSchedule, string statusName, byte showSplittedArticle, byte includeArchive, bool cacheResult, double cacheInterval, bool useClientSelection, bool withReset)
            : this(dbConnector, string.Empty, string.Empty, fields, whereExpression, orderExpression, startRow, pageSize, useSchedule, statusName, showSplittedArticle, includeArchive, cacheResult, cacheInterval, useClientSelection, withReset)
        {
            ContentId = contentId;
        }

        public ContentDataQueryObject(DBConnector dbConnector, int contentId, string fields, string whereExpression, string orderExpression, long startRow, long pageSize)
            : this(dbConnector, string.Empty, string.Empty, fields, whereExpression, orderExpression, startRow, pageSize)
        {
            ContentId = contentId;
        }

        public ContentDataQueryObject(DBConnector dbConnector, string siteName, string contentName, string fields, string whereExpression, string orderExpression, long startRow, long pageSize)
            : this(dbConnector, siteName, contentName, fields, whereExpression, orderExpression, startRow, pageSize, 1, DefaultStatusName, 0, 0, false, 0, false, false)
        {
        }

        public ContentDataQueryObject(DBConnector dbConnector, string siteName, string contentName, string fields, string whereExpression, string orderExpression, long startRow, long pageSize, byte useSchedule, string statusName, byte showSplittedArticle, byte includeArchive, bool cacheResult, double cacheInterval, bool useClientSelection, bool withReset)
        {
            Parameters = new List<SqlParameter>();
            SiteName = siteName;
            ContentName = contentName;
            Fields = fields;
            Where = whereExpression;
            OrderBy = orderExpression;
            StartRow = startRow;
            PageSize = pageSize;
            UseSchedule = useSchedule;
            StatusName = statusName;
            ShowSplittedArticle = showSplittedArticle;
            IncludeArchive = includeArchive;
            DbConnector = dbConnector;
            UseClientSelection = useClientSelection;
            CacheResult = cacheResult;
            CacheInterval = cacheInterval;
            WithReset = withReset;
            GetCount = true;
        }

        public SqlCommand GetSqlCommand()
        {
            int contentId;
            var siteId = 0;
            if (ContentId != 0)
            {
                contentId = ContentId;
            }
            else
            {
                siteId = DbConnector.GetSiteId(SiteName);
                if (siteId == 0)
                {
                    throw new ApplicationException($"Site '{SiteName}' is not found");
                }

                contentId = DbConnector.GetContentId(DbConnector.GetSiteId(SiteName), ContentName);
                if (contentId == 0)
                {
                    throw new ApplicationException($"Content '{SiteName}.{ContentName}' is not found");
                }
            }

            var select = GetSqlCommandSelect(contentId);
            var from = GetSqlCommandFrom(contentId);
            if (UseSecurity)
            {
                from = from.Replace(InsertKey, DbConnector.GetSecuritySql(contentId, UserId, GroupId, StartLevel, EndLevel));
            }

            var where = GetSqlCommandWhere(siteId);
            var orderBy = GetSqlCommandOrderBy();
            var startRow = StartRowExpression <= 0 ? 1 : StartRowExpression;
            var endRow = new long[] { 0, int.MaxValue, int.MaxValue - 1 }.Contains(PageSizeExpression) ? 0 : startRow + PageSizeExpression - 1;
            CountSql = $"SELECT cast(COUNT(*) as bigint) FROM {from} WHERE {where}";

            var sb = new StringBuilder();
#if ASPNETCORE
            if (string.IsNullOrEmpty(DbConnector.DbConnectorSettings.Sql2012ModeDll) || GetCount)
#else
            if (string.IsNullOrEmpty(DbConnector.AppSettings["Sql2012ModeDll"]) || GetCount)
#endif
            {
                sb.AppendLine("WITH PAGED_DATA_CTE AS");
                sb.AppendLine("(");
                sb.AppendLine($"	SELECT c.*, ROW_NUMBER() OVER (ORDER BY {orderBy}) AS ROW_NUMBER, {(GetCount ? "COUNT(*) OVER()" : "0")} AS ROWS_COUNT");
                sb.AppendLine($"	FROM ( SELECT {select} FROM {from} WHERE {where} ) AS c");
                sb.AppendLine(")");
                sb.AppendLine("SELECT * FROM PAGED_DATA_CTE");
                if (endRow > 0 || startRow > 1)
                {
                    sb.AppendLine(" WHERE 1 = 1");
                    if (startRow > 1)
                    {
                        sb.AppendLine(" AND ROW_NUMBER >= @startRow");
                        Parameters.Add(new SqlParameter("@startRow", SqlDbType.Int) { Value = startRow });
                    }
                    if (endRow > 0)
                    {
                        sb.AppendLine(" AND ROW_NUMBER <= @endRow");
                        Parameters.Add(new SqlParameter("@endRow", SqlDbType.Int) { Value = endRow });
                    }
                }
                sb.AppendLine("ORDER BY ROW_NUMBER ASC");
            }
            else
            {
                sb.AppendLine($"SELECT {select} FROM {from} WHERE {where} ");
                sb.Append("ORDER BY ");
                sb.AppendLine(orderBy);
                if (endRow > 0 || startRow > 1)
                {
                    if (endRow != int.MaxValue)
                    {
                        Parameters.Add(new SqlParameter("@startRow", SqlDbType.Int) { Value = startRow - 1 });
                        Parameters.Add(new SqlParameter("@endRow", SqlDbType.Int) { Value = endRow });
                        sb.AppendLine(@"OFFSET @startRow ROWS FETCH NEXT @endRow - @startRow ROWS ONLY");
                    }
                    else
                    {
                        if (startRow > 1)
                        {
                            Parameters.Add(new SqlParameter("@startRow", SqlDbType.Int) { Value = startRow - 1 });
                            sb.AppendLine(@"OFFSET @startRow ROWS");
                        }
                    }
                }
            }

            var cmd = new SqlCommand
            {
                CommandText = sb.ToString(),
                CommandType = CommandType.Text
            };

            if (Parameters != null)
            {
                foreach (var param in Parameters)
                {
                    cmd.Parameters.Add(param);
                }
            }

            return cmd;
        }

        private string GetSqlCommandOrderBy() => !string.IsNullOrEmpty(OrderByExpression) ? OrderByExpression : DefaultOrderBy;

        private string GetSqlCommandFrom(int contentId)
        {
            var tableSuffix = ShowSplittedArticle == 0 ? "" : "_united";
            var from = "content_" + contentId + tableSuffix + " as c WITH(NOLOCK) ";
            if (UseSecurity)
            {
                if (FilterRecords)
                {
                    from += $" INNER JOIN ({InsertKey}) as pi on c.content_item_id = pi.content_item_id ";
                }
                else
                {
                    from += $" LEFT OUTER JOIN ({InsertKey}) as pi on c.content_item_id = pi.content_item_id ";
                }
            }

            if (!string.IsNullOrEmpty(ExtraFrom))
            {
                from += " " + ExtraFrom;
            }

            return from;
        }

        private string GetSqlCommandWhere(int siteId)
        {
            var whereBuilder = new StringBuilder(!string.IsNullOrEmpty(WhereExpression) ? WhereExpression : "1 = 1");
            if (UseSchedule == 1)
            {
                whereBuilder.Append(" and c.visible = 1");
            }

            if (IncludeArchive == 0)
            {
                whereBuilder.Append(" and c.archive = 0");
            }

            whereBuilder.AppendFormat(" and c.status_type_id in ({0})", GetSqlCommandStatusString(siteId));
            return whereBuilder.ToString();
        }

        private string GetSqlCommandStatusString(int siteId)
        {
            string statusString;
            if (string.IsNullOrEmpty(StatusName) && siteId != 0)
            {
                statusString = DbConnector.GetMaximumWeightStatusTypeId(siteId).ToString();
            }
            else
            {
                var statusName = !string.IsNullOrEmpty(StatusName) ? StatusName : DefaultStatusName;
                var filterStatuses = siteId != 0 && !string.Equals(statusName, DefaultStatusName, StringComparison.InvariantCultureIgnoreCase);
                var statuses = !filterStatuses
                    ? null
                    : new HashSet<string>(DbConnector.GetStatuses(string.Empty).OfType<DataRowView>().Select(rowView => Convert.ToString(rowView.Row["STATUS_TYPE_NAME"]).ToLowerInvariant()));

                bool Lambda(string n) => statuses == null || statuses.Contains(n.ToLowerInvariant());
                var resultStatuses = statusName.Split(',').Select(n => n.Trim()).Where(Lambda).ToArray();
                if (!resultStatuses.Any())
                {
                    throw new ApplicationException($"None of the given statuses ({statusName}) has been found");
                }

                var statusParams = new string[resultStatuses.Length];
                for (var i = 0; i < resultStatuses.Length; i++)
                {
                    var paramName = "@status" + i;
                    Parameters.Add(new SqlParameter(paramName, SqlDbType.NVarChar, 255) { Value = resultStatuses[i] });
                    statusParams[i] = paramName;
                }

                statusString = "select status_type_id from status_type where status_type_name in (" + string.Join(", ", statusParams) + ")";
            }

            return statusString;
        }

        private string GetSqlCommandSelect(int contentId)
        {
            string select = null;

            if (!string.IsNullOrEmpty(Fields))
            {
                var orderBy = GetSqlCommandOrderBy();
                var orderByAttrs = string.IsNullOrEmpty(orderBy)
                    ? new string[] { }
                    : orderBy
                        .Split(',')
                        .Select(n => n.Trim())
                        .Select(n => CRegex.Replace(AscRegex.Replace(DescRegex.Replace(n, ""), ""), ""))
                        .Select(n => n.Trim().Replace("[", "").Replace("]", ""))
                        .ToArray();

                var attrs = new HashSet<string>(
                    DbConnector.GetContentAttributeObjects(contentId)
                        .Select(n => n.Name.ToLowerInvariant())
                        .Union(new[] { "content_item_id", "archive", "visible", "created", "modified", "last_modified_by" })
                );

                select = string.Join(", ", Fields
                    .Split(',')
                    .Select(n => n.Trim().Replace("[", "").Replace("]", ""))
                    .Union(orderByAttrs, StringComparer.InvariantCultureIgnoreCase)
                    .Where(n => attrs.Contains(n.ToLowerInvariant()))
                    .Select(n => "[" + n + "]")
                    .ToArray()
                );
            }

            if (string.IsNullOrEmpty(select))
            {
                select = "c.*";
            }

            if (UseSecurity && !FilterRecords)
            {
                select += ", IsNull(pi.permission_level, 0) as current_permission_level ";
            }

            return select;
        }

        public string GetKey(string prefix) => $"{prefix}GetContentData.::{SiteName}::{ContentName}::{Fields}::{WhereExpression}::{OrderByExpression}::{StartRowExpression}::{PageSizeExpression}::{UseSchedule}::{StatusName}::{ShowSplittedArticle}::{IncludeArchive}::{UserId}::{GroupId}::{StartLevel}::{EndLevel}::{FilterRecords}";

        public string OutputParamName => "@total_records";
    }
}
