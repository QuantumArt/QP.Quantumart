using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using QP.ConfigurationService.Models;
using Quantumart.QPublishing.Info;

// ReSharper disable once CheckNamespace
namespace Quantumart.QPublishing.Database
{
    // ReSharper disable once InconsistentNaming
    public partial class DBConnector
    {
        public void ImportToContent(int contentId, IEnumerable<Dictionary<string, string>> values, int lastModifiedBy = 1, int[] attrIds = null)
        {
            ImportToContent(contentId, values, lastModifiedBy, attrIds, false);
        }

        public void ImportToContent(int contentId, IEnumerable<Dictionary<string, string>> values, int lastModifiedBy, int[] attrIds, bool overrideMissedFields)
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

            ContentAttribute[] attrs;
            var fullUpdate = (attrIds == null || attrIds.Length == 0) && overrideMissedFields;
            var enumerable = values?.ToArray() ?? new Dictionary<string, string>[0];
            var fullAttrs = GetContentAttributeObjects(contentId).Where(n => n.Type != AttributeType.M2ORelation).ToArray();
            if (!overrideMissedFields && attrIds == null)
            {
                var names = new HashSet<string>(enumerable.SelectMany(n => n.Keys).Distinct().Select(n => n.ToLowerInvariant()));
                attrs = fullAttrs.Where(n => names.Contains(n.Name.ToLowerInvariant())).ToArray();
            }
            else
            {
                attrs = fullAttrs.Where(n => fullUpdate || attrIds != null && attrIds.Contains(n.Id)).ToArray();
            }

            CreateInternalConnection();
            try
            {
                var doc = GetImportContentItemDocument(enumerable, content);
                ImportContentItem(contentId, enumerable, lastModifiedBy, doc);

                var dataDoc = GetImportContentDataDocument(enumerable, attrs, content, overrideMissedFields);
                ImportContentData(dataDoc);

                var replAttrIds = fullUpdate ? new int[] {} : attrs.Select(n => n.Id).ToArray();
                ReplicateData(enumerable, replAttrIds);

                var manyToManyAttrs = attrs.Where(n => n.Type == AttributeType.Relation && n.LinkId.HasValue);
                var toManyAttrs = manyToManyAttrs as ContentAttribute[] ?? manyToManyAttrs.ToArray();
                if (toManyAttrs.Any())
                {
                    var linkDoc = GetImportItemLinkDocument(enumerable, toManyAttrs);
                    ImportItemLink(linkDoc);
                }

                CommitInternalTransaction();
            }
            finally
            {
                DisposeInternalConnection();
            }
        }

        private void ReplicateData(IEnumerable<Dictionary<string, string>> values, int[] attrIds)
        {
            var cmd = GetReplicateDataCommand(values, attrIds);
            ProcessData(cmd);
        }

        private DbCommand GetReplicateDataCommand(IEnumerable<Dictionary<string, string>> values, int[] attrIds)
        {
            var cmd = CreateDbCommand();
            var ids = values.Select(n => Int32.Parse(n[SystemColumnNames.Id])).ToArray();
            if (DatabaseType == DatabaseType.SqlServer)
            {
                cmd.CommandText = "qp_replicate_items";
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.Add(new SqlParameter("@ids", SqlDbType.NVarChar, -1) { Value = string.Join(",", ids ) });
                cmd.Parameters.Add(new SqlParameter("@attr_ids", SqlDbType.NVarChar, -1) { Value = string.Join(",", attrIds ) });
            }
            else
            {
                cmd.CommandText = attrIds.Any() ? "call qp_replicate_items(@ids, @attr_ids);" : "call qp_replicate_items(@ids);";
                cmd.Parameters.Add(SqlQuerySyntaxHelper.GetIdsDatatableParam("@ids", ids, DatabaseType));
                cmd.Parameters.Add(SqlQuerySyntaxHelper.GetIdsDatatableParam("@attr_ids", attrIds, DatabaseType));
            }

            return cmd;
        }

        private void ImportItemLink(XDocument linkDoc)
        {
            if (linkDoc == null)
            {
                throw new ArgumentNullException(nameof(linkDoc));
            }

            var cmd = CreateDbCommand("qp_update_m2m_values");
            cmd.CommandTimeout = 120;

            if (DatabaseType == DatabaseType.SqlServer)
            {
                cmd.CommandType = CommandType.StoredProcedure;
            }
            else
            {
                cmd.CommandText = "call qp_update_m2m_values(@xmlParameter);";
            }

            cmd.Parameters.Add(SqlQuerySyntaxHelper.GetXmlParam(
                "@xmlParameter", linkDoc.ToString(SaveOptions.None), DatabaseType)
            );

            ProcessData(cmd);
        }

