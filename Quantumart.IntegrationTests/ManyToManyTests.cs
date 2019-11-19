using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Moq;
using NUnit.Framework;
using Quantumart.IntegrationTests.Constants;
using Quantumart.IntegrationTests.Infrastructure;
using Quantumart.QPublishing.Database;
using Quantumart.QPublishing.FileSystem;
#if ASPNETCORE
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
#else
using System.Web;

#endif

namespace Quantumart.IntegrationTests
{
    [TestFixture]
    public class ManyToManyTests
    {
        private const string ContentName = "Test M2M";

        private const string DictionaryContentName = "Test Category";

        public static DBConnector DbConnector { get; private set; }

        public static int NoneId { get; private set; }

        public static int PublishedId { get; private set; }

        public static bool EfLinksExists { get; private set; }

        public static string TitleName { get; private set; }

        public static string MainCategoryName { get; private set; }

        public static string NumberName { get; private set; }

        public static string CategoryName { get; private set; }

        public static int[] BaseArticlesIds { get; private set; }

        public static int[] CategoryIds { get; private set; }

        public static int ContentId { get; private set; }

        public static int DictionaryContentId { get; private set; }

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

            Global.ReplayXml(@"TestData/m2m.xml");

            ContentId = Global.GetContentId(DbConnector, ContentName);
            DictionaryContentId = Global.GetContentId(DbConnector, DictionaryContentName);

            EfLinksExists = Global.EfLinksExists(DbConnector, ContentId);
            TitleName = DbConnector.FieldName(Global.SiteId, ContentName, "Title");
            MainCategoryName = DbConnector.FieldName(Global.SiteId, ContentName, "MainCategory");
            NumberName = DbConnector.FieldName(Global.SiteId, ContentName, "Number");
            CategoryName = DbConnector.FieldName(Global.SiteId, ContentName, "Categories");
            BaseArticlesIds = Global.GetIds(DbConnector, ContentId);
            CategoryIds = Global.GetIds(DbConnector, DictionaryContentId);
            NoneId = DbConnector.GetStatusTypeId(Global.SiteId, "None");
            PublishedId = DbConnector.GetStatusTypeId(Global.SiteId, "Published");
        }

        [Test]
        public void MassUpdate_SplitAndMergeData_ForStatusChanging()
        {
            var values = new List<Dictionary<string, string>>();
            var article1 = new Dictionary<string, string>
            {
                [FieldName.ContentItemId] = BaseArticlesIds[0].ToString(),
                [FieldName.StatusTypeId] = NoneId.ToString()
            };

            values.Add(article1);
            var article2 = new Dictionary<string, string>
            {
                [FieldName.ContentItemId] = BaseArticlesIds[1].ToString(),
                [FieldName.StatusTypeId] = NoneId.ToString()
            };

            values.Add(article2);
            var ints = new[] { BaseArticlesIds[0], BaseArticlesIds[1] };
            var cntAsyncBefore = Global.CountLinks(DbConnector, ints, true);
            var cntBefore = Global.CountLinks(DbConnector, ints);
            var titlesBefore = Global.GetTitles(DbConnector, ContentId, ints);
            var cntArticlesAsyncBefore = Global.CountArticles(DbConnector, ContentId, ints, true);
            var cntArticlesBefore = Global.CountArticles(DbConnector, ContentId, ints);

            Assert.That(cntAsyncBefore, Is.EqualTo(0));
            Assert.That(cntBefore, Is.Not.EqualTo(0));
            Assert.That(cntArticlesAsyncBefore, Is.EqualTo(0));
            Assert.That(cntArticlesBefore, Is.Not.EqualTo(0));

            if (EfLinksExists)
            {
                var cntEfAsyncBefore = Global.CountEfLinks(DbConnector, ints, ContentId, true);
                var cntEfBefore = Global.CountEfLinks(DbConnector, ints, ContentId);
                Assert.That(cntEfAsyncBefore, Is.EqualTo(cntAsyncBefore));
                Assert.That(cntEfBefore, Is.EqualTo(cntBefore));
            }

            Assert.DoesNotThrow(() => DbConnector.MassUpdate(ContentId, values, 1));
            var cntAsyncAfterSplit = Global.CountLinks(DbConnector, ints, true);
            var cntAfterSplit = Global.CountLinks(DbConnector, ints);
            var asyncTitlesAfterSplit = Global.GetTitles(DbConnector, ContentId, ints, true);
            var cntArticlesAsyncAfterSplit = Global.CountArticles(DbConnector, ContentId, ints, true);
            var cntArticlesAfterSplit = Global.CountArticles(DbConnector, ContentId, ints);

            Assert.That(cntAsyncAfterSplit, Is.Not.EqualTo(0));
            Assert.That(cntAfterSplit, Is.EqualTo(cntAsyncAfterSplit));
            Assert.That(cntArticlesAfterSplit, Is.Not.EqualTo(0));
            Assert.That(cntArticlesAsyncAfterSplit, Is.EqualTo(cntArticlesAfterSplit));

            if (EfLinksExists)
            {
                var cntEfAsyncAfterSplit = Global.CountEfLinks(DbConnector, ints, ContentId, true);
                var cntEfAfterSplit = Global.CountEfLinks(DbConnector, ints, ContentId);
                Assert.That(cntEfAsyncAfterSplit, Is.EqualTo(cntAsyncAfterSplit));
                Assert.That(cntEfAfterSplit, Is.EqualTo(cntAfterSplit));
            }

            var values2 = new List<Dictionary<string, string>>();
            var article3 = new Dictionary<string, string>
            {
                [FieldName.ContentItemId] = BaseArticlesIds[0].ToString(),
                [FieldName.StatusTypeId] = PublishedId.ToString()
            };

            values2.Add(article3);
            var article4 = new Dictionary<string, string>
            {
                [FieldName.ContentItemId] = BaseArticlesIds[1].ToString(),
                [FieldName.StatusTypeId] = PublishedId.ToString()
            };

            values2.Add(article4);
            Assert.DoesNotThrow(() => DbConnector.MassUpdate(ContentId, values2, 1));

            var cntAsyncAfterMerge = Global.CountLinks(DbConnector, ints, true);
            var cntAfterMerge = Global.CountLinks(DbConnector, ints);
            var titlesAfterMerge = Global.GetTitles(DbConnector, ContentId, ints);
            var cntArticlesAsyncAfterMerge = Global.CountArticles(DbConnector, ContentId, ints, true);
            var cntArticlesAfterMerge = Global.CountArticles(DbConnector, ContentId, ints);

            Assert.That(cntAsyncAfterMerge, Is.EqualTo(0));
            Assert.That(cntAfterMerge, Is.Not.EqualTo(0));
            Assert.That(cntAfterMerge, Is.EqualTo(cntBefore));

            Assert.That(cntArticlesAsyncAfterMerge, Is.EqualTo(0));
            Assert.That(cntArticlesAfterMerge, Is.EqualTo(cntArticlesBefore));

            Assert.That(titlesBefore, Is.EqualTo(titlesAfterMerge));
            Assert.That(titlesBefore, Is.EqualTo(asyncTitlesAfterSplit));

            if (EfLinksExists)
            {
                var cntEfAsyncAfterMerge = Global.CountEfLinks(DbConnector, ints, ContentId, true);
                var cntEfAfterMerge = Global.CountEfLinks(DbConnector, ints, ContentId);
                Assert.That(cntEfAsyncAfterMerge, Is.EqualTo(cntAsyncAfterMerge));
                Assert.That(cntEfAfterMerge, Is.EqualTo(cntAfterMerge));
            }
        }

