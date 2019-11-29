using System;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using Microsoft.Win32;
using Npgsql;
using QP.ConfigurationService.Models;

// ReSharper disable once CheckNamespace
namespace Quantumart.QP8.Assembling
{
    public class DbConnector
    {
        internal static readonly string RegistryPath = @"Software\Quantum Art\Q-Publishing";

        public DbConnector(string connectionParameter, bool isCustomerCode, DatabaseType dbType)
        {
            if (isCustomerCode)
            {
                CustomerCode = connectionParameter;
            }
            else
            {
                _connectionString = RemoveProvider(connectionParameter);
                DbType = dbType;
            }
        }

        private static string RemoveProvider(string cnnString) => Regex.Replace(cnnString, @"provider[\s]?=[\s]?[^;]+", "", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public string CustomerCode { get; }

        private string _connectionString;

        public DatabaseType DbType { get; set; }

        public string ConnectionString
        {
            get
            {
                if (string.IsNullOrEmpty(_connectionString))
                {
                    _connectionString = GetConnectionString();
                }

                return _connectionString;
            }
        }

        private string GetConnectionString()
        {
            var localKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
            var qKey = localKey.OpenSubKey(RegistryPath);
            if (qKey == null)
            {
                throw new InvalidOperationException("QP7 is not installed");
            }

            var regValue = qKey.GetValue("Configuration File");
            if (regValue == null)
            {
                throw new InvalidOperationException("QP7 records in the registry are inconsistent or damaged");
            }

            var doc = new XmlDocument();
            doc.Load(regValue.ToString());
            var node = doc.SelectSingleNode("configuration/customers/customer[@customer_name='" + CustomerCode + "']/db/text()");
            if (node == null)
            {
                throw new InvalidOperationException("Cannot load connection string for ASP.NET in QP7 configuration file");
            }

            return node.Value.Replace("Provider=SQLOLEDB;", "");
        }

        public DbConnection CreateConnection()
        {
            DbConnection result;
            if (DbType == DatabaseType.Postgres)
            {
                result = new NpgsqlConnection(ConnectionString);
            }
            else
            {
                result = new SqlConnection(ConnectionString);
            }

            result.Open();
            return result;
        }


        public void ExecuteSql(string sqlQuery)
        {
            using (var cnn = CreateConnection())
            {
                var cmd = CreateCommand(sqlQuery, cnn);
                cmd.ExecuteNonQuery();
                cnn.Close();
            }
        }

        public DbCommand CreateCommand(string sqlQuery, DbConnection cnn)
        {
            DbCommand result;
            if (cnn is NpgsqlConnection)
            {
                result = new NpgsqlCommand(sqlQuery);
            }
            else
            {
                result = new SqlCommand(sqlQuery);
            }
            result.CommandType = CommandType.Text;
            result.Connection = cnn;
            return result;
        }

        public DbCommand CreateCommand(string sqlQuery)
        {
            var cnn = CreateConnection();
            return CreateCommand(sqlQuery, cnn);
        }

        public DbDataAdapter CreateDataAdapter(DbConnection cnn)
        {
            DbDataAdapter result;
            if (cnn is NpgsqlConnection)
            {
                result = new NpgsqlDataAdapter();
            }
            else
            {
                result = new SqlDataAdapter();
            }
            return result;
        }

        public DataSet GetData(string sqlQuery)
        {
            var ds = new DataSet { Locale = CultureInfo.InvariantCulture };
            using (var cnn = CreateConnection())
            {
                var cmd = CreateCommand(sqlQuery, cnn);
                var da = CreateDataAdapter(cnn);
                da.SelectCommand = cmd;
                da.Fill(ds);
            }
            return ds;
        }

        public void GetData(string sqlQuery, DataTable dt)
        {
            using (var cnn = CreateConnection())
            {
                var cmd = CreateCommand(sqlQuery, cnn);
                var da = CreateDataAdapter(cnn);
                da.SelectCommand = cmd;
                da.Fill(dt);
            }
        }

        public DataTable GetDataTable(string sqlQuery)
        {
            DataTable result = null;
            var ds = GetData(sqlQuery);
            if (ds.Tables.Count > 0)
            {
                result = ds.Tables[0];
            }

            return result;
        }

        public DataRow GetOneRow(string sqlQuery)
        {
            DataRow result = null;
            var dt = GetDataTable(sqlQuery);
            if (dt != null && dt.Rows.Count > 0)
            {
                result = dt.Rows[0];
            }

            return result;
        }

        public static string GetValue(DataRow row, string columnName, string defaultValue)
        {
            var obj = row[columnName];
            return obj == null || obj.ToString() == string.Empty ? defaultValue : obj.ToString();
        }
    }
}
