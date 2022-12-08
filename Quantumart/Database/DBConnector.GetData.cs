using System;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Globalization;
using Quantumart.QPublishing.Info;
using NLog.Fluent;

// ReSharper disable once CheckNamespace
namespace Quantumart.QPublishing.Database
{
    // ReSharper disable once InconsistentNaming
    public partial class DBConnector
    {
        public DataTable GetDataViaDataSet(string queryString)
        {
            var dataset = new DataSet();
            var arr = queryString.Split(';');
            var connection = GetActualConnection();

            try
            {
                if (connection.State == ConnectionState.Closed)
                {
                    connection.Open();
                }

                var adapter = CreateDbAdapter();
                adapter.AcceptChangesDuringFill = false;
                dataset.EnforceConstraints = false;
                adapter.SelectCommand = CreateDbCommand(queryString, connection);
                adapter.SelectCommand.Transaction = GetActualTransaction();

                int i;
                for (i = arr.GetLowerBound(0); i <= arr.GetUpperBound(0); i++)
                {
                    dataset.Tables.Add("QTable" + i);
                }

                int j;
                for (j = 0; j <= i - 1; j++)
                {
                    dataset.Tables[j].BeginLoadData();
                    adapter.Fill(dataset.Tables[j]);
                    dataset.Tables[j].EndLoadData();
                    dataset.Tables[j].AcceptChanges();
                }

                dataset.EnforceConstraints = true;
                adapter.AcceptChangesDuringFill = true;
                return dataset.Tables[0];
            }
            finally
            {
                if (NeedToDisposeActualConnection)
                {
                    connection.Dispose();
                }
            }
        }

        public DataTable GetRealData(string queryString)
        {
            var cmd = CreateDbCommand(queryString);
            return GetRealData(cmd);
        }

        public DataTable GetRealData(DbCommand cmd) => GetRealData(cmd, GetActualConnection(), GetActualTransaction(), NeedToDisposeActualConnection);

        public DataTable GetRealData(DbCommand cmd, DbConnection cn, DbTransaction tr, bool disposeConnection)
        {
            try
            {
                if (cn.State == ConnectionState.Closed)
                {
                    cn.Open();
                }

                cmd.Connection = cn;
                cmd.Transaction = tr;

                var adapter = CreateDbAdapter();
                adapter.SelectCommand = cmd;

                return GetFilledDataTable(adapter);
            }
            finally
            {
                if (disposeConnection)
                {
                    cn.Dispose();
                }
            }
        }

        public object GetRealScalarData(DbCommand command)
        {
            var connection = GetActualConnection();
            try
            {
                if (connection.State == ConnectionState.Closed)
                {
                    connection.Open();
                }

                command.Connection = connection;
                command.Transaction = GetActualTransaction();
                return command.ExecuteScalar();
            }
            finally
            {
                if (NeedToDisposeActualConnection)
                {
                    connection.Dispose();
                }
            }
        }

        public DataTable GetData(string queryString) => GetData(queryString, 0, true);

        public DataTable GetData(string queryString, double cacheInterval) => GetData(queryString, cacheInterval, false);

        private DataTable GetData(string queryString, double cacheInterval, bool useDefaultInterval) => DbConnectorSettings.CacheGetData

            ? GetCachedData(queryString, cacheInterval, useDefaultInterval)
            : GetRealData(queryString);

        public DataTable GetCachedData(string queryString) => CacheManager.GetCachedTable(CacheManager.GetDataKeyPrefix + queryString);

        public DataTable GetCachedData(string queryString, double cacheInterval) => CacheManager.GetCachedTable(CacheManager.GetDataKeyPrefix + queryString, cacheInterval);

        private DataTable GetCachedData(string queryString, double cacheInterval, bool useDefaultInterval) => useDefaultInterval
            ? CacheManager.GetCachedTable(CacheManager.GetDataKeyPrefix + queryString)
            : CacheManager.GetCachedTable(CacheManager.GetDataKeyPrefix + queryString, cacheInterval);

