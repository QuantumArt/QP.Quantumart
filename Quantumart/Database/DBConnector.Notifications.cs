using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;
using NLog.Fluent;
using QP.ConfigurationService.Models;
using Quantumart.QPublishing.Info;
using Quantumart.QPublishing.Info.Subscription;
using Quantumart.QPublishing.Services;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Xml.Linq;

// ReSharper disable once CheckNamespace
namespace Quantumart.QPublishing.Database
{
    // ReSharper disable once InconsistentNaming
    public partial class DBConnector
    {
        private static Logger _logger = LogManager.GetCurrentClassLogger();

        private const int RecursionLevelLimit = 1;
        private const string FirstNameField = "first_name";
        private const string LastNameField = "last_name";
        private const string SingleArticleMessageBodyFieldName = "SINGLE_ARTICLE_MESSAGE_BODY_FIELD_NAME";
        private const string MultiArticleMessageBodyFieldName = "MULTI_ARTICLE_MESSAGE_BODY_FIELD_NAME";
        private const string SingleArticleMessageSubjectFieldName = "SINGLE_ARTICLE_MESSAGE_SUBJECT_FIELD_NAME";
        private const string MultiArticleMessageSubjectFieldName = "MULTI_ARTICLE_MESSAGE_SUBJECT_FIELD_NAME";
        private const string NotificationReceiverContentId = "NOTIFICATION_RECEIVER_CONTENT_ID";
        private const int NotificationUserId = 1;

        public bool ThrowNotificationExceptions { get; set; }
        public bool DisableServiceNotifications { get; set; }

        public bool DisableInternalNotifications { get; set; }

        public Action<Exception> ExternalExceptionHandler { get; set; }

        private string NoLock => DatabaseType == DatabaseType.SqlServer ? " with(nolock) " : string.Empty;
        private string On => DatabaseType == DatabaseType.SqlServer ? " = 1" : string.Empty;

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

        /// <summary>
        /// Создание подписки
        /// </summary>
        /// <param name="notificationId">Идентификатор уведомления</param>
        /// <param name="notificationEmail">Email</param>
        /// <param name="categoryIds">Идентификаторы категорий</param>
        /// <param name="userData">Пользовательские данные</param>
        /// <param name="confirmationPeriod">Время жизни кода активации подписки</param>
        /// <returns>Данные подписки</returns>
        public NotificationSubscription AddNotificationSubscriber(int notificationId, string notificationEmail, int[] categoryIds, string userData, TimeSpan confirmationPeriod)
        {
            var id = SaveNotificationSubscription(notificationId, notificationEmail, categoryIds, userData, confirmationPeriod);

            if (id.HasValue)
            {
                return SendConfirmSubscribeNotification(id.Value);
            }

            return null;
        }

        /// <summary>
        /// Отправка уведомления на почту для подтверждения подписки
        /// </summary>
        /// <param name="subscribeItemId">Идентификатор получателя подписки</param>
        /// <returns>Данные подписки или null, если отправка уведомлений не настроена</returns>
        public NotificationSubscription SendConfirmSubscribeNotification(int subscribeItemId)
        {
            var newSubscription = GetNotificationSubscription(subscribeItemId);

            if (newSubscription is { Confirmed: false })
            {
                var notification = GetNotificationById(newSubscription.NotificationId);

                if (notification.ConfirmationTemplateId.HasValue)
                {
                    var oldSubscription = GetConfirmedNotificationSubscription(notification.NotificationId, newSubscription.Email);

                    var request = new SubscribeRequest
                    {
                        Action = SubscribeRequestMode.Unsubscribe,
                        Email = newSubscription.Email,
                        OldUserData = oldSubscription?.UserData,
                        OldCategories = oldSubscription?.Categories.Select(c => c.Name).ToArray() ?? Array.Empty<string>(),
                        NewCategories = newSubscription.Categories.Select(c => c.Name).ToArray(),
                        NewUserData = newSubscription.UserData
                    };

                    SendConfirmationNotification(notification, request);
                }
            }

            return newSubscription;
        }

        /// <summary>
        /// Отправка уведомления на почту для подтверждения отписки
        /// </summary>
        /// <param name="notificationId"></param>
        /// <param name="notificationEmail"></param>
        /// <returns>Данные подписки или null, если отправка уведомлений не настроена</returns>
        public NotificationSubscription SendUnSubscribeNotification(int notificationId, string notificationEmail)
        {
            var subscription = GetConfirmedNotificationSubscription(notificationId, notificationEmail);

            if (subscription != null)
            {
                var notification = GetNotificationById(notificationId);

                if (notification.ConfirmationTemplateId.HasValue)
                {
                    var request = new SubscribeRequest
                    {
                        Action = SubscribeRequestMode.Unsubscribe,
                        Email = notificationEmail,
                        OldUserData = subscription.UserData,
                        OldCategories = subscription.Categories.Select(c => c.Name).ToArray(),
                        NewCategories = Array.Empty<string>(),
                        NewUserData = string.Empty
                    };

                    SendConfirmationNotification(notification, request);
                }
            }

            return subscription;
        }

        /// <summary>
        /// Отписка
        /// </summary>
        /// <param name="confirmationCode">Код подтверждения</param>
        /// <returns>Данные отписки</returns>
        public SubscribeResult UnsubscribeNotificationSubscriber(string confirmationCode)
        {
            if (!ReceiverContentId.HasValue)
            {
                throw new($"Setting {NotificationReceiverContentId} is not supplied");
            }

            var subscription = GetNotificationSubscription(confirmationCode);

            SubscribeResultMode action = SubscribeResultMode.Unsubscribe;

            if (subscription == null)
            {
                action = SubscribeResultMode.ConfirmationCodeNotFound;
            }
            else if (!subscription.Confirmed)
            {
                action = SubscribeResultMode.NotConfirmed;
            }
            else if (subscription.ConfirmationDate < DateTime.Now)
            {
                action = SubscribeResultMode.ConfirmationDateExpired;
            }
            else
            {
                RemoveNotificationSubscriptions(subscription.NotificationId, subscription.Email);
            }

            return new SubscribeResult
            {
                Action = action,
                OldSubscription = subscription,
                NewSubscription = null
            };
        }

