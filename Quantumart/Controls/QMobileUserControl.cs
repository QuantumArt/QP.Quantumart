using System;
using System.Collections;
using System.Data;
using Quantumart.QPublishing.Database;
using Quantumart.QPublishing.Helpers;
using Quantumart.QPublishing.Pages;

// ReSharper disable once CheckNamespace
namespace Quantumart.QPublishing.Controls
{
    public class QMobileUserControl : QMobileUserControlBase, IQUserControl
    {
        public QMobileUserControl()
        {
            QUserControlEssential = new QUserControlEssential(this);
        }

        public QUserControlEssential QUserControlEssential { get; set; }

        public bool DisableDataBind
        {
            get => QUserControlEssential.DisableDataBind;
            set => QUserControlEssential.DisableDataBind = value;
        }

        public bool UseSimpleInitOrder
        {
            get => QUserControlEssential.UseSimpleInitOrder;
            set => QUserControlEssential.UseSimpleInitOrder = value;
        }

        public DateTime TraceStartTime
        {
            get => QUserControlEssential.TraceStartTime;
            set => QUserControlEssential.TraceStartTime = value;
        }

        public string TraceObjectString
        {
            get => QUserControlEssential.TraceObjectString;
            set => QUserControlEssential.TraceObjectString = value;
        }

        public string UndefTraceString
        {
            get => QUserControlEssential.UndefTraceString;
            set => QUserControlEssential.UndefTraceString = value;
        }

        public Hashtable QpDefValues
        {
            get => QUserControlEssential.QpDefValues;
            set => QUserControlEssential.QpDefValues = value;
        }

        public void AddQpDefValue(string key, object value)
        {
            QUserControlEssential.AddQpDefValue(key, value);
        }

        public string QpDefValue(string key) => QUserControlEssential.QpDefValue(key);

        public int FormatId
        {
            get => QUserControlEssential.FormatId;
            set => QUserControlEssential.FormatId = value;
        }

        public bool OnInitFired
        {
            get => QUserControlEssential.OnInitFired;
            set => QUserControlEssential.OnInitFired = value;
        }

        public QpTrace QPTrace
        {
            get => QPage.QPTrace;
            set => QPage.QPTrace = value;
        }

        public virtual void OnLoadControl(object sender, ref string objectCallName)
        {
            QUserControlEssential.OnLoadControl(sender, ref objectCallName);
        }

        public bool IsTest => QPage.IsTest;

        public bool IsStage => QPage.IsStage;

        // ReSharper disable once InconsistentNaming
        public bool QP_IsInStageMode => IsStage;

        public IQPage QPage
        {
            get => (QMobilePage)Page;
            set => Page = (QMobilePage)value;
        }

        public void ShowObject(string name)
        {
            QPage.ShowObject(name);
        }

        public void ShowObject(string name, object sender)
        {
            QPage.ShowObject(name, sender);
        }

        public void ShowObject(string name, object sender, object[] parameters)
        {
            QPage.ShowObject(name, sender, parameters);
        }

        public void ShowObjectSimple(string name)
        {
            QPage.ShowObjectSimple(name);
        }

        public void ShowObjectSimple(string name, object sender)
        {
            QPage.ShowObjectSimple(name, sender);
        }

        public void ShowObjectSimple(string name, object sender, object[] parameters)
        {
            QPage.ShowObjectSimple(name, sender, parameters);
        }

        public void ShowObjectNS(string name)
        {
            ShowObject(name);
        }

        public void ShowObjectNS(string name, object sender)
        {
            ShowObject(name, sender);
        }

        public void ShowObjectNS(string name, object sender, object[] parameters)
        {
            ShowObject(name, sender, parameters);
        }

        public virtual void ShowControl(string name)
        {
            QPage.ShowControl(name);
        }

        public virtual void ShowControl(string name, object sender)
        {
            QPage.ShowControl(name, sender);
        }

        public virtual void ShowControl(string name, object sender, object[] parameters)
        {
            QPage.ShowControl(name, sender, parameters);
        }