        public string GetSecuritySql(int contentId, long userId, long groupId, long startLevel, long endLevel)
        {
            string result;
            var cmd = CreateDbCommand("qp_GetPermittedItemsAsQuery");
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.Add(new SqlParameter("@user_id", SqlDbType.Decimal) { Value = userId });
            cmd.Parameters.Add(new SqlParameter("@group_id", SqlDbType.Decimal) { Value = groupId });
            cmd.Parameters.Add(new SqlParameter("@start_level", SqlDbType.Int) { Value = startLevel });
            cmd.Parameters.Add(new SqlParameter("@end_level", SqlDbType.Int) { Value = endLevel });
            cmd.Parameters.Add(new SqlParameter("@entity_name", SqlDbType.VarChar, 100) { Value = "content_item" });
            cmd.Parameters.Add(new SqlParameter("@parent_entity_name", SqlDbType.VarChar, 100) { Value = "content" });
            cmd.Parameters.Add(new SqlParameter("@parent_entity_id", SqlDbType.Decimal) { Value = contentId });
            cmd.Parameters.Add(new SqlParameter("@SQLOut", SqlDbType.NVarChar, -1) { Direction = ParameterDirection.Output });

            var connection = GetActualConnection();
            try
            {
                if (connection.State == ConnectionState.Closed)
                {
                    connection.Open();
                }

                cmd.Connection = connection;
                cmd.ExecuteNonQuery();
                result = (string)cmd.Parameters["@SQLOut"].Value;
            }
            finally
            {
                if (NeedToDisposeActualConnection)
                {
                    connection.Dispose();
                }
            }

            return result;
        }

        public DataTable GetContentDataWithSecurity(string siteName, string contentName, string whereExpression, string orderExpression, long startRow, long pageSize, out long totalRecords, byte useSchedule, string statusName, byte showSplittedArticle, byte includeArchive, long lngUserId, long lngGroupId, int intStartLevel, int intEndLevel, bool blnFilterRecords)
        {
            var obj = new ContentDataQueryObject(this, siteName, contentName, string.Empty, whereExpression, orderExpression, startRow, pageSize, useSchedule, statusName, showSplittedArticle, includeArchive, false, 0, false, false)
            {
                UseSecurity = true,
                UserId = lngUserId,
                GroupId = lngGroupId,
                StartLevel = intStartLevel,
                EndLevel = intEndLevel,
                FilterRecords = blnFilterRecords
            };

            var result = CacheManager.GetQueryResult(obj, out totalRecords);
            var dv = new DataView(result);
            return dv.ToTable();
        }

        internal QueryResult GetFilledDataTable(IQueryObject obj)
        {
            var adapter = CreateDbAdapter();
            var cmd = obj.GetDbCommand();
            adapter.SelectCommand = cmd;
            var cnn = GetActualConnection(obj.DbConnector.InstanceConnectionString);
            try
            {
                if (cnn.State == ConnectionState.Closed)
                {
                    cnn.Open();
                }
                adapter.SelectCommand.Connection = cnn;
                adapter.SelectCommand.Transaction = GetActualTransaction();
                var result = GetFilledDataTable(adapter);
                long totalRecords;
                if (obj.GetCount)
                {
                    var countCmd = CreateDbCommand(obj.CountSql);
                    var dbParameters = new DbParameter[cmd.Parameters.Count];
                    cmd.Parameters.CopyTo(dbParameters, 0);
                    cmd.Parameters.Clear();
                    countCmd.Parameters.AddRange(dbParameters);
                    totalRecords = (long)GetRealScalarData(countCmd);
                }
                else
                {
                    totalRecords = result.Rows.Count;
                }

                return new QueryResult { DataTable = result, TotalRecords = totalRecords };
            }
            finally
            {
                if (NeedToDisposeActualConnection)
                {
                    cnn.Dispose();
                }
            }
        }

