using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using NUnit.Framework;
using Quantumart.IntegrationTests.Constants;
using Quantumart.IntegrationTests.Infrastructure;
using Quantumart.QPublishing.Database;
using Moq;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;

namespace Quantumart.IntegrationTests
{
    [TestFixture]
    public class NullifyTests
    {
        private const string ContentName = "Test unique";

        public static DBConnector DbConnector { get; private set; }

        public static int ContentId { get; private set; }

        public static int[] BaseArticlesIds { get; private set; }

        [OneTimeSetUp]
        public static void Init()
        {
            DbConnector = new DBConnector(
                new DbConnectorSettings { ConnectionString = Global.ConnectionString, DbType = Global.DBType},
                new MemoryCache(new MemoryCacheOptions()),
                new HttpContextAccessor { HttpContext = new DefaultHttpContext { Session = Mock.Of<ISession>() } }
            )
            {
                ForceLocalCache = true
            };
            Clear();

            Global.ReplayXml(@"TestData/nullify.xml");

            ContentId = Global.GetContentId(DbConnector, ContentName);
            BaseArticlesIds = Global.GetIds(DbConnector, ContentId);
        }

        [Test]
        public void MassUpdate_SaveNull_ForNull([DBConnectorContextValues] IDBConnectorContext context)
        {
            var values = new List<Dictionary<string, string>>();
            var article2 = new Dictionary<string, string>
            {
                [FieldName.ContentItemId] = "0",
                ["Title"] = "Test,Test",
                ["Number"] = "5",
                ["Parent"] = BaseArticlesIds[0].ToString(),
                ["Date"] = DateTime.Now.ToString(CultureInfo.CurrentCulture),
                ["Flag"] = "1"
            };

            values.Add(article2);
            context.MassUpdate(DbConnector, ContentId, values, 1);
            var id2 = int.Parse(article2[FieldName.ContentItemId]);
            var ids = new[] { id2 };

            var titleBefore = Global.GetFieldValues<string>(DbConnector, ContentId, "Title", ids)[0];
            var numBefore = Global.GetFieldValues<decimal?>(DbConnector, ContentId, "Number", ids)[0];
            var parentBefore = Global.GetFieldValues<decimal?>(DbConnector, ContentId, "Parent", ids)[0];
            var dateBefore = Global.GetFieldValues<DateTime?>(DbConnector, ContentId, "Date", ids)[0];
            var flagBefore = Global.GetFieldValues<decimal?>(DbConnector, ContentId, "Flag", ids)[0];

            Assert.That(titleBefore, Is.Not.Null);
            Assert.That(numBefore, Is.Not.Null);
            Assert.That(parentBefore, Is.Not.Null);
            Assert.That(dateBefore, Is.Not.Null);
            Assert.That(flagBefore, Is.Not.Null);

            values.Clear();
            var article3 = new Dictionary<string, string>
            {
                [FieldName.ContentItemId] = id2.ToString(),
                ["Title"] = null,
                ["Number"] = null,
                ["Parent"] = null,
                ["Date"] = null,
                ["Flag"] = null
            };

            values.Add(article3);
            context.MassUpdate(DbConnector, ContentId, values, 1);

            var titleAfter = Global.GetFieldValues<string>(DbConnector, ContentId, "Title", ids)[0];
            var numAfter = Global.GetFieldValues<decimal?>(DbConnector, ContentId, "Number", ids)[0];
            var parentAfter = Global.GetFieldValues<decimal?>(DbConnector, ContentId, "Parent", ids)[0];
            var dateAfter = Global.GetFieldValues<DateTime?>(DbConnector, ContentId, "Date", ids)[0];
            var flagAfter = Global.GetFieldValues<decimal?>(DbConnector, ContentId, "Flag", ids)[0];

            Assert.That(titleAfter, Is.Null);
            Assert.That(numAfter, Is.Null);
            Assert.That(parentAfter, Is.Null);
            Assert.That(dateAfter, Is.Null);
            Assert.That(flagAfter, Is.Null);
        }