        private XDocument GetImportItemLinkDocument(IEnumerable<Dictionary<string, string>> values, IEnumerable<ContentAttribute> manyToManyAttrs)
        {
            var linkDoc = new XDocument();
            linkDoc.Add(new XElement("items"));

            var toManyAttrs = manyToManyAttrs as ContentAttribute[] ?? manyToManyAttrs.ToArray();
            foreach (var value in values)
            {
                foreach (var attr in toManyAttrs)
                {
                    var linkElem = new XElement("item");
                    linkElem.Add(new XAttribute("id", value[SystemColumnNames.Id]));
                    if (attr.LinkId != null)
                    {
                        linkElem.Add(new XAttribute("linkId", attr.LinkId.Value));
                    }

                    if (value.TryGetValue(attr.Name, out var temp))
                    {
                        linkElem.Add(new XAttribute("value", temp));
                        linkDoc.Root?.Add(linkElem);
                    }
                }
            }

            return linkDoc;
        }

        private void ImportContentData(XNode dataDoc)
        {
            var cmd = GetImportContentDataCommand(dataDoc);
            ProcessData(cmd);
        }

        private DbCommand GetImportContentDataCommand(XNode dataDoc)
        {
            string sql;

            if (DatabaseType == DatabaseType.SqlServer)
            {
                sql = @"
                    WITH X (CONTENT_ITEM_ID, ATTRIBUTE_ID, DATA, BLOB_DATA)
                    AS
                    (
                        SELECT
                        doc.col.value('./@id', 'int') CONTENT_ITEM_ID
                        ,doc.col.value('./@attrId', 'int') ATTRIBUTE_ID
                        ,doc.col.value('(DATA)[1]', 'nvarchar(3500)') DATA
                        ,doc.col.value('(BLOB_DATA)[1]', 'nvarchar(max)') BLOB_DATA
                        FROM @xmlParameter.nodes('/ITEMS/ITEM') doc(col)
                    )
                    UPDATE CONTENT_DATA
                    SET CONTENT_DATA.DATA = case when X.DATA = '' then NULL else X.DATA end,
                        CONTENT_DATA.BLOB_DATA = case when X.BLOB_DATA = '' then NULL else X.BLOB_DATA end,
                        NOT_FOR_REPLICATION = 1, MODIFIED = GETDATE()
                    FROM dbo.CONTENT_DATA
                    INNER JOIN X ON CONTENT_DATA.CONTENT_ITEM_ID = X.CONTENT_ITEM_ID AND dbo.CONTENT_DATA.ATTRIBUTE_ID = X.ATTRIBUTE_ID ";
            }
            else
            {
                sql = @"
                    WITH X
                    AS
                    (
                        select xml.content_item_id, xml.attribute_id,
                            case when xml.DATA = '' then NULL else xml.DATA end as data,
                            case when xml.BLOB_DATA = '' then NULL else xml.BLOB_DATA end
                        from XMLTABLE('/ITEMS/ITEM' PASSING @xmlParameter COLUMNS
			                CONTENT_ITEM_ID int PATH '@id',
			                ATTRIBUTE_ID int PATH '@attrId',
			                DATA text PATH 'DATA',
			                BLOB_DATA text PATH 'BLOB_DATA'
		                ) xml
                    )
                    UPDATE CONTENT_DATA cd
                    SET DATA = coalesce(X.BLOB_DATA, X.DATA), NOT_FOR_REPLICATION = true, MODIFIED = now()
                    FROM X WHERE cd.CONTENT_ITEM_ID = X.CONTENT_ITEM_ID AND cd.ATTRIBUTE_ID = X.ATTRIBUTE_ID ";
            }

            var cmd = CreateDbCommand(sql);
            cmd.CommandTimeout = 120;
            cmd.Parameters.Add(SqlQuerySyntaxHelper.GetXmlParam("@xmlParameter", dataDoc.ToString(SaveOptions.None), DatabaseType));
            return cmd;
        }

        private XDocument GetImportContentDataDocument(IEnumerable<Dictionary<string, string>> values, IEnumerable<ContentAttribute> attrs, Content content, bool overrideMissedFields = false)
        {
            var longUploadUrl = GetImagesUploadUrl(content.SiteId);
            var longSiteLiveUrl = GetSiteUrl(content.SiteId, true);
            var longSiteStageUrl = GetSiteUrl(content.SiteId, false);
            var dataDoc = new XDocument();
            dataDoc.Add(new XElement("ITEMS"));

            var contentAttributes = attrs as ContentAttribute[] ?? attrs.ToArray();
            foreach (var value in values)
            {
                foreach (var attr in contentAttributes)
                {
                    var elem = new XElement("ITEM");
                    elem.Add(new XAttribute("id", XmlValidChars(value[SystemColumnNames.Id])));
                    elem.Add(new XAttribute("attrId", attr.Id));
                    var valueExists = value.TryGetValue(attr.Name, out var temp);
                    if (attr.LinkId.HasValue)
                    {
                        elem.Add(new XElement("DATA", attr.LinkId.Value));
                        valueExists = true;
                    }
                    else if (attr.BackRelation != null)
                    {
                        elem.Add(new XElement("DATA", attr.BackRelation.Id));
                        valueExists = true;
                    }
                    else if (!string.IsNullOrEmpty(temp))
                    {
                        temp = FormatResult(attr, temp, longUploadUrl, longSiteStageUrl, longSiteLiveUrl, true);
                        elem.Add(new XElement(attr.DbField, XmlValidChars(temp)));
                    }

                    if (valueExists || overrideMissedFields)
                    {
                        dataDoc.Root?.Add(elem);
                    }
                }
            }

            return dataDoc;
        }