        internal DataTable GetFilledDataTable(DbDataAdapter adapter)
        {
            var dt = new DataTable
            {
                CaseSensitive = false,
                Locale = CultureInfo.InvariantCulture
            };

            adapter.Fill(dt);
            return dt;
        }

        public DataTable GetContentData(string siteName, string contentName, string fields, string whereExpression, string orderExpression, long startRow, long pageSize, out long totalRecords, byte useSchedule, string statusName, byte showSplittedArticle, byte includeArchive, bool cacheResult, double cacheInterval, bool useClientSelection, bool withReset)
        {
            var obj = new ContentDataQueryObject(this, siteName, contentName, fields, whereExpression, orderExpression, startRow, pageSize, useSchedule, statusName, showSplittedArticle, includeArchive, cacheResult, cacheInterval, useClientSelection, withReset);
            return GetContentData(obj, out totalRecords);
        }

        public DataTable GetContentData(ContentDataQueryObject obj)
        {
            obj.GetCount = false;
            return GetContentData(obj, out var _);
        }

        public DbDataReader GetContentDataReader(ContentDataQueryObject obj, CommandBehavior readerParams = CommandBehavior.Default)
        {
            if (ExternalConnection == null)
            {
                throw new ApplicationException("ExternalConnection for DbConnector instance has not been defined");
            }

            obj.GetCount = false;
            obj.UseClientSelection = false;
            var cmd = obj.GetDbCommand();
            cmd.Connection = ExternalConnection as SqlConnection;
            cmd.Transaction = GetActualTransaction();
            if (cmd.Connection != null && cmd.Connection.State == ConnectionState.Closed)
            {
                cmd.Connection.Open();
            }

            return cmd.ExecuteReader(readerParams);
        }

        public DataTable GetContentData(ContentDataQueryObject obj, out long totalRecords)
        {
            var result = CacheManager.GetQueryResult(obj, out totalRecords);
            if ((!CacheData || !obj.CacheResult) && !obj.UseClientSelection)
            {
                return result;
            }

            var dv = new DataView(result);
            if (obj.UseClientSelection)
            {
                var hasRegionId = dv.Table.Columns.Contains("RegionId");
                if (!string.IsNullOrEmpty(obj.Where))
                {
                    dv.RowFilter = obj.Where;
                }

                totalRecords = dv.Count;
                if (!string.IsNullOrEmpty(obj.OrderBy))
                {
                    dv.Sort = obj.OrderBy;
                }

                if (obj.StartRow < 1)
                {
                    obj.StartRow = 1;
                }

                if (obj.PageSize < 0)
                {
                    obj.PageSize = 0;
                }

                if (obj.StartRow > 1 || obj.PageSize > 0)
                {
                    if (dv.Count > 0)
                    {
                        if ((int)obj.StartRow <= dv.Count)
                        {
                            int endRow;
                            if (obj.PageSize == 0)
                            {
                                endRow = dv.Count;
                            }
                            else if (obj.StartRow + obj.PageSize > dv.Count)
                            {
                                endRow = dv.Count;
                            }
                            else
                            {
                                endRow = (int)obj.StartRow + (int)obj.PageSize - 1;
                            }

                            var ids = new string[endRow - (int)obj.StartRow + 1];
                            int i;
                            var j = 0;
                            for (i = (int)obj.StartRow - 1; i <= endRow - 1; i++)
                            {
                                if (hasRegionId)
                                {
                                    ids[j] =
                                        $"(CONTENT_ITEM_ID = {dv[i]["CONTENT_ITEM_ID"]} and RegionId = {dv[i]["RegionId"]})";
                                }
                                else
                                {
                                    ids[j] = $"CONTENT_ITEM_ID = {dv[i]["CONTENT_ITEM_ID"]}";
                                }
                                j = j + 1;
                            }

                            dv.RowFilter = string.Join(" or ", ids);
                        }
                        else
                        {
                            dv.RowFilter = "CONTENT_ITEM_ID = 0";
                        }
                    }
                }
            }

            return dv.ToTable();
        }

