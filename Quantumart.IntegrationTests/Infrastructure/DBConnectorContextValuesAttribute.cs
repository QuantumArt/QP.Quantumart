using NUnit.Framework;
using Quantumart.QPublishing.Database;
using System;
using System.Collections.Generic;

namespace Quantumart.IntegrationTests.Infrastructure
{
    public class DBConnectorContextValuesAttribute : ValuesAttribute
    {
        public DBConnectorContextValuesAttribute()
            : base(
                  new DBConnectorContextSync(),
                  new DBConnectorContextAsync())
        {
        }
    }

    public interface IDBConnectorContext
    {
        void MassUpdate(DBConnector dbConnector, int contentId, IEnumerable<Dictionary<string, string>> values, int lastModifiedBy);
    }

    public class DBConnectorContextSync : IDBConnectorContext
    {
        public void MassUpdate(DBConnector dbConnector, int contentId, IEnumerable<Dictionary<string, string>> values, int lastModifiedBy)
        {
            Assert.DoesNotThrow(() => dbConnector.MassUpdate(contentId, values, lastModifiedBy));
        }

        public override string ToString() => "Sync";
    }

    public class DBConnectorContextAsync : IDBConnectorContext
    {
        public void MassUpdate(DBConnector dbConnector, int contentId, IEnumerable<Dictionary<string, string>> values, int lastModifiedBy)
        {
            Assert.DoesNotThrowAsync(async () => await dbConnector.MassUpdateAsync(contentId, values, lastModifiedBy));
        }

        public override string ToString() => "Async";
    }
}