        public void ShowControlNS(string name)
        {
            ShowControl(name);
        }

        public void ShowControlNS(string name, object sender)
        {
            ShowControl(name, sender);
        }

        public void ShowControlNS(string name, object sender, object[] parameters)
        {
            ShowControl(name, sender, parameters);
        }

        public DataTable GetContentData(string siteName, string contentName, string whereExpression, string orderExpression, long startRow, long pageSize, ref long totalRecords, byte useSchedule, string statusName, byte showSplittedArticle, byte includeArchive) => QPage.GetContentData(siteName, contentName, whereExpression, orderExpression, startRow, pageSize, ref totalRecords, useSchedule, statusName, showSplittedArticle, includeArchive);

        public DBConnector Cnn => QPage.Cnn;

        public DataTable GetData(string queryString) => QPage.Cnn.GetData(queryString);

        public int site_id => QPage.site_id;

        public string site_url => QPage.site_url;

        public string absolute_site_url => QPage.absolute_site_url;

        public int page_id => QPage.page_id;

        public string upload_url => QPage.upload_url;

        public bool AbsUploadURL => QPage.AbsUploadURL;

        public string UploadURLPrefix => QPage.UploadURLPrefix;

        public void AddValue(string key, object value)
        {
            QPage.AddValue(key, value);
        }

        public void AddObjectValue(string key, object value)
        {
            QPage.AddObjectValue(key, value);
        }

        public string Value(string key) => QPage.Value(key);

        public string Value(string key, string defaultValue) => QPage.Value(key, defaultValue);

        public long NumValue(string key) => QPage.NumValue(key);

        public string StrValue(string key) => QPage.StrValue(key);

        public string InternalStrValue(string valueName) => QPage.InternalStrValue(valueName);

        public Hashtable Values => QPage.Values;

        public string DirtyValue(string key) => QPage.DirtyValue(key);

        public void RemoveContentItem(int contentItemId)
        {
            QPage.RemoveContentItem(contentItemId);
        }

        public void DeleteContentItem()
        {
            QPage.DeleteContentItem();
        }

        public int GetContentID(string contentName) => QPage.GetContentID(contentName);

        public string FieldName(string contentName, string fieldName) => QPage.FieldName(contentName, fieldName);

        public int FieldID(string contentName, string fieldName) => QPage.FieldID(contentName, fieldName);

        public bool CheckMaxLength(string str, int maxlength) => QPage.CheckMaxLength(str, maxlength);

        public string ReplaceHTML(string str) => QPage.ReplaceHTML(str);

        public void SendNotification(string notificationOn, int contentItemId, string notificationEmail)
        {
            QPage.SendNotification(notificationOn, contentItemId, notificationEmail);
        }

        public string GetSiteUrl() => QPage.GetSiteUrl();

        public string GetActualSiteUrl() => QPage.GetActualSiteUrl();

        public string GetContentItemLinkIDs(string linkFieldName, long itemId) => QPage.GetContentItemLinkIDs(linkFieldName, itemId);

        public string GetContentItemLinkIDs(string linkFieldName, string itemId) => QPage.GetContentItemLinkIDs(linkFieldName, itemId);

        public string GetContentItemLinkQuery(string linkFieldName, long itemId) => QPage.GetContentItemLinkQuery(linkFieldName, itemId);

        public string GetContentItemLinkQuery(string linkFieldName, string itemId) => QPage.GetContentItemLinkQuery(linkFieldName, itemId);

        public string GetLinkIDs(string linkFieldName) => QPage.GetLinkIDs(linkFieldName);

        public int GetLinkIDForItem(string linkFieldName, int itemId) => QPage.GetLinkIDForItem(linkFieldName, itemId);

        public string GetContentFieldValue(int itemId, string fieldName) => QPage.GetContentFieldValue(itemId, fieldName);

        public int AddFormToContentWithoutNotification(string contentName, string statusName, int contentItemId) => QPage.AddFormToContentWithoutNotification(contentName, statusName, contentItemId);