        public DataTable GetContentData(string siteName, string contentName, string fields, string whereExpression, string orderExpression, long startRow, long pageSize, out long totalRecords, byte useSchedule, string statusName, byte showSplittedArticle, byte includeArchive, bool cacheResult, double cacheInterval) =>
            GetContentData(siteName, contentName, fields, whereExpression, orderExpression, startRow, pageSize, out totalRecords, useSchedule, statusName, showSplittedArticle, includeArchive, cacheResult, cacheInterval, false, false);

        public DataTable GetContentData(string siteName, string contentName, string fields, string whereExpression, string orderExpression, long startRow, long pageSize, out long totalRecords, byte useSchedule, string statusName, byte showSplittedArticle, byte includeArchive) =>
            GetContentData(siteName, contentName, fields, whereExpression, orderExpression, startRow, pageSize, out totalRecords, useSchedule, statusName, showSplittedArticle, includeArchive, false, 0);

        public DataTable GetContentData(string siteName, string contentName, string whereExpression, string orderExpression, long startRow, long pageSize, out long totalRecords, byte useSchedule, string statusName, byte showSplittedArticle, byte includeArchive) =>
            GetContentData(siteName, contentName, string.Empty, whereExpression, orderExpression, startRow, pageSize, out totalRecords, useSchedule, statusName, showSplittedArticle, includeArchive);

        public DataTable GetCachedContentData(string siteName, string contentName, string whereExpression, string orderExpression, long startRow, long pageSize, out long totalRecords, byte useSchedule, string statusName, byte showSplittedArticle, byte includeArchive) =>
            GetCachedContentData(siteName, contentName, string.Empty, whereExpression, orderExpression, startRow, pageSize, out totalRecords, useSchedule, statusName, showSplittedArticle, includeArchive);

        public DataTable GetCachedContentData(string siteName, string contentName, string whereExpression, string orderExpression, long startRow, long pageSize, out long totalRecords, byte useSchedule, string statusName, byte showSplittedArticle, byte includeArchive, bool useClientSelection) =>
            GetContentData(siteName, contentName, string.Empty, whereExpression, orderExpression, startRow, pageSize, out totalRecords, useSchedule, statusName, showSplittedArticle, includeArchive, true, DbCacheManager.DefaultExpirationTime, useClientSelection, false);

        public DataTable GetCachedContentData(string siteName, string contentName, string whereExpression, string orderExpression, long startRow, long pageSize, out long totalRecords, byte useSchedule, string statusName, byte showSplittedArticle, byte includeArchive, double cacheInterval) =>
            GetContentData(siteName, contentName, string.Empty, whereExpression, orderExpression, startRow, pageSize, out totalRecords, useSchedule, statusName, showSplittedArticle, includeArchive, true, cacheInterval);

        public DataTable GetCachedContentData(string siteName, string contentName, string whereExpression, string orderExpression, long startRow, long pageSize, out long totalRecords, byte useSchedule, string statusName, byte showSplittedArticle, byte includeArchive, double cacheInterval, bool useClientSelection) =>
            GetContentData(siteName, contentName, string.Empty, whereExpression, orderExpression, startRow, pageSize, out totalRecords, useSchedule, statusName, showSplittedArticle, includeArchive, true, cacheInterval, useClientSelection, false);

        public DataTable GetCachedContentData(string siteName, string contentName, string fields, string whereExpression, string orderExpression, long startRow, long pageSize, out long totalRecords, byte useSchedule, string statusName, byte showSplittedArticle, byte includeArchive) =>
            GetContentData(siteName, contentName, fields, whereExpression, orderExpression, startRow, pageSize, out totalRecords, useSchedule, statusName, showSplittedArticle, includeArchive, true, DbCacheManager.DefaultExpirationTime);

