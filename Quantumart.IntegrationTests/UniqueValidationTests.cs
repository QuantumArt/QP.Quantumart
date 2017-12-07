using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using NUnit.Framework;
using Quantumart.IntegrationTests.Constants;
using Quantumart.IntegrationTests.Infrastructure;
using Quantumart.QPublishing.Database;
using Quantumart.QPublishing.Info;

#if ASPNETCORE
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
#endif

namespace Quantumart.IntegrationTests
{
    [TestFixture]
    public class UniqueValidationTests
    {
        private const string ContentName = "Test unique";

        public static DBConnector DbConnector { get; private set; }

        public static int ContentId { get; private set; }

        public static int[] BaseArticlesIds { get; private set; }

        [OneTimeSetUp]
        public static void Init()
        {
#if ASPNETCORE
            DbConnector = new DBConnector(new DbConnectorSettings { ConnectionString = Global.ConnectionString }, new MemoryCache(new MemoryCacheOptions()), new HttpContextAccessor()) { ForceLocalCache = true };
#else
            DbConnector = new DBConnector(Global.ConnectionString) { ForceLocalCache = true };
#endif
            Clear();

            Global.ReplayXml(@"TestData/unique.xml");

            ContentId = Global.GetContentId(DbConnector, ContentName);
            BaseArticlesIds = Global.GetIds(DbConnector, ContentId);
        }

        [Test]
        public void MassUpdate_DoesntThrowException_SelfValidateForNonExistingFields()
        {
            var values = new List<Dictionary<string, string>>();
            var article1 = new Dictionary<string, string>
            {
                [FieldName.ContentItemId] = BaseArticlesIds[0].ToString(),
                ["abc"] = "Name3"
            };

            values.Add(article1);
            var article2 = new Dictionary<string, string>
            {
                [FieldName.ContentItemId] = BaseArticlesIds[1].ToString(),
                ["abc"] = "Name3"
            };

            values.Add(article2);
            Assert.DoesNotThrow(() => DbConnector.MassUpdate(ContentId, values, 1), "Non-existing fields shouldn't violate rules");
        }

        [Test]
        public void MassUpdate_ThrowsException_SelfValidateForDataConflict()
        {
            var values = new List<Dictionary<string, string>>();
            var article1 = new Dictionary<string, string>
            {
                [FieldName.ContentItemId] = "0",
                ["Title"] = "Name3"
            };

            values.Add(article1);
            var article2 = new Dictionary<string, string>
            {
                [FieldName.ContentItemId] = "0",
                ["Title"] = "Name3"
            };

            values.Add(article2);
            Assert.That(
                () => DbConnector.MassUpdate(ContentId, values, 1),
                Throws.Exception.TypeOf<QpInvalidAttributeException>().And.Message.Contains("between articles being added/updated"),
                "Field Title should violate rules"
            );
        }

        [Test]
        public void MassUpdate_ThrowsException_ValidateConstraintForDataConflict()
        {
            var values = new List<Dictionary<string, string>>();
            var id = (decimal)DbConnector.GetRealScalarData(new SqlCommand($"select content_item_id from content_{ContentId}_united where [Title] <> 'Name2'"));
            var article1 = new Dictionary<string, string>
            {
                [FieldName.ContentItemId] = id.ToString(CultureInfo.InvariantCulture),
                ["Title"] = "Name2",
                ["Number"] = "9,5"
            };

            values.Add(article1);
            Assert.That(() => DbConnector.MassUpdate(ContentId, values, 1), Throws.Exception.TypeOf<QpInvalidAttributeException>().And.Message.Contains("for content articles"), "Duplicate of test data should violate rules");
        }

        [Test]
        public void MassUpdate_ThrowsNothing_NullifyField()
        {
            var values = new List<Dictionary<string, string>>();
            var article1 = new Dictionary<string, string>
            {
                [FieldName.ContentItemId] = "0",
                ["Title"] = "Name2",
                ["Number"] = ""
            };

            values.Add(article1);
            Assert.That(() => DbConnector.MassUpdate(ContentId, values, 1), Throws.Nothing);
            DbConnector.DeleteContentItem(int.Parse(article1[FieldName.ContentItemId]));
        }

