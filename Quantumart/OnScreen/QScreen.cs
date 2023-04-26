using System;
using Quantumart.QPublishing.Database;
using Quantumart.QPublishing.Info;
using System.Net;
using Microsoft.AspNetCore.Http;

// ReSharper disable once CheckNamespace
namespace Quantumart.QPublishing.OnScreen
{
    public class QScreen
    {
        private readonly DBConnector _dbConnector;

        public QScreen(DBConnector dbConnector)
        {
            _dbConnector = dbConnector;
        }

        internal static readonly string AuthenticationKey = "QA.dll.CustomTabAuthUser";

        public int FieldBorderMode { get; set; }

        public int ObjectBorderMode { get; set; }

        public int ObjectBorderTypeMask { get; set; }

        public string QpBackendUrl => _dbConnector.HttpContext.Session.GetString("qp_backend_url") ?? string.Empty;

        public int OnFlyObjCount { get; set; } = 0;

        internal void SetSiteBorderModes(Site site)
        {
            if (site != null)
            {
                FieldBorderMode = site.FieldBorderMode;
            }
        }

        internal bool SessionEnabled() => _dbConnector.HttpContext.Session != null && _dbConnector.HttpContext.Session.IsAvailable;

        private void SaveInSession(string key, int value)
        {
            _dbConnector.HttpContext.Session.SetInt32(key, value);
        }

        private void SaveInSession(string key, string value)
        {
            _dbConnector.HttpContext.Session.SetString(key, value);
        }

        private string GetQueryParameter(string key)
        {
            var request = _dbConnector.HttpContext.Request;
            var value = request.Query[key].ToString();
            if (string.IsNullOrEmpty(value) && request.HasFormContentType)
            {
                value = request.Form[key].ToString();
            }
            return value;
        }

        public int AuthenticateForCustomTab(DBConnector cnn, string backendSid)
        {
            var result = 0;
            if (!string.IsNullOrEmpty(backendSid))
            {
                var readCmd = cnn.CreateDbCommand("SELECT user_id from sessions_log WHERE sid = @sid");
                readCmd.Parameters.AddWithValue("@sid", backendSid);
                var dt = cnn.GetRealData(readCmd);
                if (dt.Rows.Count > 0)
                {
                    result = (int)(decimal)dt.Rows[0]["user_id"];
                    var updateCmd = cnn.CreateDbCommand("UPDATE sessions_log SET sid = NULL WHERE sid = @sid");
                    updateCmd.Parameters.AddWithValue("@sid", backendSid);
                    cnn.ProcessData(updateCmd);
                }
            }

            return result;
        }

        public int AuthenticateForCustomTab(DBConnector cnn)
        {
            var backendSid = GetQueryParameter("backend_sid").Replace("'", "''");
            return AuthenticateForCustomTab(cnn, backendSid);
        }

        public int AuthenticateForCustomTab() => AuthenticateForCustomTab(_dbConnector);

        public int AuthenticateForCustomTab(string backendSid) => AuthenticateForCustomTab(_dbConnector, backendSid);

        public bool CheckCustomTabAuthentication(DBConnector dbConnector)
        {
            var backendSid = GetQueryParameter("backend_sid").Replace("'", "''");
            if (string.IsNullOrEmpty(backendSid))
            {
                return _dbConnector.HttpContext.Session.GetInt32(AuthenticationKey).HasValue;
            }

            var result = AuthenticateForCustomTab(dbConnector);
            if (result == 0)
            {
                return false;
            }

            _dbConnector.HttpContext.Session.SetInt32(AuthenticationKey, result);
            return true;
        }

        public bool CheckCustomTabAuthentication() => CheckCustomTabAuthentication(_dbConnector);

        public int GetCustomTabUserId() => _dbConnector.HttpContext?.Session?.GetInt32(AuthenticationKey) ?? 0;