        public DataTable GetCachedContentData(string siteName, string contentName, string fields, string whereExpression, string orderExpression, long startRow, long pageSize, out long totalRecords, byte useSchedule, string statusName, byte showSplittedArticle, byte includeArchive, bool useClientSelection) =>
            GetContentData(siteName, contentName, fields, whereExpression, orderExpression, startRow, pageSize, out totalRecords, useSchedule, statusName, showSplittedArticle, includeArchive, true, DbCacheManager.DefaultExpirationTime, useClientSelection, false);

        public DataTable GetCachedContentData(string siteName, string contentName, string fields, string whereExpression, string orderExpression, long startRow, long pageSize, out long totalRecords, byte useSchedule, string statusName, byte showSplittedArticle, byte includeArchive, double cacheInterval) =>
            GetContentData(siteName, contentName, fields, whereExpression, orderExpression, startRow, pageSize, out totalRecords, useSchedule, statusName, showSplittedArticle, includeArchive, true, cacheInterval);

        public DataTable GetCachedContentData(string siteName, string contentName, string fields, string whereExpression, string orderExpression, long startRow, long pageSize, out long totalRecords, byte useSchedule, string statusName, byte showSplittedArticle, byte includeArchive, double cacheInterval, bool useClientSelection) =>
            GetContentData(siteName, contentName, fields, whereExpression, orderExpression, startRow, pageSize, out totalRecords, useSchedule, statusName, showSplittedArticle, includeArchive, true, cacheInterval, useClientSelection, false);

        public DataTable GetCachedContentData(string siteName, string contentName, string fields, string whereExpression, string orderExpression, long startRow, long pageSize, out long totalRecords, byte useSchedule, string statusName, byte showSplittedArticle, byte includeArchive, bool useClientSelection, bool withReset) =>
            GetContentData(siteName, contentName, fields, whereExpression, orderExpression, startRow, pageSize, out totalRecords, useSchedule, statusName, showSplittedArticle, includeArchive, true, DbCacheManager.DefaultExpirationTime, useClientSelection, withReset);

        public void ProcessData(string queryString)
        {
            var command = CreateDbCommand(queryString);
            ProcessData(command);
        }

        public void ProcessData(DbCommand command)
        {
            ProcessData(command, GetActualConnection(), GetActualTransaction(), NeedToDisposeActualConnection);
        }

        public void ProcessData(DbCommand command, DbConnection cnn, DbTransaction tr, bool disposeConnection)
        {
            try
            {
                if (cnn.State == ConnectionState.Closed)
                {
                    cnn.Open();
                }

                command.Connection = cnn;
                command.Transaction = tr;
                command.ExecuteNonQuery();
            }
            finally
            {
                if (disposeConnection)
                {
                    cnn.Dispose();
                }
            }
        }

        private void ProcessDataAsNewTransaction(DbCommand command)
        {
            const int maxRetries = 5;
            var retry = maxRetries;
            while (retry > 0)
            {
                var connection = GetActualConnection();
                var extTran = GetActualTransaction();
                try
                {
                    if (connection.State == ConnectionState.Closed)
                    {
                        connection.Open();
                    }

                    try
                    {
                        command.Connection = connection;
                        command.Transaction = extTran ?? connection.BeginTransaction();
                        command.ExecuteNonQuery();
                        if (extTran == null)
                        {
                            command.Transaction.Commit();
                        }
                    }
                    catch (DbException)
                    {
                        try
                        {
                            if (extTran == null)
                            {
                                command.Transaction.Rollback();
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.Error().Exception(ex).Message("Error while updating db").Write();
                        }

                        if (retry == 0)
                        {
                            throw;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error().Exception(ex).Message("Error while updating db").Write();
                    }
                }
                finally
                {
                    retry = -1;
                    if (NeedToDisposeActualConnection)
                    {
                        connection.Dispose();
                    }
                }
            }
        }
    }
}