        [Test]
        public void MassUpdate_InsertSplitAndMergeData_ForM2MAndStatusChanging()
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
                Assert.That(intsEfSaved1, Is.EquivalentTo(intsSaved1));
                Assert.That(intsEfSaved2, Is.EquivalentTo(intsSaved2));
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

            Assert.DoesNotThrow(() => DbConnector.MassUpdate(ContentId, values, 1), "Change and split");

            var intsUpdated1 = Global.GetLinks(DbConnector, ids1);
            var intsUpdated2 = Global.GetLinks(DbConnector, ids2);
            var intsUpdatedAsync1 = Global.GetLinks(DbConnector, ids1, true);
            var intsUpdatedAsync2 = Global.GetLinks(DbConnector, ids2, true);

            Assert.That(ints1, Is.EqualTo(intsUpdated1), "First article M2M (main) remains the same");
            Assert.That(ints2, Is.EqualTo(intsUpdated2), "Second article M2M (main) remains the same");
            Assert.That(intsNew1, Is.EqualTo(intsUpdatedAsync1), "First article M2M (async) saved");
            Assert.That(intsNew2, Is.EqualTo(intsUpdatedAsync2), "Second article M2M (async) saved");

            if (EfLinksExists)
            {
                var intsEfUpdated1 = Global.GetEfLinks(DbConnector, ids1, ContentId);
                var intsEfUpdated2 = Global.GetEfLinks(DbConnector, ids2, ContentId);
                var intsEfUpdatedAsync1 = Global.GetEfLinks(DbConnector, ids1, ContentId, true);
                var intsEfUpdatedAsync2 = Global.GetEfLinks(DbConnector, ids2, ContentId, true);
                Assert.That(intsEfUpdated1, Is.EquivalentTo(intsUpdated1));
                Assert.That(intsEfUpdated2, Is.EquivalentTo(intsUpdated2));
                Assert.That(intsEfUpdatedAsync1, Is.EquivalentTo(intsUpdatedAsync1));
                Assert.That(intsEfUpdatedAsync2, Is.EquivalentTo(intsUpdatedAsync2));
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
            Assert.DoesNotThrow(() => DbConnector.MassUpdate(ContentId, values2, 1), "Merge");
            var intsMerged1 = Global.GetLinks(DbConnector, ids1);
            var intsMerged2 = Global.GetLinks(DbConnector, ids2);
            var intsMergedAsync1 = Global.GetLinks(DbConnector, ids1, true);
            var intsMergedAsync2 = Global.GetLinks(DbConnector, ids2, true);
            var mergedTitles = Global.GetTitles(DbConnector, ContentId, ids1.Union(ids2).ToArray());
            var mergedTitlesAsync = Global.GetTitles(DbConnector, ContentId, ids1.Union(ids2).ToArray(), true);

            Assert.That(titles, Is.EqualTo(mergedTitles), "Updated articles (main) after merge");
            Assert.That(mergedTitlesAsync, Is.Empty, "Empty articles (async) after merge");
            Assert.That(intsMerged2, Is.EqualTo(intsUpdatedAsync2), "Second article M2M (main) merged");
            Assert.That(intsMerged1, Is.EqualTo(intsUpdatedAsync1), "First article M2M (main) merged");
            Assert.That(intsMergedAsync1, Is.Empty, "First article M2M (async) cleared");
            Assert.That(intsMergedAsync2, Is.Empty, "Second article M2M (async) cleared");

            if (EfLinksExists)
            {
                var intsEfMerged1 = Global.GetEfLinks(DbConnector, ids1, ContentId);
                var intsEfMerged2 = Global.GetEfLinks(DbConnector, ids2, ContentId);
                var intsEfMergedAsync1 = Global.GetEfLinks(DbConnector, ids1, ContentId, true);
                var intsEfMergedAsync2 = Global.GetEfLinks(DbConnector, ids2, ContentId, true);
                Assert.That(intsEfMerged1, Is.EquivalentTo(intsMerged1));
                Assert.That(intsEfMerged2, Is.EquivalentTo(intsMerged2));
                Assert.That(intsEfMergedAsync1, Is.EquivalentTo(intsMergedAsync1));
                Assert.That(intsEfMergedAsync2, Is.EquivalentTo(intsMergedAsync2));
            }
        }