        internal void GetBackendAuthentication()
        {
            if (SessionEnabled())
            {
                var backendSid = GetQueryParameter("backend_sid");
                if (!string.IsNullOrEmpty(backendSid))
                {
                    backendSid = backendSid.Replace("'", "''");
                    var backendUrl = GetQueryParameter("qp_backend_url");
                    if (!string.IsNullOrEmpty(backendUrl))
                    {
                        SaveInSession("qp_backend_url", backendUrl);
                        var sql = $" SELECT s.*, u.language_id, u.allow_stage_edit_object, u.allow_stage_edit_field FROM sessions_log AS s LEFT OUTER JOIN users AS u ON u.user_id = s.user_id WHERE sid='{backendSid}'";
                        var dt = _dbConnector.GetRealData(sql);
                        if (dt.Rows.Count > 0)
                        {
                            var row = dt.Rows[0];
                            SaveInSession("uid", int.Parse(row["user_id"].ToString()));
                            SaveInSession("allow_stage_edit_field", int.Parse(row["allow_stage_edit_field"].ToString()));
                            SaveInSession("allow_stage_edit_object", int.Parse(row["allow_stage_edit_object"].ToString()));
                            SaveInSession("CurrentLanguageID", int.Parse(row["language_id"].ToString()));
                            _dbConnector.ProcessData($"UPDATE sessions_log SET sid=NULL WHERE sid='{backendSid}'");
                        }
                    }
                }
            }
        }

        public string GetSiteDns(int siteId) => "http://" + _dbConnector.GetDns(siteId, false);

        public string GetObjectStageRedirectHref(string redirect, int templateId, int pageId, int objectId, int formatId)
        {
            var queryString = _dbConnector.HttpContext.Request.QueryString.ToString();
            return QpBackendUrl + "?redirect=" + redirect + "&page_template_id=" + templateId + "&page_id=" + pageId + "&object_id=" + objectId + "&format_id=" + formatId + "&ret_stage_url=" + RemoveIisErrorCode(queryString);
        }

        public int GetObjectTypeIdByObjectId(int objectId)
        {
            var strSql = "select o.object_type_id from object as o where o.object_id=" + objectId;
            var dt = _dbConnector.GetCachedData(strSql);
            return dt.Rows.Count > 0 ? DBConnector.GetNumInt(dt.Rows[0]["object_type_id"]) : 0;
        }

        public int GetAllowStageEditByObjectId(int objectId)
        {
            var strSql = "select o.allow_stage_edit from object as o where o.object_id=" + objectId;
            var dt = _dbConnector.GetCachedData(strSql);
            return dt.Rows.Count > 0 ? DBConnector.GetNumInt(dt.Rows[0]["allow_stage_edit"]) : 0;
        }

        public static string RemoveIisErrorCode(string queryString)
        {
            if (queryString.StartsWith("?"))
            {
                var errorCodeEndIndex = queryString.IndexOf(";", StringComparison.Ordinal);
                queryString = queryString.Remove(0, errorCodeEndIndex + 1);
            }

            return queryString;
        }

        public string GetButtonHtml() => " <td width=\"28\" id=\"onfly_obj_<@2@>_btn_<@0@>\" <@3@> style=\"margin:0;padding:0\"><div style=\"cursor:hand;margin:0;padding:0\" border=\"0\" ><img src=\"/rs/images/onfly/onfly_obj_<@2@>_over.jpg\" width=\"0\" height=\"0\" border=\"0\" style=\"margin:0;padding:0\" ><img src=\"/rs/images/onfly/onfly_obj_<@2@>.jpg\" picture_name=\"onfly_obj_<@2@>\" border=\"0\" title=\"<@1@>\" width=\"28\" height=\"26\" onmouseover=\"onfly_obj_div_<@0@>.btnMouseOver(this)\" onmouseout=\"onfly_obj_div_<@0@>.btnMouseOut(this)\" onclick=\"onfly_obj_div_<@0@>.btnClick(this)\" id=\"onfly_obj_<@2@>_btn_img_<@0@>\" style=\"margin:0;padding:0\"></div></td>";

