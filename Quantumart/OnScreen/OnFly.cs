#if ASPNETCORE || NET4
using System;
using System.Data;
using System.Globalization;
using Quantumart.QPublishing.Database;
using Quantumart.QPublishing.Helpers;
using Quantumart.QPublishing.Info;
#if ASPNETCORE
using System.Net;
using Microsoft.AspNetCore.Http;
#else
using System.Web;

#endif

// ReSharper disable once CheckNamespace
namespace Quantumart.QPublishing.OnScreen
{
    public class OnFly
    {
        private readonly DBConnector _dbConnector;

        public OnFly(DBConnector dbConnector)
        {
            _dbConnector = dbConnector;
        }

        private long GetUid()
        {
#if ASPNETCORE
            var obj = _dbConnector.HttpContext.Session.GetString("UID");
#else
            var obj = HttpContext.Current.Session["UID"]?.ToString();
#endif
            if (obj != null && long.TryParse(obj, out var result))
            {
                return result;
            }

            return 0;
        }

        private string UnauthorizedMessage() => new TranslateManager(_dbConnector).Translate("You are not authorized as QP7 user");

        public string DecreaseStatus(int itemId)
        {
            string functionReturnValue;
            var uid = (int)GetUid();
            if (uid == 0)
            {
                return UnauthorizedMessage();
            }

            var sql = $" SELECT c.site_id, i.content_id, i.status_type_id, st.weight FROM content_item AS i LEFT OUTER JOIN content AS c ON c.content_id = i.content_id LEFT OUTER JOIN status_type AS st ON st.status_type_id= i.status_type_id WHERE i.content_item_id = {itemId}";
            var dt = _dbConnector.GetRealData(sql);
            if (dt.Rows.Count > 0)
            {
                var siteId = DBConnector.GetNumInt(dt.Rows[0]["site_id"]);
                var currentStatusWeight = DBConnector.GetNumInt(dt.Rows[0]["weight"]);
                var decrease = new Workflow(_dbConnector).DbWillContentItemStatusBeDecreased(siteId, itemId, uid, currentStatusWeight, out _, out _);
                functionReturnValue = decrease ? new TranslateManager(_dbConnector).Translate(@"While article updating status will be decreased. Click ""OK"" to proceed") : "0";
            }
            else
            {
                functionReturnValue = "0";
            }

            return functionReturnValue;
        }

