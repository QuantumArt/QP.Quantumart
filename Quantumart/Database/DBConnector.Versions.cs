using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using System.IO;
using System.Linq;
using Quantumart.QPublishing.Helpers;
using Quantumart.QPublishing.Info;

// ReSharper disable once CheckNamespace
namespace Quantumart.QPublishing.Database
{
    // ReSharper disable once InconsistentNaming
    public partial class DBConnector
    {
        public DataTable GetArticleVersions(int id) => GetRealData("exec qp_get_versions " + id);

        private static string GetVersionFolderFormat() => @"{0}\_qp7_article_files_versions\{1}";

        public string GetContentLibraryFolder(int articleId)
        {
            var contentId = GetContentIdForItem(articleId);
            var siteId = GetSiteIdByContentId(contentId);
            return GetContentLibraryDirectory(siteId, contentId);
        }

        public string GetVersionFolder(int articleId, int versionId) => versionId == 0 ? string.Empty : string.Format(GetVersionFolderFormat(), GetContentLibraryFolder(articleId), versionId);

        public string GetVersionFolderForContent(int contentId, int versionId) => versionId == 0 ? string.Empty : string.Format(GetVersionFolderFormat(), GetContentLibraryDirectory(contentId), versionId);

        public string GetCurrentVersionFolder(int articleId) => string.Format(GetVersionFolderFormat(), GetContentLibraryFolder(articleId), "current");

        public string GetCurrentVersionFolderForContent(int contentId) => string.Format(GetVersionFolderFormat(), GetContentLibraryDirectory(contentId), "current");

        private IEnumerable<ContentAttribute> GetFilesAttributesForVersionControl(int contentId)
        {
            return GetContentAttributeObjects(contentId).Where(n => (n.Type == AttributeType.Image || n.Type == AttributeType.File) && !n.DisableVersionControl);
        }

        private void CreateFilesVersions(int articleId)
        {
            var contentId = GetContentIdForItem(articleId);
            var newVersionId = GetLatestVersionId(articleId);
            var currentVersionFolder = GetCurrentVersionFolder(articleId);
            if (newVersionId != 0)
            {
                var files = GetFilesAttributesForVersionControl(contentId)
                    .Select(n => GetVersionDbDataValue(newVersionId, n))
                    .Where(n => !string.IsNullOrEmpty(n))
                    .Select(n => new FileToCopy
                    {
                        Name = Path.GetFileName(n),
                        Folder = currentVersionFolder,
                        ToFolder = GetVersionFolder(articleId, newVersionId)
                    });

                CopyArticleFiles(files);
            }

            var newFiles = GetFilesAttributesForVersionControl(contentId).Select(n => new FileToCopy
            {
                Name = GetDbDataValue(articleId, n),
                Folder = GetDirectoryForFileAttribute(n.Id),
                ToFolder = currentVersionFolder
            }).Where(n => !string.IsNullOrEmpty(n.Name));

            CopyArticleFiles(newFiles);
        }

        private string GetDbDataValue(int articleId, ContentAttribute field)
        {
            var cmd = CreateDbCommand();
            var dbFieldName = field.Type == AttributeType.Textbox || field.Type == AttributeType.VisualEdit ? "BLOB_DATA" : "DATA";
            cmd.CommandText = $"select {dbFieldName} from content_data where content_item_id = @itemId and attribute_id = @fieldId";
            cmd.Parameters.AddWithValue("@itemId", articleId);
            cmd.Parameters.AddWithValue("@fieldId", field.Id);
            var result = GetRealScalarData(cmd);
            return result?.ToString() ?? string.Empty;
        }

        private string GetVersionDbDataValue(int versionId, ContentAttribute field)
        {
            var cmd = CreateDbCommand();
            var dbFieldName = field.Type == AttributeType.Textbox || field.Type == AttributeType.VisualEdit ? "BLOB_DATA" : "DATA";
            cmd.CommandText = $"select {dbFieldName} from version_content_data where content_item_version_id = @versionId and attribute_id = @fieldId";
            cmd.Parameters.AddWithValue("@versionId", versionId);
            cmd.Parameters.AddWithValue("@fieldId", field.Id);
            var result = GetRealScalarData(cmd);
            return result?.ToString() ?? string.Empty;
        }

        private DataTable GetVersionDataValues(IEnumerable<int> versionIds, IEnumerable<int> attrIds)
        {
            var cmd = CreateDbCommand($@"
                select attribute_id, content_item_version_id, data from version_content_data
                where content_item_version_id in (select id from {IdList("@versionIds", "i")}) and attribute_id in (select id from {IdList("@attrIds", "a")})
            ");
            cmd.Parameters.Add(SqlQuerySyntaxHelper.GetIdsDatatableParam("@versionIds", versionIds, DatabaseType));
            cmd.Parameters.Add(SqlQuerySyntaxHelper.GetIdsDatatableParam("@attrIds", attrIds, DatabaseType));
            return GetRealData(cmd);
        }

        private class FileToCopy
        {
            public string Name { get; set; }

            public string Folder { get; set; }

            public string ToFolder { get; set; }
        }

        private void CopyArticleFiles(IEnumerable<FileToCopy> files)
        {
            var fileToCopies = files as FileToCopy[] ?? files.ToArray();
            foreach (var file in fileToCopies)
            {
                FileSystem.CreateDirectory(file.ToFolder);
                FileSystem.CreateDirectory(file.Folder);

                var sourceName = $@"{file.Folder}{Path.DirectorySeparatorChar}{file.Name.Replace("/", Path.DirectorySeparatorChar.ToString())}";
                var destName = $@"{file.ToFolder}{Path.DirectorySeparatorChar}{Path.GetFileName(file.Name)}";
                FileSystem.CopyFile(sourceName, destName);
            }
        }

        private int GetEarliestVersionId(int articleId) => GetAggregateVersionFunction("MIN", articleId);

        private int GetLatestVersionId(int articleId) => GetAggregateVersionFunction("MAX", articleId);

        private IEnumerable<int> GetLatestVersionIds(int[] ids) => GetAggregateVersionFunction("MAX", ids);

        private int GetVersionsCount(int articleId) => GetAggregateVersionFunction("COUNT", articleId);

        private int GetAggregateVersionFunction(string function, int articleId)
        {
            if (articleId == 0)
            {
                return 0;
            }


            using (var cmd = CreateDbCommand())
            {
                cmd.CommandText = $"select cast({function}(content_item_version_id) as int) from content_item_version where content_item_id = @id";
                cmd.Parameters.AddWithValue("@id", articleId);
                return CastDbNull.To<int>(GetRealScalarData(cmd));
            }
        }

        private IEnumerable<int> GetAggregateVersionFunction(string function, int[] ids)
        {
            if (!ids.Any())
            {
                return new int[0];
            }

            using (var cmd = CreateDbCommand())
            {
                cmd.CommandText = $@"
                    select cast({function}(content_item_version_id) as int) as data
                    from content_item_version where content_item_id in (select id from {IdList()})
                    group by content_item_id
                ";
                cmd.Parameters.Add(SqlQuerySyntaxHelper.GetIdsDatatableParam("@ids", ids, DatabaseType));
                return GetRealData(cmd).Select().Select(row => Convert.ToInt32(row["data"])).ToArray();
            }
        }
    }
}
