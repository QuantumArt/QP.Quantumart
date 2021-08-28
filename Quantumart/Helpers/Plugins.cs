using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Quantumart.QPublishing.Database;

namespace Quantumart.QPublishing.Helpers
{
    public class Plugins
    {
        private readonly DBConnector _connector;

        public Plugins(DBConnector connector)
        {
            _connector = connector;
        }

        public int GetId(string code, string instanceKey)
        {
            var hasKey = !String.IsNullOrEmpty(instanceKey);
            var query = "select id from plugin where code = @code";
            if (hasKey)
            {
                query += " and lower(instance_key) = @key";
            }
            var command = _connector.CreateDbCommand(query);
            command.Parameters.AddWithValue("code", code.ToLower());
            if (hasKey)
            {
                command.Parameters.AddWithValue("key", instanceKey.ToLower());
            }

            var dt = _connector.GetRealData(command);
            if (dt.Rows.Count > 1 && !hasKey)
            {
                throw new ArgumentException($"Cannot find plugin by code '{code}' only");
            }

            if (dt.Rows.Count == 0)
            {
                throw new ArgumentException($"Cannot find plugin by code '{code}'");
            }

            return (int)(decimal)dt.Rows[0]["id"];
        }

        public DataRow GetSiteMetaData(string siteName, string code, string instanceKey = null)
        {
            var pluginId = GetId(code, instanceKey);
            var siteId = _connector.GetSiteId(siteName);
            var query = $"select * from plugin_site_{pluginId} where id = {siteId}";
            var dt = _connector.GetRealData(query);
            return dt.Rows.Count > 0 ? dt.Rows[0] : null;
        }

        public DataRow GetContentMetaData(string siteName, string contentName, string code, string instanceKey = null)
        {
            var pluginId = GetId(code, instanceKey);
            var siteId = _connector.GetSiteId(siteName);
            var contentId = _connector.GetContentId(siteId, contentName);
            var query = $"select * from plugin_content_{pluginId} where id = {contentId}";
            var dt = _connector.GetRealData(query);
            return dt.Rows.Count > 0 ? dt.Rows[0] : null;
        }

        public IEnumerable<DataRow> GetContentListMetaData(string siteName, string code, string instanceKey = null)
        {
            var pluginId = GetId(code, instanceKey);
            var siteId = _connector.GetSiteId(siteName);
            var contentQuery = $"select content_id from content where site_id = {siteId}";
            var query = $"select * from plugin_content_{pluginId} where id in ({contentQuery})";
            var dt = _connector.GetRealData(query);
            return dt.AsEnumerable();
        }

        public DataRow GetContentAttributeMetaData(string siteName, string contentName, string fieldName, string code, string instanceKey = null)
        {
            var pluginId = GetId(code, instanceKey);
            var siteId = _connector.GetSiteId(siteName);
            var contentId = _connector.GetContentId(siteId, contentName);
            var query = $"select * from plugin_content_attribute_{pluginId} p " +
                $"inner join content_attribute ca on p.id = ca.attribute_id " +
                $"where ca.content_id = @id and lower(ca.attribute_name) = @name";
            var command = _connector.CreateDbCommand(query);
            command.Parameters.AddWithValue("id", contentId);
            command.Parameters.AddWithValue("name", fieldName.ToLower());
            var dt = _connector.GetRealData(command);
            return dt.Rows.Count > 0 ? dt.Rows[0] : null;
        }

        public IEnumerable<DataRow> GetContentAttributeListMetaData(string siteName, string contentName, string code, string instanceKey = null)
        {
            var siteId = _connector.GetSiteId(siteName);
            var contentId = _connector.GetContentId(siteId, contentName);
            var pluginId = GetId(code, instanceKey);
            var contentQuery = $"select attribute_id from content_attribute where content_id = {contentId}";
            var query = $"select * from plugin_content_attribute_{pluginId} where id in ({contentQuery})";
            var dt = _connector.GetRealData(query);
            return dt.AsEnumerable();
        }
    }
}