        public string UpdateArticle(int itemId, string attrName, string uploadUrl, string siteUrl, string attrValue)
        {
            var uid = (int)GetUid();
            if (uid == 0)
            {
                return UnauthorizedMessage();
            }

            if (new QScreen(_dbConnector).DbGetUserAccess("content_item", itemId, uid) < 3)
            {
                return new TranslateManager(_dbConnector).Translate("You have no enough permissions to update");
            }

            var lockUid = GetLockRecordUid(itemId);
            if (lockUid != 0 && lockUid != uid)
            {
                return string.Format(new TranslateManager(_dbConnector).Translate("Cannot update article because it is locked by {0}"), GetUserName(lockUid));
            }

            var sql = $" SELECT a.attribute_id, t.type_name, t.database_type, a.required, c.site_id, i.content_id,  i.status_type_id, st.weight FROM content_item AS i LEFT OUTER JOIN content_attribute AS a ON a.content_id = i.content_id AND a.attribute_name = N'{attrName.Replace("'", "''")}' LEFT OUTER JOIN attribute_type AS t ON t.attribute_type_id = a.attribute_type_id LEFT OUTER JOIN content AS c ON c.content_id = i.content_id LEFT OUTER JOIN status_type AS st ON st.status_type_id= i.status_type_id WHERE i.content_item_id = {itemId}";
            var dt = _dbConnector.GetRealData(sql);
            if (dt.Rows.Count > 0)
            {
                var siteId = DBConnector.GetNumInt(dt.Rows[0]["site_id"]);
                DBConnector.GetNumInt(dt.Rows[0]["content_id"]);

                var attrId = DBConnector.GetNumInt(dt.Rows[0]["attribute_id"]);
                var attrDbType = dt.Rows[0]["database_type"].ToString();
                var attrRequired = DBConnector.GetNumInt(dt.Rows[0]["required"]);
                var currentStatusId = DBConnector.GetNumInt(dt.Rows[0]["status_type_id"]);
                var currentStatusWeight = DBConnector.GetNumInt(dt.Rows[0]["weight"]);

                if (attrRequired != 0 && string.IsNullOrEmpty(attrValue))
                {
                    return new TranslateManager(_dbConnector).Translate("Field is required");
                }

                if (attrDbType == "DATETIME")
                {
                    if (!DateTime.TryParse(attrValue, out DateTime _))
                    {
                        return new TranslateManager(_dbConnector).Translate("Value is not a date");
                    }

                    var culture = new CultureInfo("ru-RU", true);
                    attrValue = DateTime.ParseExact(attrValue, "yyyy-MM-dd HH:mm:ss", culture).ToString("yyyy-MM-dd HH:mm:ss");
                }

                // *************************************
                // *** Create Backup Copy of the Article
                // *************************************
                var backupSql = $" IF EXISTS(SELECT * FROM system_info WHERE field_name LIKE 'Version Control Add-on%') BEGIN EXEC create_content_item_version {uid}, {itemId} END";
                _dbConnector.ProcessData(backupSql);

                // ************
                // *** Workflow
                // ************
                var decreaseStatus = new Workflow(_dbConnector).DbWillContentItemStatusBeDecreased(siteId, itemId, uid, currentStatusWeight, out var decreaseToStatusId, out var decreaseToStatusWeight);
                var newStatus = decreaseStatus ? decreaseToStatusId : currentStatusId;
                _dbConnector.ProcessData(" UPDATE content_item SET" + " last_modified_by = " + uid + "," + " modified = GETDATE(), " + " status_type_id = " + newStatus + " WHERE content_item_id = " + itemId);

                string dataField;
                if (attrDbType == "NTEXT")
                {
                    // *** Upload URL is different for frontend and backend
                    // *** UploadURL = GetImagesUploadUrl(SiteID)
#if ASPNETCORE
                    uploadUrl = WebUtility.UrlDecode(uploadUrl);
                    siteUrl = WebUtility.UrlDecode(siteUrl);
#else
                    uploadUrl = HttpContext.Current.Server.UrlDecode(uploadUrl);
                    siteUrl = HttpContext.Current.Server.UrlDecode(siteUrl);
#endif
                    attrValue = attrValue.Replace(uploadUrl ?? throw new ArgumentNullException(nameof(uploadUrl)), "<" + "%=upload_url%" + ">");
                    attrValue = attrValue.Replace(siteUrl ?? throw new ArgumentNullException(nameof(siteUrl)), "<" + "%=site_url%" + ">");
                    dataField = "BLOB_DATA";
                }
                else
                {
                    dataField = "DATA";
                }
                attrValue = attrValue.Replace("'", "''");
                sql = $"UPDATE content_data SET {dataField} = N'{attrValue}' WHERE content_item_id = {itemId} AND attribute_id = {attrId}";
                _dbConnector.ProcessData(sql);

                if (decreaseStatus)
                {
                    //fill other fields
                    const string updateSql = "update content_data set {0} = {1} where attribute_id = {2} and content_item_id = {3}";
                    sql = $"select cd.attribute_id, cd.data, cd.blob_data, ca.attribute_name, at.database_type from content_data cd  join content_attribute ca on cd.attribute_id = ca.attribute_id  join attribute_type at on ca.attribute_type_id = at.attribute_type_id  where cd.content_item_id = {itemId} and cd.attribute_id <> {attrId}";
                    dt = _dbConnector.GetRealData(sql);
                    foreach (DataRow curRow in dt.Rows)
                    {
                        var dataType = (string)curRow["database_type"] == "NTEXT" ? "BLOB_DATA" : "DATA";
                        var dataObj = curRow[dataType];
                        var dataValue = ReferenceEquals(dataObj, DBNull.Value) ? "NULL" : $"'{dataObj}'";
                        _dbConnector.ProcessData(string.Format(updateSql, dataType, dataValue, curRow["attribute_id"], itemId));
                    }
                }

                if (decreaseToStatusWeight > currentStatusWeight)
                {
                    return "1|" + new TranslateManager(_dbConnector).Translate(@"Your changes were saved, but the required workflow was not applied. To apply, click ""Edit in Form View"" and follow the instructions on the screen");
                }
            }

            return "1";
        }

