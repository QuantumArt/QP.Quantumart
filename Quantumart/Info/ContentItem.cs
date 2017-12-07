using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Quantumart.QPublishing.Database;
using Quantumart.QPublishing.Helpers;
using Quantumart.QPublishing.OnScreen;
#if !ASPNETCORE
using System.Web;

#endif

// ReSharper disable once CheckNamespace
namespace Quantumart.QPublishing.Info
{
    [Serializable]
    public class ContentItem
    {
        public int Id { get; set; }

        public int VersionId { get; set; }

        public bool Visible { get; set; }

        public bool Archive { get; set; }

        public bool DelayedSchedule { get; set; }

        public int LastModifiedBy { get; set; }

        public int ContentId { get; set; }

        public DateTime Created { get; internal set; }

        public DateTime Modified { get; internal set; }

        public bool Splitted { get; internal set; }

        private string _statusName;

        public string StatusName
        {
            get => _statusName;
            set
            {
                if (_statusName != value)
                {
                    _statusName = value;
                    StatusChanged = true;
                }
            }
        }

        public bool StatusChanged { get; private set; }

        private readonly DBConnector _dbConnector;

        public Dictionary<string, ContentItemValue> FieldValues { get; } = new Dictionary<string, ContentItemValue>();

        private Dictionary<string, ContentItemValue> RestrictedFieldValues { get; } = new Dictionary<string, ContentItemValue>();

        public List<ContentItem> AggregatedItems { get; } = new List<ContentItem>();

        public bool IsNew => Id == 0;

        private ContentItem(DBConnector dbConnector)
        {
            Id = 0;
            ContentId = 0;
            Visible = true;
            Archive = false;
            DelayedSchedule = false;
            LastModifiedBy = 1;
            _statusName = "Published";
            StatusChanged = false;
            _dbConnector = dbConnector;
            LoadLastModifiedFromCustomTab();
        }

        public static ContentItem New(int contentId, DBConnector dbConnector)
        {
            if (contentId <= 0)
            {
                throw new ArgumentException("contentId");
            }

            var item = new ContentItem(dbConnector)
            {
                Id = 0,
                ContentId = contentId
            };

            item.InitFieldValues();
            if (dbConnector.GetContentObject(contentId).IsWorkflowAssigned)
            {
                item._statusName = "None";
            }

            return item;
        }

        public static ContentItem Read(int id, DBConnector dbConnector)
        {
            if (id <= 0)
            {
                throw new ArgumentException("id");
            }

            var item = new ContentItem(dbConnector)
            {
                Id = id
            };

            item.Load();
            return item;
        }

        public static ContentItem ReadLastVersion(int id, DBConnector dbConnector)
        {
            if (id <= 0)
            {
                throw new ArgumentException("id");
            }

            var item = new ContentItem(dbConnector)
            {
                Id = id
            };

            item.LoadLastVersion();
            return item;
        }

        public static void Remove(int id, DBConnector cnn)
        {
            cnn.SendNotification(id, NotificationEvent.Remove);
            cnn.DeleteContentItem(id);
        }

        private void Load()
        {
            var sb = new StringBuilder();
            sb.AppendLine("sp_executesql N'");
            sb.AppendLine("select ci.*, st.status_type_name from content_item ci with (nolock) ");
            sb.AppendLine("inner join status_type st on ci.status_type_id = st.status_type_id ");
            sb.AppendLine("where content_item_id = @id");
            sb.AppendFormat("', N'@id NUMERIC', @id = {0}", Id);
            var dt = _dbConnector.GetRealData(sb.ToString());

            if (dt.Rows.Count == 0)
            {
                throw new Exception($"Article is not found (ID = {Id}) ");
            }

            var dr = dt.Rows[0];
            ContentId = (int)(decimal)dr["CONTENT_ID"];
            LastModifiedBy = (int)(decimal)dr["LAST_MODIFIED_BY"];
            Visible = Convert.ToBoolean((decimal)dr["VISIBLE"]);
            Archive = Convert.ToBoolean((decimal)dr["ARCHIVE"]);
            DelayedSchedule = (bool)dr["SCHEDULE_NEW_VERSION_PUBLICATION"];
            Splitted = (bool)dr["SPLITTED"];
            Created = (DateTime)dr["CREATED"];
            Modified = (DateTime)dr["MODIFIED"];
            _statusName = dr["STATUS_TYPE_NAME"].ToString();
            LoadFieldValues();
        }