        public string GetHrefHtml() => "<td width=\"0\" style=\"margin:0;padding:0\"><a id=\"onfly_obj_<@1@>_href_<@0@>\" href=\"<@2@>\" target=\"main\" obj_removed=\"<@2@>\"></a></td>";

        public int DbGetUserAccess(string table, int id, int gid) => GetAccessInternal(table, id, gid, true, false);

        public int GetAccessInternal(string table, int id, int uid, bool isUser, bool useDictionary)
        {
            string sql;
            int groupId = 0, userId = 0;
            int contentId = 0, allowItemPermission = 0;

            if (isUser)
            {
                userId = uid;
            }
            else
            {
                groupId = uid;
            }

            if (uid == 1)
            {
                return 4;
            }

            if (string.Equals(table, "content_item", StringComparison.InvariantCultureIgnoreCase))
            {
                sql = $" SELECT c.* FROM content_item AS i with(nolock) LEFT OUTER JOIN content AS c ON c.content_id = i.content_id WHERE i.content_item_id = {id}";

                var dt = _dbConnector.GetCachedData(sql);
                if (dt.Rows.Count > 0)
                {
                    contentId = DBConnector.GetNumInt(dt.Rows[0]["content_id"]);
                    allowItemPermission = DBConnector.GetNumInt(dt.Rows[0]["allow_items_permission"]);
                }

                if (allowItemPermission == 0 && !string.IsNullOrEmpty(contentId.ToString()))
                {
                    return GetAccessInternal("content", contentId, uid, isUser, useDictionary);
                }
            }

            sql = $"select dbo.qp_is_entity_accessible('{table}', {id}, {userId}, {groupId}, 0, 0, 1) as level";

            var dt1 = _dbConnector.GetCachedData(sql);
            var result = (int)dt1.Rows[0]["level"];
            if (result < 0)
            {
                result = 0;
            }

            return result;
        }

        public string GetUrlPort()
        {
            var serverPort = _dbConnector.HttpContext.Request.Headers["SERVER_PORT"];
            if (!string.IsNullOrEmpty(serverPort) && serverPort != "80")
            {
                return ":" + serverPort;
            }

            return string.Empty;
        }

        public string GetReturnStageUrl() =>
            WebUtility.UrlEncode("http://" + _dbConnector.HttpContext.Request.Headers["SERVER_NAME"] + GetUrlPort() + _dbConnector.HttpContext.Request.Headers["SCRIPT_NAME"] + "?" + _dbConnector.HttpContext.Request.Headers["QUERY_STRING"]);

        public bool IsBrowseServerMode() => SessionEnabled() && int.TryParse(_dbConnector.HttpContext.Session.GetString("BrowseServerSessionID"), out int _);

        public string GetBrowserInfo()
        {
            var agent = _dbConnector.HttpContext.Request.Headers["User-Agent"].ToString();
            if (agent.IndexOf("Opera", StringComparison.Ordinal) >= 0)
            {
                return "opera";
            }

            if (agent.IndexOf("MSIE", StringComparison.Ordinal) >= 0)
            {
                return "ie";
            }

            if (agent.IndexOf("Firefox", StringComparison.Ordinal) >= 0)
            {
                return "firefox";
            }

            if (agent.IndexOf("Netscape/7", StringComparison.Ordinal) >= 0)
            {
                return "ns7";
            }

            if (agent.IndexOf("Mozilla/5", StringComparison.Ordinal) >= 0 && agent.IndexOf("Netscape", StringComparison.Ordinal) < 0)
            {
                return "mozilla";
            }

            return agent.IndexOf("Mozilla", StringComparison.Ordinal) >= 0 ? "ns" : "unknown";
        }

        public bool UserAuthenticated() => SessionEnabled() && _dbConnector.HttpContext.Session.GetString("uid") != null;
    }
}
