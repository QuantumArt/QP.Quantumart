﻿using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using Quantumart.QPublishing.Helpers;
using Quantumart.QPublishing.Info;

namespace Quantumart.QPublishing.Database
{
    // ReSharper disable once InconsistentNaming
    public partial class DBConnector
    {
        public DataTable GetArticleVersions(int id)
        {
            return GetRealData("exec qp_get_versions " + id);
        }

        private static string GetVersionFolderFormat()
        {
            return @"{0}\_qp7_article_files_versions\{1}";
        }

        public string GetContentLibraryFolder(int articleId)
        {
            var contentId = GetContentIdForItem(articleId);
            var siteId = GetSiteIdByContentId(contentId);
            return GetContentLibraryDirectory(siteId, contentId);
        }

        public string GetVersionFolder(int articleId, int versionId)
        {
            return versionId == 0 ? string.Empty : string.Format(GetVersionFolderFormat(), GetContentLibraryFolder(articleId), versionId);
        }

        public string GetVersionFolderForContent(int contentId, int versionId)
        {
            return versionId == 0 ? string.Empty : string.Format(GetVersionFolderFormat(), GetContentLibraryDirectory(contentId), versionId);
        }

        public string GetCurrentVersionFolder(int articleId)
        {
            return string.Format(GetVersionFolderFormat(), GetContentLibraryFolder(articleId), "current");
        }

        public string GetCurrentVersionFolderForContent(int contentId)
        {
            return string.Format(GetVersionFolderFormat(), GetContentLibraryDirectory(contentId), "current");
        }

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
            var cmd = new SqlCommand { CommandType = CommandType.Text };
            var dbFieldName = field.Type == AttributeType.Textbox || field.Type == AttributeType.VisualEdit ? "BLOB_DATA" : "DATA";
            cmd.CommandText = $"select {dbFieldName} from content_data where content_item_id = @itemId and attribute_id = @fieldId";
            cmd.Parameters.AddWithValue("@itemId", articleId);
            cmd.Parameters.AddWithValue("@fieldId", field.Id);
            var result = GetRealScalarData(cmd);
            return result?.ToString() ?? string.Empty;
        }

        private string GetVersionDbDataValue(int versionId, ContentAttribute field)
        {
            var cmd = new SqlCommand { CommandType = CommandType.Text };
            var dbFieldName = field.Type == AttributeType.Textbox || field.Type == AttributeType.VisualEdit ? "BLOB_DATA" : "DATA";
            cmd.CommandText = $"select {dbFieldName} from version_content_data where content_item_version_id = @versionId and attribute_id = @fieldId";
            cmd.Parameters.AddWithValue("@versionId", versionId);
            cmd.Parameters.AddWithValue("@fieldId", field.Id);
            var result = GetRealScalarData(cmd);
            return result?.ToString() ?? string.Empty;

        }

        private DataTable GetVersionDataValues(IEnumerable<int> versionIds, IEnumerable<int> attrIds)
        {
            var cmd = new SqlCommand
            {
                CommandType = CommandType.Text,
                CommandText = "select attribute_id, content_item_version_id, data from version_content_data where content_item_version_id in (select id from @versionIds) and attribute_id in (select id from @attrIds)",
                Parameters =
                {
                    new SqlParameter("@versionIds", SqlDbType.Structured) { TypeName = "Ids", Value = IdsToDataTable(versionIds) },
                    new SqlParameter("@attrIds", SqlDbType.Structured) { TypeName = "Ids", Value = IdsToDataTable(attrIds) }
                }
            };

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

                var sourceName = $@"{file.Folder}\{file.Name.Replace("/", "\\")}";
                var destName = $@"{file.ToFolder}\{Path.GetFileName(file.Name)}";
                FileSystem.CopyFile(sourceName, destName);
            }
        }

        private int GetEarliestVersionId(int articleId)
        {
            return GetAggregateVersionFunction("MIN", articleId);
        }

        private int GetLatestVersionId(int articleId)
        {
            return GetAggregateVersionFunction("MAX", articleId);
        }

        private int[] GetLatestVersionIds(int[] ids)
        {
            return GetAggregateVersionFunction("MAX", ids);
        }

        private int GetVersionsCount(int articleId)
        {
            return GetAggregateVersionFunction("COUNT", articleId);
        }

        private int GetAggregateVersionFunction(string function, int articleId)
        {
            if (articleId == 0)
            {
                return 0;
            }

            using (var cmd = new SqlCommand())
            {
                cmd.CommandType = CommandType.Text;
                cmd.CommandText = $"select cast({function}(content_item_version_id) as int) from content_item_version where content_item_id = @id";
                cmd.Parameters.AddWithValue("@id", articleId);
                return CastDbNull.To<int>(GetRealScalarData(cmd));
            }
        }

        private int[] GetAggregateVersionFunction(string function, int[] ids)
        {
            if (!ids.Any())
            {
                return new int[0];
            }

            var cmd = new SqlCommand
            {
                CommandType = CommandType.Text,
                CommandText = $"select cast({function}(content_item_version_id) as int) as data from content_item_version where content_item_id in (select id from @ids) group by content_item_id",
                Parameters =
                {
                    new SqlParameter("@ids", SqlDbType.Structured) {TypeName = "Ids", Value = IdsToDataTable(ids)}
                }
            };

            return GetRealData(cmd).AsEnumerable().Select(n => n.Field<int>("data")).ToArray();
        }
    }
}
