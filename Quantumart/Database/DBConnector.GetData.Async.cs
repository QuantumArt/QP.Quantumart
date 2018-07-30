using System.Data;
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
            var cmd = new SqlCommand(queryString);
            return await GetRealDataAsync(cmd, cancellationToken);
        }

        public async Task<DataTable> GetRealDataAsync(SqlCommand cmd, CancellationToken cancellationToken = default(CancellationToken)) => await GetRealDataAsync(cmd, GetActualSqlConnection(), GetActualSqlTransaction(), NeedToDisposeActualSqlConnection, cancellationToken);

        public async Task<DataTable> GetRealDataAsync(SqlCommand cmd, SqlConnection cn, SqlTransaction tr, bool disposeConnection, CancellationToken cancellationToken = default(CancellationToken))
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
            var command = new SqlCommand(queryString);
            await ProcessDataAsync(command, cancellationToken);
        }

        public async Task ProcessDataAsync(SqlCommand command, CancellationToken cancellationToken = default(CancellationToken))
        {
            await ProcessDataAsync(command, GetActualSqlConnection(), GetActualSqlTransaction(), NeedToDisposeActualSqlConnection, cancellationToken);
        }

        public async Task ProcessDataAsync(SqlCommand command, SqlConnection cnn, SqlTransaction tr, bool disposeConnection, CancellationToken cancellationToken = default(CancellationToken))
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
