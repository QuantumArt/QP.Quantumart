using System.Collections;
using System.Data;
using System.Linq;
using System.Text;
using QP.ConfigurationService.Models;
using Quantumart.QPublishing.Database;

// ReSharper disable once CheckNamespace
namespace Quantumart.QPublishing.Helpers
{
    public class Status
    {
        private readonly DBConnector _dbConnector;
        private readonly Workflow _workflow;

        private string NoLock => _dbConnector.DatabaseType == DatabaseType.SqlServer ? " with(nolock) " : string.Empty;

        public Status(DBConnector dbConnector, Workflow workflow)
        {
            _dbConnector = dbConnector;
            _workflow = workflow;
        }

        public string GetPreviousStatus(int id)
        {
            var row = GetPreviousStatusHistoryRecord(id, _dbConnector);
            return row?["status_type_name"].ToString() ?? "None";
        }

        public string GetStatusName(int id)
        {
            var strSql = $"select status_type_name from status_type where status_type_id = {id}";
            var dt = _dbConnector.GetCachedData(strSql);
            return dt.Rows.Count > 0 ? dt.Rows[0]["status_type_name"].ToString() : string.Empty;
        }

        public string GetLastModifiedLogin(int userId)
        {
            var strSql = $"SELECT * from USERS WHERE USER_ID = {userId}";
            var dt = _dbConnector.GetCachedData(strSql);
            return dt.Rows.Count > 0 ? dt.Rows[0]["LOGIN"].ToString() : string.Empty;
        }

        public string GetUserName(int userId)
        {
            var strSql = $"SELECT * from USERS WHERE USER_ID = {userId}";
            var dt = _dbConnector.GetCachedData(strSql);
            return dt.Rows.Count > 0 ? GetUserName(dt.Rows[0]) : string.Empty;
        }

        public Hashtable GetContainerStatuses(decimal objectId)
        {
            var containerStatuses = new Hashtable();
            var dtObjectParameters = _dbConnector.GetCachedData($"select * from container_statuses where object_id = {objectId}");
            foreach (DataRow dr in dtObjectParameters.Rows)
            {
                containerStatuses.Add((long)dr["status_type_id"], "1");
            }

            return containerStatuses;
        }

        public string GetParallelApproved(int contentItemId, int statusTypeId)
        {
            var workflowId = _workflow.DbGetWorkflowId(contentItemId);
            var sb = new StringBuilder();

            sb.Append($"select u.* from users u inner join user_group_bind ug {NoLock} on u.user_id = ug.user_id ");
            sb.Append(" inner join workflow_rules wr on ug.group_id = wr.group_id ");
            sb.Append(string.Format($"where wr.workflow_id = {workflowId} and wr.successor_status_id = {GetNextWorkflowStatus(workflowId, statusTypeId)} and ug.user_id not in (select user_id from waiting_for_approval {NoLock} where content_item_id = {contentItemId})"));

            var dtApproving = _dbConnector.GetRealData(sb.ToString());
            return GetUserCommaList(dtApproving);
        }

        public int GetNextWorkflowStatus(int workflowId, int statusTypeId)
        {
            var sb = new StringBuilder();
            sb.Append($"SELECT st.status_type_id FROM workflow_rules wr {NoLock} ");
            sb.Append($"INNER JOIN status_type st {NoLock} ON wr.successor_status_id = st.status_type_id ");
            sb.Append($"WHERE wr.workflow_id = {workflowId} And st.weight > (select weight from status_type where status_type_id = {statusTypeId}) ORDER BY st.weight ");

            var dtStatus = _dbConnector.GetRealData(sb.ToString());
            return dtStatus.Rows.Count > 0 ? DBConnector.GetNumInt(dtStatus.Rows[0]["STATUS_TYPE_ID"]) : 0;
        }

        public string GetParallelWaitingForApproval(int contentItemId)
        {
            var strSql = $"select u.* from waiting_for_approval wa {NoLock} inner join users u {NoLock} on wa.user_id = u.user_id where content_item_id = {contentItemId}";
            var dtApproving = _dbConnector.GetRealData(strSql);
            return GetUserCommaList(dtApproving);
        }

        public static string GetUserName(DataRow row) => $"{row["first_name"]} {row["last_name"]}";

        public static string GetUserCommaList(DataTable dt) => dt.Rows.Count == 0 ? "none" : string.Join(", ", (from DataRow row in dt.Rows select $"<strong>{GetUserName(row)}</strong>").ToArray());

        internal static DataRow GetPreviousStatusHistoryRecord(int id, DBConnector cnn)
        {
            var strSql = cnn.DatabaseType == DatabaseType.SqlServer
                ? $"SELECT Top 1 * FROM CONTENT_ITEM_STATUS_HISTORY AS c WITH (NOLOCK) INNER JOIN status_type AS s WITH (NOLOCK) ON c.status_type_id = s.status_type_id WHERE content_item_id = {id} AND status_history_id NOT IN ( SELECT Top 1 status_history_id FROM CONTENT_ITEM_STATUS_HISTORY WITH (NOLOCK) WHERE content_item_id = {id} AND status_type_id is not null ORDER BY status_history_id DESC ) ORDER BY status_history_id DESC"
                : $"SELECT * FROM CONTENT_ITEM_STATUS_HISTORY AS c INNER JOIN status_type AS s ON c.status_type_id = s.status_type_id WHERE content_item_id = {id} AND status_history_id NOT IN ( SELECT status_history_id FROM CONTENT_ITEM_STATUS_HISTORY WHERE content_item_id = {id} AND status_type_id is not null ORDER BY status_history_id DESC LIMIT 1 ) ORDER BY status_history_id DESC LIMIT 1";
            var dt = cnn.GetRealData(strSql);
            return dt.Rows.Count > 0 ? dt.Rows[0] : null;
        }
    }
}
