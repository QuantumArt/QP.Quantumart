using System;
using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using Npgsql;

namespace Quantumart.QPublishing.Database
{
    public static class DbExtensions
    {
        public static DbParameter AddWithValue(this DbParameterCollection parameterCollection, string parameterName, object value)
        {
            switch (parameterCollection)
            {
                case SqlParameterCollection sqlParameterCollection:
                    return sqlParameterCollection.AddWithValue(parameterName, value);
                case NpgsqlParameterCollection npgsqlParameterCollection:
                    return npgsqlParameterCollection.AddWithValue(parameterName, value ?? DBNull.Value);
                default:
                    throw new ApplicationException("Unknown db type");
            }
        }

        public static void Fill(this DataAdapter da, DataTable dt)
        {
            switch (da)
            {
                case SqlDataAdapter sqlDataAdapter:
                    sqlDataAdapter.Fill(dt);
                    break;
                case NpgsqlDataAdapter npgsqlDataAdapter:
                    npgsqlDataAdapter.Fill(dt);
                    break;
                default:
                    throw new ApplicationException("Unknown db type");
            }
        }
    }
}
