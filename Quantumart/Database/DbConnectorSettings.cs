using System.Collections.Generic;
using System.Collections.Specialized;
using QP.ConfigurationService.Models;

namespace Quantumart.QPublishing.Database
{
    public class DbConnectorSettings
    {
        public DbConnectorSettings()
        {

        }

        public DbConnectorSettings(NameValueCollection appSettings)
        {
            IsLive = appSettings["isLive"] != "false";
            UseAbsoluteSiteUrl = appSettings["UseAbsoluteSiteUrl"] == "1";
            PrefetchLimit = appSettings["PrefetchLimit"];
            CacheGetData = appSettings["CacheGetData"] == "1";
            MailComponent = appSettings["MailComponent"];
            MailAssemble = appSettings["MailAssemble"] == "yes";
            MailLogin = appSettings["MailLogin"];
            MailPassword = appSettings["MailPassword"];
            MailFromName = appSettings["MailFromName"];
            RelNotifyUrl = appSettings["RelNotifyUrl"];
            ConnectionString = appSettings["ConnectionString"];
            BackendUrlForNotification = appSettings["BackendUrlForNotification"];
            InternalShortExpirationTime = appSettings["InternalShortExpirationTime"];
            InternalLongExpirationTime = appSettings["InternalLongExpirationTime"];
            InternalExpirationTime = appSettings["InternalExpirationTime"];
            UseMultiSiteConfiguration = appSettings["UseMultiSiteConfiguration"] == "1";

        }

        public bool IsLive { get; set; }

        public bool UseAbsoluteSiteUrl { get; set; }

        public string ConnectionString { get; set; }

        public string PrefetchLimit { get; set; }

        public bool CacheGetData { get; set; }

        public string MailComponent { get; set; }

        public bool MailAssemble { get; set; }

        public string RelNotifyUrl { get; set; }

        public string MailHost { get; set; }

        public string MailLogin { get; set; }

        public string MailPassword { get; set; }

        public string MailFromName { get; set; }

        public string BackendUrlForNotification { get; private set; }

        public string InternalShortExpirationTime { get; private set; }

        public string InternalLongExpirationTime { get; private set; }


        public string InternalExpirationTime { get; private set; }

        public bool UseMultiSiteConfiguration { get; private set; }

        public DatabaseType DbType { get; set; }
    }
}
