using System.Data.SqlClient;
using Quantumart.QPublishing.Database;

// ReSharper disable once CheckNamespace
namespace Quantumart.QPublishing.Info
{
    public interface IQueryObject
    {
        DBConnector DbConnector { get; set; }

        bool CacheResult { get; set; }

        double CacheInterval { get; set; }

        bool GetCount { get; set; }

        bool GetCountInTable { get; }

        bool WithReset { get; set; }

        bool IsFirstPage { get; }

        string CountSql { get; }

        string GetKey(string prefix);

        SqlCommand GetSqlCommand();

        string OutputParamName { get; }
    }
}
