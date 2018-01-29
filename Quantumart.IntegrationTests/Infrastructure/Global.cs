using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using QP8.Infrastructure.Helpers;
using QP8.Infrastructure.Helpers.ProcessHelpers;
using Quantumart.QPublishing.Database;

namespace Quantumart.IntegrationTests.Infrastructure
{
    internal static class Global
    {
        private const int Lcid = 1049;

        public const string QpDbUpdateToolPath = @"Tools/qpdbupdate.exe";

        public static int SiteId => 35;
        
        public static string ConnectionString => $"Initial Catalog={TestEnvironmentHelpers.SqlDbNameToRunTests};Data Source=mscsql01;Integrated Security=True;Application Name=UnitTest";

        public static string GetXml(string fileName) => File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, fileName));

        public static int[] GetIds(DBConnector dbConnector, int contentId) => dbConnector.GetRealData($"select content_item_id from content_{contentId}_united")
            .Select()
            .Select(row => Convert.ToInt32(row["content_item_id"]))
            .OrderBy(row => row)
            .ToArray();

        public static int[] GetIdsFromArchive(DBConnector dbConnector, int[] ids) => dbConnector.GetRealData($"select content_item_id from content_item where archive = 1 and content_item_id in ({string.Join(",", ids)})")
            .Select()
            .Select(row => Convert.ToInt32(row["content_item_id"]))
            .OrderBy(row => row)
            .ToArray();

        public static Dictionary<string, int> GetIdsWithTitles(DBConnector dbConnector, int contentId) => dbConnector.GetRealData($"select content_item_id, Title from content_{contentId}_united")
            .Select()
            .Select(row => new { Id = Convert.ToInt32(row["content_item_id"]), Title = Convert.ToString(row["Title"]) })
            .ToDictionary(row => row.Title, row => row.Id);

        public static DateTime[] GetModified(DBConnector dbConnector, int contentId) => dbConnector.GetRealData($"select Modified from content_{contentId}_united")
            .Select()
            .Select(row => Convert.ToDateTime(row["Modified"]))
            .ToArray();

        public static int CountLinks(DBConnector dbConnector, int[] ids, bool isAsync = false)
        {
            var asyncString = isAsync ? "_async" : string.Empty;
            return dbConnector.GetRealData($"select count(*) as cnt from item_link{asyncString} where item_id in ({string.Join(",", ids)})")
                .Select()
                .Select(row => Convert.ToInt32(row["cnt"]))
                .Single();
        }

        public static int CountEfLinks(DBConnector dbConnector, int[] ids, int contentId, bool isAsync = false)
        {
            var asyncString = isAsync ? "_async" : string.Empty;
            return dbConnector.GetRealData($"select count(*) as cnt from item_link_{contentId}{asyncString} where item_id in ({string.Join(",", ids)})")
                .Select()
                .Select(row => Convert.ToInt32(row["cnt"]))
                .Single();
        }

        public static int[] GetLinks(DBConnector dbConnector, int[] ids, bool isAsync = false)
        {
            var asyncString = isAsync ? "_async" : string.Empty;
            return dbConnector.GetRealData($"select linked_item_id as id from item_link{asyncString} where item_id in ({string.Join(",", ids)})")
                .Select()
                .Select(row => Convert.ToInt32(row["id"]))
                .OrderBy(row => row)
                .ToArray();
        }

        public static int[] GetEfLinks(DBConnector dbConnector, int[] ids, int contentId, bool isAsync = false)
        {
            var asyncString = isAsync ? "_async" : string.Empty;
            return dbConnector.GetRealData($"select linked_item_id as id from item_link{contentId}{asyncString} where item_id in ({string.Join(",", ids)})")
                .Select()
                .Select(row => Convert.ToInt32(row["id"]))
                .OrderBy(row => row)
                .ToArray();
        }

        public static bool EfLinksExists(DBConnector dbConnector, int contentId)
        {
            using (var cmd = new SqlCommand($"select cast(count(null) as bit) cnt from sysobjects where xtype='u' and name='item_link_{contentId}'"))
            {
                return (bool)dbConnector.GetRealScalarData(cmd);
            }
        }

        public static int CountData(DBConnector dbConnector, int[] ids) => dbConnector.GetRealData($"select count(*) as cnt from content_data where content_item_id in ({string.Join(",", ids)})")
            .Select()
            .Select(row => Convert.ToInt32(row["cnt"]))
            .Single();

        public static int CountVersionData(DBConnector dbConnector, int[] ids) => dbConnector.GetRealData($"select count(*) as cnt from version_content_data where content_item_version_id in ({string.Join(",", ids)})")
            .Select()
            .Select(row => Convert.ToInt32(row["cnt"]))
            .Single();

        public static int CountVersionLinks(DBConnector dbConnector, int[] ids) => dbConnector.GetRealData($"select count(*) as cnt from item_to_item_version where content_item_version_id in ({string.Join(",", ids)})")
            .Select()
            .Select(row => Convert.ToInt32(row["cnt"]))
            .Single();

        public static int CountArticles(DBConnector dbConnector, int contentId, int[] ids = null, bool isAsync = false)
        {
            var asyncString = isAsync ? "_async" : string.Empty;
            var idsString = ids != null ? $"where content_item_id in ({string.Join(",", ids)})" : string.Empty;
            return dbConnector.GetRealData($"select count(*) as cnt from content_{contentId}{asyncString} {idsString}")
                .Select()
                .Select(row => Convert.ToInt32(row["cnt"]))
                .Single();
        }

        public static string[] GetTitles(DBConnector localdbConnector, int contentId, int[] ids = null, bool isAsync = false) => GetFieldValues<string>(localdbConnector, contentId, "Title", ids, isAsync);

        public static T[] GetFieldValues<T>(DBConnector localdbConnector, int contentId, string fieldName, int[] ids = null, bool isAsync = false)
        {
            var asyncString = isAsync ? "_async" : string.Empty;
            var idsString = ids != null ? $"where content_item_id in ({string.Join(",", ids)})" : string.Empty;
            return localdbConnector.GetRealData($"select [{fieldName}] from content_{contentId}{asyncString} {idsString}")
                .Select()
                .Select(row => ConvertHelpers.ChangeType<T>(row[fieldName]))
                .ToArray();
        }

        public static decimal[] GetNumbers(DBConnector localdbConnector, int contentId, int[] ids = null, bool isAsync = false) => GetFieldValues<decimal?>(localdbConnector, contentId, "Number", ids, isAsync)
            .Select(n => n ?? 0)
            .ToArray();

        public static int[] GetMaxVersions(DBConnector localdbConnector, int[] ids) => localdbConnector.GetRealData($"select max(content_item_version_id) as id from content_item_version where content_item_id in ({string.Join(",", ids)}) group by content_item_id")
            .Select()
            .Select(row => Convert.ToInt32(row["id"]))
            .ToArray();

        public static int GetContentId(DBConnector dbConnector, string contentName) => dbConnector.GetRealData($"select content_id from content where site_id = {SiteId} and content_name = '{contentName}'")
            .Select()
            .Select(row => Convert.ToInt32(row["content_id"]))
            .SingleOrDefault();

        public static int GetFieldId(DBConnector dbConnector, string contentName, string fieldName) => dbConnector.FieldID(SiteId, contentName, fieldName);

        public static void ClearContentData(DBConnector dbConnector, int articleId)
        {
            using (var cmd = new SqlCommand("delete from CONTENT_DATA where CONTENT_ITEM_ID = @id"))
            {
                cmd.Parameters.AddWithValue("@id", articleId);
                dbConnector.GetRealData(cmd);
            }
        }

        public static ContentDataItem[] GetContentData(DBConnector dbConnector, int articleId)
        {
            using (var cmd = new SqlCommand("select * from CONTENT_DATA where CONTENT_ITEM_ID = @id"))
            {
                cmd.Parameters.AddWithValue("@id", articleId);
                return dbConnector.GetRealData(cmd)
                    .Select()
                    .Select(row => new ContentDataItem
                    {
                        FieldId = Convert.ToInt32(row["ATTRIBUTE_ID"]),
                        Data = Convert.ToString(row["DATA"]),
                        BlobData = Convert.ToString(row["BLOB_DATA"])
                    }).ToArray();
            }
        }

        public static void ReplayXml(string filePath)
        {
            var assemblyDirLocation = new Uri(Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase) ?? string.Empty).AbsolutePath;
            var exitCode = ProcessHelpers.ExecuteFileAndReadOutput(new ProcessExecutionSettings
            {
                ProcessExePath = Path.Combine(assemblyDirLocation, QpDbUpdateToolPath),
                Arguments = $@"-s -vvv --disableDataIntegrity --disablePipedInput -m=""xml"" -p={Path.Combine(assemblyDirLocation, filePath)} ""{ConnectionString}"""
            }, out var _, out var errorOutput);

            if (exitCode != 0)
            {
                throw new Exception($"Exit code was: {exitCode}, but should be 0. Error output from db update utility: {errorOutput}");
            }
        }

        public static void RemoveFieldIfExists(int fieldId, int contentId)
        {
            ReplayDynamicXml(
                $@"<?xml version=""1.0"" encoding=""utf-8""?>
<actions backendUrl=""http://mscdev02:90/Backend/"" dbVersion=""7.9.9.0"">
  <action code=""remove_field"" ids=""{fieldId}"" parentId=""{contentId}"" lcid=""{Lcid}"" executed=""{DateTime.Now.ToString(CultureHelpers.GetCultureByLcid(Lcid))}"" executedBy=""AuthorProxy"" />
</actions>
");
        }

        public static void RemoveArticlesIfExists(int[] articleIds, int contentId)
        {
            ReplayDynamicXml(
                $@"<?xml version=""1.0"" encoding=""utf-8""?>
<actions backendUrl=""http://mscdev02:90/Backend/"" dbVersion=""7.9.9.0"">
  <action code=""multiple_remove_article"" ids=""{string.Join(",", articleIds)}"" parentId=""{contentId}"" lcid=""{Lcid}"" executed=""{DateTime.Now.ToString(CultureHelpers.GetCultureByLcid(Lcid))}"" executedBy=""AuthorProxy"" />
</actions>
");
        }

        public static void RemoveContentIfExists(int contentId)
        {
            if (contentId > 0)
            {
                ReplayDynamicXml(
                    $@"<?xml version=""1.0"" encoding=""utf-8""?>
<actions backendUrl=""http://mscdev02:90/Backend/"" dbVersion=""7.9.9.0"">
  <action code=""simple_remove_content"" ids=""{contentId}"" parentId=""35"" lcid=""{Lcid}"" executed=""{DateTime.Now.ToString(CultureHelpers.GetCultureByLcid(Lcid))}"" executedBy=""AuthorProxy"" />
</actions>
");
            }
        }

        private static void ReplayDynamicXml(string standardInputData)
        {
            var assemblyDirLocation = new Uri(Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase) ?? string.Empty).AbsolutePath;
            var exitCode = ProcessHelpers.ExecuteFileAndReadOutput(new ProcessExecutionSettings
            {
                ProcessExePath = Path.Combine(assemblyDirLocation, QpDbUpdateToolPath),
                Arguments = $@"-s -vvv --disableDataIntegrity ""{ConnectionString}""",
                StandardInputData = standardInputData
            }, out var _, out var errorOutput);

            if (exitCode != 0)
            {
                throw new Exception($"Exit code was: {exitCode}, but should be 0. Error output from db update utility: {errorOutput}");
            }
        }
    }
}
