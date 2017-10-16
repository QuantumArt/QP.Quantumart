using System.Collections.Generic;

namespace Quantumart.QPublishing.Database
{
    public class DbConnectorSettings
    {
        public bool IsLive { get; set; }

        public int UseAbsoluteSiteUrl { get; set; }

        public string Sql2012ModeDll { get; set; }

        public string ConnectionString { get; set; }

        public IDictionary<string, string> ConnectionStrings { get; set; }

        public string PrefetchLimit { get; set; }

        public int CacheGetData { get; set; }

        public string MailComponent { get; set; }

        public string MailAssemble { get; set; }

        public string RelNotifyUrl { get; set; }

        public string MailHost { get; set; }

        public string MailLogin { get; set; }

        public string MailPassword { get; set; }

        public string MailFromName { get; internal set; }
    }
}