        public string CreateLikeArticle(int itemId, int contentId, int siteId)
        {
            var uid = GetUid();
            if (uid == 0)
            {
                return UnauthorizedMessage();
            }

            if (new QScreen(_dbConnector).DbGetUserAccess("content", contentId, (int)uid) < 3)
            {
                var translateManager = new TranslateManager(_dbConnector);
                return translateManager.Translate("You have no enough permissions to clone");
            }

            var createdId = 0;
            var cloneResult = CloneArticle(itemId, siteId, (int)uid, ref createdId);
            if (cloneResult != "1")
            {
                return "0|" + cloneResult;
            }

            _dbConnector.SendNotification(siteId, NotificationEvent.Create, createdId, string.Empty, false);
            return "1|" + createdId;
        }

        private string CloneArticle(int id, int siteId, int userId, ref int createdId)
        {
            string functionReturnValue = null;
            var strSql = $"SELECT CONTENT_ID, STATUS_TYPE_ID, VISIBLE, ARCHIVE FROM CONTENT_ITEM WHERE CONTENT_ITEM_ID = {id}";
            var dt = _dbConnector.GetRealData(strSql);

            if (dt.Rows.Count > 0)
            {
                var statusTypeId = int.Parse(dt.Rows[0]["STATUS_TYPE_ID"].ToString());
                var contentId = int.Parse(dt.Rows[0]["CONTENT_ID"].ToString());
                var visible = int.Parse(dt.Rows[0]["VISIBLE"].ToString());
                var archive = int.Parse(dt.Rows[0]["ARCHIVE"].ToString());

                strSql = $"SELECT * FROM CONTENT_CONSTRAINT WHERE CONTENT_ID = {contentId}";
                dt = _dbConnector.GetRealData(strSql);

                var translateManager = new TranslateManager(_dbConnector);
                if (dt.Rows.Count > 0)
                {
                    return translateManager.Translate("Cannot clone article because of constraint on the content");
                }

                // set None status for article with workflow'
                if (new Workflow(_dbConnector).ContentItemHasOwnWorkflow(id) || new Workflow(_dbConnector).GetContentWorkflowId(contentId) != 0)
                {
                    statusTypeId = new Workflow(_dbConnector).GetNoneId(siteId);
                }

                strSql = $"insert into content_item(CONTENT_ID, STATUS_TYPE_ID, VISIBLE, ARCHIVE, LAST_MODIFIED_BY) values({contentId}, {statusTypeId}, {visible}, {archive}, {userId})";
                var newId = _dbConnector.InsertDataWithIdentity(strSql);
                createdId = newId;

                if (newId == 0)
                {
                    return translateManager.Translate("Error while cloning article");
                }

#if ASPNETCORE
                _dbConnector.HttpContext.Session.SetInt32("newCloneArticleID", newId);
#else
                HttpContext.Current.Session["newCloneArticleID"] = newId;
#endif

                //inserting other fields
                strSql = $"SELECT AT.TYPE_NAME AS TYPE_NAME, AT.INPUT_TYPE AS INPUTTYPE,AT.DATABASE_TYPE, CA.* FROM ATTRIBUTE_TYPE AT, CONTENT_ATTRIBUTE CA WHERE AT.ATTRIBUTE_TYPE_ID=CA.ATTRIBUTE_TYPE_ID AND CA.CONTENT_ID = {contentId}";
                dt = _dbConnector.GetRealData(strSql);

                if (dt.Rows.Count > 0)
                {
                    foreach (DataRow curRow in dt.Rows)
                    {
                        var typeName = curRow["TYPE_NAME"].ToString();
                        var attributeId = DBConnector.GetNumInt(curRow["ATTRIBUTE_ID"]);
                        var linkId = curRow["link_id"] == DBNull.Value ? 0 : DBConnector.GetNumInt(curRow["link_id"]);
                        strSql = "UPDATE cd SET cd.data = {0}, cd.blob_data = cd2.blob_data FROM content_data cd INNER JOIN content_data cd2 ON cd2.attribute_id = {1} AND cd2.content_item_id = {2} WHERE cd.attribute_id= {1} AND cd.content_item_id = {3}";

                        if (typeName != "Relation")
                        {
                            _dbConnector.ProcessData(string.Format(strSql, "cd2.data", attributeId, id, newId));
                        }
                        else if (linkId == 0)
                        {
                            var s2 = $"CASE cd2.data WHEN '{id}' THEN '{newId}' ELSE cd2.data END";
                            _dbConnector.ProcessData(string.Format(strSql, s2, attributeId, id, newId));
                        }
                        else
                        {
                            CloneManyToMany(_dbConnector, id, newId, linkId, linkId);
                        }
                    }
                }

                _dbConnector.ProcessData($"INSERT INTO article_workflow_bind SELECT {newId}, workflow_id, is_async FROM article_workflow_bind WHERE content_item_id ={id}");
                AdjustManyToManySelfRelation(_dbConnector, id, newId);
                functionReturnValue = "1";
            }

            return functionReturnValue;
        }