        private void LoadLastVersion()
        {
            var statusRow = Status.GetPreviousStatusHistoryRecord(Id, _dbConnector);
            if (statusRow == null)
            {
                throw new Exception($"Status row is not found for article (ID = {Id}) ");
            }

            var sb = new StringBuilder();
            sb.AppendLine("sp_executesql N'");
            sb.AppendLine("select top 1 civ.content_item_version_id, civ.modified as version_modified, ci.*, st.status_type_name from content_item_version civ with (nolock) ");
            sb.AppendLine("inner join content_item ci with(nolock) on civ.content_item_id = ci.content_item_id");
            sb.AppendLine("inner join status_type st on ci.status_type_id = st.status_type_id ");
            sb.AppendLine("where civ.content_item_id = @id order by content_item_version_id desc");
            sb.AppendFormat("', N'@id NUMERIC', @id = {0}", Id);

            var dt = _dbConnector.GetRealData(sb.ToString());
            if (dt.Rows.Count == 0)
            {
                throw new VersionNotFoundException($"Version is not found for article (ID = {Id}) ");
            }

            var versionRow = dt.Rows[0];
            ContentId = (int)(decimal)versionRow["CONTENT_ID"];
            VersionId = (int)(decimal)versionRow["content_item_version_id"];
            LastModifiedBy = (int)(decimal)statusRow["USER_ID"];
            Visible = (bool)statusRow["VISIBLE"];
            Archive = (bool)statusRow["ARCHIVE"];
            Created = (DateTime)versionRow["CREATED"];
            Modified = (DateTime)versionRow["VERSION_MODIFIED"];
            _statusName = statusRow["STATUS_TYPE_NAME"].ToString();
            LoadFieldValues();
        }

        private IEnumerable<int> GetRealLinkedItems(int linkId)
        {
            var linkTable = Splitted ? "item_link_united" : "item_link";
            var sql = string.Format("EXEC sp_executesql N'SELECT linked_item_id FROM {2} WHERE item_id = @itemId AND link_id = @linkId', N'@itemId NUMERIC, @linkId NUMERIC', @itemId = {0}, @linkId = {1};", Id, linkId, linkTable);
            var items = _dbConnector.GetRealData(sql).Select().Select(row => Convert.ToInt32(row["linked_item_id"]));
            return items;
        }

        private IEnumerable<int> GetVersionLinkedItems(int attrId)
        {
            var sql = $"EXEC sp_executesql N'SELECT linked_item_id FROM item_to_item_version WHERE content_item_version_id = @itemId AND attribute_id = @attrId', N'@itemId NUMERIC, @attrId NUMERIC', @itemId = {VersionId}, @attrId = {attrId};";
            var items = _dbConnector.GetRealData(sql).Select().Select(n => (int)(decimal)n["linked_item_id"]);
            return items;
        }

        private IEnumerable<int> GetRealRelatedItems(int contentId, string fieldName)
        {
            var cmd = new SqlCommand
            {
                CommandType = CommandType.StoredProcedure,
                CommandText = "qp_get_m2o_ids"
            };

            cmd.Parameters.AddWithValue("@contentId", contentId);
            cmd.Parameters.AddWithValue("@fieldName", fieldName);
            cmd.Parameters.AddWithValue("@id", Id);
            return _dbConnector.GetRealData(cmd).Select().Select(row => Convert.ToInt32(row["content_item_id"]));
        }

        private void InitFieldValues()
        {
            var attrs = _dbConnector.GetContentAttributeObjects(ContentId).ToArray();
            foreach (var attr in attrs)
            {
                FieldValues.Add(attr.Name, new ContentItemValue());
            }

            foreach (var attr in attrs.Where(n => n.Aggregated || n.IsClassifier))
            {
                RestrictedFieldValues.Add(attr.Name, new ContentItemValue());
            }
        }