        [Test]
        public void MassUpdate_ThrowsException_ValidateConstraintForNull()
        {
            var values = new List<Dictionary<string, string>>();
            var article1 = new Dictionary<string, string>
            {
                [FieldName.ContentItemId] = "0",
                ["Title"] = "Name2",
                ["Number"] = ""
            };

            values.Add(article1);
            Assert.That(() => DbConnector.MassUpdate(ContentId, values, 1), Throws.Nothing);

            values.Clear();
            var article2 = new Dictionary<string, string>
            {
                [FieldName.ContentItemId] = "0",
                ["Title"] = "Name2",
                ["Number"] = ""
            };
            values.Add(article2);
            Assert.That(() => DbConnector.MassUpdate(ContentId, values, 1), Throws.Exception.TypeOf<QpInvalidAttributeException>().And.Message.Contains("Unique constraint violation"));

            DbConnector.DeleteContentItem(int.Parse(article1[FieldName.ContentItemId]));
            DbConnector.DeleteContentItem(int.Parse(article2[FieldName.ContentItemId]));
        }

        [Test]
        public void AddFormToContent_ThrowsException_ValidateConstraintForDataConflict()
        {
            var titleName = DbConnector.FieldName(Global.SiteId, ContentName, "Title");
            var numberName = DbConnector.FieldName(Global.SiteId, ContentName, "Number");
            var id = (int)(decimal)DbConnector.GetRealScalarData(new SqlCommand($"select content_item_id from content_{ContentId}_united where [Title] <> 'Name2'"));
            var article1 = new Hashtable
            {
                [titleName] = "Name2",
                [numberName] = "9,5"
            };

            Assert.That(() => { id = DbConnector.AddFormToContent(Global.SiteId, ContentName, "Published", ref article1, id); }, Throws.Exception.TypeOf<QpInvalidAttributeException>().And.Message.Contains("Unique constraint violation"), "Duplicate of test data should violate rules");
        }

        [Test]
        public void MassUpdate_UpdateNothing_InCaseOfAnyError()
        {
            var maxId = (decimal)DbConnector.GetRealScalarData(new SqlCommand("select max(content_item_id) from content_item") { CommandType = CommandType.Text });
            var values = new List<Dictionary<string, string>>();
            var article1 = new Dictionary<string, string>
            {
                [FieldName.ContentItemId] = BaseArticlesIds[0].ToString(),
                ["Title"] = "Name5"
            };

            values.Add(article1);
            var article2 = new Dictionary<string, string>
            {
                [FieldName.ContentItemId] = BaseArticlesIds[1].ToString(),
                ["Title"] = "Name5"
            };

            values.Add(article2);
            Assert.That(() => { DbConnector.MassUpdate(ContentId, values, 1); }, Throws.Exception);

            var maxIdAfter =
                (decimal)DbConnector.GetRealScalarData(new SqlCommand("select max(content_item_id) from content_item")
                {
                    CommandType = CommandType.Text
                });

            var titles = Global.GetTitles(DbConnector, ContentId);
            Assert.That(titles, Does.Not.Contain("Name5"), "In case of any error the internal transaction should be rolled back");
            Assert.That(maxId, Is.EqualTo(maxIdAfter), "No new content items");
        }

        [Test]
        public void MassUpdate_UpdateNothing_InCaseOfAnyErrorAndExternalTransaction()
        {
            var values = new List<Dictionary<string, string>>();
            var article1 = new Dictionary<string, string>
            {
                [FieldName.ContentItemId] = BaseArticlesIds[0].ToString(),
                ["Title"] = "Name5",
                ["Number"] = "10"
            };

            values.Add(article1);
            var article2 = new Dictionary<string, string>
            {
                [FieldName.ContentItemId] = BaseArticlesIds[1].ToString(),
                ["Title"] = "Name5"
            };

            values.Add(article2);
            var values2 = new List<Dictionary<string, string>>();
            var article3 = new Dictionary<string, string>
            {
                [FieldName.ContentItemId] = "0"
            };

            values2.Add(article3);
            using (var cn = new SqlConnection(Global.ConnectionString))
            {
                cn.Open();
                var tr = cn.BeginTransaction();
#if ASPNETCORE
                var localConnector = new DBConnector(cn, tr, new DbConnectorSettings { ConnectionString = Global.ConnectionString }, new MemoryCache(new MemoryCacheOptions()), new HttpContextAccessor());
#else
                var localConnector = new DBConnector(cn, tr);
#endif

                Assert.DoesNotThrow(() => localConnector.MassUpdate(ContentId, values, 1));
                Assert.That(() => { localConnector.MassUpdate(ContentId, values2, 1); }, Throws.Exception);

                tr.Rollback();
                var titles = Global.GetTitles(localConnector, ContentId);
                Assert.That(titles, Does.Not.Contain("Name5"), "In case of any error the external transaction should be rolled back");
            }
        }

