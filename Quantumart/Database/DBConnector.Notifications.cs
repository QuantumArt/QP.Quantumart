using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Xml.Linq;
using NLog;
using NLog.Fluent;
using QP.ConfigurationService.Models;
using Quantumart.QPublishing.Info;
using Quantumart.QPublishing.Services;

// ReSharper disable once CheckNamespace
namespace Quantumart.QPublishing.Database
{
    // ReSharper disable once InconsistentNaming
    public partial class DBConnector
    {
        private static Logger _logger = LogManager.GetCurrentClassLogger();

        public bool ThrowNotificationExceptions { get; set; }
        public bool DisableServiceNotifications { get; set; }

        public bool DisableInternalNotifications { get; set; }

        public Action<Exception> ExternalExceptionHandler { get; set; }

        private void ProceedExternalNotification(int id, string eventName, string externalUrl, ContentItem item, bool useService)
        {
            eventName = eventName.ToLowerInvariant();
            if (!string.IsNullOrEmpty(externalUrl))
            {
                if (useService)
                {
                    if (!DisableServiceNotifications)
                    {
                        EnqueueNotification(id, eventName, externalUrl, item);
                    }
                }
                else
                {
                    MakeExternalCall(id, eventName, externalUrl, item);
                }
            }
        }

        private void EnqueueNotification(int id, string eventName, string externalUrl, ContentItem item)
        {
            var newDoc = item.GetXDocument();
            XDocument oldDoc = null;

            if (eventName == NotificationEvent.Modify || eventName == NotificationEvent.StatusChanged || eventName == NotificationEvent.StatusPartiallyChanged || eventName == NotificationEvent.DelayedPublication)
            {
                try
                {
                    oldDoc = ContentItem.ReadLastVersion(item.Id, this).GetXDocument();
                }
                catch (Exception ex)
                {
                    ExternalExceptionHandler?.Invoke(ex);
                    oldDoc = newDoc;
                }
            }
            else if (eventName == NotificationEvent.Remove)
            {
                oldDoc = newDoc;
                newDoc = null;
            }

            using (var cmd = new SqlCommand())
            {
                cmd.CommandType = CommandType.Text;
                var sb = new StringBuilder();
                sb.AppendLine("insert into EXTERNAL_NOTIFICATION_QUEUE(ARTICLE_ID, EVENT_NAME, URL, NEW_XML, OLD_XML, CONTENT_ID, SITE_ID)");
                sb.AppendLine("values (@id, @eventName, @url, @newXml, @oldXml, @contentId, @siteId)");
                cmd.CommandText = sb.ToString();
                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.Add(new SqlParameter("@eventName", SqlDbType.NVarChar, 50) { Value = eventName });
                cmd.Parameters.Add(new SqlParameter("@url", SqlDbType.NVarChar, 1024) { Value = externalUrl });
                cmd.Parameters.Add(new SqlParameter("@contentId", SqlDbType.Decimal) { Value = item.ContentId });
                cmd.Parameters.Add(new SqlParameter("@siteId", SqlDbType.Decimal) { Value = item.SiteId });
                cmd.Parameters.Add(new SqlParameter("@newXml", SqlDbType.NVarChar, -1) { Value = newDoc == null ? DBNull.Value : (object)newDoc.ToString() });
                cmd.Parameters.Add(new SqlParameter("@oldXml", SqlDbType.NVarChar, -1) { Value = oldDoc == null ? DBNull.Value : (object)oldDoc.ToString() });
                ProcessData(cmd);
            }
        }

        private void MakeExternalCall(int id, string eventName, string externalUrl, ContentItem item)
        {
            var queryParams = new Dictionary<string, object>
            {
                { "eventName", eventName },
                { "id", id },
                { "contentId", item.ContentId },
                { "siteId", GetSiteIdByContentId(item.ContentId) },
                { "visible", item.Visible },
                { "archive", item.Archive },
                { "splitted", item.Splitted },
                { "statusName", item.StatusName }
            };

            if (eventName == NotificationEvent.Modify || eventName == NotificationEvent.StatusChanged || eventName == NotificationEvent.StatusPartiallyChanged)
            {
                var row = GetPreviousStatusHistoryRecord(id);
                if (row != null)
                {
                    queryParams.Add("oldVisible", (bool)row["visible"]);
                    queryParams.Add("oldArchive", (bool)row["archive"]);
                    queryParams.Add("oldStatusName", row["status_type_name"].ToString());
                }
            }

            var arr = queryParams.AsEnumerable().Select(n => $"{n.Key}={n.Value.ToString().ToLowerInvariant()}").ToArray();
            var queryString = string.Join("&", arr);
            var delimiter = externalUrl.Contains("?") ? "&" : "?";
            var fullUrl = string.Concat(externalUrl, delimiter, queryString);
            var request = (HttpWebRequest)WebRequest.Create(fullUrl);
            var result = request.GetResponseAsync().ContinueWith(t => { InternalExceptionHandler(t.Exception, "GetResponseAsync", request); });
            if (ExternalExceptionHandler != null)
            {
                result.ContinueWith(t => { ExternalExceptionHandler(t.Exception); });
            }
        }