        /// <summary>
        /// Подписка
        /// </summary>
        /// <param name="confirmationCode">Код подтверждения</param>
        /// <returns>Данные подписки</returns>
        public SubscribeResult ConfirmNotificationSubscriber(string confirmationCode)
        {
            if (!ReceiverContentId.HasValue)
            {
                throw new($"Setting {NotificationReceiverContentId} is not supplied");
            }

            var newSubscription = GetNotificationSubscription(confirmationCode);
            NotificationSubscription oldSubscription = null;

            SubscribeResultMode action = SubscribeResultMode.Subscribe;

            if (newSubscription == null)
            {
                action = SubscribeResultMode.ConfirmationCodeNotFound;
            }
            else if (!newSubscription.Confirmed)
            {
                action = SubscribeResultMode.NotConfirmed;
            }
            else if (newSubscription.ConfirmationDate < DateTime.Now)
            {
                action = SubscribeResultMode.ConfirmationDateExpired;
            }
            else
            {
                oldSubscription = GetConfirmedNotificationSubscription(newSubscription.NotificationId, newSubscription.Email);

                var values = new Dictionary<string, string>()
                {
                    { SystemColumnNames.Id, newSubscription.Id.ToString(CultureInfo.InvariantCulture) },
                    { "Confirmed", "true" },
                };

                MassUpdate(ReceiverContentId.Value, new[] { values }, NotificationUserId);

                RemoveNotificationSubscriptions(newSubscription.NotificationId, newSubscription.Email, exceptId: newSubscription.Id);
            }

            return new SubscribeResult
            {
                Action = action,
                OldSubscription = oldSubscription,
                NewSubscription = newSubscription
            };
        }

        /// <summary>
        /// Получение доступных категориий для уведомления
        /// </summary>
        /// <param name="notificationId">Идентификатор уведомления</param>
        /// <returns>Массив категорий</returns>
        public SubscriptionCategory[] GetSubscriptionCategories(int notificationId)
        {
            if (!ReceiverContentId.HasValue)
            {
                throw new($"Setting {NotificationReceiverContentId} is not supplied");
            }

            var query = @$"
                select
                    subscription_category.content_item_id id,
                    subscription_category.category categoryid,
                    category.category categoryname
                from
                    content_{GetReceiverCategoryContentId()}_united subscription_category
                    join content_{GetNotificationCategoryContentId()}_united category on subscription_category.category = category.content_item_id
                where
                    subscription_category.notification = {notificationId}";

            var data = GetCachedData(query);

            return data
            .AsEnumerable()
            .Select(row => new SubscriptionCategory
            {
                Id = GetNumInt(row["id"]),
                CategoryId = GetNumInt(row["categoryid"]),
                Name = GetString(row["categoryname"], string.Empty)
            })
            .ToArray();
        }

        /// <summary>
        /// Создает неподтвержденную подписку
        /// </summary>
        /// <param name="notificationId"></param>
        /// <param name="notificationEmail"></param>
        /// <param name="categoryIds"></param>
        /// <param name="userData"></param>
        /// <param name="confirmationPeriod"></param>
        /// <returns>Идентификатор пописки</returns>
        private int? SaveNotificationSubscription(int notificationId, string notificationEmail, int[] categoryIds, string userData, TimeSpan confirmationPeriod)
        {
            if (!ReceiverContentId.HasValue)
            {
                throw new($"Setting {NotificationReceiverContentId} is not supplied");
            }

            if (!string.IsNullOrWhiteSpace(userData))
            {
                try
                {
                    _ = JObject.Parse(userData);
                }
                catch(JsonReaderException ex)
                {
                    throw new ArgumentException("User data not valid", nameof(userData), ex);
                }
            }

            var values = new Dictionary<string, string>()
            {
                { SystemColumnNames.Id, "0" },
                { "Email", notificationEmail },
                { "Notification", notificationId.ToString() },
                { "Category", string.Join(",", categoryIds) },
                { "UserData", userData },
                { "Confirmed", "false" },
                { "ConfirmationCode", Guid.NewGuid().ToString() },
                { "ConfirmationDate", DateTime.Now.Add(confirmationPeriod).ToString(CultureInfo.CurrentCulture) },
            };

            MassUpdate(ReceiverContentId.Value, new[] { values }, NotificationUserId);

            if (int.TryParse(values[SystemColumnNames.Id], out int id) && id > 0)
            {
                return id;
            }

            return null;
        }

        /// <summary>
        /// Поиск подписки по идентификатору
        /// </summary>
        /// <param name="id">Идентификатор подписки</param>
        /// <returns>Данные подписки</returns>
        private NotificationSubscription GetNotificationSubscription(int id)
        {
            var query = @$"
                select
                    content_item_id,
                    notification,
                    email,
                    userdata,
                    confirmed
                    confirmationcode,
                    confirmationdate,
                    (select a.link_id from content_attribute a where a.content_id = {ReceiverContentId} and a.attribute_name = 'Category') as category
                from content_{ReceiverContentId}_united
                where content_item_id = @id";

            var cmd = CreateDbCommand(query);
            cmd.Parameters.AddWithValue("@id", id);

            return GetNotificationSubscription(cmd);
        }

