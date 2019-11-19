using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Quantumart.QPublishing.Info;
using Quantumart.QPublishing.Resizer;

// ReSharper disable once CheckNamespace
namespace Quantumart.QPublishing.Database
{
    public class MassUpdateOptions
    {
        public MassUpdateOptions()
        {
            CreateVersions = true;
            ReturnModified = true;
            ReplaceUrls = true;
            IsDefault = false;
        }

        public bool IsDefault { get; set; }

        public bool CreateVersions { get; set; }

        public bool ReturnModified { get; set; }

        public bool ReplaceUrls { get; set; }

    }

    // ReSharper disable once InconsistentNaming
    public partial class DBConnector
    {
        public void MassUpdate(int contentId, IEnumerable<Dictionary<string, string>> values, int lastModifiedBy)
        {
            MassUpdate(contentId, values, lastModifiedBy, new MassUpdateOptions() { IsDefault = true });
        }

        public void MassUpdate(int contentId, IEnumerable<Dictionary<string, string>> values, int lastModifiedBy, MassUpdateOptions options)
        {
            var content = GetContentObject(contentId);
            if (content == null)
            {
                throw new Exception($"Content not found (ID = {contentId})");
            }

            if (content.VirtualType > 0)
            {
                throw new Exception($"Cannot modify virtual content (ID = {contentId})");
            }

            if (options.IsDefault)
            {
                options.ReplaceUrls = GetReplaceUrlsInDB(content.SiteId);
            }

            var arrValues = values as Dictionary<string, string>[] ?? values.ToArray();
            var existingIds = arrValues.Select(n => int.Parse(n[SystemColumnNames.Id])).Where(n => n != 0).ToArray();
            var versionIdsToRemove = GetVersionIdsToRemove(existingIds, content.MaxVersionNumber);
            var createVersions = options.CreateVersions && content.UseVersionControl;

            CreateInternalConnection(true);
            try
            {
                var doc = GetImportContentItemDocument(arrValues, content);
                var newIds = MassUpdateContentItem(contentId, arrValues, lastModifiedBy, doc, createVersions);
                var fullAttrs = GetContentAttributeObjects(contentId).Where(n => n.Type != AttributeType.M2ORelation).ToArray();
                var resultAttrs = GetResultAttrs(arrValues, fullAttrs, newIds);

                CreateDynamicImages(arrValues, fullAttrs);

                ValidateConstraints(arrValues, fullAttrs, content, options.ReplaceUrls);

                var dataDoc = GetMassUpdateContentDataDocument(arrValues, resultAttrs, newIds, content, options.ReplaceUrls);
                ImportContentData(dataDoc);

                var attrString = string.Join(",", resultAttrs.Select(n => n.Id.ToString()).ToArray());
                ReplicateData(arrValues, attrString);

                var manyToManyAttrs = resultAttrs.Where(n => n.Type == AttributeType.Relation && n.LinkId.HasValue).ToArray();
                if (manyToManyAttrs.Any())
                {
                    var linkDoc = GetImportItemLinkDocument(arrValues, manyToManyAttrs);
                    ImportItemLink(linkDoc);
                }

                if (options.ReturnModified)
                {
                    UpdateModified(arrValues, existingIds, newIds, contentId);
                }

                if (createVersions)
                {
                    CreateFilesVersions(arrValues, existingIds, contentId);
                    foreach (var id in versionIdsToRemove)
                    {
                        var oldFolder = GetVersionFolderForContent(contentId, id);
                        FileSystem.RemoveDirectory(oldFolder);
                    }
                }

                CommitInternalTransaction();
            }
            finally
            {
                DisposeInternalConnection();
            }
        }

        private static ContentAttribute[] GetResultAttrs(Dictionary<string, string>[] arrValues, ContentAttribute[] fullAttrs, int[] newIds)
        {
            var resultAttrs = fullAttrs;
            var isNewArticle = newIds.Any();
            if (!isNewArticle)
            {
                var names = new HashSet<string>(arrValues.SelectMany(n => n.Keys).Distinct().Select(n => n.ToLowerInvariant()));
                if (fullAttrs.Any(n => n.Type == AttributeType.DynamicImage))
                {
                    names.UnionWith(GetDynamicImageExtraNames(arrValues, fullAttrs));
                }

                resultAttrs = fullAttrs.Where(n => names.Contains(n.Name.ToLowerInvariant())).ToArray();
            }

            return resultAttrs;
        }