        private void LoadFieldValues()
        {
            if (Id == 0)
            {
                throw new Exception("Cannot read values for new article");
            }

            InitFieldValues();
            var classifierIds = new List<int>();
            var typeIds = new List<int>();
            var dt = _dbConnector.GetRealData(VersionId != 0
                ? $"sp_executesql N'select cd.attribute_id, case when ca.attribute_type_id in (9, 10) then cd.blob_data else cd.data end as data from version_content_data cd inner join content_attribute ca on cd.attribute_id = ca.attribute_id where content_item_version_id = @id', N'@id NUMERIC', @id = {VersionId}"
                : $"sp_executesql N'select cd.attribute_id, case when ca.attribute_type_id in (9, 10) then cd.blob_data else cd.data end as data from content_data cd inner join content_attribute ca on cd.attribute_id = ca.attribute_id where content_item_id = @id', N'@id NUMERIC', @id = {Id}");

            foreach (DataRow dr in dt.Rows)
            {
                var attr = _dbConnector.GetContentAttributeObject((int)(decimal)dr["ATTRIBUTE_ID"]);
                if (FieldValues.ContainsKey(attr.Name))
                {
                    var value = FieldValues[attr.Name];
                    value.Data = dr["DATA"].ToString();

                    if (RestrictedFieldValues.ContainsKey(attr.Name))
                    {
                        var restValue = RestrictedFieldValues[attr.Name];
                        restValue.Data = dr["DATA"].ToString();
                    }

                    if (attr.Type == AttributeType.String || attr.Type == AttributeType.VisualEdit || attr.Type == AttributeType.Textbox)
                    {
                        value.Data = value.Data.Replace(_dbConnector.UploadPlaceHolder, _dbConnector.GetImagesUploadUrl(SiteId)).Replace(_dbConnector.SitePlaceHolder, _dbConnector.GetSiteUrl(SiteId, true));
                    }

                    if (attr.Type == AttributeType.Numeric && attr.IsClassifier)
                    {
                        classifierIds.Add(attr.Id);
                        typeIds.Add(int.Parse(value.Data));
                    }

                    if (attr.Type == AttributeType.Relation && attr.LinkId.HasValue || attr.Type == AttributeType.M2ORelation)
                    {
                        var items = VersionId != 0
                            ? GetVersionLinkedItems(attr.Id)
                            : (attr.Type == AttributeType.M2ORelation
                                ? GetRealRelatedItems(attr.BackRelation.ContentId, attr.BackRelation.Name)
                                : GetRealLinkedItems(attr.LinkId ?? 0));

                        value.LinkedItems = new HashSet<int>(items);
                    }
                }
            }

            if (classifierIds.Any())
            {
                if (VersionId != 0)
                {
                    foreach (var id in typeIds)
                    {
                        var ci = new ContentItem(_dbConnector)
                        {
                            ContentId = id,
                            Id = Id,
                            VersionId = VersionId
                        };

                        ci.LoadFieldValues();
                        AggregatedItems.Add(ci);
                    }
                }
                else
                {
                    var aggrIds = GetAggregatedArticlesIDs(classifierIds.ToArray(), typeIds.ToArray());
                    foreach (var ci in aggrIds.Select(id => Read(id, _dbConnector)))
                    {
                        AggregatedItems.Add(ci);
                    }
                }
            }
        }

        public int SiteId => _dbConnector.GetSiteIdByContentId(ContentId);

        public void Save()
        {
            var attrs = _dbConnector.GetContentAttributeObjects(ContentId).ToDictionary(n => n.Name.ToLowerInvariant(), n => n);
            var values = new Hashtable();
            var restAttrs = attrs.Values.Where(n => n.IsClassifier || n.Aggregated).ToDictionary(n => n.Name.ToLowerInvariant(), n => n);
            foreach (var fieldValue in FieldValues)
            {
                var attrKey = fieldValue.Key.ToLowerInvariant();
                if (restAttrs.ContainsKey(attrKey))
                {
                    var key = restAttrs[attrKey].Name;
                    if (RestrictedFieldValues[key].Data != fieldValue.Value.Data)
                    {
                        throw new Exception("Change of Aggregated or Classifier fields are not supported");
                    }
                }
            }

            foreach (var fieldValue in FieldValues)
            {
                var attrKey = fieldValue.Key.ToLowerInvariant();
                if (!attrs.ContainsKey(attrKey))
                {
                    throw new Exception($"Field '{fieldValue.Key}' is not found");
                }

                var attr = attrs[attrKey];
                string value;
                if (attr.Type == AttributeType.Relation && attr.LinkId.HasValue || attr.Type == AttributeType.M2ORelation)
                {
                    value = string.Join(",", fieldValue.Value.LinkedItems.Select(n => n.ToString()).ToArray());
                }
                else
                {
                    value = fieldValue.Value.Data;
                }

                values.Add(_dbConnector.FieldName(attr.Id), value);
            }

#if !ASPNETCORE
            HttpFileCollection files = null;
#endif
            var modified = DateTime.MinValue;
            var notificationEvent = IsNew ? NotificationEvent.Create : NotificationEvent.Modify;
#if ASPNETCORE
            Id = _dbConnector.AddFormToContent(SiteId, ContentId, StatusName, ref values, Id, true, 0, Visible, Archive, LastModifiedBy, DelayedSchedule, false, ref modified, true, true);
#else
            Id = _dbConnector.AddFormToContent(SiteId, ContentId, StatusName, ref values, ref files, Id, true, 0, Visible, Archive, LastModifiedBy, DelayedSchedule, false, ref modified, true, true);
#endif

            _dbConnector.SendNotification(Id, notificationEvent);
            if (!IsNew && StatusChanged)
            {
                _dbConnector.SendNotification(Id, NotificationEvent.StatusChanged);
            }
        }