        public int AddFormToContentWithoutNotification(string contentName, string statusName) => AddFormToContentWithoutNotification(contentName, statusName, 0);

        public int AddFormToContent(string contentName, string statusName, int contentItemId) => QPage.AddFormToContent(contentName, statusName, contentItemId);

        public int AddFormToContent(string contentName, string statusName) => AddFormToContent(contentName, statusName, 0);

        public void UpdateContentItemField(string contentName, string fieldName, int contentItemId)
        {
            QPage.UpdateContentItemField(contentName, fieldName, contentItemId);
        }

        public void UpdateContentItemField(string contentName, string fieldName, int contentItemId, bool withNotification)
        {
            QPage.UpdateContentItemField(contentName, fieldName, contentItemId, withNotification);
        }

        public void UpdateContentItem()
        {
            QPage.UpdateContentItem(true, "");
        }

        public void UpdateContentItemWithoutNotification()
        {
            QPage.UpdateContentItemWithoutNotification(false, "");
        }

        public void UpdateContentItem(bool updateEmpty, string statusName)
        {
            QPage.UpdateContentItem(updateEmpty, statusName);
        }

        public void UpdateContentItemWithoutNotification(bool updateEmpty, string statusName)
        {
            QPage.UpdateContentItemWithoutNotification(updateEmpty, statusName);
        }

        public string GetContentUploadUrl(string contentName) => QPage.GetContentUploadUrl(contentName);

        public string GetContentUploadUrlByID(int contentId) => QPage.GetContentUploadUrlByID(contentId);

        public string GetContentName(int contentId) => QPage.GetContentName(contentId);

        public Hashtable FieldValuesDictionary
        {
            get => QPage.FieldValuesDictionary;
            set => QPage.FieldValuesDictionary = value;
        }

        public Hashtable FieldNamesDictionary
        {
            get => QPage.FieldNamesDictionary;
            set => QPage.FieldNamesDictionary = value;
        }

        public int published_status_type_id => QPage.published_status_type_id;

        public string published_status_name => QPage.published_status_name;

        public virtual void LoadControlData(object sender, EventArgs e)
        {
            QUserControlEssential.LoadControlData(sender, e);
        }

        public virtual void InitUserHandlers(EventArgs e)
        {
            QUserControlEssential.InitUserHandlers(e);
        }

        public void SimulateOnInit(EventArgs e)
        {
            OnInit(e);
        }

        protected override void OnInit(EventArgs e)
        {
            if (UseSimpleInitOrder)
            {
                base.OnInit(e);
                LoadControlData(this, new EventArgs());
                InitUserHandlers(new EventArgs());
            }
            else
            {
                if (!OnInitFired)
                {
                    base.OnInit(e);

                    //if traceString doesn't end with "-", that means it's PartialCachingControl
                    var isCached = !TraceObjectString.EndsWith("-");

                    LoadControlData(this, new EventArgs());

                    //saving trace for object if it's not PartialCachingControl
                    if (QPTrace != null && FormatId != 0 && !isCached)
                    {
                        QUserControlEssential.TraceObject(QPage);
                        QPTrace.TraceString = QPTrace.TraceString + TraceObjectString;
                    }

                    InitUserHandlers(new EventArgs());
                    if (!DisableDataBind)
                    {
                        DataBind();
                    }
                    OnInitFired = true;
                }
            }
        }

        public override void DataBind()
        {
            if (UseSimpleInitOrder)
            {
                base.DataBind();
            }
            else
            {
                if (!ChildControlsCreated)
                {
                    base.DataBind();
                    ChildControlsCreated = true;
                }
            }
        }

        public string GetObjectFullName(string templateNetName, string objectNetName, string formatNetName) => QUserControlEssential.GetObjectFullName(templateNetName, objectNetName, formatNetName);

        public string TraceString
        {
            get => QPage.QPTrace.TraceString;
            set => QPage.QPTrace.TraceString = value;
        }

