using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlTypes;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Quantumart.QPublishing.Info;

// ReSharper disable once CheckNamespace
namespace Quantumart.QPublishing.Database
{
    // ReSharper disable once InconsistentNaming
    public partial class DBConnector
    {
        public string GetContentItemLinkIDs(string linkFieldName, long itemId) => GetContentItemLinkIDs(linkFieldName, itemId.ToString());

        public string GetContentItemLinkIDs(string linkFieldName, string itemIds)
        {
            var info = GetRelationInfoForItem(linkFieldName, itemIds);
            return info == null ? "0" : GetContentItemLinkIDs(info.LinkId, itemIds, info.IsManyToMany);
        }

        public string GetContentItemLinkIDs(int linkId, long itemId) => GetContentItemLinkIDs(linkId, itemId.ToString());

        public string GetContentItemLinkIDs(int linkId, string itemIds) => GetContentItemLinkIDs(linkId, itemIds, true);

        public string GetContentItemLinkIDs(int linkId, long itemId, bool isManyToMany) => GetContentItemLinkIDs(linkId, itemId.ToString(), isManyToMany);

        public string GetContentItemLinkIDs(int linkId, string itemIds, bool isManyToMany)
        {
            var itemLinkHash = GetItemLinkHashTable();
            var key = CacheManager.GetItemLinkElementHashKey(linkId, itemIds, isManyToMany);
            return itemLinkHash.ContainsKey(key) ? itemLinkHash[key].ToString() : CacheManager.AddItemLinkHashEntry(linkId, itemIds, isManyToMany);
        }

        public static string IdsToXml(IEnumerable<int> ids)
        {
            return new XElement("items", ids.Select(n => new XElement("item", n))).ToString();
        }

        public static IEnumerable<int> CommaListToIds(string commaList)
        {
            var re = new Regex(@"^[\d]+$");
            return commaList.Split(',').Where(n => re.IsMatch(n)).Select(int.Parse);
        }

        public static string CommaListToXml(string commaList) => IdsToXml(CommaListToIds(commaList));

        public static DataTable CommaListToDataTable(string commaList) => IdsToDataTable(CommaListToIds(commaList));

        public static SqlXml XmlToSqlXml(string xml) => new SqlXml(new XmlTextReader(new StringReader(xml)));

        public static SqlXml IdsToSqlXml(IEnumerable<int> ids) => XmlToSqlXml(IdsToXml(ids));

        public static DataTable IdsToDataTable(IEnumerable<int> ids)
        {
            var dt = new DataTable();
            dt.Columns.Add("id");
            if (ids != null)
            {
                foreach (var id in ids)
                {
                    dt.Rows.Add(id);
                }
            }

            return dt;
        }

        public static SqlXml CommaListToSqlXml(string commaList) => XmlToSqlXml(CommaListToXml(commaList));

        public string GetRealContentItemLinkIDs(string linkFieldName, long itemId) => GetRealContentItemLinkIDs(linkFieldName, itemId.ToString());

        public string GetRealContentItemLinkIDs(string linkFieldName, string itemIds)
        {
            var info = GetRelationInfoForItem(linkFieldName, itemIds);
            return info == null ? string.Empty : GetRealContentItemLinkIDs(info.LinkId, itemIds, info.IsManyToMany);
        }

        public string GetRealContentItemLinkIDs(int linkId, long itemId) => GetRealContentItemLinkIDs(linkId, itemId.ToString());

        public string GetRealContentItemLinkIDs(int linkId, string itemIds) => GetRealContentItemLinkIDs(linkId, itemIds, true);

        public string GetRealContentItemLinkIDs(int linkId, long itemId, bool isManyToMany) => GetRealContentItemLinkIDs(linkId, itemId.ToString(), isManyToMany);

        public string GetRealContentItemLinkIDs(int linkId, string itemIds, bool isManyToMany)
        {
            var cmd = GetContentItemLinkCommand(linkId, itemIds, isManyToMany, false);
            if (cmd == null)
            {
                return string.Empty;
            }

            var dt = GetRealData(cmd);
            var result = new List<string> { "0" };
            result.AddRange(dt.Rows.OfType<DataRow>().Select(n => n[0].ToString()));
            return string.Join(",", result.ToArray());
        }

        public Dictionary<int, string> GetRealContentItemLinkIDsMultiple(int linkId, IEnumerable<int> ids, bool isManyToMany)
        {
            var result = new Dictionary<int, List<string>>();
            var idstr = string.Join(",", ids.Select(n => n.ToString()).ToArray());
            var cmd = GetContentItemLinkCommand(linkId, idstr, isManyToMany, true);
            if (cmd != null)
            {
                var dt = GetRealData(cmd);
                foreach (DataRow dr in dt.Rows)
                {
                    var itemId = (int)(decimal)dr["item_id"];
                    var linkedItemId = (int)(decimal)dr["linked_item_id"];
                    if (!result.ContainsKey(itemId))
                    {
                        result.Add(itemId, new List<string> { linkedItemId.ToString() });
                    }
                    else
                    {
                        result[itemId].Add(linkedItemId.ToString());
                    }
                }
            }

            return result.Select(n => new KeyValuePair<int, string>(n.Key, string.Join(",", n.Value.ToArray()))).ToDictionary(n => n.Key, n => n.Value);
        }

