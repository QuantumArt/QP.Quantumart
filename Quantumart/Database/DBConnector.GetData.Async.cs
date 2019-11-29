using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable once CheckNamespace
namespace Quantumart.QPublishing.Database
{
    // ReSharper disable once InconsistentNaming
    public partial class DBConnector
    {
        public async Task<DataTable> GetRealDataAsync(string queryString, CancellationToken cancellationToken = default(CancellationToken))
        {
            var cmd = CreateDbCommand(queryString);
            return await GetRealDataAsync(cmd, cancellationToken);
        }

        public async Task<DataTable> GetRealDataAsync(
            DbCommand cmd, CancellationToken cancellationToken = default(CancellationToken)
        ) => await GetRealDataAsync(
            cmd, GetActualConnection(), GetActualTransaction(), NeedToDisposeActualConnection, cancellationToken
         );

        public async Task<DataTable> GetRealDataAsync(DbCommand cmd, DbConnection cn, DbTransaction tr, bool disposeConnection, CancellationToken cancellationToken = default(CancellationToken))
        {
            try
            {
                if (cn.State == ConnectionState.Closed)
                {
                    await cn.OpenAsync(cancellationToken);
                }

                cmd.Connection = cn;
                cmd.Transaction = tr;

                using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
                {
                    var table = new DataTable();
                    table.Load(reader);
                    return table;
                }
            }
            finally
            {
                if (disposeConnection)
                {
                    cn.Dispose();
                }
            }
        }

        public async Task ProcessDataAsync(string queryString, CancellationToken cancellationToken = default(CancellationToken))
        {
            var command = CreateDbCommand(queryString);
            await ProcessDataAsync(command, cancellationToken);
        }

        public async Task ProcessDataAsync(DbCommand command, CancellationToken cancellationToken = default(CancellationToken))
        {
            await ProcessDataAsync(command, GetActualConnection(), GetActualTransaction(), NeedToDisposeActualConnection, cancellationToken);
        }

        public async Task ProcessDataAsync(DbCommand command, DbConnection cnn, DbTransaction tr, bool disposeConnection, CancellationToken cancellationToken = default(CancellationToken))
        {
            try
            {
                if (cnn.State == ConnectionState.Closed)
                {
                    await cnn.OpenAsync(cancellationToken);
                }

                command.Connection = cnn;
                command.Transaction = tr;
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            finally
            {
                if (disposeConnection)
                {
                    cnn.Dispose();
                }
            }
        }
    }
}