        /// <summary>
        /// Поиск подписки по коду подтверждения
        /// </summary>
        /// <param name="confirmationCode">Код подтверждения</param>
        /// <returns>Данные подписки</returns>
        private NotificationSubscription GetNotificationSubscription(string confirmationCode)
        {
            var query = @$"
                select
                    content_item_id,
                    notification,
                    email,
                    userdata,
                    confirmed,
                    confirmationcode,
                    confirmationdate,
                    (select a.link_id from content_attribute a where a.content_id = {ReceiverContentId} and a.attribute_name = 'Category') as category
                from content_{ReceiverContentId}_united
                where confirmationcode = @confirmationCode";

            var cmd = CreateDbCommand(query);
            cmd.Parameters.AddWithValue("@confirmationCode", confirmationCode);

            return GetNotificationSubscription(cmd);
        }

        /// <summary>
        /// Поиск подтвержденной подписки по еmail
        /// </summary>
        /// <param name="notificationId">Идентификатор уведомления</param>
        /// <param name="notificationEmail">Адрес электронной почты</param>
        /// <returns>Данные подписки</returns>
        private NotificationSubscription GetConfirmedNotificationSubscription(int notificationId, string notificationEmail)
        {
            var query = @$"
                select
                content_item_id,
                notification,
                email,
                userdata,
                confirmed,
                confirmationcode,
                confirmationdate,
                category
                from content_{ReceiverContentId}_united
                where notification = @notificationId and email = @notificationEmail and confirmed = 1";

            var cmd = CreateDbCommand(query);
            cmd.Parameters.AddWithValue("@notificationId", notificationId);
            cmd.Parameters.AddWithValue("@notificationEmail", notificationEmail);

            return GetNotificationSubscription(cmd);
        }

        /// <summary>
        /// Поиск подписки
        /// Поскольку операции одноразовые, то запросы не кэшируются
        /// </summary>
        /// <param name="command">Комманда поиска подписки</param>
        /// <returns>Данные подписки</returns>
        private NotificationSubscription GetNotificationSubscription(DbCommand command)
        {
            var data = GetRealData(command);

            var item = data
                .AsEnumerable()
                .Select(row => new NotificationSubscription
                {
                    Id = GetNumInt(row["content_item_id"]),
                    NotificationId = GetNumInt(row["notification"]),
                    Email = GetString(row["email"], string.Empty),
                    UserData = GetString(row["userdata"], string.Empty),
                    Confirmed = GetNumBool(row["confirmed"]),
                    ConfirmationCode = GetString(row["confirmationcode"], string.Empty),
                    ConfirmationDate = (DateTime)row["confirmationdate"],
                    CategoryLinkId = GetNumInt(row["category"])
                })
                .FirstOrDefault();

            if (item == null)
            {
                return null;
            }

            var categoriesQuery = @$"
                select
                    subscription_category.content_item_id id,
                    subscription_category.category categoryid,
                    category.category categoryname
                from
                    content_{GetReceiverCategoryContentId()}_united subscription_category
                    join content_{GetNotificationCategoryContentId()}_united category on subscription_category.category = category.content_item_id
                    join item_link_{item.CategoryLinkId} l on subscription_category.content_item_id = l.linked_id
                where
                    subscription_category.notification = @notificationId and l.id = @id";

            var categoriesCommand = CreateDbCommand(categoriesQuery);

            categoriesCommand.Parameters.AddWithValue("@notificationId", item.NotificationId);
            categoriesCommand.Parameters.AddWithValue("@id", item.Id);

            var categoriesData = GetRealData(categoriesCommand);

            var categories = categoriesData
                .AsEnumerable()
                .Select(row => new SubscriptionCategory
                {
                    Id = GetNumInt(row["id"]),
                    CategoryId = GetNumInt(row["categoryid"]),
                    Name = GetString(row["categoryname"], string.Empty)
                })
                .ToArray();

            item.Categories = categories;

            return item;
        }

        private void RemoveNotificationSubscriptions(int notificationId, string notificationEmail, int? exceptId = null)
        {
            var query = $@"
                select
                    content_item_id id
                from content_{ReceiverContentId}_united
                where notification = @notificationId and email = @email";

            if (exceptId.HasValue)
            {
                query += " and content_item_id <> @id";
            }

            var cmd = CreateDbCommand(query);

            cmd.Parameters.AddWithValue("@notificationId", notificationId);
            cmd.Parameters.AddWithValue("@email", notificationEmail);

            if (exceptId.HasValue)
            {
                cmd.Parameters.AddWithValue("@id", exceptId.Value);
            }

            var data = GetRealData(cmd);

            var ids = data.AsEnumerable().Select(row => GetNumInt(row["id"])).ToArray();

            DeleteContentItems(ids);
        }

        private void DeleteContentItems(int[] ids)
        {
            if (ids == null)
            {
                throw new ArgumentNullException(nameof(ids));
            }

            if (ids.Any())
            {
                var query = $@"
                DELETE FROM CONTENT_ITEM {SqlQuerySyntaxHelper.WithRowLock(DatabaseType)}
                WHERE CONTENT_ITEM_ID in (SELECT ID FROM {SqlQuerySyntaxHelper.GetIdTable(DatabaseType, "@ids")})";

                var cmd = CreateDbCommand(query);
                cmd.Parameters.AddWithValue(DatabaseType, "@ids", ids);
                ProcessData(cmd);
            }
        }

