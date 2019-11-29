using System;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using Npgsql;
using QP.ConfigurationService.Models;

namespace Quantumart.QPublishing.Database
{
    public partial class DBConnector
    {
        public static string ConnectionString { get; set; }

        public string CustomConnectionString { get; set; }

        public string InstanceConnectionString => !string.IsNullOrEmpty(CustomConnectionString) ? CustomConnectionString : ConnectionString;

        public IDbConnection ExternalConnection { get; set; }

        public IDbTransaction ExternalTransaction { get; set; }

        private IDbConnection InternalConnection { get; set; }

        private IDbTransaction InternalTransaction { get; set; }

        public bool WithTransaction { get; set; }

        private void CreateInternalConnection()
        {
            InternalConnection = GetActualConnection();
            if (InternalConnection.State == ConnectionState.Closed)
            {
                InternalConnection.Open();
            }

            if (WithTransaction)
            {
                var extTr = GetActualTransaction();
                InternalTransaction = extTr ?? InternalConnection.BeginTransaction();
            }
        }

        private void CommitInternalTransaction()
        {
            if (WithTransaction && ExternalTransaction == null)
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

        private bool NeedToDisposeActualConnection => ExternalConnection == null && InternalConnection == null;


        private DbConnection GetActualConnection(string internalConnectionString)
        {
            if (DbConnectorSettings.DbType == DatabaseType.SqlServer)
            {
                return (InternalConnection ?? ExternalConnection) as SqlConnection ?? new SqlConnection(internalConnectionString);
            }
            else
            {
                return (InternalConnection ?? ExternalConnection) as NpgsqlConnection ?? new NpgsqlConnection(internalConnectionString);
            }
        }

        private DbConnection GetActualConnection() => GetActualConnection(InstanceConnectionString);

        private DbTransaction GetActualTransaction()
        {
            if (DbConnectorSettings.DbType == DatabaseType.SqlServer)
            {
                return (InternalTransaction ?? ExternalTransaction) as SqlTransaction;
            }
            else
            {
                return (InternalTransaction ?? ExternalTransaction) as NpgsqlTransaction;
            }
        }

        public DbCommand CreateDbCommand()
        {
            return CreateDbCommand(String.Empty);
        }

        public DbCommand CreateDbCommand(string text, bool sp = false)
        {
            return CreateDbCommand(text, GetActualConnection(), sp);
        }

        public DbCommand CreateDbCommand(string text, DbConnection cnn, bool sp = false)
        {
            DbCommand result;

            if (cnn is NpgsqlConnection)
            {
                result = new NpgsqlCommand(text);
            }
            else
            {
                result = new SqlCommand(text);
            }

            result.Connection = cnn;
            result.CommandType = sp ? CommandType.StoredProcedure : CommandType.Text;
            return result;
        }

        public DatabaseType DatabaseType => DbConnectorSettings.DbType;

        public bool IsPostgres => DatabaseType == DatabaseType.Postgres;

        private DbDataAdapter CreateDbAdapter() => IsPostgres ? (DbDataAdapter)new NpgsqlDataAdapter() : new SqlDataAdapter();

        public string WithNoLock => SqlQuerySyntaxHelper.WithNoLock(DatabaseType);

        public string IdList(string name = "@ids", string alias = "i") => SqlQuerySyntaxHelper.IdList(DatabaseType, name, alias);

        public string Schema => SqlQuerySyntaxHelper.DbSchemaName(DatabaseType);
    }
}