        [Test]
        public void AddFormToContent_InsertSplitAndMergeData_ForM2MAndStatusChanging()
        {
            var ints1 = new[] { CategoryIds[1], CategoryIds[3], CategoryIds[5] };

            var titles1 = new[] { "newtest" };
            var article1 = new Hashtable
            {
                [TitleName] = titles1[0],
                [CategoryName] = string.Join(",", ints1),
                [MainCategoryName] = CategoryIds[0]
            };

            var id = 0;
            Assert.DoesNotThrow(() => { id = DbConnector.AddFormToContent(Global.SiteId, ContentName, "Published", ref article1, 0); }, "Create");

            var ids1 = new[] { id };
            var intsSaved1 = Global.GetLinks(DbConnector, ids1);
            Assert.That(ints1, Is.EqualTo(intsSaved1), "article M2M saved");

            if (EfLinksExists)
            {
                var intsEfSaved1 = Global.GetEfLinks(DbConnector, ids1, ContentId);
                Assert.That(ints1, Is.EqualTo(intsEfSaved1), "article EF M2M saved");
            }

            var titles2 = new[] { "xnewtest" };
            var intsNew1 = new[] { CategoryIds[0], CategoryIds[2], CategoryIds[3] };
            article1[CategoryName] = string.Join(",", intsNew1);
            article1[TitleName] = titles2[0];

            Assert.DoesNotThrow(() => { id = DbConnector.AddFormToContent(Global.SiteId, ContentName, "None", ref article1, id); }, "Change and split");

            var intsUpdated1 = Global.GetLinks(DbConnector, ids1);
            var intsUpdatedAsync1 = Global.GetLinks(DbConnector, ids1, true);
            var updatedTitlesAsync = Global.GetTitles(DbConnector, ContentId, ids1, true);
            var updatedTitles = Global.GetTitles(DbConnector, ContentId, ids1);

            Assert.That(titles1, Is.EqualTo(updatedTitles), "Article (main) remains the same");
            Assert.That(titles2, Is.EqualTo(updatedTitlesAsync), "Article (async) saved");
            Assert.That(ints1, Is.EqualTo(intsUpdated1), "Article M2M (main) remains the same");
            Assert.That(intsNew1, Is.EqualTo(intsUpdatedAsync1), "Article M2M (async) saved");

            if (EfLinksExists)
            {
                var intsEfUpdated1 = Global.GetEfLinks(DbConnector, ids1, ContentId);
                var intsEfUpdatedAsync1 = Global.GetEfLinks(DbConnector, ids1, ContentId, true);
                Assert.That(ints1, Is.EqualTo(intsEfUpdated1), "Article EF M2M (main) remains the same");
                Assert.That(intsNew1, Is.EqualTo(intsEfUpdatedAsync1), "Article EF M2M (async) saved");
            }

            Assert.DoesNotThrow(() => { id = DbConnector.AddFormToContent(Global.SiteId, ContentName, "Published", ref article1, id); }, "Merge with values");

            var intsMerged1 = Global.GetLinks(DbConnector, ids1);
            var intsMergedAsync1 = Global.GetLinks(DbConnector, ids1, true);
            var mergedTitles = Global.GetTitles(DbConnector, ContentId, ids1);
            var mergedTitlesAsync = Global.GetTitles(DbConnector, ContentId, ids1, true);

            Assert.That(titles2, Is.EqualTo(mergedTitles), "Updated article (main) after merge");
            Assert.That(mergedTitlesAsync, Is.Empty, "Empty article (async) after merge");
            Assert.That(intsMerged1, Is.EqualTo(intsUpdatedAsync1), "Article M2M (main) merged");
            Assert.That(intsMergedAsync1, Is.Empty, "Article M2M (async) cleared");

            if (EfLinksExists)
            {
                var intsEfMerged1 = Global.GetEfLinks(DbConnector, ids1, ContentId);
                var intsEfMergedAsync1 = Global.GetEfLinks(DbConnector, ids1, ContentId, true);
                Assert.That(intsEfMerged1, Is.EqualTo(intsUpdatedAsync1), "Article EF M2M (main) merged");
                Assert.That(intsEfMergedAsync1, Is.Empty, "Article EF M2M (async) cleared");
            }
        }

        [Test]
        public void MassUpdate_SaveAndUpdateOK_ForM2MData()
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
            var ids = ids1.Union(ids2).ToArray();
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

            var cntData = Global.CountData(DbConnector, ids);
            var cntLinks = Global.CountLinks(DbConnector, ids);

            if (EfLinksExists)
            {
                var cntEfLinks = Global.CountEfLinks(DbConnector, ids, ContentId);
                Assert.That(cntEfLinks, Is.EqualTo(cntLinks), "EF links");
            }

            Assert.DoesNotThrow(() => DbConnector.MassUpdate(ContentId, values, 1), "Change");
            var intsUpdated1 = Global.GetLinks(DbConnector, ids1);
            var intsUpdated2 = Global.GetLinks(DbConnector, ids2);

            Assert.That(intsNew1, Is.EqualTo(intsUpdated1), "First article M2M updated");
            Assert.That(intsNew2, Is.EqualTo(intsUpdated2), "Second article M2M updated");
            if (EfLinksExists)
            {
                var intsEfUpdated1 = Global.GetEfLinks(DbConnector, ids1, ContentId);
                var intsEfUpdated2 = Global.GetEfLinks(DbConnector, ids2, ContentId);
                Assert.That(intsNew1, Is.EqualTo(intsEfUpdated1), "First article EF M2M updated");
                Assert.That(intsNew2, Is.EqualTo(intsEfUpdated2), "Second article EF M2M updated");
            }

            var versions = Global.GetMaxVersions(DbConnector, ids);
            var cntVersionData = Global.CountVersionData(DbConnector, versions);
            var cntVersionLinks = Global.CountVersionLinks(DbConnector, versions);

