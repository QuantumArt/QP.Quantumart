using System;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using Microsoft.Win32;

// ReSharper disable once CheckNamespace
namespace Quantumart.QP8.Assembling
{
    public class DbConnector
    {
        internal static readonly string RegistryPath = @"Software\Quantum Art\Q-Publishing";

        public DbConnector(string connectionParameter, bool isCustomerCode)
        {
            if (isCustomerCode)
            {
                CustomerCode = connectionParameter;
            }
            else
            {
                _mConnectionString = RemoveProvider(connectionParameter);
            }
        }

        private static string RemoveProvider(string cnnString) => Regex.Replace(cnnString, @"provider[\s]?=[\s]?[^;]+", "", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public string CustomerCode { get; }

        private string _mConnectionString;

        public string ConnectionString
        {
            get
            {
                if (string.IsNullOrEmpty(_mConnectionString))
                {
                    _mConnectionString = GetConnectionString();
                }

                return _mConnectionString;
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

        public SqlConnection CreateConnection()
        {
            var cnn = new SqlConnection(ConnectionString);
            cnn.Open();
            return cnn;
        }

        public void ExecuteCmd(SqlCommand cmd)
        {
            using (var cnn = CreateConnection())
            {
                cmd.Connection = cnn;
                cmd.ExecuteNonQuery();
                cnn.Close();
            }
        }

        public void ExecuteSql(string sqlQuery)
        {
            var cmd = new SqlCommand(sqlQuery);
            ExecuteCmd(cmd);
        }

        public DataSet GetData(string sqlQuery)
        {
            var ds = new DataSet { Locale = CultureInfo.InvariantCulture };
            var cnn = CreateConnection();
            var cmd = new SqlCommand(sqlQuery, cnn);
            var da = new SqlDataAdapter { SelectCommand = cmd };
            da.Fill(ds);
            cnn.Close();
            cmd.Dispose();
            return ds;
        }

        public void GetData(string sqlQuery, DataTable dt)
        {
            var cnn = CreateConnection();
            var cmd = new SqlCommand(sqlQuery, cnn);
            var da = new SqlDataAdapter { SelectCommand = cmd };
            da.Fill(dt);
            cnn.Close();
            cmd.Dispose();
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
