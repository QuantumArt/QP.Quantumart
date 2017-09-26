using System;
using System.Collections;
using System.Data;
using System.Text;

// ReSharper disable once CheckNamespace
namespace Quantumart.QPublishing.Database
{
    // ReSharper disable once InconsistentNaming
    public partial class DBConnector
    {
        public DataTable GetDataTable(string key) => CacheManager.GetDataTable(key);

        public DataView GetDataView(string key, string rowFilter) => CacheManager.GetDataView(key, rowFilter);

        internal DataView GetConstraints(string rowFilter) => GetDataView(CacheManager.ConstraintKey, rowFilter);

        internal DataView GetStatuses(string rowFilter) => GetDataView(CacheManager.StatusKey, rowFilter);

        internal DataView GetTemplates(string rowFilter) => GetDataView(CacheManager.TemplateKey, rowFilter);

        internal DataView GetAllTemplates(string rowFilter) => GetDataView(CacheManager.AllTemplatesKey, rowFilter);

        internal DataView GetPages(string rowFilter) => GetDataView(CacheManager.PageKey, rowFilter);

        internal DataView GetAllPages(string rowFilter) => GetDataView(CacheManager.AllPagesKey, rowFilter);

        internal DataView GetAllTemplateObjects(string rowFilter) => GetDataView(CacheManager.AllTemplateObjectsKey, rowFilter);

        internal DataView GetAllTemplateObjects(string rowFilter, int pageTemplateId) => GetAllTemplateObjects(AppendFilter(rowFilter, "PAGE_TEMPLATE_ID", pageTemplateId));

        internal DataView GetAllPageObjects(string rowFilter, int pageId) => GetAllPageObjects(AppendFilter(rowFilter, "PAGE_ID", pageId));

        internal DataView GetAllPageObjects(string rowFilter) => GetDataView(CacheManager.AllPageObjectsKey, rowFilter);

        internal DataView GetTemplateObjects(string rowFilter) => GetDataView(CacheManager.TemplateObjectKey, rowFilter);

        internal DataView GetPageObjects(string rowFilter) => GetDataView(CacheManager.PageObjectKey, rowFilter);

        internal DataView GetTemplateMapping(string rowFilter) => GetDataView(CacheManager.TemplateMappingKey, rowFilter);

        internal DataView GetPageMapping(string rowFilter) => GetDataView(CacheManager.PageMappingKey, rowFilter);

        private static string AppendFilter(string rowFilter, string key, int value)
        {
            var sb = new StringBuilder(rowFilter);
            if (!string.IsNullOrEmpty(rowFilter))
            {
                sb.Append(" AND ");
            }

            sb.AppendFormat("{0} = {1}", key, value);
            return sb.ToString();
        }

        internal Hashtable GetContentHashTable() => CacheManager.GetCachedDualHashTable(CacheManager.ContentHashKey).Items;

        internal Hashtable GetContentIdHashTable() => CacheManager.GetCachedDualHashTable(CacheManager.ContentHashKey).Ids;

        internal Hashtable GetContentIdForLinqHashTable() => CacheManager.GetCachedHashTable(CacheManager.ContentIdForLinqHashKey);

        internal Hashtable GetTemplateHashTable() => CacheManager.GetCachedHashTable(CacheManager.TemplateHashKey);

        internal Hashtable GetPageHashTable() => CacheManager.GetCachedHashTable(CacheManager.PageHashKey);

        internal Hashtable GetTemplateMappingHashTable() => CacheManager.GetCachedHashTable(CacheManager.TemplateMappingHashKey);

        internal Hashtable GetPageMappingHashTable() => CacheManager.GetCachedHashTable(CacheManager.PageMappingHashKey);

        internal Hashtable GetTemplateObjectHashTable() => CacheManager.GetCachedHashTable(CacheManager.TemplateObjectHashKey);

        internal Hashtable GetPageObjectHashTable() => CacheManager.GetCachedHashTable(CacheManager.PageObjectHashKey);

        internal Hashtable GetLinkHashTable() => CacheManager.GetCachedHashTable(CacheManager.LinkHashKey);

        internal Hashtable GetLinkForLinqHashTable() => CacheManager.GetCachedHashTable(CacheManager.LinkForLinqHashKey);

        internal Hashtable GetItemLinkHashTable() => CacheManager.GetCachedHashTable(CacheManager.ItemLinkHashKey);

        internal Hashtable GetItemHashTable() => CacheManager.GetCachedHashTable(CacheManager.ItemHashKey);

        internal Hashtable GetStatusHashTable() => CacheManager.GetCachedHashTable(CacheManager.StatusHashKey);

        internal Hashtable GetSiteHashTable() => CacheManager.GetCachedHashTable(CacheManager.SiteHashKey);

        internal Hashtable GetSiteIdHashTable() => CacheManager.GetCachedHashTable(CacheManager.SiteIdHashKey);

        internal Hashtable GetAttributeHashTable() => CacheManager.GetCachedDualHashTable(CacheManager.AttributeHashKey).Items;

        internal Hashtable GetAttributeIdHashTable() => CacheManager.GetCachedDualHashTable(CacheManager.AttributeHashKey).Ids;

        internal Hashtable GetAttributeIdForLinqHashTable() => CacheManager.GetCachedHashTable(CacheManager.AttributeIdForLinqHashKey);

        public T GetCachedEntity<T>(string key, Func<string, T> fillAction)
            where T : class => CacheManager.GetCachedEntity(key, fillAction);

        public T GetCachedEntity<T>(string key, double interval, Func<string, T> fillAction)
            where T : class => CacheManager.GetCachedEntity(key, interval, fillAction);

        public T GetCachedEntity<T>(string key, Func<T> fillAction)
            where T : class => CacheManager.GetCachedEntity(key, fillAction);

        public T GetCachedEntity<T>(string key, double interval, Func<T> fillAction)
            where T : class => CacheManager.GetCachedEntity(key, interval, fillAction);
    }
}