        private static IEnumerable<string> GetDynamicImageExtraNames(Dictionary<string, string>[] arrValues, ContentAttribute[] fullAttrs)
        {
            var baseImages = fullAttrs.Where(n => n.Type == AttributeType.Image).Where(n => arrValues.Any(m => m.ContainsKey(n.Name))).ToDictionary(n => n.Id, m => m.Name.ToLowerInvariant());
            var baseImagesValues = baseImages.Select(n => n.Value).ToArray();
            var dynImages = fullAttrs.Where(n => n.Type == AttributeType.DynamicImage)

                // ReSharper disable once PossibleInvalidOperationException
                .Where(n => baseImages.ContainsKey(n.RelatedImageId.Value))
                .Select(n => new
                {
                    BaseImageName = baseImages[n.RelatedImageId.Value],
                    DynImageName = n.Name.ToLowerInvariant()
                })
                .GroupBy(n => n.BaseImageName).ToDictionary(
                    n => n.Key,
                    n => n.Select(k => k.DynImageName).ToArray()
                );

            return baseImagesValues.SelectMany(n => dynImages.ContainsKey(n) ? dynImages[n] : Enumerable.Empty<string>()).ToArray();
        }

        private void UpdateModified(IEnumerable<Dictionary<string, string>> arrValues, IEnumerable<int> existingIds, int[] newIds, int contentId)
        {
            var cmd = GetUpdateModifiedCommand(existingIds, newIds, contentId);

            var arrModified = GetRealData(cmd)
                .Select()
                .ToDictionary(kRow => Convert.ToInt32(kRow["content_item_id"]), vRow => Convert.ToDateTime(vRow["modified"]));

            var newHash = new HashSet<int>(newIds);
            foreach (var value in arrValues)
            {
                var id = int.Parse(value[SystemColumnNames.Id]);
                if (id != 0 && arrModified.TryGetValue(id, out var modified))
                {
                    value[SystemColumnNames.Modified] = modified.ToString("MM/dd/yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture);
                    if (newHash.Contains(id))
                    {
                        value[SystemColumnNames.Created] = value[SystemColumnNames.Modified];
                    }
                }
            }
        }

        private SqlCommand GetUpdateModifiedCommand(IEnumerable<int> existingIds, int[] newIds, int contentId)
        {
            return new SqlCommand
            {
                CommandText = $"select content_item_id, Modified from content_{contentId}_united with(nolock) where content_item_id in (select id from @ids)",
                CommandType = CommandType.Text,
                Parameters =
                {
                    new SqlParameter("@ids", SqlDbType.Structured)
                    {
                        TypeName = "Ids",
                        Value = IdsToDataTable(existingIds.Union(newIds))
                    }
                }
            };
        }

        private int[] GetVersionIdsToRemove(int[] ids, int maxNumber)
        {
            var cmd = GetVersionIdsToRemoveCommand(ids, maxNumber);
            return GetRealData(cmd).Select().Select(row => Convert.ToInt32(row["content_item_version_id"])).ToArray();
        }

        private SqlCommand GetVersionIdsToRemoveCommand(int[] ids, int maxNumber)
        {
            var cmd = new SqlCommand(@"  select content_item_version_id from
                (
                    select content_item_id, content_item_version_id,
                    row_number() over(partition by civ.content_item_id order by civ.content_item_version_id desc) as num
                    from content_item_version civ
                    where content_item_id in (select id from @ids)
                    ) c
                    where c.num >= @maxNumber")
            {
                CommandType = CommandType.Text
            };

            cmd.Parameters.AddWithValue("@maxNumber", maxNumber);
            cmd.Parameters.Add(new SqlParameter("@ids", SqlDbType.Structured) { TypeName = "Ids", Value = IdsToDataTable(ids) });
            return cmd;
        }

        private void CreateDynamicImages(Dictionary<string, string>[] arrValues, ContentAttribute[] fullAttrs)
        {
            foreach (var dynImageAttr in fullAttrs.Where(n => n.RelatedImageId.HasValue))
            {
                if (dynImageAttr.RelatedImageId == null)
                {
                    continue;
                }

                var imageAttr = fullAttrs.Single(n => n.Id == dynImageAttr.RelatedImageId.Value);
                var attrDir = GetDirectoryForFileAttribute(imageAttr.Id);
                var contentDir = GetContentLibraryDirectory(imageAttr.SiteId, imageAttr.ContentId);
                foreach (var article in arrValues)
                {
                    if (article.TryGetValue(imageAttr.Name, out var image))
                    {
                        var info = new DynamicImageInfo
                        {
                            ContentLibraryPath = contentDir,
                            ImagePath = attrDir,
                            ImageName = image,
                            AttrId = dynImageAttr.Id,
                            Width = dynImageAttr.DynamicImage.Width,
                            Height = dynImageAttr.DynamicImage.Height,
                            Quality = dynImageAttr.DynamicImage.Quality,
                            FileType = dynImageAttr.DynamicImage.Type,
                            MaxSize = dynImageAttr.DynamicImage.MaxSize
                        };


                        DynamicImageCreator.CreateDynamicImage(info);
                        article[dynImageAttr.Name] = DynamicImage.GetDynamicImageRelUrl(info?.ImageName, info.AttrId, info.FileType);
                    }
                }
            }
        }

        private void CreateFilesVersions(IEnumerable<Dictionary<string, string>> values, int[] ids, int contentId)
        {
            var fileAttrs = GetFilesAttributesForVersionControl(contentId).ToArray();
            if (fileAttrs.Any())
            {
                var newVersionIds = GetLatestVersionIds(ids).ToList();
                var fileAttrIds = fileAttrs.Select(n => n.Id).ToArray();
                var fileAttrDirs = fileAttrs.ToDictionary(n => n.Name, m => GetDirectoryForFileAttribute(m.Id));
                var currentVersionFolder = GetCurrentVersionFolderForContent(contentId);
                if (newVersionIds.Any())
                {
                    var files = GetVersionDataValues(newVersionIds, fileAttrIds)
                        .Select()
                        .Select(row => new
                        {
                            FieldId = Convert.ToInt32(row["attribute_id"]),
                            VersionId = Convert.ToInt32(row["content_item_version_id"]),
                            Data = Convert.ToString(row["data"])
                        })
                        .Where(n => !string.IsNullOrEmpty(n.Data))
                        .Select(n => new FileToCopy
                        {
                            Name = Path.GetFileName(n.Data),
                            Folder = currentVersionFolder,
                            ToFolder = GetVersionFolderForContent(contentId, n.VersionId)
                        }).Distinct().ToArray();

                    CopyArticleFiles(files);
                }

                var strIds = new HashSet<string>(ids.Select(n => n.ToString()));
                var newFiles = values
                    .Where(n => strIds.Contains(n[SystemColumnNames.Id]))
                    .SelectMany(n => n)
                    .Where(n => fileAttrDirs.ContainsKey(n.Key) && !string.IsNullOrEmpty(n.Value))
                    .Distinct()
                    .Select(n => new FileToCopy
                    {
                        Name = n.Value,
                        Folder = fileAttrDirs[n.Key],
                        ToFolder = currentVersionFolder
                    }).ToArray();

                CopyArticleFiles(newFiles);
            }
        }

        private void ValidateConstraints(IList<Dictionary<string, string>> values, IEnumerable<ContentAttribute> attrs, Content content, bool replaceUrls)
        {
            var validatedAttrs = attrs.Where(n => n.ConstraintId.HasValue).ToArray();
            if (validatedAttrs.Any())
            {
                var constraints = validatedAttrs.GroupBy(n => n.ConstraintId).Select(n => new
                {
                    // ReSharper disable once PossibleInvalidOperationException
                    Id = (int)n.Key,
                    Attrs = n.ToArray()
                }).ToArray();

                foreach (var constraint in constraints)
                {
                    var validatedDataDoc = GetValidatedDataDocument(values, constraint.Attrs, content, replaceUrls);
                    SelfValidate(validatedDataDoc);
                    ValidateConstraint(validatedDataDoc, constraint.Attrs);
                }
            }
        }

        private static void SelfValidate(XDocument validatedDataDoc)
        {
            if (validatedDataDoc.Root != null)
            {
                var items = validatedDataDoc.Root.Descendants("ITEM").ToArray();
                var set = new HashSet<string>();
                foreach (var item in items)
                {
                    var str = string.Join("", item.Elements("DATA").Select(n => n.ToString()));
                    if (!string.IsNullOrEmpty(str))
                    {
                        if (set.Contains(str))
                        {
                            var msg = string.Join(", ", item.Descendants("DATA").Select(n => $"[{n.Attribute("name")?.Value}] = '{n.Value}'"));
                            throw new QpInvalidAttributeException("Unique constraint violation between articles being added/updated: " + msg);
                        }

                        set.Add(str);
                    }
                }
            }
        }

        private XDocument GetValidatedDataDocument(IEnumerable<Dictionary<string, string>> values, IEnumerable<ContentAttribute> attrs, Content content, bool replaceUrls)
        {
            var longUploadUrl = GetImagesUploadUrl(content.SiteId);
            var longSiteLiveUrl = GetSiteUrl(content.SiteId, true);
            var longSiteStageUrl = GetSiteUrl(content.SiteId, false);
            var result = new XDocument();
            var root = new XElement("ITEMS", values.Select(m =>
            {
                var temp = new XElement("ITEM");
                temp.Add(new XAttribute("id", m[SystemColumnNames.Id]));
                temp.Add(attrs.Select(n =>
                {
                    var valueExists = m.TryGetValue(n.Name, out var value);
                    if (valueExists)
                    {
                        value = FormatResult(n, value, longUploadUrl, longSiteStageUrl, longSiteLiveUrl, replaceUrls);
                    }

                    var elem = valueExists ? new XElement("DATA", value) : new XElement("MISSED_DATA");
                    elem.Add(new XAttribute("name", n.Name));
                    elem.Add(new XAttribute("id", n.Id));

                    return elem;
                }));

                return temp;
            }));

            result.Add(root);
            return result;
        }

        private void ValidateConstraint(XContainer validatedDataDoc, IReadOnlyList<ContentAttribute> attrs)
        {
            string attrNames;
            var cmd = GetValidateConstraintCommand(validatedDataDoc, attrs, out attrNames);
            var conflictIds = GetRealData(cmd).Select().Select(row => Convert.ToInt32(row["CONTENT_ITEM_ID"])).ToArray();
            if (conflictIds.Any())
            {
                throw new QpInvalidAttributeException($"Unique constraint violation for content articles. Fields: {attrNames}. Article IDs: {string.Join(", ", conflictIds.ToArray())}");
            }
        }

        private SqlCommand GetValidateConstraintCommand(XContainer validatedDataDoc, IReadOnlyList<ContentAttribute> attrs, out string attrNames)
        {
            var sb = new StringBuilder();
            var validatedIds = validatedDataDoc
                .Descendants("ITEM")
                .Where(n => !n.Descendants("MISSED_DATA").Any())
                .Select(n => int.Parse(n.Attribute("id")?.Value ?? throw new InvalidOperationException()))
                .ToArray();

            var contentId = attrs[0].ContentId;
            attrNames = string.Join(", ", attrs.Select(n => n.Name));
            sb.AppendLine("declare @default_num int, @default_date datetime;");
            sb.AppendLine("set @default_num = -2147483648;");
            sb.AppendLine("set @default_date = getdate();");

            sb.AppendLine($"WITH X(CONTENT_ITEM_ID, {attrNames})");
            sb.AppendLine(@"AS (SELECT doc.col.value('./@id', 'int') CONTENT_ITEM_ID");
            foreach (var attr in attrs)
            {
                sb.AppendLine($",doc.col.value('(DATA)[@id={attr.Id}][1]', 'nvarchar(max)') {attr.Name}");
            }

            sb.AppendLine("FROM @xmlParameter.nodes('/ITEMS/ITEM') doc(col))");
            sb.AppendLine($" SELECT c.CONTENT_ITEM_ID FROM dbo.CONTENT_{contentId}_UNITED c with(nolock) INNER JOIN X ON c.CONTENT_ITEM_ID NOT IN (select id from @validatedIds)");
            foreach (var attr in attrs)
            {
                if (attr.IsNumeric)
                {
                    sb.AppendLine($"AND ISNULL(c.[{attr.Name}], @default_num) = case when X.[{attr.Name}] = '' then @default_num else cast (X.[{attr.Name}] as numeric(18, {attr.Size})) end");
                }
                else if (attr.IsDateTime)
                {
                    sb.AppendLine($"AND ISNULL(c.[{attr.Name}], @default_date) = case when X.[{attr.Name}] = '' then @default_date else cast (X.[{attr.Name}] as datetime) end");
                }
                else
                {
                    sb.AppendLine($"AND ISNULL(c.[{attr.Name}], '') = ISNULL(X.[{attr.Name}], '')");
                }
            }

            var cmd = new SqlCommand(sb.ToString())
            {
                CommandTimeout = 120,
                CommandType = CommandType.Text
            };

            cmd.Parameters.Add(new SqlParameter("@xmlParameter", SqlDbType.Xml) { Value = validatedDataDoc.ToString(SaveOptions.None) });
            cmd.Parameters.Add(new SqlParameter("@validatedIds", SqlDbType.Structured) { TypeName = "Ids", Value = IdsToDataTable(validatedIds) });

            return cmd;
        }

        private XDocument GetMassUpdateContentDataDocument(IEnumerable<Dictionary<string, string>> values, ContentAttribute[] attrs, int[] newIds, Content content, bool replaceUrls)
        {
            var longUploadUrl = GetImagesUploadUrl(content.SiteId);
            var longSiteLiveUrl = GetSiteUrl(content.SiteId, true);
            var longSiteStageUrl = GetSiteUrl(content.SiteId, false);
            var dataDoc = new XDocument();
            dataDoc.Add(new XElement("ITEMS"));

            foreach (var value in values)
            {
                var isNewArticle = newIds.Contains(int.Parse(value[SystemColumnNames.Id]));
                foreach (var attr in attrs)
                {
                    var elem = new XElement("ITEM");
                    elem.Add(new XAttribute("id", value[SystemColumnNames.Id]));
                    elem.Add(new XAttribute("attrId", attr.Id));
                    var valueExists = value.TryGetValue(attr.Name, out var result);
                    if (attr.LinkId.HasValue)
                    {
                        if (!valueExists && isNewArticle && !string.IsNullOrEmpty(attr.DefaultValue))
                        {
                            value[attr.Name] = attr.DefaultValue;
                        }

                        result = attr.LinkId.Value.ToString();
                        valueExists = true;
                    }
                    else if (attr.BackRelation != null)
                    {
                        result = attr.BackRelation.Id.ToString();
                        valueExists = true;
                    }
                    else if (!string.IsNullOrEmpty(result))
                    {
                        result = FormatResult(attr, result, longUploadUrl, longSiteStageUrl, longSiteLiveUrl, replaceUrls);
                        result = XmlValidChars(result);
                    }
                    else if (isNewArticle)
                    {
                        result = attr.DefaultValue;
                    }

                    if (isNewArticle || valueExists)
                    {
                        ValidateAttributeValue(attr, result, true);
                        if (result != null)
                        {
                            elem.Add(new XElement(attr.DbField, result));
                        }
                        dataDoc.Root?.Add(elem);
                    }
                }
            }

            return dataDoc;
        }

        private string FormatResult(ContentAttribute attr, string result, string longUploadUrl, string longSiteStageUrl, string longSiteLiveUrl, bool replaceUrls)
        {
            switch (attr.DbTypeName)
            {
                case "DATETIME" when DateTime.TryParse(result, out DateTime _):
                    result = DateTime.Parse(result).ToString("yyyy-MM-dd HH:mm:ss");
                    break;
                case "NUMERIC":
                    result = result.Replace(",", ".");
                    break;
                default:
                    if (attr.Type == AttributeType.String || attr.Type == AttributeType.Textbox || attr.Type == AttributeType.VisualEdit)
                    {
                        if (replaceUrls)
                        {
                            result = result
                                .Replace(longUploadUrl, UploadPlaceHolder)
                                .Replace(longSiteStageUrl, SitePlaceHolder)
                                .Replace(longSiteLiveUrl, SitePlaceHolder);
                        }
                    }

                    break;
            }

            return result;
        }

        private int[] MassUpdateContentItem(int contentId, IEnumerable<Dictionary<string, string>> values, int lastModifiedBy, XDocument doc, bool createVersions)
        {
            var cmd = GetMassUpdateContentItemCommand(contentId, lastModifiedBy, doc, createVersions);

            var ids = new Queue<int>(GetRealData(cmd).Select().Select(row => Convert.ToInt32(row["ID"])).ToArray());
            var newIds = ids.ToArray();
            foreach (var value in values)
            {
                if (value[SystemColumnNames.Id] == "0")
                {
                    value[SystemColumnNames.Id] = ids.Dequeue().ToString();
                }
            }

            return newIds;
        }

        private SqlCommand GetMassUpdateContentItemCommand(int contentId, int lastModifiedBy, XDocument doc, bool createVersions)
        {
            var createVersionsString = createVersions
                ? "exec qp_create_content_item_versions @OldIds, @lastModifiedBy"
                : string.Empty;

            var insertInto = $@"
                DECLARE @Articles TABLE
                (
                    CONTENT_ITEM_ID NUMERIC,
                    STATUS_TYPE_ID NUMERIC,
                    VISIBLE NUMERIC,
                    ARCHIVE NUMERIC
                )

                DECLARE @NewArticles [Ids]
                DECLARE @OldIds [Ids]
                DECLARE @OldNonSplittedIds [Ids]
                DECLARE @NewSplittedIds [Ids]
                DECLARE @OldSplittedIds [Ids]
                DECLARE @NewNonSplittedIds [Ids]

                INSERT INTO @Articles
                    SELECT
                     doc.col.value('(CONTENT_ITEM_ID)[1]', 'numeric') CONTENT_ITEM_ID
                    ,doc.col.value('(STATUS_TYPE_ID)[1]', 'numeric') STATUS_TYPE_ID
                    ,doc.col.value('(VISIBLE)[1]', 'numeric') VISIBLE
                    ,doc.col.value('(ARCHIVE)[1]', 'numeric') ARCHIVE
                    FROM @xmlParameter.nodes('/ITEMS/ITEM') doc(col)

                INSERT into CONTENT_ITEM (CONTENT_ID, VISIBLE, ARCHIVE, STATUS_TYPE_ID, LAST_MODIFIED_BY, NOT_FOR_REPLICATION)
                OUTPUT inserted.[CONTENT_ITEM_ID] INTO @NewArticles
                SELECT @contentId, VISIBLE, ARCHIVE, STATUS_TYPE_ID, @lastModifiedBy, @notForReplication
                FROM @Articles a WHERE a.CONTENT_ITEM_ID = 0

                INSERT INTO @OldIds
                SELECT a.CONTENT_ITEM_ID from @Articles a INNER JOIN content_item ci with(rowlock, updlock) on a.CONTENT_ITEM_ID = ci.CONTENT_ITEM_ID

                {createVersionsString}

                INSERT INTO @OldNonSplittedIds
                SELECT i.Id from @OldIds i INNER JOIN content_item ci on i.id = ci.CONTENT_ITEM_ID where ci.SPLITTED = 0

                INSERT INTO @OldSplittedIds
                SELECT i.Id from @OldIds i INNER JOIN content_item ci on i.id = ci.CONTENT_ITEM_ID where ci.SPLITTED = 1

                UPDATE CONTENT_ITEM SET
                    VISIBLE = COALESCE(a.visible, ci.visible),
                    ARCHIVE = COALESCE(a.archive, ci.archive),
                    STATUS_TYPE_ID = COALESCE(a.STATUS_TYPE_ID, ci.STATUS_TYPE_ID),
                    LAST_MODIFIED_BY = @lastModifiedBy,
                    MODIFIED = GETDATE()
                FROM @Articles a INNER JOIN content_item ci on a.CONTENT_ITEM_ID = ci.CONTENT_ITEM_ID

                INSERT INTO @NewSplittedIds
                SELECT i.Id from @OldNonSplittedIds i INNER JOIN content_item ci on i.ID = ci.CONTENT_ITEM_ID where ci.SPLITTED = 1

                INSERT INTO @NewNonSplittedIds
                SELECT i.Id from @OldSplittedIds i INNER JOIN content_item ci on i.ID = ci.CONTENT_ITEM_ID where ci.SPLITTED = 0

                exec qp_split_articles @NewSplittedIds, @lastModifiedBy

                exec qp_merge_articles @NewNonSplittedIds, @lastModifiedBy, 1

                SELECT ID FROM @NewArticles                ";

            var cmd = new SqlCommand(insertInto) { CommandType = CommandType.Text };
            cmd.Parameters.Add(new SqlParameter("@xmlParameter", SqlDbType.Xml) { Value = doc.ToString(SaveOptions.None) });
            cmd.Parameters.AddWithValue("@contentId", contentId);
            cmd.Parameters.AddWithValue("@lastModifiedBy", lastModifiedBy);
            cmd.Parameters.AddWithValue("@notForReplication", 1);

            return cmd;
        }
    }
}