        public string TraceStartText
        {
            get => QPage.QPTrace.TraceStartText;
            set => QPage.QPTrace.TraceStartText = value;
        }

        public virtual string Field(string key) => QPage.Field(key);

        public virtual string Field(DataRowView pDataItem, string key) => QUserControlEssential.Field(QPage.IsStage, pDataItem.Row, key, "");

        public virtual string Field(DataRowView pDataItem, string key, string defaultValue) => QUserControlEssential.Field(QPage.IsStage, pDataItem.Row, key, defaultValue);

        public virtual string Field(DataRow pDataItem, string key) => QUserControlEssential.Field(QPage.IsStage, pDataItem, key, "");

        public virtual string Field(DataRow pDataItem, string key, string defaultValue) => QUserControlEssential.Field(QPage.IsStage, pDataItem, key, defaultValue);

        public string FormatField(string field) => QUserControlEssential.FormatField(field);

        public virtual string FieldNS(string key) => QPage.Field(key);

        public virtual string FieldNS(DataRowView pDataItem, string key) => QUserControlEssential.FieldNs(pDataItem.Row, key, "");

        public virtual string FieldNS(DataRowView pDataItem, string key, string defaultValue) => QUserControlEssential.FieldNs(pDataItem.Row, key, defaultValue);

        public virtual string FieldNS(DataRow pDataItem, string key) => QUserControlEssential.FieldNs(pDataItem, key, "");

        public virtual string FieldNS(DataRow pDataItem, string key, string defaultValue) => QUserControlEssential.FieldNs(pDataItem, key, defaultValue);

        public string OnFly(DataRowView pDataItem, string key) => QUserControlEssential.OnFly(pDataItem.Row, key, "");

        public string OnFly(DataRowView pDataItem, string key, string defaultValue) => QUserControlEssential.OnFly(pDataItem.Row, key, defaultValue);

        public string OnFly(DataRow pDataItem, string key) => QUserControlEssential.OnFly(pDataItem, key, "");

        public string OnFly(DataRow pDataItem, string key, string defaultValue) => QUserControlEssential.OnFly(pDataItem, key, defaultValue);

        public string OnFlyExec(DataRow pDataItem, string key, string defaultValue) => QUserControlEssential.OnFly(pDataItem, key, defaultValue);

        public string OnFlyExec(DataRowView pDataItem, string key, string defaultValue) => QUserControlEssential.OnFly(pDataItem.Row, key, defaultValue);

        public string OnStage(string value, string itemId) => QUserControlEssential.OnStage(value, itemId);

        public string OnStage(string value, int itemId) => QUserControlEssential.OnStage(value, itemId);

        public string OnScreen(string value, int itemId) => QUserControlEssential.OnScreen(value, itemId);

        public string OnScreen(string value, string itemId) => QUserControlEssential.OnScreen(value, itemId);

        public string OnScreenFlyEdit(string value, int itemId, string fieldName) => QUserControlEssential.OnScreenFlyEdit(value, itemId, fieldName);

        public string OnScreenFlyEdit(string value, string itemId, string fieldName) => QUserControlEssential.OnScreenFlyEdit(value, itemId, fieldName);

        public string OnStageFlyEdit(string value, string itemId, string fieldName) => QUserControlEssential.OnStageFlyEdit(value, itemId, fieldName);

        public string OnStageFlyEdit(string value, int itemId, string fieldName) => QUserControlEssential.OnStageFlyEdit(value, itemId, fieldName);

        public string OnStageDiv(string value, string fieldName, int itemId, int contentId, string isBorderStatic, bool editable, string attrType, int attrRequired) => QUserControlEssential.OnStageDiv(value, fieldName, itemId, contentId, isBorderStatic, editable, attrType, attrRequired);

        public string GetReturnStageURL() => QUserControlEssential.GetReturnStageUrl();

        public string GetFieldUploadUrl(string contentName, string fieldName) => QPage.GetFieldUploadUrl(fieldName, GetContentID(contentName));
    }
}
