using System;
using System.Collections;
using System.Data;
using System.Text;
using System.Web;
using System.Web.UI.MobileControls;
using Quantumart.Helpers;
using Quantumart.QPublishing.Database;
using Quantumart.QPublishing.Helpers;
using Quantumart.QPublishing.OnScreen;

// ReSharper disable once CheckNamespace
namespace Quantumart.QPublishing.Pages
{
#pragma warning disable 618
    public class QMobilePage : MobilePage, IQPage
#pragma warning restore 618
    {
        public QMobilePage(DBConnector dbConnector)
        {
            QPageEssential = new QPageEssential(this, dbConnector);
        }

        public QPageEssential QPageEssential { get; set; }

        public bool UseMultiSiteLogic
        {
            get => QPageEssential.UseMultiSiteLogic;
            set => QPageEssential.UseMultiSiteLogic = value;
        }

        public bool IsLocalAssembling
        {
            get => QPageEssential.IsLocalAssembling;
            set => QPageEssential.IsLocalAssembling = value;
        }

        public string URLToSave
        {
            get => QPageEssential.UrlToSave;
            set => QPageEssential.UrlToSave = value;
        }

        public string PageFolder
        {
            get => QPageEssential.PageFolder;
            set => QPageEssential.PageFolder = value;
        }

        public QScreen QScreen
        {
            get => QPageEssential.QScreen;
            set => QPageEssential.QScreen = value;
        }

        public QpTrace QPTrace
        {
            get => QPageEssential.QpTrace;
            set => QPageEssential.QpTrace = value;
        }

        public string Controls_Folder
        {
            get => QPageEssential.PageControlsFolder;
            set => QPageEssential.PageControlsFolder = value;
        }

        public string ControlsFolderName => QPageEssential.TemplateControlsFolderPrefix;

        public string TemplateNetName
        {
            get => QPageEssential.TemplateNetName;
            set => QPageEssential.TemplateNetName = value;
        }

        public string TemplateName
        {
            get => QPageEssential.TemplateName;
            set => QPageEssential.TemplateName = value;
        }

        public bool IsPreview
        {
            get => QPageEssential.IsPreview;
            set => QPageEssential.IsPreview = value;
        }

        public int Expires
        {
            get => QPageEssential.Expires;
            set => QPageEssential.Expires = value;
        }

        public DateTime LastModified
        {
            get => QPageEssential.LastModified;
            set => QPageEssential.LastModified = value;
        }

        public bool IsLastModifiedDynamic
        {
            get => QPageEssential.IsLastModifiedDynamic;
            set => QPageEssential.IsLastModifiedDynamic = value;
        }

        public bool GenerateTrace
        {
            get => QPageEssential.GenerateTrace;
            set => QPageEssential.GenerateTrace = value;
        }

        public HttpCacheability HttpCacheability
        {
            get => QPageEssential.HttpCacheability;
            set => QPageEssential.HttpCacheability = value;
        }

        public HttpCacheRevalidation HttpCacheRevalidation
        {
            get => QPageEssential.HttpCacheRevalidation;
            set => QPageEssential.HttpCacheRevalidation = value;
        }

        public TimeSpan ProxyMaxAge
        {
            get => QPageEssential.ProxyMaxAge;
            set => QPageEssential.ProxyMaxAge = value;
        }

        public string CharSet
        {
            get => QPageEssential.CharSet;
            set => QPageEssential.CharSet = value;
        }

        public Encoding ContentEncoding
        {
            get => QPageEssential.ContentEncoding;
            set => QPageEssential.ContentEncoding = value;
        }

        public bool IsTest
        {
            get => QPageEssential.IsTest;
            set => QPageEssential.IsTest = value;
        }

        public bool IsStage
        {
            get => QPageEssential.IsStage;
            set => QPageEssential.IsStage = value;
        }

        public bool QP_IsInStageMode
        {
            get => QPageEssential.IsStage;
            set => QPageEssential.IsStage = value;
        }

        public void SetLastModified(DataTable dt)
        {
            QPageEssential.SetLastModified(dt);
        }

        public DataTable GetContentData(string siteName, string contentName, string whereExpression, string orderExpression, long startRow, long pageSize, out long totalRecords, byte useSchedule, string statusName, byte showSplittedArticle, byte includeArchive) =>
            QPageEssential.GetContentData(siteName, contentName, whereExpression, orderExpression, startRow, pageSize, out totalRecords, useSchedule, statusName, showSplittedArticle, includeArchive);

        public DataTable GetContentDataWithSecurity(string siteName, string contentName, string whereExpression, string orderExpression, long startRow, long pageSize, out long totalRecords, byte useSchedule, string statusName, byte showSplittedArticle, byte includeArchive, long lngUserId, long lngGroupId, int intStartLevel, int intEndLevel) =>
            QPageEssential.GetContentDataWithSecurity(siteName, contentName, whereExpression, orderExpression, startRow, pageSize, out totalRecords, useSchedule, statusName, showSplittedArticle, includeArchive, lngUserId, lngGroupId, intStartLevel, intEndLevel);

        public DataTable GetContentDataWithSecurity(string siteName, string contentName, string whereExpression, string orderExpression, long startRow, long pageSize, out long totalRecords, byte useSchedule, string statusName, byte showSplittedArticle, byte includeArchive, long lngUserId, long lngGroupId, int intStartLevel, int intEndLevel, bool blnFilterRecords) =>
            QPageEssential.GetContentDataWithSecurity(siteName, contentName, whereExpression, orderExpression, startRow, pageSize, out totalRecords, useSchedule, statusName, showSplittedArticle, includeArchive, lngUserId, lngGroupId, intStartLevel, intEndLevel, blnFilterRecords);

        #region "Functions for loading controls"

        public virtual void ShowObject(string name, object sender, object[] parameters, IQPage qPage)
        {
            QPageEssential.ShowObject(name, sender, parameters, qPage, false);
        }

        public void ShowObject(string name, object sender, object[] parameters)
        {
            ShowObject(name, sender, parameters, this);
        }

        public void ShowObject(string name, object sender)
        {
            ShowObject(name, sender, null, this);
        }

        public void ShowObject(string name)
        {
            ShowObject(name, this, null, this);
        }

        public virtual void ShowObjectSimple(string name, object sender, object[] parameters, IQPage qPage)
        {
            QPageEssential.ShowObject(name, sender, parameters, qPage, true);
        }

        public void ShowObjectSimple(string name, object sender, object[] parameters)
        {
            ShowObjectSimple(name, sender, parameters, this);
        }

        public void ShowObjectSimple(string name, object sender)
        {
            ShowObjectSimple(name, sender, null, this);
        }

        public void ShowObjectSimple(string name)
        {
            ShowObjectSimple(name, this, null, this);
        }

        public void ShowControl(string name, object sender, object[] parameters)
        {
            QPageEssential.ShowControl(name, sender, parameters);
        }

        public void ShowControl(string name, object sender)
        {
            QPageEssential.ShowControl(name, sender, null);
        }

        public void ShowControl(string name)
        {
            QPageEssential.ShowControl(name, this, null);
        }

        public void ShowTemplateControl(string name)
        {
            QPageEssential.ShowTemplateControl(name, this);
        }

        #endregion

        public DBConnector DbConnector => QPageEssential.DbConnector;

        public int site_id
        {
            get => QPageEssential.site_id;
            set => QPageEssential.site_id = value;
        }

        public int page_id
        {
            get => QPageEssential.page_id;
            set => QPageEssential.page_id = value;
        }

        public int page_template_id
        {
            get => QPageEssential.page_template_id;
            set => QPageEssential.page_template_id = value;
        }

        public Mode PageAssembleMode
        {
            get => QPageEssential.PageAssembleMode;
            set => QPageEssential.PageAssembleMode = value;
        }

        public Hashtable FieldValuesDictionary
        {
            get => QPageEssential.FieldValuesDictionary;
            set => QPageEssential.FieldValuesDictionary = value;
        }

        public Hashtable FieldNamesDictionary
        {
            get => QPageEssential.FieldNamesDictionary;
            set => QPageEssential.FieldNamesDictionary = value;
        }

        public string upload_url
        {
            get => QPageEssential.UploadUrl;
            set => QPageEssential.UploadUrl = value;
        }

        public string site_url
        {
            get => QPageEssential.SiteUrl;
            set => QPageEssential.SiteUrl = value;
        }

        public string absolute_site_url
        {
            get => QPageEssential.AbsoluteSiteUrl;
            set => QPageEssential.AbsoluteSiteUrl = value;
        }

        public bool AbsUploadURL
        {
            get => QPageEssential.AbsUploadUrl;
            set => QPageEssential.AbsUploadUrl = value;
        }

        public string UploadURLPrefix
        {
            get => QPageEssential.UploadUrlPrefix;
            set => QPageEssential.UploadUrlPrefix = value;
        }

        public int published_status_type_id
        {
            get => QPageEssential.PublishedStatusTypeId;
            set => QPageEssential.PublishedStatusTypeId = value;
        }

        public string published_status_name
        {
            get => QPageEssential.PublishedStatusName;
            set => QPageEssential.PublishedStatusName = value;
        }

        public void Initialize()
        {
            QPageEssential.HandleInit(this);
        }

        public void HandlePreRender()
        {
            QPageEssential.HandlePreRender();
        }

        public void HandleRender()
        {
            QPageEssential.HandleRender();
        }

        public void Initialize(int siteId, int pageId, int pageTemplateId, string uploadUrl, string uploadUrlPrefix, string siteUrl, string pageFileName, string pageFolder)
        {
            QPageEssential.Initialize(siteId, pageId, pageTemplateId, uploadUrl, uploadUrlPrefix, siteUrl, pageFileName, pageFolder);
        }

        public void Initialize(int siteId, int pageId, int pageTemplateId, string pageFileName, string pageFolder)
        {
            QPageEssential.Initialize(siteId, pageId, pageTemplateId, pageFileName, pageFolder);
        }

        public void Initialize(int siteId)
        {
            QPageEssential.Initialize(siteId);
        }

        public void Initialize(int siteId, string uploadUrl, string siteUrl, string pageFileName, string templateNetName)
        {
            QPageEssential.Initialize(siteId, uploadUrl, siteUrl, pageFileName, TemplateNetName);
        }

        public void Initialize(int siteId, string uploadUrl, string siteUrl, string pageFileName, string templateNetName, string pageFolder, Hashtable pageObjects)
        {
            QPageEssential.Initialize(siteId, uploadUrl, siteUrl, pageFileName, templateNetName, pageFolder);
        }

        public void Initialize(int siteId, string uploadUrl, string siteUrl, string pageFileName, string templateNetName, string pageFolder, Hashtable pageObjects, Hashtable templates)
        {
            QPageEssential.Initialize(siteId, uploadUrl, siteUrl, pageFileName, templateNetName, pageFolder);
        }

        public void AddLastModifiedHeader()
        {
            AddLastModifiedHeader(LastModified);
        }

        public void AddLastModifiedHeader(DateTime dt)
        {
            Response.Cache.SetLastModified(dt);
        }

        public void EnableBackendOnScreen()
        {
            QPageEssential.EnableBackendOnScreen(this, site_id);
        }

        public void FillValues()
        {
            QPageEssential.FillValues();
        }

        public void AddValue(string key, object value)
        {
            QPageEssential.AddValue(key, value);
        }

        public void AddObjectValue(string key, object value)
        {
            QPageEssential.AddObjectValue(key, value);
        }

        public string DirtyValue(string key) => QPageEssential.DirtyValue(key);

        public string Value(string key) => QPageEssential.Value(key);

        public string Value(string key, string defaultValue) => QPageEssential.Value(key, defaultValue);

        public long NumValue(string key) => QPageEssential.NumValue(key);

        public string StrValue(string key) => QPageEssential.StrValue(key);

        public string InternalStrValue(string valueName) => QPageEssential.InternalStrValue(valueName);

        public Hashtable Values => QPageEssential.Values;

        public void CallStackOverflow()
        {
            QPageEssential.CallStackOverflow();
        }

        public bool IsOrderSqlValid(string orderSql) => QPageEssential.IsOrderSqlValid(orderSql);

        public string CleanSQL(string text) => Utils.CleanSql(text);

        public string Field(string key) => QPageEssential.Field(key);

        public void AddHeader(string key, string value)
        {
            QPageEssential.AddHeader(key, value);
        }

        public void SaveURL(string siteId)
        {
            QPageEssential.SaveUrl(siteId);
        }

        public string GetSiteDNS(string siteId) => QPageEssential.GetSiteDns(siteId);

        public string GetInternalCall(string userCall) => QPageEssential.GetInternalCall(userCall);

        public virtual void BeforeFirstCallInitialize()
        {
        }

        public string GetControlUrl(string controlFileName) => QPageEssential.GetControlUrl(controlFileName);

        #region "form.inc"

        public void RemoveContentItem(int contentItemId)
        {
            QPageEssential.RemoveContentItem(contentItemId);
        }

        public void DeleteContentItem()
        {
            QPageEssential.DeleteContentItem();
        }

        public int GetContentID(string contentName) => QPageEssential.GetContentId(contentName);

        public int GetContentVirtualType(int contentId) => QPageEssential.GetContentVirtualType(contentId);

        public string FieldName(string contentName, string fieldName) => QPageEssential.FieldName(contentName, fieldName);

        public int FieldID(string contentName, string fieldName) => QPageEssential.FieldId(contentName, fieldName);

        public string InputName(string contentName, string fieldName) => QPageEssential.InputName(contentName, fieldName);

        public bool CheckMaxLength(string str, int maxlength) => QPageEssential.CheckMaxLength(str, maxlength);

        public string ReplaceHTML(string str) => QPageEssential.ReplaceHtml(str);

        public void SendNotification(string notificationOn, int contentItemId, string notificationEmail)
        {
            QPageEssential.SendNotification(notificationOn, contentItemId, notificationEmail);
        }

        public string GetSiteUrl() => QPageEssential.GetSiteUrl();

        public string GetActualSiteUrl() => QPageEssential.GetActualSiteUrl();

        public string GetContentItemLinkIDs(string linkFieldName, long itemId) => QPageEssential.GetContentItemLinkIDs(linkFieldName, itemId);

        public string GetContentItemLinkIDs(string linkFieldName, string itemId) => QPageEssential.GetContentItemLinkIDs(linkFieldName, itemId);

        public string GetContentItemLinkQuery(string linkFieldName, long itemId) => QPageEssential.GetContentItemLinkQuery(linkFieldName, itemId);

        public string GetContentItemLinkQuery(string linkFieldName, string itemId) => QPageEssential.GetContentItemLinkQuery(linkFieldName, itemId);

        public string GetLinkIDs(string linkFieldName) => QPageEssential.GetLinkIDs(linkFieldName);

        public int GetLinkIDForItem(string linkFieldName, int itemId) => QPageEssential.GetLinkIdForItem(linkFieldName, itemId);

        public string GetContentFieldValue(int itemId, string fieldName) => QPageEssential.GetContentFieldValue(itemId, fieldName);

        public int AddFormToContentWithoutNotification(string contentName, string statusName, int contentItemId) => QPageEssential.AddFormToContentWithoutNotification(contentName, statusName, contentItemId);

        public int AddFormToContent(string contentName, string statusName, int contentItemId) => QPageEssential.AddFormToContent(contentName, statusName, contentItemId);

        public int AddFormToContentWithoutNotification(string contentName, string statusName) => AddFormToContentWithoutNotification(contentName, statusName, 0);

        public int AddFormToContent(string contentName, string statusName) => AddFormToContent(contentName, statusName, 0);

        public void UpdateContentItemField(string contentName, string fieldName, int contentItemId)
        {
            QPageEssential.UpdateContentItemField(contentName, fieldName, contentItemId);
        }

        public void UpdateContentItemField(string contentName, string fieldName, int contentItemId, bool withNotification)
        {
            QPageEssential.UpdateContentItemField(contentName, fieldName, contentItemId, withNotification);
        }

        public void UpdateContentItem()
        {
            QPageEssential.UpdateContentItem(true, "");
        }

        public void UpdateContentItemWithoutNotification()
        {
            QPageEssential.UpdateContentItemWithoutNotification(false, "");
        }

        public void UpdateContentItem(bool updateEmpty, string statusName)
        {
            QPageEssential.UpdateContentItem(updateEmpty, statusName);
        }

        public void UpdateContentItemWithoutNotification(bool updateEmpty, string statusName)
        {
            QPageEssential.UpdateContentItemWithoutNotification(updateEmpty, statusName);
        }

        public string GetContentUploadUrl(string contentName) => QPageEssential.GetContentUploadUrl(contentName);

        public string GetContentUploadUrlByID(int contentId) => QPageEssential.GetContentUploadUrlById(contentId);

        public string GetContentName(int contentId) => QPageEssential.GetContentName(contentId);

        public string GetFieldUploadUrl(string fieldName, int contentId) => QPageEssential.GetFieldUploadUrl(fieldName, contentId);

        #endregion

        public DataTable GetUsersByItemID_And_Permission(int itemId, int permissionLevel) => QPageEssential.GetUsersByItemID_And_Permission(itemId, permissionLevel);
    }
}