        /// <summary>
        /// Ищет уведомление по идентификатору
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        private Notification GetNotificationById(int id)
        {
            var notificationQuery = @$"
                select
                    n.NOTIFICATION_ID,
                    n.NOTIFICATION_NAME,
                    n.CONTENT_ID,
                    n.FROM_DEFAULT_NAME,
                    n.FROM_USER_NAME,
                    n.FROM_USER_EMAIL,
                    n.FROM_BACKENDUSER,
                    n.FROM_BACKENDUSER_ID,
                    u.EMAIL FROM_BACKENDUSER_EMAIL,
                    n.USE_EMAIL_FROM_CONTENT,
                    n.CONFIRMATION_TEMPLATE_ID
                from notifications n
                join users u on n.FROM_BACKENDUSER_ID = u.user_id
                where n.NOTIFICATION_ID = {id}";

            var notificationData = GetCachedData(notificationQuery);

            return notificationData
                .AsEnumerable()
                .Select(row => new Notification
                {
                    NotificationId = GetNumInt(row["NOTIFICATION_ID"]),
                    NotificationName = GetString(row["NOTIFICATION_NAME"], string.Empty),
                    ContentId = GetNumInt(row["CONTENT_ID"]),
                    FromDefaultName = GetNumBool(row["FROM_DEFAULT_NAME"]),
                    FromUserName = GetString(row["FROM_USER_NAME"], string.Empty),
                    FromUserEmail = GetString(row["FROM_USER_EMAIL"], string.Empty),
                    FromBackendUser = GetNumBool(row["FROM_BACKENDUSER"]),
                    FromBackendUserId = GetNumInt(row["FROM_BACKENDUSER_ID"]),
                    FromBackendUserEmail = GetString(row["FROM_BACKENDUSER_EMAIL"], string.Empty),
                    UseEmailFromContent = GetNumBool(row["USE_EMAIL_FROM_CONTENT"]),
                    ConfirmationTemplateId = GetNumInt(row["CONFIRMATION_TEMPLATE_ID"]),
                })
                .First();
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
                            if (!ReferenceEquals(notifyRow["TEMPLATE_ID"], DBNull.Value) && !GetNumBool(notifyRow["NO_EMAIL"]) && !EmailFromContentIsNotAvaliable(notifyRow))
                            {
                                int contentId = GetNumInt(notifyRow["CONTENT_ID"]);

                                if (UseEmailFromContent(notifyRow))
                                {
                                    SendEmailFromContentNotification(notifyRow, contentItemId, contentId, siteId);
                                    continue;
                                }

                                var mailMess = new MailMessage
                                {
                                    From = GetFromAddress(notifyRow)
                                };

                                SetToMail(
                                    notifyRow,
                                    new[] { contentItemId },
                                    notificationOn,
                                    notificationEmail,
                                    mailMess,
                                    ref strSqlRegisterNotifyForUsers
                                );

                                mailMess.IsBodyHtml = true;

                                var doAttachFiles = (bool)notifyRow["SEND_FILES"];

                                try
                                {
                                    IMailRenderService renderer = new FluidBaseMailRenderService();
                                    (string subjectTemplate, string bodyTemplate) = GetTemplate(GetNumInt(notifyRow["TEMPLATE_ID"]));
                                    object model = BuildObjectModelFromArticle(contentItemId);
                                    AddUserInfoToModel(notifyRow, model);
                                    mailMess.Subject = renderer.RenderText(subjectTemplate, model);
                                    mailMess.Body = renderer.RenderText(bodyTemplate, model);
                                }
                                catch (Exception ex)
                                {
                                    mailMess.Subject = "Error while building mail message.";
                                    mailMess.Body = $"An error has occurred while building notification theme or message body for article with id {contentItemId}. Error message: {ex.Message}";
                                    _logger.Error().Exception(ex).Message("Error while building message").Write();
                                    doAttachFiles = false;
                                }

                                if (doAttachFiles)
                                {
                                    AttachFiles(mailMess, siteId, contentId, contentItemId);
                                }

                                SendMail(mailMess);

                                if (!string.IsNullOrEmpty(strSqlRegisterNotifyForUsers))
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

        public void SendInternalNotificationBatch(string notificationOn, int[] contentItemIds, string notificationEmail = null)
        {
            try
            {
                if (DisableInternalNotifications)
                {
                    return;
                }

                if (string.Equals(DbConnectorSettings.MailComponent, "qa_mail", StringComparison.InvariantCultureIgnoreCase) || string.IsNullOrEmpty(DbConnectorSettings.MailHost)
                )
                {
                    throw new("Internal notifications component not configured properly");
                }

                DataTable possibleNotifications = GetNotificationsTable(notificationOn, contentItemIds.First(), null);
                IEnumerable<DataRow> internalNotifications = possibleNotifications.Rows.Cast<DataRow>().Where(n => !(bool)n["is_external"]);

                foreach (DataRow notification in internalNotifications)
                {
                    if (ReferenceEquals(notification["TEMPLATE_ID"], DBNull.Value) || GetNumBool(notification["NO_EMAIL"]))
                    {
                        continue;
                    }

                    if (UseEmailFromContent(notification))
                    {
                        continue;
                    }

                    MailMessage mailMessage = new()
                    {
                        From = GetFromAddress(notification),
                        IsBodyHtml = true
                    };

                    try
                    {
                        IMailRenderService renderer = new FluidBaseMailRenderService();
                        (string subjectTemplate, string bodyTemplate) = GetTemplate(GetNumInt(notification["TEMPLATE_ID"]), true);
                        object[] articles = contentItemIds.Select(contentItemId => BuildObjectModelFromArticle(contentItemId)).ToArray();
                        dynamic model = new ExpandoObject();
                        ICollection<KeyValuePair<string, object>> collection = (ICollection<KeyValuePair<string, object>>)model;
                        collection.Add(new("Articles", articles));
                        mailMessage.Subject = renderer.RenderText(subjectTemplate, model);
                        mailMessage.Body = renderer.RenderText(bodyTemplate, model);
                    }
                    catch (Exception ex)
                    {
                        mailMessage.Subject = "Error while building mail message.";
                        mailMessage.Body = $"An error has occurred while building notification theme or message body for articles with ids {string.Join(", ", contentItemIds)}. Error message: {ex.Message}";
                        _logger.Error().Exception(ex).Message("Error while building message").Write();
                    }

                    string strSqlRegisterNotifyForUsers = string.Empty;

                    SetToMail(notification,
                        contentItemIds,
                        notificationOn,
                        notificationEmail,
                        mailMessage,
                        ref strSqlRegisterNotifyForUsers);

                    SendMail(mailMessage);

                    if (!string.IsNullOrEmpty(strSqlRegisterNotifyForUsers))
                    {
                        ProcessData(strSqlRegisterNotifyForUsers);
                    }
                }
            }
            catch (Exception ex)
            {
                InternalExceptionHandler(ex, "SendInternalNotificationBatch", null);
                ExternalExceptionHandler?.Invoke(ex);
            }
        }

        private void SendConfirmationNotification(Notification notification, SubscribeRequest request)
        {
            IMailRenderService renderer = new FluidBaseMailRenderService();
            (string subjectTemplate, string bodyTemplate) = GetTemplate(notification.ConfirmationTemplateId.Value);

            dynamic model = new ExpandoObject();
            ICollection<KeyValuePair<string, object>> collection = (ICollection<KeyValuePair<string, object>>)model;

            collection.Add(new(nameof(request.Action), request.Action));
            collection.Add(new(nameof(request.Email), request.Email));
            collection.Add(new(nameof(request.OldCategories), request.OldCategories));
            collection.Add(new(nameof(request.NewCategories), request.NewCategories));

            if (!string.IsNullOrWhiteSpace(request.OldUserData))
            {
                try
                {
                    dynamic oldUserData = JObject.Parse(request.OldUserData);
                    collection.Add(new(nameof(request.OldUserData), oldUserData));
                }
                catch (JsonReaderException)
                {
                }
            }

            if (!string.IsNullOrWhiteSpace(request.NewUserData))
            {
                try
                {
                    dynamic newUserData = JObject.Parse(request.NewUserData);
                    collection.Add(new(nameof(request.NewUserData), newUserData));
                }
                catch (JsonReaderException)
                {
                }
            }


            var mailMess = new MailMessage
            {
                From = GetFromAddress(notification),
                IsBodyHtml = true
            };

            mailMess.Subject = renderer.RenderText(subjectTemplate, model);
            mailMess.Body = renderer.RenderText(bodyTemplate, model);
            mailMess.To.Add(request.Email);
            SendMail(mailMess);
        }

        private void SendEmailFromContentNotification(DataRow notifyRow, int contentItemId, int contentId, int siteId)
        {
            int notificationId = GetNumInt(notifyRow["NOTIFICATION_ID"]);
            var toTable = GetRecipientTable(notifyRow, new[] { contentItemId }, notificationId);
            (string subjectTemplate, string bodyTemplate) = GetTemplate(GetNumInt(notifyRow["TEMPLATE_ID"]));
            object model = BuildObjectModelFromArticle(contentItemId);
            IMailRenderService renderer = new FluidBaseMailRenderService();

            foreach (DataRow row in toTable.Rows)
            {
                var email = ConvertToString(row["EMAIL"]);
                var userForm = ConvertToString(row["USER_DATA"]);

                var mailMess = new MailMessage
                {
                    From = GetFromAddress(notifyRow),
                    IsBodyHtml = true
                };

                mailMess.To.Add(new MailAddress(email));

                var doAttachFiles = (bool)notifyRow["SEND_FILES"];

                try
                {
                    AddUserFormToModel(userForm, model);
                    mailMess.Subject = renderer.RenderText(subjectTemplate, model);
                    mailMess.Body = renderer.RenderText(bodyTemplate, model);
                    if (doAttachFiles)
                    {
                        AttachFiles(mailMess, siteId, contentId, contentItemId);
                    }

                    SendMail(mailMess);
                }
                catch (Exception ex)
                {
                    _logger.Error().Exception(ex).Message("Error while building or sending message").Write();
                }
            }
        }

        private void AddUserInfoToModel(DataRow notification, dynamic model)
        {
            object userId = notification["USER_ID"];

            if (ReferenceEquals(userId, DBNull.Value))
            {
                return;
            }

            DataRow user = GetUserInfoByUserId(userId);

            if (user is null)
            {
                return;
            }

            ICollection<KeyValuePair<string, object>> collection = (ICollection<KeyValuePair<string, object>>)model;

            collection.Add(new("RecipientFirstName", user[FirstNameField]));
            collection.Add(new("RecipientLastName", user[LastNameField]));
        }

        private void AddUserFormToModel(string userData, dynamic model)
        {
            var map = (IDictionary<string, object>)model;
            map.Remove("UserForm");

            if (!string.IsNullOrWhiteSpace(userData))
            {
                try
                {
                    dynamic userForm = JObject.Parse(userData);
                    map.Add("UserForm", userForm);
                }
                catch (JsonReaderException)
                {
                }
            }
        }

        private DataRow GetUserInfoByUserId(object userId)
        {
            StringBuilder stringBuilder = new();
            stringBuilder.Append($"select u.{FirstNameField}, u.{LastNameField}");
            stringBuilder.Append($" from users as u {NoLock}");
            stringBuilder.Append($" where user_id = {userId}");

            DataTable userData = GetCachedData(stringBuilder.ToString());

            return userData.Rows.Count == 0 ? null : userData.Rows[0];
        }

        private object BuildObjectModelFromArticle(int contentItemId, int recursionLevel = 0, int? parentId = null)
        {
            if (recursionLevel > RecursionLevelLimit)
            {
                return default;
            }

            ContentItem article = ContentItem.Read(contentItemId, this);

            if (article.Archive)
            {
                return default;
            }

            dynamic model = new ExpandoObject();
            ICollection<KeyValuePair<string, object>> collection = (ICollection<KeyValuePair<string, object>>)model;

            collection.Add(new(nameof(article.Id), article.Id));
            collection.Add(new(nameof(article.ContentId), article.ContentId));
            collection.Add(new(nameof(article.Created), article.Created));
            collection.Add(new(nameof(article.Modified), article.Modified));
            collection.Add(new(nameof(article.StatusName), article.StatusName));
            collection.Add(new(nameof(article.LastModifiedBy), article.LastModifiedBy));

            ProcessFields(collection, article.FieldValues, recursionLevel, article.Id, parentId);

            return model;
        }

        private void ProcessFields(ICollection<KeyValuePair<string, object>> collection,
            Dictionary<string, ContentItemValue> fields,
            int recursionLevel,
            int currentArticleId,
            int? parentId
        )
        {
            if (recursionLevel > RecursionLevelLimit)
            {
                return;
            }

            recursionLevel++;

            foreach (KeyValuePair<string, ContentItemValue> field in fields)
            {
                switch (field.Value.ItemType)
                {
                    case AttributeType.Relation:
                        ProcessSimpleRelationField(collection, field.Key, field.Value.Data, recursionLevel, currentArticleId, parentId);
                        break;
                    case AttributeType.M2ORelation:
                        ProcessManyToManyRelationField(collection,
                            field.Key,
                            field.Value.LinkedItems,
                            recursionLevel,
                            currentArticleId,
                            parentId
                        );
                        break;
                    case AttributeType.Numeric:
                        ProcessNumericField(collection,
                            field.Key,
                            field.Value.Data,
                            field.Value.IsClassifier,
                            field.Value.BaseArticleId,
                            recursionLevel
                        );
                        break;
                    default:
                        collection.Add(new(field.Key, field.Value.Data));
                        break;
                }
            }
        }

        private void ProcessNumericField(ICollection<KeyValuePair<string, object>> collection,
            string key,
            string value,
            bool isClassifier,
            int baseArticleId,
            int recursionLevel)
        {
            if (!isClassifier)
            {
                collection.Add(new(key, value));

                return;
            }

            if (!int.TryParse(value, out int contentId))
            {
                _logger.Warn("Unable to parse classifier id value {ClassifierId} as int. Skipping it",
                    value);

                return;
            }

            int? item = GetClassifierData(contentId, baseArticleId);

            if (!item.HasValue)
            {
                return;
            }

            ContentItem classifier = ContentItem.Read(item.Value, this);
            ProcessFields(collection, classifier.FieldValues, recursionLevel, baseArticleId, baseArticleId);
        }

        private void ProcessManyToManyRelationField(
            ICollection<KeyValuePair<string, object>> collection,
            string key,
            IReadOnlyCollection<int> values,
            int recursionLevel,
            int currentArticleId,
            int? parentId
        )
        {
            if (values.Count == 0)
            {
                return;
            }

            if (parentId.HasValue && values.Contains(parentId.Value))
            {
                return;
            }

            List<object> internalCollection = values
               .Select(linkedItem => BuildObjectModelFromArticle(linkedItem, recursionLevel, currentArticleId))
               .ToList();
            collection.Add(new(key, internalCollection));
        }

        private void ProcessSimpleRelationField(ICollection<KeyValuePair<string, object>> collection,
            string key,
            string value,
            int recursionLevel,
            int currentArticleId,
            int? parentId
        )
        {
            if (string.IsNullOrEmpty(value))
            {
                collection.Add(new(key, default));

                return;
            }

            if (!int.TryParse(value, out int id))
            {
                _logger.Warn("Unable to parse relation {RelationName} value {RelationId} as int. Skipping it",
                    key,
                    value);

                return;
            }

            if (parentId == id)
            {
                return;
            }

            try
            {
                collection.Add(new(key, BuildObjectModelFromArticle(id, recursionLevel, currentArticleId)));
            }
            catch (Exception e)
            {
                if (e.Message.StartsWith("Article is not found"))
                {
                    return;
                }

                throw;
            }
        }

        private (string, string) GetTemplate(int templateId, bool multipleArticles = false)
        {
            string subjectTemplateFieldName = GetSettingByName<string>(multipleArticles ? MultiArticleMessageSubjectFieldName : SingleArticleMessageSubjectFieldName);
            string bodyTemplateFieldName = GetSettingByName<string>(multipleArticles ? MultiArticleMessageBodyFieldName : SingleArticleMessageBodyFieldName);

            ContentItem result = ContentItem.Read(templateId, this);

            if (result == null)
            {
                throw new InvalidOperationException($"Unable to load template with id {templateId}");
            }

            return (
                result.FieldValues
                   .Where(x => x.Key == subjectTemplateFieldName)
                   .Select(x => x.Value.Data).Single(),
                result.FieldValues
                   .Where(x => x.Key == bodyTemplateFieldName)
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

        private int? GetClassifierData(int contentId, int parentId)
        {
            string parentFieldName = GetClassifierParentFieldName(contentId);

            StringBuilder sb = new();
            sb.Append("select c.content_item_id");
            sb.Append($" from content_{contentId} as c {NoLock}");
            sb.Append($" where c.{parentFieldName} = {parentId}");

            DataTable data = GetCachedData(sb.ToString());

            return data.Rows.Count == 0 ? null : GetNumInt(data.Rows[0]["content_item_id"]);
        }

        private string GetClassifierParentFieldName(int contentId)
        {
            StringBuilder sb = new();
            sb.Append("select a.attribute_name");
            sb.Append($" from content_attribute as a {NoLock}");
            sb.Append($" where a.content_id = {contentId}");
            sb.Append($" and a.aggregated{On}");

            DataTable data = GetCachedData(sb.ToString());

            if (data.Rows.Count == 0)
            {
                return string.Empty;
            }

            return (string)data.Rows[0]["attribute_name"];
        }

        private DataTable GetNotificationsTable(string notificationOn, int contentItemId, int[] notificationIds)
        {
            var contentId = GetContentIdForItem(contentItemId);
            var sb = new StringBuilder();

            sb.Append($" select n.NOTIFICATION_ID, n.NOTIFICATION_NAME, n.CONTENT_ID, n.FORMAT_ID, n.USER_ID, n.GROUP_ID,");
            sb.Append($" n.NOTIFY_ON_STATUS_TYPE_ID, n.EMAIL_ATTRIBUTE_ID, n.NO_EMAIL, n.SEND_FILES, n.FROM_BACKENDUSER_ID, n.FROM_BACKENDUSER,");
            sb.Append($" n.FROM_DEFAULT_NAME, n.FROM_USER_EMAIL, n.FROM_USER_NAME, n.USE_SERVICE, n.is_external,");
            sb.Append($" n.template_id, c.site_id, coalesce(n.external_url, s.external_url) as external_url,");
            sb.Append($" n.HIDE_RECIPIENTS, n.USE_EMAIL_FROM_CONTENT, n.CATEGORY_ATTRIBUTE_ID");
            sb.Append($" FROM notifications AS n {NoLock}");
            sb.Append($" INNER JOIN content AS c {NoLock} ON c.content_id = n.content_id");
            sb.Append($" INNER JOIN site AS s {NoLock} ON c.site_id = s.site_id");
            sb.Append($" WHERE n.content_id = {contentId}");
            sb.Append($" AND n.{notificationOn}{On}");

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

        private DataTable GetRecipientTable(DataRow notifyRow, int[] contentItemIds, int notificationId)
        {
            var userId = notifyRow["USER_ID"];
            var groupId = notifyRow["GROUP_ID"];
            var eMailAttrId = notifyRow["EMAIL_ATTRIBUTE_ID"];
            var categoryAttrId = notifyRow["CATEGORY_ATTRIBUTE_ID"];
            var ids = string.Join(",", contentItemIds);

            string strSql;
            if (!ReferenceEquals(userId, DBNull.Value))
            {
                strSql = $"SELECT EMAIL, USER_ID FROM users, NULL USER_DATA WHERE user_id = {userId}";
            }
            else if (!ReferenceEquals(groupId, DBNull.Value))
            {
                strSql = $"SELECT U.EMAIL, U.USER_ID, NULL USER_DATA FROM users AS u LEFT OUTER JOIN user_group_bind AS ub ON ub.user_id = u.user_id WHERE ub.group_id = {groupId}";
            }
            else if (!ReferenceEquals(eMailAttrId, DBNull.Value))
            {
                strSql = $"SELECT DISTINCT(DATA) AS EMAIL, NULL AS USER_ID, NULL USER_DATA FROM content_data WHERE content_item_id in ({ids}) AND attribute_id = {eMailAttrId}";
            }
            else if (UseEmailFromContent(notifyRow))
            {
                if (ReferenceEquals(categoryAttrId, DBNull.Value))
                {
                    strSql = @$"
                        SELECT
                            r.email,
                            NULL USER_ID,
                            r.userData USER_DATA
                        FROM content_{ReceiverContentId}_united r {NoLock}
                        JOIN notifications n {NoLock} ON r.notification = n.notification_id
                        WHERE
                            (r.confirmed{On} OR n.confirmation_template_id IS NULL) AND
                            r.email IS NOT NULL AND
                            n.use_email_from_content{On} AND
                            n.category_attribute_id IS NULL AND
                            r.notification = {notificationId}";
                }
                else
                {
                    strSql = @$"
                        SELECT
                            receiver_r.email,
                            NULL USER_ID,
                            receiver_r.userData USER_DATA,
                            article.item_id
                        FROM content_{ReceiverContentId}_united receiver_r {NoLock}
                        JOIN notifications receiver_n {NoLock} ON
                            receiver_r.notification = receiver_n.notification_id
                        JOIN item_to_item receiver_iti {NoLock} ON
                            receiver_r.category = receiver_iti.link_id AND
                            receiver_r.content_item_id = receiver_iti.l_item_id AND
                            not receiver_iti.is_rev
                        JOIN content_item receiver_i {NoLock} ON
                            receiver_iti.r_item_id = receiver_i.content_item_id
                        JOIN content_attribute receiver_a {NoLock} ON
                            receiver_i.content_id = receiver_a.content_id AND
                            receiver_a.attribute_name = 'Category'
                        JOIN content_data receiver_d {NoLock} ON
                            receiver_d.attribute_id = receiver_a.attribute_id AND
                            receiver_d.content_item_id = receiver_i.content_item_id
                        JOIN (
                                SELECT
                                    article_n.notification_id,
                                    article_iti.l_item_id item_id,
                                    article_iti.r_item_id category_id
                                FROM item_to_item article_iti {NoLock}
                                JOIN content_attribute article_a {NoLock} ON article_iti.link_id = article_a.link_id
                                JOIN notifications article_n {NoLock} ON article_a.attribute_id = article_n.category_attribute_id
                                WHERE
                                    article_iti.l_item_id in ({ids}) AND
                                    article_n.use_email_from_content{On} AND
                                    article_n.category_attribute_id IS NOT NULL

                                UNION ALL

                                SELECT
                                    article_n.notification_id,
                                    article_d.content_item_id item_id,
                                    article_d.o2m_data category_id
                                FROM content_data article_d {NoLock}
                                JOIN notifications article_n {NoLock} ON article_n.category_attribute_id = article_d.attribute_id
                                WHERE
                                    article_d.content_item_id in ({ids}) AND
                                    article_n.use_email_from_content{On} AND
                                    article_n.category_attribute_id IS NOT NULL AND
                                    article_d.o2m_data IS NOT NULL
                            ) article ON
                            receiver_n.notification_id = article.notification_id AND
                            receiver_d.o2m_data = article.category_id
                        WHERE
                            (receiver_r.confirmed{On} OR receiver_n.confirmation_template_id IS NULL) AND
                            receiver_r.email IS NOT NULL AND
                            receiver_n.use_email_from_content{On} AND
                            eceiver_n.category_attribute_id IS NOT NULL AND
                            receiver_r.notification = {notificationId}";
                }
            }
            else
            {
                strSql = $"SELECT DISTINCT(U.EMAIL), U.USER_ID, NULL USER_DATA FROM content_item_status_history AS ch LEFT OUTER JOIN users AS u ON ch.user_id = u.user_id WHERE ch.content_item_id in ({ids})";
            }

            return GetCachedData(strSql);
        }

        private static string GetSqlRegisterNotificationsForUsers(DataTable toTable, int[] contentItemIds, int notificationId, string notificationOn)
        {
            var sb = new StringBuilder();
            sb.Append("INSERT INTO notifications_sent VALUES ");
            foreach (DataRow dr in toTable.Rows)
            {
                if (!ReferenceEquals(dr["EMAIL"], DBNull.Value))
                {
                    if (!ReferenceEquals(dr["USER_ID"], DBNull.Value))
                    {
                        foreach (int contentItemId in contentItemIds)
                        {
                            sb.Append($"({dr["USER_ID"]}, {notificationId}, {contentItemId}, DEFAULT, '{notificationOn.ToLower()}'),");
                        }
                    }
                }
            }

            return sb.ToString().TrimEnd(',');
        }

        private static string ConvertToString(object obj) => obj == DBNull.Value ? "" : obj.ToString();

        private static string ConvertToNullString(object obj) => ReferenceEquals(obj, DBNull.Value) ? "NULL" : obj.ToString();

        private MailAddress GetFromAddress(Notification notification)
        {
            MailAddress functionReturnValue;
            var fromName = notification.FromDefaultName ? DbConnectorSettings.MailFromName : notification.FromUserName;
            var from = notification.FromBackendUser ? notification.FromBackendUserEmail : notification.FromUserEmail;

            if (string.IsNullOrWhiteSpace(from))
            {
                throw new Exception("Mail sender is not defined");
            }
            else
            {
                functionReturnValue = !string.IsNullOrWhiteSpace(fromName) ? new MailAddress(from, fromName) : new MailAddress(from);
            }

            return functionReturnValue;
        }

        /// <summary>
        /// TODO: refactior calls to GetFromAddress(Notification notification)
        /// </summary>
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
                if (FileSystem.FileExists(fileName))
                {
                    using var stream = FileSystem.LoadStream(fileName);
                    mailMess.Attachments.Add(new Attachment(stream, fileName));
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

        private static void SetToMail(MailMessage mailMess, DataTable toTable, bool hideRecipients)
        {
            foreach (DataRow dr in toTable.Rows)
            {
                SetToMail(mailMess, ConvertToString(dr["EMAIL"]), hideRecipients);
            }
        }

        private static void SetToMail(MailMessage mailMess, string allEmails, bool hideRecipients)
        {
            var emails = allEmails.Split(';');
            foreach (string email in emails.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                if (hideRecipients)
                {
                    mailMess.Bcc.Add(new MailAddress(email));
                }
                else
                {
                    mailMess.To.Add(new MailAddress(email));
                }
            }
        }

        private void SetToMail(
            DataRow notifyRow,
            int[] contentItemIds,
            string notificationOn,
            string notificationEmail,
            MailMessage mailMess,
            ref string strSqlRegisterNotificationsForUsers)
        {
            int notificationId = GetNumInt(notifyRow["NOTIFICATION_ID"]);
            bool hideRecipients = (bool)notifyRow["HIDE_RECIPIENTS"];
            if (!string.IsNullOrWhiteSpace(notificationEmail))
            {
                SetToMail(mailMess, notificationEmail, hideRecipients);
            }
            else
            {
                var toTable = GetRecipientTable(notifyRow, contentItemIds, notificationId);
                SetToMail(mailMess, toTable, hideRecipients);
                strSqlRegisterNotificationsForUsers = GetSqlRegisterNotificationsForUsers(toTable, contentItemIds, notificationId, notificationOn);
            }
        }

        private int? ReceiverContentId => GetSettingByName<int?>(NotificationReceiverContentId);

        private int GetReceiverCategoryContentId()
        {
            var query = @$"
                select r.r_content_id contentid
                from content_attribute a
                join content_to_content r on a.link_id = r.link_id
                where a.content_id = {ReceiverContentId} and a.attribute_name = 'Category'";

            var table = GetCachedData(query);

            return GetNumInt(table.Rows[0]["contentid"]);
        }


        private int GetNotificationCategoryContentId()
        {
            var query = @$"
                select a3.content_id contentid
                from content_attribute a
                join content_to_content r on a.link_id = r.link_id
                join content_attribute a2 on r.r_content_id = a2.content_id and a2.attribute_name = 'Category'
                join content_attribute a3 on a2.related_attribute_id = a3.attribute_id
                where a.content_id = {ReceiverContentId} and a.attribute_name = 'Category'";

            var table = GetCachedData(query);

            return GetNumInt(table.Rows[0]["contentid"]);
        }

        private bool UseEmailFromContent(DataRow notification) => (bool)notification["USE_EMAIL_FROM_CONTENT"];

        private bool EmailFromContentIsNotAvaliable(DataRow notification) => UseEmailFromContent(notification) && !ReceiverContentId.HasValue;
    }
}
