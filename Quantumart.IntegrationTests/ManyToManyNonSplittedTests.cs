using System.Collections.Generic;
using NUnit.Framework;
using Quantumart.IntegrationTests.Constants;
using Quantumart.IntegrationTests.Infrastructure;
using Quantumart.QPublishing.Database;

#if ASPNETCORE
using Moq;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;

#endif

namespace Quantumart.IntegrationTests
{
    [TestFixture]
    public class M2MNonSplittedFixture
    {
        private const string ContentName = "Test M2M";

        private const string DictionaryContentName = "Test Category";

        public static int NoneId { get; private set; }

        public static int PublishedId { get; private set; }

        public static DBConnector DbConnector { get; private set; }

        public static int ContentId { get; private set; }

        public static int DictionaryContentId { get; private set; }

        public static int[] BaseArticlesIds { get; private set; }

        public static int[] CategoryIds { get; private set; }

        public static bool EfLinksExists { get; private set; }

        [OneTimeSetUp]
        public static void Init()
        {
#if ASPNETCORE
            DbConnector = new DBConnector(
                new DbConnectorSettings { ConnectionString = Global.ConnectionString },
                new MemoryCache(new MemoryCacheOptions()),
                new HttpContextAccessor { HttpContext = new DefaultHttpContext { Session = Mock.Of<ISession>() } }
            )
            {
                ForceLocalCache = true
            };
#else
            DbConnector = new DBConnector(Global.ConnectionString) { ForceLocalCache = true };
#endif
            Clear();

            Global.ReplayXml(@"TestData/m2m_nonsplitted.xml");

            ContentId = Global.GetContentId(DbConnector, ContentName);
            EfLinksExists = Global.EfLinksExists(DbConnector, ContentId);
            DictionaryContentId = Global.GetContentId(DbConnector, DictionaryContentName);
            BaseArticlesIds = Global.GetIds(DbConnector, ContentId);
            CategoryIds = Global.GetIds(DbConnector, DictionaryContentId);
            NoneId = DbConnector.GetStatusTypeId(Global.SiteId, "None");
            PublishedId = DbConnector.GetStatusTypeId(Global.SiteId, "Published");
        }