        public DbCommand GetContentItemLinkCommand(string linkFieldName, long itemId) => GetContentItemLinkCommand(linkFieldName, itemId.ToString());

        public DbCommand GetContentItemLinkCommand(string linkFieldName, string itemIds)
        {
            var info = GetRelationInfoForItem(linkFieldName, itemIds);
            return info == null ? null : GetContentItemLinkCommand(info.LinkId, itemIds, info.IsManyToMany, false);
        }

        public DbCommand GetContentItemLinkCommand(int linkId, long itemId) => GetContentItemLinkCommand(linkId, itemId.ToString());

        public DbCommand GetContentItemLinkCommand(int linkId, string itemIds) => GetContentItemLinkCommand(linkId, itemIds, true, false);

        public DbCommand GetContentItemLinkCommand(int linkId, long itemId, bool isManyToMany) => GetContentItemLinkCommand(linkId, itemId.ToString(), isManyToMany, false);

        public DbCommand GetContentItemLinkCommand(int linkId, string itemIds, bool isManyToMany, bool returnAll)
        {
            if (linkId == LegacyNotFound)
            {
                return null;
            }

            var ids = itemIds.Split(',');
            string table;
            string query;
            if (isManyToMany)
            {
                table = IsStage ? "item_link_united" : "item_link";
                if (ids.Length == 1)
                {
                    query = $@"SELECT linked_item_id FROM {table} {WithNoLock} WHERE item_id = @itemId AND link_id = @linkId";
                }
                else
                {
                    query = returnAll ? "item_id, linked_item_id" : "DISTINCT linked_item_id";
                    query = $@"SELECT {query} FROM {table} {WithNoLock} where item_id in ({itemIds}) AND link_id = @linkId";
                }
            }
            else
            {
                var attr = GetContentAttributeObject(linkId);

                if (attr == null)
                {
                    return null;
                }

                var attrName = SqlQuerySyntaxHelper.FieldName(DatabaseType, attr.Name);

                table = IsStage ? $@"content_{attr.ContentId}_united" : $@"content_{attr.ContentId}";
                if (ids.Length == 1)
                {
                    query = $"SELECT content_item_id FROM {table} {WithNoLock} WHERE {attrName} = @itemId";
                }
                else
                {
                    query = returnAll
                        ? $"[{attr.Name}] as item_id, content_item_id as linked_item_id"
                        : "DISTINCT content_item_id";

                    query = $@"SELECT {query} FROM {table} {WithNoLock} WHERE {attrName} in ({itemIds})";
                }
            }
            var cmd = CreateDbCommand(query);
            cmd.Parameters.AddWithValue("@linkId", linkId);
            cmd.Parameters.AddWithValue("@itemId", ids[0]);
            return cmd;
        }

        // ReSharper disable once InconsistentNaming
        public int GetLinkIDForItem(string linkFieldName, int itemId) => GetLinkID(linkFieldName, GetContentIdForItem(itemId));

        // ReSharper disable once InconsistentNaming
        public int GetLinkIDForItem(string linkFieldName, string itemIds) => GetLinkID(linkFieldName, GetContentIdForItem(itemIds));

        // ReSharper disable once InconsistentNaming
        public int GetLinkID(string linkFieldName, int contentId)
        {
            var info = GetRelationInfo(linkFieldName, contentId);
            if (info != null && info.IsManyToMany)
            {
                return info.LinkId;
            }

            return LegacyNotFound;
        }

        public int GetLinkIdByNetName(int siteId, string netName)
        {
            var key = netName.ToLowerInvariant();
            var localHash = CacheManager.GetLinkForLinqHashTable(siteId);
            return localHash.ContainsKey(key) ? (int)localHash[key] : 0;
        }

        internal RelationInfo GetRelationInfoForItem(string linkFieldName, string itemId) => GetRelationInfo(linkFieldName, GetContentIdForItem(itemId));

        internal RelationInfo GetRelationInfo(string linkFieldName, int contentId)
        {
            var linkHash = GetLinkHashTable();
            var contentKey = contentId.ToString();
            var nameKey = linkFieldName.ToLowerInvariant();
            var localHash = (Hashtable)linkHash[contentKey];
            if (localHash == null)
            {
                return CacheManager.AddLinkHashEntry(contentKey, nameKey);
            }

            return localHash.ContainsKey(nameKey) ? (RelationInfo)localHash[nameKey] : null;
        }
    }
}
