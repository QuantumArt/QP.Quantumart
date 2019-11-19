using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Quantumart.QPublishing.Info;
using Quantumart.QPublishing.Resizer;

// ReSharper disable once CheckNamespace
namespace Quantumart.QPublishing.Database
{
    // ReSharper disable once InconsistentNaming
    public partial class DBConnector
    {
        public async Task MassUpdateAsync(int contentId, IEnumerable<Dictionary<string, string>> values, int lastModifiedBy, CancellationToken cancellationToken = default(CancellationToken))
        {
            await MassUpdateAsync(contentId, values, lastModifiedBy, new MassUpdateOptions() {IsDefault = true}, cancellationToken);
        }

        public async Task MassUpdateAsync(int contentId, IEnumerable<Dictionary<string, string>> values, int lastModifiedBy, MassUpdateOptions options, CancellationToken cancellationToken = default(CancellationToken))
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
            var versionIdsToRemove = await GetVersionIdsToRemoveAsync(existingIds, content.MaxVersionNumber, cancellationToken);
            var createVersions = options.CreateVersions && content.UseVersionControl;

            CreateInternalConnection(true);
            try
            {
                var doc = GetImportContentItemDocument(arrValues, content);
                var newIds = await MassUpdateContentItemAsync(contentId, arrValues, lastModifiedBy, doc, createVersions, cancellationToken);

                var fullAttrs = GetContentAttributeObjects(contentId).Where(n => n.Type != AttributeType.M2ORelation).ToArray();
                var resultAttrs = GetResultAttrs(arrValues, fullAttrs, newIds);

                CreateDynamicImages(arrValues, fullAttrs);

                await ValidateConstraintsAsync(arrValues, fullAttrs, content, options.ReplaceUrls, cancellationToken);

                var dataDoc = GetMassUpdateContentDataDocument(arrValues, resultAttrs, newIds, content, options.ReplaceUrls);
                await ImportContentDataAsync(dataDoc, cancellationToken);

                var attrString = string.Join(",", resultAttrs.Select(n => n.Id.ToString()).ToArray());
                await ReplicateDataAsync(arrValues, attrString, cancellationToken);

                var manyToManyAttrs = resultAttrs.Where(n => n.Type == AttributeType.Relation && n.LinkId.HasValue).ToArray();
                if (manyToManyAttrs.Any())
                {
                    var linkDoc = GetImportItemLinkDocument(arrValues, manyToManyAttrs);
                    ImportItemLink(linkDoc);
                }

                if (options.ReturnModified)
                {
                    await UpdateModifiedAsync(arrValues, existingIds, newIds, contentId, cancellationToken);
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

        private async Task UpdateModifiedAsync(IEnumerable<Dictionary<string, string>> arrValues, IEnumerable<int> existingIds, int[] newIds, int contentId, CancellationToken cancellationToken)
        {
            var cmd = GetUpdateModifiedCommand(existingIds, newIds, contentId);

            var arrModified = (await GetRealDataAsync(cmd, cancellationToken))
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

        private async Task<int[]> GetVersionIdsToRemoveAsync(int[] ids, int maxNumber, CancellationToken cancellationToken)
        {
            var cmd = GetVersionIdsToRemoveCommand(ids, maxNumber);
            var data = await GetRealDataAsync(cmd, cancellationToken);
            return data.Select().Select(row => Convert.ToInt32(row["content_item_version_id"])).ToArray();
        }

        private async Task ValidateConstraintsAsync(IList<Dictionary<string, string>> values, IEnumerable<ContentAttribute> attrs, Content content, bool replaceUrls, CancellationToken cancellationToken)
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
                    cancellationToken.ThrowIfCancellationRequested();
                    var validatedDataDoc = GetValidatedDataDocument(values, constraint.Attrs, content, replaceUrls);
                    SelfValidate(validatedDataDoc);
                    await ValidateConstraintAsync(validatedDataDoc, constraint.Attrs, cancellationToken);
                }
            }
        }

        private async Task ValidateConstraintAsync(XContainer validatedDataDoc, IReadOnlyList<ContentAttribute> attrs, CancellationToken cancellationToken)
        {
            var cmd = GetValidateConstraintCommand(validatedDataDoc, attrs, out string attrNames);
            var data = await GetRealDataAsync(cmd, cancellationToken);
            var conflictIds = data.Select().Select(row => Convert.ToInt32(row["CONTENT_ITEM_ID"])).ToArray();
            if (conflictIds.Any())
            {
                throw new QpInvalidAttributeException($"Unique constraint violation for content articles. Fields: {attrNames}. Article IDs: {string.Join(", ", conflictIds.ToArray())}");
            }
        }

        private async Task<int[]> MassUpdateContentItemAsync(int contentId, IEnumerable<Dictionary<string, string>> values, int lastModifiedBy, XDocument doc, bool createVersions, CancellationToken cancellationToken)
        {
            var cmd = GetMassUpdateContentItemCommand(contentId, lastModifiedBy, doc, createVersions);
            var data = await GetRealDataAsync(cmd, cancellationToken);
            var ids = new Queue<int>(data.Select().Select(row => Convert.ToInt32(row["ID"])).ToArray());
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
    }
}