        [Test]
        public void MassUpdate_NoSplitAndMerge_ForSynWorkflow()
        {
            var values = new List<Dictionary<string, string>>();
            var ints1 = new[] { CategoryIds[1], CategoryIds[3], CategoryIds[5] };
            var ints2 = new[] { CategoryIds[2], CategoryIds[3], CategoryIds[4] };
            var article1 = new Dictionary<string, string>
            {
                [FieldName.ContentItemId] = "0",
                ["Title"] = "newtest",
                ["Categories"] = string.Join(",", ints1),
                [FieldName.StatusTypeId] = PublishedId.ToString()
            };

            values.Add(article1);
            var article2 = new Dictionary<string, string>
            {
                [FieldName.ContentItemId] = "0",
                ["Title"] = "newtest",
                ["Categories"] = string.Join(",", ints2),
                [FieldName.StatusTypeId] = PublishedId.ToString()
            };

            values.Add(article2);
            Assert.DoesNotThrow(() => DbConnector.MassUpdate(ContentId, values, 1), "Create");

            var ids1 = new[] { int.Parse(article1[FieldName.ContentItemId]) };
            var ids2 = new[] { int.Parse(article2[FieldName.ContentItemId]) };
            var intsSaved1 = Global.GetLinks(DbConnector, ids1);
            var intsSaved2 = Global.GetLinks(DbConnector, ids2);

            Assert.That(ints1, Is.EqualTo(intsSaved1), "First article M2M saved");
            Assert.That(ints2, Is.EqualTo(intsSaved2), "Second article M2M saved");
            if (EfLinksExists)
            {
                var intsEfSaved1 = Global.GetEfLinks(DbConnector, ids1, ContentId);
                var intsEfSaved2 = Global.GetEfLinks(DbConnector, ids2, ContentId);
                Assert.That(ints1, Is.EqualTo(intsEfSaved1), "First article EF M2M saved");
                Assert.That(ints2, Is.EqualTo(intsEfSaved2), "Second article EF M2M saved");
            }

            var titles = new[] { "xnewtest", "xnewtest" };
            var intsNew1 = new[] { CategoryIds[0], CategoryIds[2], CategoryIds[3] };
            var intsNew2 = new[] { CategoryIds[3], CategoryIds[5] };
            article1["Categories"] = string.Join(",", intsNew1);
            article2["Categories"] = string.Join(",", intsNew2);
            article1["Title"] = titles[0];
            article2["Title"] = titles[1];
            article1[FieldName.StatusTypeId] = NoneId.ToString();
            article2[FieldName.StatusTypeId] = NoneId.ToString();

            Assert.DoesNotThrow(() => DbConnector.MassUpdate(ContentId, values, 1), "Change to none");
            var intsUpdated1 = Global.GetLinks(DbConnector, ids1);
            var intsUpdated2 = Global.GetLinks(DbConnector, ids2);
            var intsUpdatedAsync1 = Global.GetLinks(DbConnector, ids1, true);
            var intsUpdatedAsync2 = Global.GetLinks(DbConnector, ids2, true);

            Assert.That(intsNew1, Is.EqualTo(intsUpdated1), "First article M2M (main) saved");
            Assert.That(intsNew2, Is.EqualTo(intsUpdated2), "Second article M2M (main) saved");
            Assert.That(intsUpdatedAsync1, Is.Empty, "No first async M2M ");
            Assert.That(intsUpdatedAsync2, Is.Empty, "No second async M2M ");

            if (EfLinksExists)
            {
                var intsEfUpdated2 = Global.GetEfLinks(DbConnector, ids2, ContentId);
                var intsEfUpdated1 = Global.GetEfLinks(DbConnector, ids1, ContentId);
                var intsEfUpdatedAsync1 = Global.GetEfLinks(DbConnector, ids1, ContentId, true);
                var intsEfUpdatedAsync2 = Global.GetEfLinks(DbConnector, ids2, ContentId, true);

                Assert.That(intsNew1, Is.EqualTo(intsEfUpdated1), "First article EF M2M (main) saved");
                Assert.That(intsNew2, Is.EqualTo(intsEfUpdated2), "Second article EF M2M (main) saved");
                Assert.That(intsEfUpdatedAsync1, Is.Empty, "No first async EF M2M ");
                Assert.That(intsEfUpdatedAsync2, Is.Empty, "No second async EF M2M ");
            }

            var values2 = new List<Dictionary<string, string>>();
            var article3 = new Dictionary<string, string>
            {
                [FieldName.ContentItemId] = article1[FieldName.ContentItemId],
                [FieldName.StatusTypeId] = PublishedId.ToString()
            };

            values2.Add(article3);
            var article4 = new Dictionary<string, string>
            {
                [FieldName.ContentItemId] = article2[FieldName.ContentItemId],
                [FieldName.StatusTypeId] = PublishedId.ToString()
            };

            values2.Add(article4);
            Assert.DoesNotThrow(() => DbConnector.MassUpdate(ContentId, values2, 1), "Change to published");

            var intsPublished1 = Global.GetLinks(DbConnector, ids1);
            var intsPublished2 = Global.GetLinks(DbConnector, ids2);

            Assert.That(intsPublished1, Is.EqualTo(intsUpdated1), "First article same");
            Assert.That(intsPublished2, Is.EqualTo(intsUpdated2), "Second article same");
            if (EfLinksExists)
            {
                var intsEfPublished1 = Global.GetEfLinks(DbConnector, ids1, ContentId);
                var intsEfPublished2 = Global.GetEfLinks(DbConnector, ids2, ContentId);
                Assert.That(intsEfPublished1, Is.EqualTo(intsUpdated1), "First EF article same");
                Assert.That(intsEfPublished2, Is.EqualTo(intsUpdated2), "Second EF article same");
            }
        }

        [Test]
        public void MassUpdate_NoVersions_ForDisabledVersionControl()
        {
            var values = new List<Dictionary<string, string>>();
            var ints1 = new[] { CategoryIds[1], CategoryIds[3], CategoryIds[5] };
            var ints2 = new[] { CategoryIds[2], CategoryIds[3], CategoryIds[4] };
            var article1 = new Dictionary<string, string>
            {
                [FieldName.ContentItemId] = "0",
                ["Title"] = "newtest",
                ["Categories"] = string.Join(",", ints1),
                [FieldName.StatusTypeId] = PublishedId.ToString()
            };

            values.Add(article1);
            var article2 = new Dictionary<string, string>
            {
                [FieldName.ContentItemId] = "0",
                ["Title"] = "newtest",
                ["Categories"] = string.Join(",", ints2),
                [FieldName.StatusTypeId] = PublishedId.ToString()
            };

            values.Add(article2);
            var ids = new[] { int.Parse(article1[FieldName.ContentItemId]), int.Parse(article2[FieldName.ContentItemId]) };
            Assert.DoesNotThrow(() => DbConnector.MassUpdate(ContentId, values, 1), "Create");
            Assert.DoesNotThrow(() => DbConnector.MassUpdate(ContentId, values, 1), "Change");

            var versions = Global.GetMaxVersions(DbConnector, ids);
            Assert.That(versions, Is.Empty, "Versions created");
        }

        [OneTimeTearDown]
        public static void TearDown()
        {
            Clear();
        }

        private static void Clear()
        {
            Global.RemoveContentIfExists(Global.GetContentId(DbConnector, ContentName));
            Global.RemoveContentIfExists(Global.GetContentId(DbConnector, DictionaryContentName));
        }
    }
}