        private void InternalExceptionHandler(Exception ex, string code, WebRequest request)
        {
            var message = $"Unhandled exception occurs. Code: {code}, URL: {request?.RequestUri}";
            _logger.Error().Exception(ex).Message(message).Write();

            if (ThrowNotificationExceptions)
            {
                throw new Exception(message, ex);
            }
        }

        private int GetContentId(int contentItemId)
        {
            int contentId = GetContentIdForItem(contentItemId);
            if (contentId == 0)
            {
                throw new Exception($"Article (ID = {contentItemId}) is not found");
            }

            return contentId;
        }

        public void SendNotificationById(int contentItemId, int[] notificationIds)
        {
            int contentId = GetContentId(contentItemId);
            int siteId = GetSiteIdByContentId(contentId);
            SendNotification(siteId, NotificationEvent.FrontendRequest, contentItemId, string.Empty, !IsStage, notificationIds);
        }

        public void SendNotification(int contentItemId, string notificationOn)
        {
            var contentId = GetContentId(contentItemId);
            var siteId = GetSiteIdByContentId(contentId);
            SendNotification(siteId, notificationOn, contentItemId, string.Empty, !IsStage);
        }

        public void SendNotification(int siteId, string notificationOn, int contentItemId, string notificationEmail, bool isLive, int[] notificationIds = null)
        {
            ValidateNotificationEvent(notificationOn);
            try
            {
                var rstNotifications = GetNotificationsTable(notificationOn, contentItemId, notificationIds);
                var hasUseServiceColumn = rstNotifications.Columns.Contains("USE_SERVICE");
                var notifications = rstNotifications.Rows.Cast<DataRow>().ToArray();
                var externalNotifications = notifications.Where(n => (bool)n["is_external"]);
                var dataRows = externalNotifications as DataRow[] ?? externalNotifications.ToArray();
                if (dataRows.Any())
                {
                    var item = ContentItem.Read(contentItemId, this);
                    foreach (var row in dataRows)
                    {
                        var useService = hasUseServiceColumn && (bool)row["USE_SERVICE"];
                        var url = GetString(row["EXTERNAL_URL"], string.Empty);
                        ProceedExternalNotification(contentItemId, notificationOn, url, item, useService);
                    }
                }

                if (!DisableInternalNotifications)
                {
                    var internalNotifications = notifications.Except(dataRows);
                    if (string.Equals(DbConnectorSettings.MailComponent, "qa_mail", StringComparison.InvariantCultureIgnoreCase) || string.IsNullOrEmpty(DbConnectorSettings.MailHost))
                    {
                        if (internalNotifications.Any())
                        {
                            LoadUrl(GetAspWrapperUrl(siteId, notificationOn, contentItemId, notificationEmail, isLive));
                        }
                    }
                    else
                    {
                        var strSqlRegisterNotifyForUsers = string.Empty;
                        foreach (var notifyRow in internalNotifications)
                        {
                            if (!ReferenceEquals(notifyRow["TEMPLATE_ID"], DBNull.Value) || !GetNumBool(notifyRow["NO_EMAIL"]))
                            {
                                int contentId = GetNumInt(notifyRow["CONTENT_ID"]);

                                var mailMess = new MailMessage
                                {
                                    From = GetFromAddress(notifyRow)
                                };

                                SetToMail(notifyRow,
                                    contentItemId,
                                    notificationOn,
                                    notificationEmail,
                                    mailMess,
                                    ref strSqlRegisterNotifyForUsers);

                                mailMess.IsBodyHtml = true;

                                var doAttachFiles = (bool)notifyRow["SEND_FILES"];

                                try
                                {
                                    IMailRenderService renderer = new FluidBaseMailRenderService();
                                    (string subjectTemplate, string bodyTemplate) = GetTemplate(GetNumInt(notifyRow["TEMPLATE_ID"]));
                                    object model = BuildObjectModelFromArticle(contentItemId);
                                    mailMess.Subject = renderer.RenderText(subjectTemplate, model);
                                    mailMess.Body = renderer.RenderText(bodyTemplate, model);
                                }
                                catch (Exception ex)
                                {
                                    mailMess.Body = $"An error has occurred while building notification theme or message body. Error message: {ex.Message}";
                                    _logger.Error().Exception(ex).Message("Error while building message").Write();
                                    doAttachFiles = false;
                                }

                                if (doAttachFiles)
                                {
                                    AttachFiles(mailMess,
                                        siteId,
                                        contentId,
                                        contentItemId);
                                }

                                SendMail(mailMess);

                                if (!string.IsNullOrEmpty(strSqlRegisterNotifyForUsers + string.Empty))
                                {
                                    ProcessData(strSqlRegisterNotifyForUsers);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                InternalExceptionHandler(ex, "SendNotification", null);
                ExternalExceptionHandler?.Invoke(ex);
            }
        }

        private object BuildObjectModelFromArticle(int contentItemId)
        {
            ContentItem article = ContentItem.Read(contentItemId, this);

            dynamic model = new ExpandoObject();
            ICollection<KeyValuePair<string, object>> collection = (ICollection<KeyValuePair<string, object>>)model;

            collection.Add(new(nameof(article.Id), article.Id));
            collection.Add(new(nameof(article.ContentId), article.ContentId));
            collection.Add(new(nameof(article.Created), article.Created));
            collection.Add(new(nameof(article.Modified), article.Modified));
            collection.Add(new(nameof(article.StatusName), article.StatusName));
            collection.Add(new(nameof(article.LastModifiedBy), article.LastModifiedBy));

            foreach (KeyValuePair<string,ContentItemValue> field in article.FieldValues)
            {
                collection.Add(new(field.Key, field.Value.Data));
            }

            return model;
        }

        private (string, string) GetTemplate(int templateId)
        {
            ContentItem result = ContentItem.Read(templateId, this);

            if (result == null)
            {
                throw new InvalidOperationException($"Unable to load template with id {templateId}");
            }

            return (
                result.FieldValues
                   .Where(x => x.Key == "SingleArticleTheme")
                   .Select(x => x.Value.Data).Single(),
                result.FieldValues
                   .Where(x => x.Key == "SingleArticleTemplate")
                   .Select(x => x.Value.Data).Single());
        }

        private static void ValidateNotificationEvent(string notificationOn)
        {
            string[] events =
            {
                NotificationEvent.Create,
                NotificationEvent.Modify,
                NotificationEvent.Remove,
                NotificationEvent.StatusChanged,
                NotificationEvent.StatusPartiallyChanged,
                NotificationEvent.FrontendRequest,
                NotificationEvent.DelayedPublication
            };

            var ok = events.Contains(notificationOn.ToLowerInvariant());
            if (!ok)
            {
                throw new Exception("notificationOn parameter is not valid. Choose it from the following range: " + string.Join(", ", events));
            }
        }

        public string GetUrlContent(string url) => GetUrlContent(url, true);

        public string GetUrlContent(string url, bool throwException)
        {
            using (var response = GetUrlResponse(url))
            {
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    var stream = response.GetResponseStream();
                    var charset = response.CharacterSet ?? "UTF-8";
                    return stream == null ? null : new StreamReader(stream, Encoding.GetEncoding(charset)).ReadToEnd();
                }

                if (throwException)
                {
                    throw new Exception(response.StatusDescription + "(" + response.StatusCode + "): " + url);
                }

                return url + "<br>" + response.StatusCode + "<br>" + response.StatusDescription;
            }
        }

        public void LoadUrl(string url)
        {
            GetUrlContent(url);
        }

        private static HttpWebResponse GetUrlResponse(string url)
        {
            HttpWebResponse functionReturnValue;
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(url);
                functionReturnValue = (HttpWebResponse)request.GetResponse();
            }
            catch (WebException ex)
            {
                functionReturnValue = (HttpWebResponse)ex.Response;
            }

            return functionReturnValue;
        }

        public void AssembleFormatToFile(int siteId, int objectFormatId)
        {
            LoadUrl(GetNotifyDirectory(siteId) + "AssembleFormat.asp?objectFormatId=" + objectFormatId);
        }

        private string GetNotifyDirectory(int siteId)
        {
            var notifyUrl = GetSiteUrl(siteId, true) + DbConnectorSettings.RelNotifyUrl;
            return notifyUrl.ToLowerInvariant().Replace("notify.asp", "");
        }

        private string GetAspWrapperUrl(int siteId, string notificationOn, int contentItemId, string notificationEmail, bool isLive)
        {
            var liveString = isLive ? "1" : "0";
            return $"{GetSiteUrl(siteId, isLive)}{DbConnectorSettings.RelNotifyUrl}?id={contentItemId}&target={notificationOn}&email={notificationEmail}&is_live={liveString}";
        }

        private DataTable GetNotificationsTable(string notificationOn, int contentItemId, int[] notificationIds)
        {
            var contentId = GetContentIdForItem(contentItemId);
            var sb = new StringBuilder();
            var nolock = DatabaseType == DatabaseType.SqlServer ? " with(nolock) " : "";
            var on = DatabaseType == DatabaseType.SqlServer ? " = 1" : "";
            sb.Append($" select n.NOTIFICATION_ID, n.NOTIFICATION_NAME, n.CONTENT_ID, n.FORMAT_ID, n.USER_ID, n.GROUP_ID,");
            sb.Append($" n.NOTIFY_ON_STATUS_TYPE_ID, n.EMAIL_ATTRIBUTE_ID, n.NO_EMAIL, n.SEND_FILES, n.FROM_BACKENDUSER_ID, n.FROM_BACKENDUSER,");
            sb.Append($" n.FROM_DEFAULT_NAME, n.FROM_USER_EMAIL, n.FROM_USER_NAME, n.USE_SERVICE, n.is_external,");
            sb.Append($" n.template_id, c.site_id, coalesce(n.external_url, s.external_url) as external_url");
            sb.Append($" FROM notifications AS n {nolock}");
            sb.Append($" INNER JOIN content AS c {nolock} ON c.content_id = n.content_id");
            sb.Append($" INNER JOIN site AS s {nolock} ON c.site_id = s.site_id");
            sb.Append($" WHERE n.content_id = {contentId}");
            sb.Append($" AND n.{notificationOn}{on}");

            if (notificationOn.ToLowerInvariant() == NotificationEvent.StatusChanged)
            {
                var stSql = $"select status_type_id from content_item where content_item_id = {contentItemId}";
                var dt = GetRealData(stSql);
                var status = dt.Rows[0]["status_type_id"].ToString();
                sb.Append($" AND (n.notify_on_status_type_id IS NULL OR n.notify_on_status_type_id = {status})");
            }

            if (notificationOn.ToLowerInvariant() == NotificationEvent.FrontendRequest && notificationIds is { Length: > 0 })
            {
                sb.Append($" AND n.NOTIFICATION_ID IN ({string.Join(",", notificationIds)})");
            }
            return GetCachedData(sb.ToString());
        }

        private DataTable GetRecipientTable(DataRow notifyRow, int contentItemId)
        {
            var userId = notifyRow["USER_ID"];
            var groupId = notifyRow["GROUP_ID"];
            var eMailAttrId = notifyRow["EMAIL_ATTRIBUTE_ID"];

            string strSql;
            if (!ReferenceEquals(userId, DBNull.Value))
            {
                strSql = $"SELECT EMAIL, USER_ID FROM users WHERE user_id = {userId}";
            }
            else if (!ReferenceEquals(groupId, DBNull.Value))
            {
                strSql = $"SELECT U.EMAIL, U.USER_ID FROM users AS u LEFT OUTER JOIN user_group_bind AS ub ON ub.user_id = u.user_id WHERE ub.group_id = {groupId}";
            }
            else if (!ReferenceEquals(eMailAttrId, DBNull.Value))
            {
                strSql = $"SELECT DATA AS EMAIL, NULL AS USER_ID FROM content_data WHERE content_item_id = @itemId AND attribute_id = {eMailAttrId}";
            }
            else
            {
                strSql = $"SELECT DISTINCT(U.EMAIL), U.USER_ID FROM content_item_status_history AS ch LEFT OUTER JOIN users AS u ON ch.user_id = u.user_id WHERE ch.content_item_id={contentItemId}";
            }

            return GetCachedData(strSql);
        }

        private static string GetSqlRegisterNotificationsForUsers(DataTable toTable, int contentItemId, int notificationId, string notificationOn)
        {
            var sb = new StringBuilder();
            sb.AppendLine();
            foreach (DataRow dr in toTable.Rows)
            {
                if (!ReferenceEquals(dr["EMAIL"], DBNull.Value))
                {
                    if (!ReferenceEquals(dr["USER_ID"], DBNull.Value))
                    {
                        sb.AppendFormat($"INSERT INTO notifications_sent VALUES ({dr["USER_ID"]}, {notificationId}, {contentItemId}, DEFAULT, '{notificationOn.ToLower()}')");
                    }
                }
            }

            return sb.ToString();
        }

        private static string ConvertToString(object obj) => obj == DBNull.Value ? "" : obj.ToString();

        private static string ConvertToNullString(object obj) => ReferenceEquals(obj, DBNull.Value) ? "NULL" : obj.ToString();

        private MailAddress GetFromAddress(DataRow notifyRow)
        {
            MailAddress functionReturnValue;
            string fromName;
            var from = string.Empty;
            if ((bool)notifyRow["FROM_DEFAULT_NAME"])
            {
                fromName = DbConnectorSettings.MailFromName;
            }
            else
            {
                fromName = ConvertToString(notifyRow["FROM_USER_NAME"]);
            }

            if ((bool)notifyRow["FROM_BACKENDUSER"])
            {
                var rstUsers = GetCachedData($"SELECT EMAIL FROM USERS WHERE USER_ID = {notifyRow["FROM_BACKENDUSER_ID"]}");
                if (rstUsers.Rows.Count > 0)
                {
                    from = rstUsers.Rows[0]["EMAIL"].ToString();
                }
            }
            else
            {
                from = ConvertToString(notifyRow["FROM_USER_EMAIL"]);
            }

            if (!string.IsNullOrEmpty(from))
            {
                functionReturnValue = !string.IsNullOrEmpty(fromName) ? new MailAddress(from, fromName) : new MailAddress(from);
            }
            else
            {
                throw new Exception("Mail sender is not defined");
            }

            return functionReturnValue;
        }

        public void AttachFiles(MailMessage mailMess, int siteId, int contentId, int contentItemId)
        {
            var strDataSql = $"select cd.data from content_data cd inner join content_attribute ca on cd.attribute_id = ca.attribute_id where ca.content_id = {contentId} and ca.attribute_type_id in (7,8) and cd.content_item_id = {contentItemId}";
            var rstData = GetRealData(strDataSql);
            var currentDir = GetUploadDir(siteId) + "\\contents\\" + contentId;
            foreach (DataRow fileRow in rstData.Rows)
            {
                var fileName = currentDir + Path.DirectorySeparatorChar + fileRow["data"];
                if (File.Exists(fileName))
                {
                    mailMess.Attachments.Add(new Attachment(fileName));
                }
            }
        }

        private void SendMail(MailMessage mailMess)
        {
            var mailHost = DbConnectorSettings.MailHost;
            var smtpMail = new SmtpClient { UseDefaultCredentials = false };
            if (string.IsNullOrEmpty(mailHost))
            {
                throw new Exception("MailHost configuration parameter is not defined");
            }

            smtpMail.Host = mailHost;
            if (!string.IsNullOrEmpty(DbConnectorSettings.MailLogin))
            {
                var credentials = new NetworkCredential
                {
                    UserName = DbConnectorSettings.MailLogin,
                    Password = DbConnectorSettings.MailPassword
                };

                smtpMail.Credentials = credentials;
            }

            smtpMail.Send(mailMess);
        }

        private static void SetToMail(MailMessage mailMess, DataTable toTable)
        {
            foreach (DataRow dr in toTable.Rows)
            {
                SetToMail(mailMess, ConvertToString(dr["EMAIL"]));
            }
        }

        private static void SetToMail(MailMessage mailMess, string allEmails)
        {
            var emails = allEmails.Split(';');
            foreach (var email in emails)
            {
                if (!string.IsNullOrEmpty(email))
                {
                    mailMess.To.Add(new MailAddress(email));
                }
            }
        }

        private void SetToMail(DataRow notifyRow, int contentItemId, string notificationOn, string notificationEmail, MailMessage mailMess, ref string strSqlRegisterNotificationsForUsers)
        {
            var notificationId = GetNumInt(notifyRow["NOTIFICATION_ID"]);
            if (notificationEmail.Length > 0)
            {
                SetToMail(mailMess, notificationEmail);
            }
            else
            {
                var toTable = GetRecipientTable(notifyRow, contentItemId);
                SetToMail(mailMess, toTable);
                strSqlRegisterNotificationsForUsers = GetSqlRegisterNotificationsForUsers(toTable, contentItemId, notificationId, notificationOn);
            }
        }
    }
}