        private static string XmlValidChars(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                return token;
            }

            try
            {
                return XmlConvert.VerifyXmlChars(token);
            }
            catch (XmlException)
            {
                return InvalidXmlChars.Replace(token, string.Empty);
            }
        }

        private static readonly Regex InvalidXmlChars = new Regex(@"[^\x09\x0A\x0D\x20-\uD7FF\uE000-\uFFFD\u10000-\u10FFFF]");

        private XDocument GetImportContentItemDocument(IEnumerable<Dictionary<string, string>> values, Content content)
        {
            var defaultStatusName = content.IsWorkflowAssigned ? "None" : "Published";
            var defaultStatusId = GetStatusTypeId(content.SiteId, defaultStatusName);
            var doc = new XDocument();
            doc.Add(new XElement("ITEMS"));

            foreach (var value in values)
            {
                var elem = new XElement("ITEM");
                var intFromDictionary = GetIntFromDictionary(value, SystemColumnNames.Id, 0);
                if (intFromDictionary != null)
                {
                    var id = intFromDictionary.Value;
                    elem.Add(new XElement(SystemColumnNames.Id, id));
                    elem.Add(GetXElementFromDictionary(value, SystemColumnNames.StatusTypeId, id == 0, defaultStatusId));
                    elem.Add(GetXElementFromDictionary(value, SystemColumnNames.Visible, id == 0, 1));
                    elem.Add(GetXElementFromDictionary(value, SystemColumnNames.Archive, id == 0, 0));
                }
                doc.Root?.Add(elem);
            }

            return doc;
        }

        private void ImportContentItem(int contentId, IEnumerable<Dictionary<string, string>> values, int lastModifiedBy, XDocument doc)
        {
            const string insertInto = @"
                DECLARE @Articles TABLE
                (
                    CONTENT_ITEM_ID NUMERIC,
                    STATUS_TYPE_ID NUMERIC,
                    VISIBLE NUMERIC,
                    ARCHIVE NUMERIC
                )

                DECLARE @NewArticles TABLE(ID INT)

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

                UPDATE CONTENT_ITEM with(rowlock) SET
                    VISIBLE = COALESCE(a.visible, ci.visible),
                    ARCHIVE = COALESCE(a.archive, ci.archive),
                    STATUS_TYPE_ID = COALESCE(a.STATUS_TYPE_ID, ci.STATUS_TYPE_ID),
                    LAST_MODIFIED_BY = @lastModifiedBy,
                    MODIFIED = GETDATE()
                FROM @Articles a INNER JOIN content_item ci on a.CONTENT_ITEM_ID = ci.CONTENT_ITEM_ID

                SELECT ID FROM @NewArticles
                ";

            var cmd = CreateDbCommand(insertInto);
            var xml = doc.ToString(SaveOptions.None);

            if (DatabaseType != DatabaseType.SqlServer)
            {
                cmd.CommandText = "select id from qp_mass_update_content_item(@xmlParameter, @contentId, @lastModifiedBy, @notForReplication, @createVersions, @importOnly);";
            }

            cmd.Parameters.Add(SqlQuerySyntaxHelper.GetXmlParam("@xmlParameter", xml, DatabaseType));
            cmd.Parameters.AddWithValue("@contentId", contentId);
            cmd.Parameters.AddWithValue("@lastModifiedBy", lastModifiedBy);
            cmd.Parameters.AddWithValue("@notForReplication", 1);
            if (DatabaseType != DatabaseType.SqlServer)
            {
                cmd.Parameters.AddWithValue("@createVersions", false);
                cmd.Parameters.AddWithValue("@importOnly", true);
            }


            var ids = new Queue<int>(GetRealData(cmd).Select().Select(row => Convert.ToInt32(row["ID"])).ToArray());
            foreach (var value in values)
            {
                if (value[SystemColumnNames.Id] == "0")
                {
                    value[SystemColumnNames.Id] = ids.Dequeue().ToString();
                }
            }
        }

        private static int? GetIntFromDictionary(IReadOnlyDictionary<string, string> value, string key, int? defaultValue)
        {
            var result = defaultValue;
            if (value.TryGetValue(key, out var tempId))
            {
                int.TryParse(tempId, out var id);
                result = id;
            }

            return result;
        }

        private static XElement GetXElementFromDictionary(IReadOnlyDictionary<string, string> value, string key, bool isNew, int? defaultValue)
        {
            var temp = GetIntFromDictionary(value, key, isNew ? defaultValue : null);
            return temp.HasValue ? new XElement(key, temp.Value) : null;
        }
    }
}