        [Test]
        public void AddFormToContent_UpdateNothing_InCaseOfAnyErrorAndExternalTransaction()
        {
            var titlesBefore = Global.GetTitles(DbConnector, ContentId);
            Assert.That(titlesBefore, Does.Not.Contain("Name5"), "correct state");

            var titleName = DbConnector.FieldName(Global.SiteId, ContentName, "Title");
            var numberName = DbConnector.FieldName(Global.SiteId, ContentName, "Number");
            var article1 = new Hashtable
            {
                [titleName] = "Name5",
                [numberName] = "10"
            };

            var article3 = new Hashtable
            {
                [FieldName.ContentItemId] = "0"
            };

            using (var cn = new SqlConnection(Global.ConnectionString))
            {
                cn.Open();
                var tr = cn.BeginTransaction();
#if ASPNETCORE
                var localConnector = new DBConnector(cn, tr, new DbConnectorSettings { ConnectionString = Global.ConnectionString }, new MemoryCache(new MemoryCacheOptions()), new HttpContextAccessor());
#else
                var localConnector = new DBConnector(cn, tr);
#endif

                Assert.DoesNotThrow(() => { localConnector.AddFormToContent(Global.SiteId, ContentName, "Published", ref article1, BaseArticlesIds[0]); }, "Update existing data");

                Assert.That(() => { localConnector.AddFormToContent(Global.SiteId, ContentName, "Published", ref article3, 0); }, Throws.Exception, "Invalid data");

                tr.Rollback();
                var titles = Global.GetTitles(localConnector, ContentId);
                Assert.That(titles, Does.Not.Contain("Name5"), "In case of any error the external transaction should be rolled back");
            }
        }

        [Test]
        public void MassUpdate_IsValid_ValidateConstraintSameData()
        {
            var values = new List<Dictionary<string, string>>();
            var first = ContentItem.Read(BaseArticlesIds[0], DbConnector);
            var second = ContentItem.Read(BaseArticlesIds[1], DbConnector);
            var article1 = new Dictionary<string, string>
            {
                [FieldName.ContentItemId] = BaseArticlesIds[0].ToString(),
                ["Title"] = first.FieldValues["Title"].Data,
                ["Number"] = first.FieldValues["Number"].Data
            };

            values.Add(article1);
            var article2 = new Dictionary<string, string>
            {
                [FieldName.ContentItemId] = BaseArticlesIds[1].ToString(),
                ["Title"] = second.FieldValues["Title"].Data,
                ["Number"] = second.FieldValues["Number"].Data
            };

            values.Add(article2);
            var modified = Global.GetModified(DbConnector, ContentId);
            Assert.DoesNotThrow(() => DbConnector.MassUpdate(ContentId, values, 1), "Update existing data");

            var modified2 = Global.GetModified(DbConnector, ContentId);
            var first2 = ContentItem.Read(BaseArticlesIds[0], DbConnector);
            var second2 = ContentItem.Read(BaseArticlesIds[1], DbConnector);

            Assert.That(modified, Is.Not.EqualTo(modified2), "Modification dates should be changed");
            Assert.That(first2.FieldValues["Title"].Data, Is.EqualTo(first.FieldValues["Title"].Data), "Data should remain the same");
            Assert.That(second2.FieldValues["Title"].Data, Is.EqualTo(second.FieldValues["Title"].Data), "Data should remain the same");
        }

        [Test]
        public void AddFormToContent_IsValid_ValidateConstraintSameData()
        {
            var first = ContentItem.Read(BaseArticlesIds[0], DbConnector);
            var titleName = DbConnector.FieldName(Global.SiteId, ContentName, "Title");
            var numberName = DbConnector.FieldName(Global.SiteId, ContentName, "Number");
            var article1 = new Hashtable
            {
                [titleName] = first.FieldValues["Title"].Data,
                [numberName] = first.FieldValues["Number"].Data
            };

            var modified = Global.GetModified(DbConnector, ContentId);
            Assert.DoesNotThrow(() => { DbConnector.AddFormToContent(Global.SiteId, ContentName, "Published", ref article1, BaseArticlesIds[0]); }, "Update existing data");

            var modified2 = Global.GetModified(DbConnector, ContentId);
            var first2 = ContentItem.Read(BaseArticlesIds[0], DbConnector);

            Assert.That(modified, Is.Not.EqualTo(modified2), "Modification dates should be changed");
            Assert.That(first2.FieldValues["Title"].Data, Is.EqualTo(first.FieldValues["Title"].Data), "Data should remain the same");
        }