        public void LoadLastModifiedFromCustomTab()
        {
            var qscreen = new QScreen(_dbConnector);
            var id = qscreen.GetCustomTabUserId();
            if (id != 0)
            {
                LastModifiedBy = id;
            }
        }

        internal XDocument GetXDocument()
        {
            var newDoc = new XDocument(new XElement("article", new XAttribute("id", Id)));
            if (newDoc.Root != null)
            {
                newDoc.Root.Add(new XElement("created", Created.ToString(CultureInfo.InvariantCulture)));
                newDoc.Root.Add(new XElement("modified", Modified.ToString(CultureInfo.InvariantCulture)));
                newDoc.Root.Add(new XElement("contentId", ContentId));
                newDoc.Root.Add(new XElement("siteId", SiteId));
                newDoc.Root.Add(new XElement("visible", Visible));
                newDoc.Root.Add(new XElement("archive", Archive));
                newDoc.Root.Add(new XElement("splitted", Splitted));
                newDoc.Root.Add(new XElement("statusName", StatusName));
                newDoc.Root.Add(new XElement("lastModifiedBy", LastModifiedBy));
                newDoc.Root.Add(GetFieldValuesElement());

                var extRoot = new XElement("extensions");
                foreach (var item in AggregatedItems)
                {
                    var attr = item.Id == Id ? null : new XAttribute("id", item.Id);
                    extRoot.Add(new XElement("extension", new XAttribute("typeId", item.ContentId), attr, item.GetFieldValuesElement()));
                }

                newDoc.Root.Add(extRoot);
            }

            return newDoc;
        }

        private XElement GetFieldValuesElement()
        {
            var fields = new XElement("customFields");
            var attrs = _dbConnector.GetContentAttributeObjects(ContentId).ToDictionary(n => n.Name.ToLowerInvariant(), n => n);
            foreach (var fieldValue in FieldValues)
            {
                var attrKey = fieldValue.Key.ToLowerInvariant();
                if (!attrs.ContainsKey(attrKey))
                {
                    throw new Exception($"Field '{fieldValue.Key}' is not found");
                }

                var attr = attrs[attrKey];
                var value = attr.Type == AttributeType.Relation && attr.LinkId.HasValue || attr.Type == AttributeType.M2ORelation
                    ? string.Join(",", fieldValue.Value.LinkedItems.Select(n => n.ToString()).ToArray())
                    : fieldValue.Value.Data;

                fields.Add(new XElement("field", new XAttribute("name", fieldValue.Key), new XAttribute("id", attr.Id), value));
            }

            return fields;
        }

        public IEnumerable<int> GetAggregatedArticlesIDs(int[] classfierFields, int[] types)
        {
            const string query = @"
            declare @attrIds table (attribute_id numeric primary key, content_id numeric, attribute_name nvarchar(255))
            declare @attribute_id numeric, @content_id numeric, @attribute_name nvarchar(255)

            insert into @attrIds(attribute_id, content_id, attribute_name)
            select attribute_id, content_id, attribute_name from content_attribute where classifier_attribute_id in (select id from @ids) and content_id in (select id from @cids)
            declare @sql nvarchar(max)
            set @sql = ''
            while exists(select * from @attrIds)
            begin
                select @attribute_id = attribute_id, @content_id = content_id, @attribute_name = attribute_name from @attrIds
                print @attribute_id
                if @sql <> ''
                    set @sql = @sql + ' union all '
                set @sql = @sql + 'select content_item_id from content_' + cast(@content_id as nvarchar(30)) + '_united where [' + @attribute_name + '] = @article_id'
                delete from @attrIds where attribute_id = @attribute_id
            end
            exec sp_executesql @sql, N'@article_id numeric', @article_id = @article_id";

            var result = new List<int>();
            using (var cmd = new SqlCommand(query))
            {
                cmd.CommandType = CommandType.Text;
                cmd.Parameters.AddWithValue("@article_id", Id);
                cmd.Parameters.Add(new SqlParameter("@ids", SqlDbType.Structured) { TypeName = "Ids", Value = DBConnector.IdsToDataTable(classfierFields) });
                cmd.Parameters.Add(new SqlParameter("@cids", SqlDbType.Structured) { TypeName = "Ids", Value = DBConnector.IdsToDataTable(types) });
                result.AddRange(_dbConnector.GetRealData(cmd).Select().Select(row => Convert.ToInt32(row[0])));
            }

            return result;
        }
    }
}
