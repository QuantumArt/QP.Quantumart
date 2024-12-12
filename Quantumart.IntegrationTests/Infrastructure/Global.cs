using System;
using System.Collections.Generic;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Reflection;
using Npgsql;
using NUnit.Framework;
using QP.ConfigurationService.Models;
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

        public static string ConnectionString
        {
            get
            {
                var basePart = $"Database={EnvHelpers.DbNameToRunTests};Server={EnvHelpers.DbServerToRunTests};Application Name=UnitTest;";
                if (!String.IsNullOrEmpty(EnvHelpers.PgDbLoginToRunTests))
                {
                    return $"{basePart}User Id={EnvHelpers.PgDbLoginToRunTests};Password={EnvHelpers.PgDbPasswordToRunTests}";
                }

                return $"{basePart}Integrated Security=True;TrustServerCertificate=true;Connection Timeout=600";
            }
        }

        public static DatabaseType DBType => !String.IsNullOrEmpty(EnvHelpers.PgDbLoginToRunTests) ? DatabaseType.Postgres : DatabaseType.SqlServer;

        public static string GetXml(string fileName) => File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, fileName));

        public static int[] GetIds(DBConnector dbConnector, int contentId)
        {
            return dbConnector.GetRealData($"select content_item_id from content_{contentId}_united")
                .Select()
                .Select(row => Convert.ToInt32(row["content_item_id"]))
                .OrderBy(row => row)
                .ToArray();
        }

        public static int[] GetIdsFromArchive(DBConnector dbConnector, int[] ids)
        {
            var sql = $"select content_item_id from content_item where archive = 1 and content_item_id in ({IdsStr(ids)})";
            return dbConnector.GetRealData(sql)
                .Select()
                .Select(row => Convert.ToInt32(row["content_item_id"]))
                .OrderBy(row => row)
                .ToArray();
        }

        public static Dictionary<string, int> GetIdsWithTitles(DBConnector dbConnector, int contentId)
        {
            return dbConnector.GetRealData($"select content_item_id, Title from content_{contentId}_united")
                .Select()
                .Select(row => new { Id = Convert.ToInt32(row["content_item_id"]), Title = Convert.ToString(row["Title"]) })
                .ToDictionary(row => row.Title, row => row.Id);
        }

        public static DateTime[] GetModified(DBConnector dbConnector, int contentId)
        {
            var sql = $"select Modified from content_{contentId}_united";
            return dbConnector.GetRealData(sql)
                .Select()
                .Select(row => Convert.ToDateTime(row["Modified"]))
                .ToArray();
        }

        public static int GetLinkId(DBConnector dbConnector, int contentId, string attributeName)
        {
            var sql = $@"select coalesce(link_id, 0) as link_id from content_attribute where content_id = {contentId} and attribute_name = '{attributeName}'";
            return dbConnector.GetRealData(sql).Select().Select(row => Convert.ToInt32(row["link_id"])).Single();
        }

        public static int CountLinks(DBConnector dbConnector, int[] ids, bool isAsync = false)
        {
            var asyncString = isAsync ? "_async" : string.Empty;
            var sql = $"select count(*) as cnt from item_link{asyncString} where item_id in ({IdsStr(ids)})";
            return dbConnector.GetRealData(sql)
                .Select()
                .Select(row => Convert.ToInt32(row["cnt"]))
                .Single();
        }

        private static string IdsStr(int[] ids)
        {
            var result = string.Join(",", ids);
            return (string.IsNullOrEmpty(result)) ? "0" : result;
        }

        public static int CountEfLinks(DBConnector dbConnector, int[] ids, int contentId, bool isAsync = false)
        {
            var asyncString = isAsync ? "_async" : string.Empty;
            var sql = $"select count(*) as cnt from item_link_{contentId}{asyncString} where id in ({IdsStr(ids)})";
            return dbConnector.GetRealData(sql)
                .Select()
                .Select(row => Convert.ToInt32(row["cnt"]))
                .Single();
        }

        public static int[] GetLinks(DBConnector dbConnector, int[] ids, bool isAsync = false)
        {
            var asyncString = isAsync ? "_async" : string.Empty;
            var sql = $"select linked_item_id as id from item_link{asyncString} where item_id in ({IdsStr(ids)})";
            return dbConnector.GetRealData(sql)
                .Select()
                .Select(row => Convert.ToInt32(row["id"]))
                .OrderBy(row => row)
                .ToArray();
        }

        public static int[] GetEfLinks(DBConnector dbConnector, int[] ids, int contentId, bool isAsync = false)
        {
            var asyncString = isAsync ? "_async" : string.Empty;
            var sql = $"select linked_id as id from item_link_{contentId}{asyncString} where id in ({IdsStr(ids)})";
            return dbConnector.GetRealData(sql)
                .Select()
                .Select(row => Convert.ToInt32(row["id"]))
                .OrderBy(row => row)
                .ToArray();
        }

        public static int CountData(DBConnector dbConnector, int[] ids)
        {
            var sql = $"select count(*) as cnt from content_data where content_item_id in ({IdsStr(ids)})";
            return dbConnector.GetRealData(sql)
                .Select()
                .Select(row => Convert.ToInt32(row["cnt"]))
                .Single();
        }

        public static int CountVersionData(DBConnector dbConnector, int[] ids)
        {
            var sql = $"select count(*) as cnt from version_content_data where content_item_version_id in ({IdsStr(ids)})";
            return dbConnector.GetRealData(sql)
                .Select()
                .Select(row => Convert.ToInt32(row["cnt"]))
                .Single();
        }

        public static int CountVersionLinks(DBConnector dbConnector, int[] ids)
        {
            var sql = $"select count(*) as cnt from item_to_item_version where content_item_version_id in ({IdsStr(ids)})";
            return dbConnector.GetRealData(sql)
                .Select()
                .Select(row => Convert.ToInt32(row["cnt"]))
                .Single();
        }

        public static int CountArticles(DBConnector dbConnector, int contentId, int[] ids = null, bool isAsync = false)
        {
            var asyncString = isAsync ? "_async" : string.Empty;
            var idsString = ids != null ? $"where content_item_id in ({IdsStr(ids)})" : string.Empty;
            var sql = $"select count(*) as cnt from content_{contentId}{asyncString} {idsString}";
            return dbConnector.GetRealData(sql)
                .Select()
                .Select(row => Convert.ToInt32(row["cnt"]))
                .Single();
        }

        public static string[] GetTitles(DBConnector localdbConnector, int contentId, int[] ids = null, bool isAsync = false)
        {
            return GetFieldValues<string>(localdbConnector, contentId, "Title", ids, isAsync);
        }

        public static T[] GetFieldValues<T>(DBConnector localdbConnector, int contentId, string fieldName, int[] ids = null, bool isAsync = false)
        {
            var asyncString = isAsync ? "_async" : string.Empty;
            var idsString = ids != null ? $"where content_item_id in ({IdsStr(ids)})" : string.Empty;
            var fn = SqlQuerySyntaxHelper.FieldName(localdbConnector.DatabaseType, fieldName);
            var sql = $"select {fn} from content_{contentId}{asyncString} {idsString} order by content_item_id asc";
            return localdbConnector.GetRealData(sql).Select().Select(row => ConvertHelpers.ChangeType<T>(row[fieldName])).ToArray();
        }

        public static decimal[] GetNumbers(DBConnector localdbConnector, int contentId, int[] ids = null, bool isAsync = false)
        {
            return GetFieldValues<decimal?>(localdbConnector, contentId, "Number", ids, isAsync)
                .Select(n => n ?? 0)
                .ToArray();
        }

        public static int[] GetMaxVersions(DBConnector localdbConnector, int[] ids)
        {
            var sql = $"select max(content_item_version_id) as id from content_item_version where content_item_id in ({IdsStr(ids)}) group by content_item_id";
            return localdbConnector.GetRealData(sql)
                .Select()
                .Select(row => Convert.ToInt32(row["id"]))
                .ToArray();
        }

        public static int GetContentId(DBConnector dbConnector, string contentName)
        {
            var sql = $"select content_id from content where site_id = {SiteId} and content_name = '{contentName}'";
            return dbConnector.GetRealData(sql)
                .Select()
                .Select(row => Convert.ToInt32(row["content_id"]))
                .SingleOrDefault();
        }

        public static int GetFieldId(DBConnector dbConnector, string contentName, string fieldName) => dbConnector.FieldID(SiteId, contentName, fieldName);

        public static void ReplayXml(string filePath)
        {
            var assemblyDirLocation = new Uri(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty).AbsolutePath;
            var exitCode = ProcessHelpers.ExecuteFileAndReadOutput(new ProcessExecutionSettings
            {
                ProcessExePath = Path.Combine(assemblyDirLocation, QpDbUpdateToolPath),
                Arguments = $@"-s -vvv --disableDataIntegrity --disablePipedInput -m=""xml"" -t={(int)DBType} -p={Path.Combine(assemblyDirLocation, filePath)} ""{ConnectionString}""",
                ProcessTimeout = 1000 * 1000
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
            var assemblyDirLocation = new Uri(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty).AbsolutePath;
            var exitCode = ProcessHelpers.ExecuteFileAndReadOutput(new ProcessExecutionSettings
            {
                ProcessExePath = Path.Combine(assemblyDirLocation, QpDbUpdateToolPath),
                Arguments = $@"-s -vvv --disableDataIntegrity -t={(int)DBType} -m=""xml"" ""{ConnectionString}""",
                StandardInputData = standardInputData
            }, out var _, out var errorOutput);

            if (exitCode != 0)
            {
                throw new Exception($"Exit code was: {exitCode}, but should be 0. Error output from db update utility: {errorOutput}");
            }
        }

        internal static DbConnection CreateConnection()
        {
            if (DBType == DatabaseType.Postgres)
            {
                return new NpgsqlConnection(ConnectionString);
            }

            return new SqlConnection(ConnectionString);
        }
    }
}