            Assert.That(versions.Length, Is.EqualTo(2), "Versions created");
            Assert.That(cntData, Is.EqualTo(cntVersionData), "Data moved to versions");
            Assert.That(cntLinks, Is.EqualTo(cntVersionLinks), "Links moved to versions");
        }

        [Test]
        public void AddFormToContent_SaveAndUpdateOK_ForM2MData()
        {
            var ints1 = new[] { CategoryIds[1], CategoryIds[3], CategoryIds[5] };
            var article1 = new Hashtable
            {
                [TitleName] = "newtest",
                [CategoryName] = string.Join(",", ints1),
                [MainCategoryName] = CategoryIds[0]
            };

            var id = 0;
            Assert.DoesNotThrow(() => { id = DbConnector.AddFormToContent(Global.SiteId, ContentName, "Published", ref article1, 0); }, "Create");

            var ids = new[] { id };
            var intsSaved1 = Global.GetLinks(DbConnector, ids);
            Assert.That(id, Is.Not.EqualTo(0), "Saved");
            Assert.That(ints1, Is.EqualTo(intsSaved1), "Article M2M saved");
            if (EfLinksExists)
            {
                var intsEfSaved1 = Global.GetEfLinks(DbConnector, ids, ContentId);
                Assert.That(ints1, Is.EqualTo(intsEfSaved1), "Article EF M2M saved");
            }

            const string title1 = "xnewtest";
            var intsNew1 = new[] { CategoryIds[0], CategoryIds[2], CategoryIds[3] };
            article1[CategoryName] = string.Join(",", intsNew1);
            article1[TitleName] = title1;

            var cntData = Global.CountData(DbConnector, ids);
            var cntLinks = Global.CountLinks(DbConnector, ids);

            if (EfLinksExists)
            {
                var cntEfLinks = Global.CountEfLinks(DbConnector, ids, ContentId);
                Assert.That(cntEfLinks, Is.EqualTo(cntLinks), "EF links");
            }

            Assert.DoesNotThrow(() => { id = DbConnector.AddFormToContent(Global.SiteId, ContentName, "Published", ref article1, id); }, "Update");

            var intsUpdated1 = Global.GetLinks(DbConnector, ids);
            Assert.That(intsNew1, Is.EqualTo(intsUpdated1), "Article M2M updated");
            if (EfLinksExists)
            {
                var intsEfUpdated1 = Global.GetEfLinks(DbConnector, ids, ContentId);
                Assert.That(intsNew1, Is.EqualTo(intsEfUpdated1), "Article EF M2M updated");
            }

            var versions = Global.GetMaxVersions(DbConnector, ids);
            var cntVersionData = Global.CountVersionData(DbConnector, versions);
            var cntVersionLinks = Global.CountVersionLinks(DbConnector, versions);

            Assert.That(versions.Length, Is.EqualTo(1), "Versions created");
            Assert.That(cntData, Is.EqualTo(cntVersionData), "Data moved to versions");
            Assert.That(cntLinks, Is.EqualTo(cntVersionLinks), "Links moved to versions");
        }

        [Test]
        public void MassUpdate_UpdateOK_ForAsymmetricData()
        {
            var ids = new[] { BaseArticlesIds[0], BaseArticlesIds[1] };
            var descriptionsBefore = Global.GetFieldValues<string>(DbConnector, ContentId, "Description", ids);
            var numbersBefore = Global.GetNumbers(DbConnector, ContentId, ids);
            var values = new List<Dictionary<string, string>>();

            const string title1 = "newtestxx";
            const string title2 = "newtestxxx";
            const int num = 30;
            var article1 = new Dictionary<string, string>
            {
                [FieldName.ContentItemId] = BaseArticlesIds[0].ToString(),
                ["Title"] = title1,
                ["Number"] = num.ToString()
            };

            values.Add(article1);
            var article2 = new Dictionary<string, string>
            {
                [FieldName.ContentItemId] = BaseArticlesIds[1].ToString(),
                ["Title"] = title2
            };

            values.Add(article2);
            Assert.DoesNotThrow(() => DbConnector.MassUpdate(ContentId, values, 1), "Update");

            var descriptionsAfter = Global.GetFieldValues<string>(DbConnector, ContentId, "Description", ids);
            var titlesAfter = Global.GetTitles(DbConnector, ContentId, ids);
            var numbersAfter = Global.GetNumbers(DbConnector, ContentId, ids);

            Assert.That(numbersAfter[1], Is.EqualTo(numbersBefore[1]), "Number 2 remains the same");
            Assert.That(numbersAfter[0], Is.EqualTo(num), "Number 1 is changed");
            Assert.That(titlesAfter[1], Is.EqualTo(title2), "Title 2 is changed");
            Assert.That(titlesAfter[0], Is.EqualTo(title1), "Title 1 is changed");
            Assert.That(descriptionsAfter[1], Is.EqualTo(descriptionsBefore[1]), "Description 2 remains the same");
            Assert.That(descriptionsAfter[0], Is.EqualTo(descriptionsBefore[0]), "Description 1 remains the same");
        }

        [Test]
        public void ImportToContent_UpdateOK_ForAsymmetricData()
        {
            var values = new List<Dictionary<string, string>>();
            var article1 = new Dictionary<string, string>
            {
                [FieldName.ContentItemId] = "0",
                ["Title"] = "testa",
                ["Number"] = "20",
                ["Description"] = "abc"
            };

            values.Add(article1);
            var article2 = new Dictionary<string, string>
            {
                [FieldName.ContentItemId] = "0",
                ["Title"] = "testb",
                ["Number"] = "30",
                ["Description"] = "def"
            };

            values.Add(article2);
            Assert.DoesNotThrow(() => DbConnector.ImportToContent(ContentId, values), "Create");

            var ids = new[] { int.Parse(article1[FieldName.ContentItemId]), int.Parse(article2[FieldName.ContentItemId]) };
            var descriptionsBefore = Global.GetFieldValues<string>(DbConnector, ContentId, "Description", ids);
            var numbersBefore = Global.GetNumbers(DbConnector, ContentId, ids);

            var values2 = new List<Dictionary<string, string>>();
            const string title1 = "newtestab";
            const string title2 = "newtestabc";
            const int num = 40;
            var article3 = new Dictionary<string, string>
            {
                [FieldName.ContentItemId] = article1[FieldName.ContentItemId],
                ["Title"] = title1,
                ["Number"] = num.ToString()
            };

            values2.Add(article3);
            var article4 = new Dictionary<string, string>
            {
                [FieldName.ContentItemId] = article2[FieldName.ContentItemId],
                ["Title"] = title2
            };

            values2.Add(article4);
            Assert.DoesNotThrow(() => DbConnector.ImportToContent(ContentId, values2), "Update");

            var descriptionsAfter = Global.GetFieldValues<string>(DbConnector, ContentId, "Description", ids);
            var titlesAfter = Global.GetTitles(DbConnector, ContentId, ids);
            var numbersAfter = Global.GetNumbers(DbConnector, ContentId, ids);

            Assert.That(numbersAfter[1], Is.EqualTo(numbersBefore[1]), "Number 2 remains the same");
            Assert.That(numbersAfter[0], Is.EqualTo(num), "Number 1 is changed");
            Assert.That(titlesAfter[1], Is.EqualTo(title2), "Title 2 is changed");
            Assert.That(titlesAfter[0], Is.EqualTo(title1), "Title 1 is changed");
            Assert.That(descriptionsAfter[1], Is.EqualTo(descriptionsBefore[1]), "Description 2 remains the same");
            Assert.That(descriptionsAfter[0], Is.EqualTo(descriptionsBefore[0]), "Description 1 remains the same");
        }

        [Test]
        public void ImportToContent_UpdateOK_ForAsymmetricDataWithOverrideMissedFields()
        {
            var values = new List<Dictionary<string, string>>();
            var article1 = new Dictionary<string, string>
            {
                [FieldName.ContentItemId] = "0",
                ["Title"] = "testa",
                ["Number"] = "20",
                ["Description"] = "abc"
            };

            values.Add(article1);
            var article2 = new Dictionary<string, string>
            {
                [FieldName.ContentItemId] = "0",
                ["Title"] = "testb",
                ["Number"] = "30",
                ["Description"] = "def"
            };

            values.Add(article2);
            Assert.DoesNotThrow(() => DbConnector.ImportToContent(ContentId, values), "Create");

            var ids = new[] { int.Parse(article1[FieldName.ContentItemId]), int.Parse(article2[FieldName.ContentItemId]) };
            var values2 = new List<Dictionary<string, string>>();
            const string title1 = "newtestab";
            const string title2 = "newtestabc";
            const int num = 40;
            var article3 = new Dictionary<string, string>
            {
                [FieldName.ContentItemId] = article1[FieldName.ContentItemId],
                ["Title"] = title1,
                ["Number"] = num.ToString()
            };

            values2.Add(article3);
            var article4 = new Dictionary<string, string>
            {
                [FieldName.ContentItemId] = article2[FieldName.ContentItemId],
                ["Title"] = title2
            };

            values2.Add(article4);
            Assert.DoesNotThrow(() => DbConnector.ImportToContent(ContentId, values2, 1, null, true), "Update");

            var descriptionsAfter = Global.GetFieldValues<string>(DbConnector, ContentId, "Description", ids);
            var titlesAfter = Global.GetTitles(DbConnector, ContentId, ids);
            var numbersAfter = Global.GetNumbers(DbConnector, ContentId, ids);

            Assert.That(numbersAfter[1], Is.EqualTo(0), "Number 2 is cleared");
            Assert.That(numbersAfter[0], Is.EqualTo(num), "Number 1 is changed");
            Assert.That(titlesAfter[1], Is.EqualTo(title2), "Title 2 is changed");
            Assert.That(titlesAfter[0], Is.EqualTo(title1), "Title 1 is changed");
            Assert.That(descriptionsAfter[1], Is.Null.Or.Empty, "Description 2 is cleared");
            Assert.That(descriptionsAfter[0], Is.Null.Or.Empty, "Description 1 is cleared");
        }

        [Test]
        public void ImportToContent_UpdateOK_ForAsymmetricDataWithOverrideMissedFieldsAndAttrIds()
        {
            var values = new List<Dictionary<string, string>>();
            var article1 = new Dictionary<string, string>
            {
                [FieldName.ContentItemId] = "0",
                ["Title"] = "testa",
                ["Number"] = "20",
                ["Description"] = "abc"
            };

            values.Add(article1);
            var article2 = new Dictionary<string, string>
            {
                [FieldName.ContentItemId] = "0",
                ["Title"] = "testb",
                ["Number"] = "30",
                ["Description"] = "def"
            };

            values.Add(article2);
            Assert.DoesNotThrow(() => DbConnector.ImportToContent(ContentId, values), "Create");

            var ids = new[] { int.Parse(article1[FieldName.ContentItemId]), int.Parse(article2[FieldName.ContentItemId]) };
            var descriptionsBefore = Global.GetFieldValues<string>(DbConnector, ContentId, "Description", ids);

            var values2 = new List<Dictionary<string, string>>();
            const int num = 40;
            const string title1 = "newtestab";
            const string title2 = "newtestabc";
            var article3 = new Dictionary<string, string>
            {
                [FieldName.ContentItemId] = article1[FieldName.ContentItemId],
                ["Title"] = title1,
                ["Number"] = num.ToString()
            };

            values2.Add(article3);
            var article4 = new Dictionary<string, string>
            {
                [FieldName.ContentItemId] = article2[FieldName.ContentItemId],
                ["Title"] = title2
            };

            values2.Add(article4);
            var titleId = DbConnector.FieldID(Global.SiteId, DbConnector.GetContentName(ContentId), "Title");
            var numberId = DbConnector.FieldID(Global.SiteId, DbConnector.GetContentName(ContentId), "Number");
            var attrIds = new[] { titleId, numberId };

            Assert.DoesNotThrow(() => DbConnector.ImportToContent(ContentId, values2, 1, attrIds, true), "Update");
            var descriptionsAfter = Global.GetFieldValues<string>(DbConnector, ContentId, "Description", ids);
            var titlesAfter = Global.GetTitles(DbConnector, ContentId, ids);
            var numbersAfter = Global.GetNumbers(DbConnector, ContentId, ids);

            Assert.That(numbersAfter[1], Is.EqualTo(0), "Number 2 is cleared");
            Assert.That(numbersAfter[0], Is.EqualTo(num), "Number 1 is changed");
            Assert.That(titlesAfter[1], Is.EqualTo(title2), "Title 2 is changed");
            Assert.That(titlesAfter[0], Is.EqualTo(title1), "Title 1 is changed");
            Assert.That(descriptionsAfter[1], Is.EqualTo(descriptionsBefore[1]), "Description 2 remains the same");
            Assert.That(descriptionsAfter[0], Is.EqualTo(descriptionsBefore[0]), "Description 1 remains the same");
        }

        [Test]
        public void MassUpdate_ReturnModified_ForInsertingAndUpdatingData()
        {
            var ids = new[] { BaseArticlesIds[0], BaseArticlesIds[1] };
            var modifiedBefore = Global.GetFieldValues<DateTime>(DbConnector, ContentId, "Modified", ids);
            var values = new List<Dictionary<string, string>>();
            var article1 = new Dictionary<string, string>
            {
                [FieldName.ContentItemId] = BaseArticlesIds[0].ToString()
            };

            values.Add(article1);
            var article2 = new Dictionary<string, string>
            {
                [FieldName.ContentItemId] = BaseArticlesIds[1].ToString()
            };

            values.Add(article2);
            var article3 = new Dictionary<string, string>
            {
                [FieldName.ContentItemId] = "0"
            };

            values.Add(article3);
            Assert.DoesNotThrow(() => DbConnector.MassUpdate(ContentId, values, 1), "Update and Insert");
            var afterIds = values.Select(n => n[FieldName.ContentItemId]).Select(int.Parse).ToArray();
            var newId = afterIds.Except(ids).Single();

            var modifiedAfter = Global.GetFieldValues<DateTime>(DbConnector, ContentId, "Modified", afterIds);
            var everyOneHasModified = values.All(n => n.ContainsKey(FieldName.Modified));
            var createdItemId = int.Parse(values.Single(n => n.ContainsKey(FieldName.Created))[FieldName.ContentItemId]);

            Assert.That(newId, Is.EqualTo(createdItemId), "New item has created");
            Assert.That(modifiedBefore, Does.Not.EqualTo(modifiedAfter.Take(2).ToArray()), "Modified changed");
            Assert.That(everyOneHasModified, Is.True, "All articles has Modified");

            var modifiedReturned = values.Select(n => DateTime.Parse(n[FieldName.Modified], CultureInfo.InvariantCulture)).ToArray();
            Assert.That(modifiedAfter, Is.EqualTo(modifiedReturned), "Return modified");
        }

        [Test]
        public void AddFormToContent_ReturnModified_ReturnModifiedTrue()
        {
            var article1 = new Hashtable
            {
                [TitleName] = "abc",
                [MainCategoryName] = CategoryIds[0]
            };

            var id = 0;
            Assert.DoesNotThrow(() => { id = DbConnector.AddFormToContent(Global.SiteId, ContentName, "Published", ref article1, 0); }, "Add article");

            var ids = new[] { id };
            var modified = DateTime.MinValue;
            Assert.DoesNotThrow(() => { id = DbConnector.AddFormToContent(Global.SiteId, ContentId, "Published", ref article1, id, true, 0, true, false, true, ref modified); }, "Update article");

            var modifiedAfter = Global.GetFieldValues<DateTime>(DbConnector, ContentId, "Modified", ids)[0];
            Assert.That(modified, Is.EqualTo(modifiedAfter), "Modified changed");
        }

        [Test]
        public void MassUpdate_DoesntReturnModified_ReturnModifiedFalse()
        {
            var values = new List<Dictionary<string, string>>();
            var article1 = new Dictionary<string, string>
            {
                [FieldName.ContentItemId] = BaseArticlesIds[0].ToString()
            };

            values.Add(article1);
            var article2 = new Dictionary<string, string>
            {
                [FieldName.ContentItemId] = BaseArticlesIds[1].ToString()
            };

            values.Add(article2);
            Assert.DoesNotThrow(() => DbConnector.MassUpdate(ContentId, values, 1, new MassUpdateOptions { ReturnModified = false }), "Update");

            var noOneHasModified = values.All(n => !n.ContainsKey(FieldName.Modified));
            Assert.That(noOneHasModified, Is.EqualTo(true), "All articles has Modified");
        }

        [Test]
        public void MassUpdate_ThrowsException_ValidateAttributeValueInvalidNumericData()
        {
            var values = new List<Dictionary<string, string>>();
            var article1 = new Dictionary<string, string>
            {
                [FieldName.ContentItemId] = BaseArticlesIds[0].ToString(),
                ["Number"] = "test"
            };

            values.Add(article1);
            Assert.That(
                () => DbConnector.MassUpdate(ContentId, values, 1),
                Throws.Exception.TypeOf<QpInvalidAttributeException>().And.Message.Contains("type is incorrect"),
                "Validate numeric data"
            );
        }

        [Test]
        public void AddFormToContent_ThrowsException_ValidateAttributeValueInvalidNumericData()
        {
            var article1 = new Hashtable
            {
                [NumberName] = "test",
                [MainCategoryName] = CategoryIds[0]
            };

            Assert.That(
                () => { DbConnector.AddFormToContent(Global.SiteId, ContentName, "Published", ref article1, BaseArticlesIds[0]); },
                Throws.Exception.TypeOf<QpInvalidAttributeException>().And.Message.Contains("type is incorrect"),
                "Validate numeric data");
        }

        [Test]
        public void MassUpdate_ThrowsException_ValidateAttributeValueStringDoesNotComplyInputMask()
        {
            var values = new List<Dictionary<string, string>>();
            var article1 = new Dictionary<string, string>
            {
                [FieldName.ContentItemId] = BaseArticlesIds[0].ToString(),
                ["Title"] = "test123"
            };

            values.Add(article1);
            Assert.That(
                () => DbConnector.MassUpdate(ContentId, values, 1),
                Throws.Exception.TypeOf<QpInvalidAttributeException>().And.Message.Contains("input mask"),
                "Validate input mask"
            );
        }

        [Test]
        public void AddFormToContent_ThrowsException_ValidateAttributeValueStringDoesNotComplyInputMask()
        {
            var article1 = new Hashtable
            {
                [TitleName] = "test123",
                [MainCategoryName] = CategoryIds[0]
            };

            Assert.That(
                () => { DbConnector.AddFormToContent(Global.SiteId, ContentName, "Published", ref article1, BaseArticlesIds[0]); },
                Throws.Exception.TypeOf<QpInvalidAttributeException>().And.Message.Contains("input mask"),
                "Validate input mask"
            );
        }

        [Test]
        public void MassUpdate_ArticleAddedWithDefaultValues_ValidateAttributeValueNewArticleWithMissedData()
        {
            var values = new List<Dictionary<string, string>>();
            var article1 = new Dictionary<string, string>
            {
                [FieldName.ContentItemId] = "0",
                ["Title"] = "newtest"
            };

            values.Add(article1);
            Assert.DoesNotThrow(() => DbConnector.MassUpdate(ContentId, values, 1), "Add article");

            var id = int.Parse(values[0][FieldName.ContentItemId]);
            var ids = new[] { id };
            Assert.That(id, Is.Not.EqualTo(0), "Return id");

            var desc = Global.GetFieldValues<string>(DbConnector, ContentId, "Description", ids)[0];
            var num = (int)Global.GetNumbers(DbConnector, ContentId, ids)[0];
            var cnt = Global.CountLinks(DbConnector, ids);
            Assert.That(num, Is.Not.EqualTo(0), "Default number");
            Assert.That(desc, Is.Not.Null.Or.Empty, "Default description");
            Assert.That(cnt, Is.EqualTo(2), "Default M2M");

            if (EfLinksExists)
            {
                var cntEf = Global.CountEfLinks(DbConnector, ids, ContentId);
                Assert.That(cntEf, Is.EqualTo(2), "Default EF M2M");
            }
        }

        [Test]
        public void MassUpdate_WithUrlsAndReplaceUrlsTrue_ReplaceUrls()
        {
            var values = new List<Dictionary<string, string>>();
            var article1 = new Dictionary<string, string>
            {
                [FieldName.ContentItemId] = "0",
                ["Description"] = $@"<a href=""{DbConnector.GetImagesUploadUrl(Global.SiteId)}"">test</a>"
            };

            values.Add(article1);
            Assert.DoesNotThrow(() => DbConnector.MassUpdate(ContentId, values, 1, new MassUpdateOptions { ReplaceUrls = true }), "Add article");

            var id = int.Parse(values[0][FieldName.ContentItemId]);
            var ids = new[] { id };
            var desc = Global.GetFieldValues<string>(DbConnector, ContentId, "Description", ids)[0];
            Assert.That(desc, Does.Contain(@"<%=upload_url%>"));
        }

        [Test]
        public void MassUpdate_WithUrlsAndReplaceUrlsDefault_DependsOnSiteProps()
        {
            var values = new List<Dictionary<string, string>>();
            var article1 = new Dictionary<string, string>
            {
                [FieldName.ContentItemId] = "0",
                ["Description"] = $@"<a href=""{DbConnector.GetImagesUploadUrl(Global.SiteId)}"">test</a>"
            };

            values.Add(article1);
            Assert.DoesNotThrow(() => DbConnector.MassUpdate(ContentId, values, 1), "Add article");
            var replace = DbConnector.GetReplaceUrlsInDB(Global.SiteId);

            var id = int.Parse(values[0][FieldName.ContentItemId]);
            var ids = new[] { id };
            var desc = Global.GetFieldValues<string>(DbConnector, ContentId, "Description", ids)[0];
            Assert.That(desc, replace ? Does.Contain(@"<%=upload_url%>") : Does.Not.Contain(@"<%=upload_url%>"));
        }

        [Test]
        public void MassUpdate_WithUrlsAndReplaceUrlsFalse_DoesNotReplaceUrls()
        {
            var values = new List<Dictionary<string, string>>();
            var article1 = new Dictionary<string, string>
            {
                [FieldName.ContentItemId] = "0",
                ["Description"] = $@"<a href=""{DbConnector.GetImagesUploadUrl(Global.SiteId)}"">test</a>"
            };

            values.Add(article1);
            Assert.DoesNotThrow(() => DbConnector.MassUpdate(ContentId, values, 1, new MassUpdateOptions { ReplaceUrls = false }), "Add article");

            var id = int.Parse(values[0][FieldName.ContentItemId]);
            var ids = new[] { id };
            var desc = Global.GetFieldValues<string>(DbConnector, ContentId, "Description", ids)[0];
            Assert.That(desc, Does.Not.Contain(@"<%=upload_url%>"));
        }

        [Test]
        public void AddFormToContent_ArticleAddedWithDefaultValues_ValidateAttributeValueNewArticleWithMissedData()
        {
            var article1 = new Hashtable
            {
                [TitleName] = "newtest"
            };

            var id = 0;
            Assert.DoesNotThrow(() => { id = DbConnector.AddFormToContent(Global.SiteId, ContentName, "Published", ref article1, 0); }, "Add article");

            var ids = new[] { id };
            Assert.That(id, Is.Not.EqualTo(0), "Return id");

            var desc = Global.GetFieldValues<string>(DbConnector, ContentId, "Description", ids)[0];
            var num = (int)Global.GetNumbers(DbConnector, ContentId, ids)[0];
            var cnt = Global.CountLinks(DbConnector, ids);

            Assert.That(num, Is.Not.EqualTo(0), "Default number");
            Assert.That(desc, Is.Not.Null.Or.Empty, "Default description");
            Assert.That(cnt, Is.EqualTo(2), "Default M2M");

            if (EfLinksExists)
            {
                var cntEf = Global.CountEfLinks(DbConnector, ids, ContentId);
                Assert.That(cntEf, Is.EqualTo(2), "Default EF M2M");
            }
        }

        [Test]
        public void MassUpdate_DoesntCreateVersionDirectory_ContentDoesntHaveFileFields()
        {
            var mockFileSystem = new Mock<IFileSystem>();
            DbConnector.FileSystem = mockFileSystem.Object;
            mockFileSystem.Setup(x => x.CreateDirectory(It.IsAny<string>()));

            var values = new List<Dictionary<string, string>>();
            var article1 = new Dictionary<string, string>
            {
                [FieldName.ContentItemId] = BaseArticlesIds[0].ToString()
            };

            values.Add(article1);
            var article2 = new Dictionary<string, string>
            {
                [FieldName.ContentItemId] = BaseArticlesIds[1].ToString()
            };

            values.Add(article2);
            Assert.DoesNotThrow(() => DbConnector.MassUpdate(ContentId, values, 1), "Update");

            mockFileSystem.Verify(x => x.CreateDirectory(It.IsAny<string>()), Times.Never(), "Shouldn't be called");
        }

        [Test]
        public void AddFormToContent_UpdateArchiveVisible_UpdateFlagsTrue()
        {
            var article1 = new Hashtable
            {
                [TitleName] = "abc",
                [MainCategoryName] = CategoryIds[0]
            };

            var id = 0;
            Assert.DoesNotThrow(() => { id = DbConnector.AddFormToContent(Global.SiteId, ContentName, "Published", ref article1, 0); }, "Add article");

            var ids = new[] { id };
            var visibleBefore = Global.GetFieldValues<decimal>(DbConnector, ContentId, "Visible", ids)[0];
            var archiveBefore = Global.GetFieldValues<decimal>(DbConnector, ContentId, "Archive", ids)[0];
            var modified = DateTime.MinValue;
            Assert.DoesNotThrow(() => { id = DbConnector.AddFormToContent(Global.SiteId, ContentId, "Published", ref article1, id, true, 0, false, true, true, ref modified); }, "Update article");

            var visibleAfter = Global.GetFieldValues<decimal>(DbConnector, ContentId, "Visible", ids)[0];
            var archiveAfter = Global.GetFieldValues<decimal>(DbConnector, ContentId, "Archive", ids)[0];
            Assert.That(visibleBefore, Is.Not.EqualTo(visibleAfter), "Visible changed");
            Assert.That(archiveBefore, Is.Not.EqualTo(archiveAfter), "Archive changed");
            Assert.That(visibleAfter, Is.EqualTo(0), "Visible updated");
            Assert.That(archiveAfter, Is.EqualTo(1), "Archive updated");
        }

        [Test]
        public void AddFormToContent_UpdateOnlyOneField_AttrIdProvided()
        {
            var article1 = new Hashtable
            {
                [TitleName] = "txt",
                [MainCategoryName] = CategoryIds[0]
            };

            var article2 = new Hashtable
            {
                [MainCategoryName] = CategoryIds[1]
            };

            var mainCatId = int.Parse(MainCategoryName.Replace("field_", string.Empty));
            var id = 0;
            Assert.DoesNotThrow(() => { id = DbConnector.AddFormToContent(Global.SiteId, ContentName, "Published", ref article1, 0); }, "Add article");

            var ids = new[] { id };
            var titleBefore = Global.GetFieldValues<string>(DbConnector, ContentId, "Title", ids)[0];
            var catBefore = (int)Global.GetFieldValues<decimal>(DbConnector, ContentId, "MainCategory", ids)[0];

#if ASPNETCORE || NETSTANDARD
            Assert.DoesNotThrow(() => { id = DbConnector.AddFormToContent(Global.SiteId, ContentId, "Published", ref article2, id, true, mainCatId); }, "Update article");
#else
            var files = (HttpFileCollection)null;
            Assert.DoesNotThrow(() => { id = DbConnector.AddFormToContent(Global.SiteId, ContentId, "Published", ref article2, ref files, id, true, mainCatId); }, "Update article");
#endif

            var titleAfter = Global.GetFieldValues<string>(DbConnector, ContentId, "Title", ids)[0];
            var catAfter = (int)Global.GetFieldValues<decimal>(DbConnector, ContentId, "MainCategory", ids)[0];
            Assert.That(titleBefore, Is.EqualTo(titleAfter), "Same Title");
            Assert.That(catBefore, Is.Not.EqualTo(catAfter), "Category changed");
            Assert.That(catAfter, Is.EqualTo(CategoryIds[1]), "Category updated");
        }

        [Test]
        public void AddFormToContent_UpdateOnlyNonEmpty_UpdateEmptyTrue()
        {
            var article1 = new Hashtable
            {
                [TitleName] = "pdf",
                [NumberName] = "10",
                [MainCategoryName] = CategoryIds[0]
            };

            var article2 = new Hashtable
            {
                [TitleName] = "docx",
                [NumberName] = string.Empty
            };

            var id = 0;
            Assert.DoesNotThrow(() => { id = DbConnector.AddFormToContent(Global.SiteId, ContentName, "Published", ref article1, 0); }, "Add article");

            var ids = new[] { id };
            var titleBefore = Global.GetFieldValues<string>(DbConnector, ContentId, "Title", ids)[0];
            var catBefore = (int)Global.GetFieldValues<decimal>(DbConnector, ContentId, "MainCategory", ids)[0];
            var numBefore = (int)Global.GetFieldValues<decimal>(DbConnector, ContentId, "Number", ids)[0];

#if ASPNETCORE || NETSTANDARD
            Assert.DoesNotThrow(() => { id = DbConnector.AddFormToContent(Global.SiteId, ContentName, "Published", ref article2, id, false); }, "Update article");
#else
            var files = (HttpFileCollection)null;
            Assert.DoesNotThrow(() => { id = DbConnector.AddFormToContent(Global.SiteId, ContentName, "Published", ref article2, ref files, id, false); }, "Update article");
#endif
            var titleAfter = Global.GetFieldValues<string>(DbConnector, ContentId, "Title", ids)[0];
            var catAfter = (int)Global.GetFieldValues<decimal>(DbConnector, ContentId, "MainCategory", ids)[0];
            var numAfter = (int)Global.GetFieldValues<decimal>(DbConnector, ContentId, "Number", ids)[0];
            Assert.That(titleBefore, Is.Not.EqualTo(titleAfter), "Changed Title");
            Assert.That(catBefore, Is.EqualTo(catAfter), "Same Category");
            Assert.That(numBefore, Is.EqualTo(numAfter), "Same Number");
            Assert.That(titleAfter, Is.EqualTo(article2[TitleName]), "Category updated");
        }

        [Test]
        public void AddFormToContent_NullifyM2M_ForEmptyM2MData()
        {
            var ints1 = new[] { CategoryIds[1], CategoryIds[3], CategoryIds[5] };
            var article1 = new Hashtable
            {
                [TitleName] = "newtest",
                [CategoryName] = string.Join(",", ints1),
                [MainCategoryName] = CategoryIds[0]
            };

            var article2 = new Hashtable
            {
                [TitleName] = "newtest",
                [CategoryName] = string.Empty,
                [MainCategoryName] = CategoryIds[0]
            };

            var id = 0;
            Assert.DoesNotThrow(() => { id = DbConnector.AddFormToContent(Global.SiteId, ContentName, "Published", ref article1, 0); }, "Create");

            var ids = new[] { id };
            var cntLinks = Global.CountLinks(DbConnector, ids);
            Assert.That(cntLinks, Is.Not.EqualTo(0), "Links saved");

            if (EfLinksExists)
            {
                var cntEfLinks = Global.CountEfLinks(DbConnector, ids, ContentId);
                Assert.That(cntEfLinks, Is.EqualTo(cntLinks), "EF links saved");
            }

            Assert.DoesNotThrow(() => { id = DbConnector.AddFormToContent(Global.SiteId, ContentName, "Published", ref article2, id); }, "Update");

            var cntLinksAfter = Global.CountLinks(DbConnector, ids);
            Assert.That(cntLinksAfter, Is.EqualTo(0), "Links nullified");
            if (EfLinksExists)
            {
                var cntEfLinksAfter = Global.CountEfLinks(DbConnector, ids, ContentId);
                Assert.That(cntEfLinksAfter, Is.EqualTo(0), "EF links nullified");
            }
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