        [Test]
        public void MassUpdate_IsValid_ValidateConstraintSwapData()
        {
            var values = new List<Dictionary<string, string>>();
            var first = ContentItem.Read(BaseArticlesIds[0], DbConnector);
            var second = ContentItem.Read(BaseArticlesIds[1], DbConnector);
            var article1 = new Dictionary<string, string>
            {
                [FieldName.ContentItemId] = BaseArticlesIds[0].ToString(),
                ["Title"] = second.FieldValues["Title"].Data,
                ["Number"] = second.FieldValues["Number"].Data
            };

            values.Add(article1);
            var article2 = new Dictionary<string, string>
            {
                [FieldName.ContentItemId] = BaseArticlesIds[1].ToString(),
                ["Title"] = first.FieldValues["Title"].Data,
                ["Number"] = first.FieldValues["Number"].Data
            };

            values.Add(article2);
            var modified = Global.GetModified(DbConnector, ContentId);
            Assert.DoesNotThrow(() => DbConnector.MassUpdate(ContentId, values, 1), "Swap existing data");

            var modified2 = Global.GetModified(DbConnector, ContentId);
            var first2 = ContentItem.Read(BaseArticlesIds[0], DbConnector);
            var second2 = ContentItem.Read(BaseArticlesIds[1], DbConnector);

            Assert.That(modified, Is.Not.EqualTo(modified2), "Modification dates should be changed");
            Assert.That(first2.FieldValues["Title"].Data, Is.EqualTo(second.FieldValues["Title"].Data), "Data should be swapped");
            Assert.That(second2.FieldValues["Title"].Data, Is.EqualTo(first.FieldValues["Title"].Data), "Data should be swapped");
        }

        [Test]
        public void MassUpdate_ThrowsException_ValidateAttributeValueMissedData()
        {
            var values = new List<Dictionary<string, string>>();
            var article1 = new Dictionary<string, string>
            {
                [FieldName.ContentItemId] = "0"
            };

            values.Add(article1);
            Assert.That(() => DbConnector.MassUpdate(ContentId, values, 1),
                Throws.Exception.TypeOf<QpInvalidAttributeException>().And.Message.Contains("is required"),
                "Validate required fields"
            );
        }

        [Test]
        public void AddFormToContent_ThrowsException_ValidateAttributeValueMissedData()
        {
            var article1 = new Hashtable
            {
                [FieldName.ContentItemId] = "0"
            };

            Assert.That(() => { DbConnector.AddFormToContent(Global.SiteId, ContentName, "Published", ref article1, BaseArticlesIds[0]); }, Throws.Exception.TypeOf<QpInvalidAttributeException>().And.Message.Contains("is required"), "Validate required fields");
        }

        [Test]
        public void MassUpdate_ThrowsException_ValidateAttributeValueStringSizeExceeded()
        {
            var values = new List<Dictionary<string, string>>();
            var article1 = new Dictionary<string, string>
            {
                [FieldName.ContentItemId] = BaseArticlesIds[0].ToString(),
                ["Title"] = new string('*', 1000)
            };

            values.Add(article1);
            Assert.That(
                () => DbConnector.MassUpdate(ContentId, values, 1),
                Throws.Exception.TypeOf<QpInvalidAttributeException>().And.Message.Contains("too long"),
                "Validate string size"
            );
        }

        [Test]
        public void AddFormToContent_ThrowsException_ValidateAttributeValueStringSizeExceeded()
        {
            var titleName = DbConnector.FieldName(Global.SiteId, ContentName, "Title");
            var article1 = new Hashtable
            {
                [titleName] = new string('*', 1000)
            };

            Assert.That(() => { DbConnector.AddFormToContent(Global.SiteId, ContentName, "Published", ref article1, BaseArticlesIds[0]); }, Throws.Exception.TypeOf<QpInvalidAttributeException>().And.Message.Contains("too long"), "Validate string size");
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
