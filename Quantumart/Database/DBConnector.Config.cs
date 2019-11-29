using System;
using System.Linq;
using System.Xml;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Xml.Serialization;
using QP.ConfigurationService.Client;
using QP.ConfigurationService.Models;

namespace Quantumart.QPublishing.Database
{
    public partial class DBConnector
    {
        internal static readonly string RegistryPath = @"Software\Quantum Art\Q-Publishing";

        public static string ConfigServiceToken { get; set; }

        public static string ConfigServiceUrl { get; set; }

        public static string XmlConfigPath { get; set; }

        public static string GetConnectionString(string customerCode)
        {
            return GetCustomerConfiguration(customerCode).Result.ConnectionString;
        }

        public static async Task<CustomerConfiguration> GetCustomerConfiguration(string customerCode)
        {
            CustomerConfiguration result;
            if (ConfigServiceUrl != null && ConfigServiceToken != null)
            {
                var service = new CachedQPConfigurationService(ConfigServiceUrl, ConfigServiceToken);
                result = await service.GetCustomer(customerCode);
            }
            else
            {
                var config = await GetQpConfiguration();
                result = config.Customers.SingleOrDefault(n => n.Name == customerCode);
            }

            if (result == null)
            {
                throw new InvalidOperationException($"Cannot load customer code {customerCode} from QP8 configuration");
            }

            result.ConnectionString = result.ConnectionString.Replace("Provider=SQLOLEDB;", "");

            return result;
        }

        public static XmlDocument GetQpConfig()
        {
            var doc = new XmlDocument();
            doc.Load(GetQpConfigPath());
            return doc;
        }

        private static string GetQpConfigPath()
        {
            if (!String.IsNullOrEmpty(XmlConfigPath))
            {
                return XmlConfigPath;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var qKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(RegistryPath);
                if (qKey != null)
                {
                    var regValue = qKey.GetValue("Configuration File");
                    if (regValue != null)
                    {
                        return regValue.ToString();
                    }

                    throw new InvalidOperationException("QP8 records in the registry are inconsistent or damaged");
                }
            }

            throw new InvalidOperationException("You should install QP8 or provide XmlConfigPath property");
        }

        public static async Task<Configuration> GetQpConfiguration()
        {
            Configuration result ;
            if (ConfigServiceUrl != null && ConfigServiceToken != null)
            {
                var service = new CachedQPConfigurationService(ConfigServiceUrl, ConfigServiceToken);
                result = new Configuration();
                result.Customers = (await service.GetCustomers()).ToArray();
                result.Variables = (await service.GetVariables()).ToArray();
            }
            else
            {
                var configPath = GetQpConfigPath();
                var configSerializer = new XmlSerializer(typeof(Configuration));
                try
                {
                    using (var xmlTextReader = new XmlTextReader(configPath))
                    {
                        result = (Configuration)configSerializer.Deserialize(xmlTextReader);
                    }
                }
                catch (Exception e)
                {
                    throw new InvalidOperationException("QP8 configuration is incorrect: " + e.Message);
                }
            }

            return result;
        }

        private static XmlDocument XmlDocument(string path)
        {
            var doc = new XmlDocument();
            doc.Load(path);
            return doc;
        }

        public static string GetQpTempDirectory()
        {
            var configuration = GetQpConfiguration().Result;
            var value = configuration.Variables.SingleOrDefault(n => n.Name == "TempDirectory")?.Value;
            if (value == null)
            {
                throw new InvalidOperationException("Cannot load TempDirectory parameter from QP8 configuration");
            }

            return value;

        }

    }
}