        private static void CloneManyToMany(DBConnector cnn, int id, int newId, int linkId, int newLinkId)
        {
            var sql = $"INSERT INTO item_to_item SELECT {newLinkId}, {newId}, r_item_id FROM item_to_item WHERE link_id = {linkId} AND l_item_id = {id}";
            cnn.ProcessData(sql);

            sql = $"INSERT INTO item_to_item SELECT {newLinkId}, l_item_id, {newId} FROM item_to_item WHERE link_id = {linkId} AND r_item_id = {id} AND l_item_id <> {newId} AND l_item_id <> {id}";
            cnn.ProcessData(sql);
        }

        private static void AdjustManyToManySelfRelation(DBConnector cnn, int id, int newId)
        {
            var sql = $"update item_to_item set l_item_id = {newId} where l_item_id = {id} and r_item_id = {newId}";
            cnn.ProcessData(sql);

            sql = $"delete from item_to_item where r_item_id = {id} and l_item_id = {newId}";
            cnn.ProcessData(sql);
        }

        private int GetLockRecordUid(int itemId)
        {
            var res = 0;
            var strSql = $"SELECT locked_by FROM content_item WHERE content_item_id={itemId}";
            var dt = _dbConnector.GetRealData(strSql);
            if (dt.Rows.Count > 0)
            {
                if (!ReferenceEquals(dt.Rows[0]["locked_by"], DBNull.Value))
                {
                    res = DBConnector.GetNumInt(dt.Rows[0]["locked_by"]);
                }
            }

            return res;
        }

        private string GetUserName(int uid)
        {
            var strSql = "SELECT * FROM users WHERE user_id=" + uid;
            var dt = _dbConnector.GetCachedData(strSql);
            return dt.Rows.Count > 0 ? dt.Rows[0]["first_name"] + " " + dt.Rows[0]["last_name"] : string.Empty;
        }
    }
}
#endif