        [Test]
        public void ImportToContent_SaveNull_ForNull()
        {
            var values = new List<Dictionary<string, string>>();
            var article2 = new Dictionary<string, string>
            {
                [FieldName.ContentItemId] = "0",
                ["Title"] = "Test,Test",
                ["Number"] = "5",
                ["Parent"] = BaseArticlesIds[0].ToString(),
                ["Date"] = DateTime.Now.ToString(CultureInfo.CurrentCulture),
                ["Flag"] = "1"
            };

            values.Add(article2);
            Assert.DoesNotThrow(() => DbConnector.ImportToContent(ContentId, values));
            var id2 = int.Parse(article2[FieldName.ContentItemId]);
            var ids = new[] { id2 };

            var titleBefore = Global.GetFieldValues<string>(DbConnector, ContentId, "Title", ids)[0];
            var numBefore = Global.GetFieldValues<decimal?>(DbConnector, ContentId, "Number", ids)[0];
            var parentBefore = Global.GetFieldValues<decimal?>(DbConnector, ContentId, "Parent", ids)[0];
            var dateBefore = Global.GetFieldValues<DateTime?>(DbConnector, ContentId, "Date", ids)[0];
            var flagBefore = Global.GetFieldValues<decimal?>(DbConnector, ContentId, "Flag", ids)[0];

            Assert.That(titleBefore, Is.Not.Null);
            Assert.That(numBefore, Is.Not.Null);
            Assert.That(parentBefore, Is.Not.Null);
            Assert.That(dateBefore, Is.Not.Null);
            Assert.That(flagBefore, Is.Not.Null);

            values.Clear();
            var article3 = new Dictionary<string, string>
            {
                [FieldName.ContentItemId] = id2.ToString(),
                ["Title"] = null,
                ["Number"] = null,
                ["Parent"] = null,
                ["Date"] = null,
                ["Flag"] = null
            };

            values.Add(article3);
            Assert.DoesNotThrow(() => DbConnector.ImportToContent(ContentId, values));

            var titleAfter = Global.GetFieldValues<string>(DbConnector, ContentId, "Title", ids)[0];
            var numAfter = Global.GetFieldValues<decimal?>(DbConnector, ContentId, "Number", ids)[0];
            var parentAfter = Global.GetFieldValues<decimal?>(DbConnector, ContentId, "Parent", ids)[0];
            var dateAfter = Global.GetFieldValues<DateTime?>(DbConnector, ContentId, "Date", ids)[0];
            var flagAfter = Global.GetFieldValues<decimal?>(DbConnector, ContentId, "Flag", ids)[0];

            Assert.That(titleAfter, Is.Null);
            Assert.That(numAfter, Is.Null);
            Assert.That(parentAfter, Is.Null);
            Assert.That(dateAfter, Is.Null);
            Assert.That(flagAfter, Is.Null);
        }


        [Test]
        public void MassUpdate_SaveNull_ForEmpty([DBConnectorContextValues] IDBConnectorContext context)
        {
            var values = new List<Dictionary<string, string>>();
            var article2 = new Dictionary<string, string>
            {
                [FieldName.ContentItemId] = "0",
                ["Title"] = "Test,Test",
                ["Number"] = "5"
            };

            values.Add(article2);
            context.MassUpdate(DbConnector, ContentId, values, 1);
            var id2 = int.Parse(article2[FieldName.ContentItemId]);
            var ids = new[] { id2 };

            values.Clear();
            var article3 = new Dictionary<string, string>
            {
                [FieldName.ContentItemId] = id2.ToString(),
                ["Title"] = string.Empty,
                ["Number"] = string.Empty
            };

            values.Add(article3);
            context.MassUpdate(DbConnector, ContentId, values, 1);

            var titleAfter = Global.GetFieldValues<string>(DbConnector, ContentId, "Title", ids)[0];
            var numAfter = Global.GetFieldValues<decimal?>(DbConnector, ContentId, "Number", ids)[0];

            Assert.That(titleAfter, Is.Null);
            Assert.That(numAfter, Is.Null);
        }


        [OneTimeTearDown]
        public static void TearDown()
        {
            Clear();
        }

        private static void Clear()
        {
            Global.RemoveContentIfExists(Global.GetContentId(DbConnector, ContentName));
        }
    }
}
