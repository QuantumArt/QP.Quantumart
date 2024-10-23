using System;
using System.Data;
using Quantumart.QPublishing.Database;
using NLog;
using QP.ConfigurationService.Models;

// ReSharper disable once CheckNamespace
namespace Quantumart.QPublishing.Helpers
{
    public class Permissions
    {
        private readonly DBConnector _dbConnector;

        public Permissions(DBConnector dbConnector)
        {
            _dbConnector = dbConnector;
        }

        private string Output(string columnName)
        {
            return _dbConnector.DatabaseType == DatabaseType.Postgres ? "" : $" OUTPUT inserted.{columnName} ";
        }

        private string Returning(string columnName)
        {
            return _dbConnector.DatabaseType == DatabaseType.SqlServer ? "" : $" returning {columnName} ";
        }

        public DataTable GetUserInfo(int userId)
        {
            var selectClause = $"select * from users where user_id = {userId}";
            return _dbConnector.GetCachedData(selectClause);
        }

        public DataTable GetUserInfo(string login)
        {
            var selectClause = $"select * from users where login = \'{login}\'";
            return _dbConnector.GetCachedData(selectClause);
        }

        public int AddUser(string username, string password, string firstName, string lastName, string email)
        {
            var outUserId = Output("user_id");
            var retUserId = Returning("user_id");
            var insertClause = $"insert into users (login, password, disabled, first_name, last_name, email, subscribed, last_modified_by, language_id, vmode) {outUserId}" +
                $"values (\'{username}\', \'{password}\', 1, \'{firstName}\', \'{lastName}\', \'{email}\', 1, 1, 1, 0) {retUserId}";
            return _dbConnector.InsertDataWithIdentity(insertClause);
        }

        public int AddUser(string username, string password, int disabled, string firstName, string lastName, string email)
        {
            var retUserId = _dbConnector.DatabaseType == DatabaseType.Postgres ? Returning("user_id") : "; select scope_identity() as user_id";
            var insertClause = $"insert into users (login, password, disabled, first_name, last_name, email, subscribed, last_modified_by, language_id, vmode)" +
                $" values (\'{username}\', \'{password}\', \'{disabled}\', \'{firstName}\', \'{lastName}\', \'{email}\', 1, 1, 1, 0) {retUserId}";
            return _dbConnector.InsertDataWithIdentity(insertClause);
        }

        public void UpdateUser(int userId, string newUserName, string newPassword, string newFirstName, string newLastName, string newEmail)
        {
            var updateClause = $" update users set login = \'{newUserName}\', password = \'{newPassword}\', first_name=\'{newFirstName}\', last_name=\'{newLastName}\', email=\'{newEmail}\'  where user_id = {userId}";
            _dbConnector.ProcessData(updateClause);
        }

        public bool RemoveUser(int userId)
        {
            var deleteClause = $"delete from users where user_id = {userId}";
            _dbConnector.ProcessData(deleteClause);
            return true;
        }

        public int AuthenticateUser(string username, string password)
        {
            var sql = _dbConnector.DatabaseType == DatabaseType.Postgres ?
                "select qp_authenticate(@login, @password)" : "qp_authenticate";
            var type = _dbConnector.DatabaseType == DatabaseType.Postgres ?
                CommandType.Text : CommandType.StoredProcedure;
            var cmd = _dbConnector.CreateDbCommand(sql);
            cmd.CommandType = type;
            cmd.Parameters.AddWithValue("@login", username);
            cmd.Parameters.AddWithValue("@password", password);
            DataTable dt;
            try
            {
                dt = _dbConnector.GetRealData(cmd);
            }
            catch (Exception ex)
            {
                LogManager.GetCurrentClassLogger().ForErrorEvent().Exception(ex).Message("Error while authenticating user").Log();
                return 0;
            }
            return dt.Rows.Count > 0 ? DBConnector.GetNumInt(dt.Rows[0]["user_id"]) : 0;
        }

        public DataTable GetGroupInfo(int groupId)
        {
            var selectClause = $"select * from user_group where group_id = {groupId}";
            return _dbConnector.GetCachedData(selectClause);
        }

        public DataTable GetGroupInfo(string groupName)
        {
            var selectClause = $"select * from user_group where group_name = \'{groupName}\'";
            return _dbConnector.GetCachedData(selectClause);
        }

        public int AddGroup(string name) => AddGroup(name, false);

        public int AddGroup(string name, bool allowSharedOwnershipOfItems)
        {
            var allowOwnership = "0";
            if (allowSharedOwnershipOfItems)
            {
                allowOwnership = "1";
            }

            var outGroupId = Output("group_id");
            var retGroupId = Returning("group_id");

            var insertClause = $"insert into user_group (group_name, shared_content_items) {outGroupId} values (\'{name}\', {allowOwnership}) {retGroupId}";
            return _dbConnector.InsertDataWithIdentity(insertClause);
        }

        public void UpdateGroup(int groupId, string newName)
        {
            UpdateGroup(groupId, newName, false);
        }

        public void UpdateGroup(int groupId, string newName, bool allowSharedOwnershipOfItems)
        {
            var allowOwnership = "0";
            if (allowSharedOwnershipOfItems)
            {
                allowOwnership = "1";
            }

            var updateClause = $"update user_group set group_name = \'{newName}\', shared_content_items={allowOwnership} where group_id = {groupId}";
            _dbConnector.ProcessData(updateClause);
        }

        public void RemoveGroup(int groupId)
        {
            var deleteClause = $"delete from user_group where group_id = {groupId}";
            _dbConnector.ProcessData(deleteClause);
        }

        public DataTable GetAllGroups()
        {
            const string selectClause = "select * from user_group ";
            return _dbConnector.GetCachedData(selectClause);
        }

        public DataTable GetChildParentGroups()
        {
            const string selectClause = "select gg.*, pg.group_name as parent_group_name, cg.group_name as child_group_name from group_to_group as gg inner join user_group as pg on pg.group_id = gg.parent_group_id inner join user_group as cg on cg.group_id = gg.child_group_id ";
            return _dbConnector.GetCachedData(selectClause);
        }

        public void AddChildGroupToParentGroup(int parentGroupId, int childGroupId)
        {
            var insertClause = $" if not exists (select * from group_to_group where parent_group_id={parentGroupId} and child_group_id={childGroupId}){Environment.NewLine} insert into (parent_group_id, child_group_id) values ({parentGroupId}, {childGroupId})";
            _dbConnector.ProcessData(insertClause);
        }

        public void RemoveChildGroupFromParentGroup(int parentGroupId, int childGroupId)
        {
            var insertClause = $"delete from group_to_group where parent_group_id = {parentGroupId} AND child_group_id = {childGroupId}";
            _dbConnector.ProcessData(insertClause);
        }

        public DataTable GetRootGroupsForUser(int userId)
        {
            var selectClause = $"select ugb.group_id from user_group_bind as ugb where ugb.user_id = {userId}";
            return _dbConnector.GetCachedData(selectClause);
        }

        public DataTable GetUsersForGroup(int groupId)
        {
            var selectClause = $"select u.* from user_group_bind as ugb inner join users as u on u.user_id = ugb.user_id  where ugb.group_id = {groupId}";
            return _dbConnector.GetCachedData(selectClause);
        }

        public void AddUserToGroup(int userId, int groupId)
        {
            var insertClause = $"insert into user_group_bind (group_id, user_id) select {groupId}, {userId} where not exists (select * from user_group_bind where group_id={groupId} and user_id={userId})";
            _dbConnector.ProcessData(insertClause);
        }

        public void RemoveUserFromGroup(int userId, int groupId)
        {
            var deleteClause = $"delete from user_group_bind where group_id = {groupId} and user_id = {userId}";
            _dbConnector.ProcessData(deleteClause);
        }

        public void MoveUsersFromGroupToGroup(int fromGroupId, int toGroupId)
        {
            var updateClause = $" update user_group_bind set group_id = {toGroupId} where group_id = {fromGroupId}";
            _dbConnector.ProcessData(updateClause);
        }

        public void CopyUsersFromGroupToGroup(int fromGroupId, int toGroupId)
        {
            var updateClause = $" insert into user_group_bind (group_id, user_id) select {toGroupId}, user_id from user_group_bind where group_id={fromGroupId} and user_id not in (select user_id from user_group_bind where group_id = {toGroupId})";
            _dbConnector.ProcessData(updateClause);
        }

        public DataTable GetAllGroupsForItemPermission(int itemId)
        {
            var selectClause = $" select ug.* from content_item_access as cia inner join user_group as ug on ug.group_id = cia.group_id where cia.content_item_id = {itemId}";
            return _dbConnector.GetRealData(selectClause);
        }

        public DataTable GetAllUsersForItemPermission(int itemId)
        {
            var selectClause = $" select u.* from content_item_access as cia inner join users as u on u.user_id = cia.user_id where cia.content_item_id = {itemId}";
            return _dbConnector.GetRealData(selectClause);
        }

        public void RemoveAllUsersFromItemPermission(int itemId)
        {
            var deleteClause = $"delete from content_item_access where content_item_id = {itemId} and user_id is not null and group_id is null and user_id!=1";
            _dbConnector.ProcessData(deleteClause);
        }

        public void RemoveAllGroupsFromItemPermission(int itemId)
        {
            var deleteClause = $"delete from content_item_access where content_item_id = {itemId} and group_id is not null and user_id is null and group_id!=1";
            _dbConnector.ProcessData(deleteClause);
        }

        public void RemoveAllEntitiesFromItemPermission(int itemId)
        {
            var deleteClause = $"delete from content_item_access where content_item_id = {itemId} and IsNull(group_id,-1)!=1 and IsNull(user_id,-1)!=1 ";
            _dbConnector.ProcessData(deleteClause);
        }

        public void AddUserToItemPermission(int userId, int itemId, int permissionId)
        {
            var insertClause = $" delete from content_item_access where user_id={userId} and content_item_id ={itemId};  insert into content_item_access (content_item_id, user_id, permission_level_id, last_modified_by) values ({itemId}, {userId}, {permissionId}, 1)";
            _dbConnector.ProcessData(insertClause);
        }

        public void AddGroupToItemPermission(int groupId, int itemId, int permissionId)
        {
            var insertClause = $" delete from content_item_access where group_id={groupId} and content_item_id ={itemId};  insert into content_item_access (content_item_id, group_id, permission_level_id, last_modified_by) values ({itemId}, {groupId}, {permissionId}, 1)";
            _dbConnector.ProcessData(insertClause);
        }

        public void RemoveUserFromItemPermission(int userId, int itemId)
        {
            var deleteClause = $"delete from content_item_access where content_item_id = {itemId} and user_id = {userId}";
            _dbConnector.ProcessData(deleteClause);
        }

        public void UpdateUserItemPermission(int userId, int itemId, int permissionId)
        {
            var updateClause = $" update content_item_access set permission_level_id = {permissionId} where content_item_id = {itemId} and user_id = {userId}";
            _dbConnector.ProcessData(updateClause);
        }

        public void RemoveGroupFromItemPermission(int groupId, int itemId)
        {
            var deleteClause = $"delete from content_item_access where content_item_id = {itemId} and group_id = {groupId}";
            _dbConnector.ProcessData(deleteClause);
        }

        public void UpdateGroupItemPermission(int groupId, int itemId, int permissionId)
        {
            var updateClause = $" update content_item_access set permission_level_id = {permissionId} where content_item_id = {itemId} and group_id = {groupId}";
            _dbConnector.ProcessData(updateClause);
        }

        public DataTable GetAllGroupsForContentPermission(int contentId)
        {
            var selectClause = $" select ug.* from content_access as ca inner join user_group as ug on ug.group_id = ca.group_id where ca.content_id = {contentId}";
            return _dbConnector.GetRealData(selectClause);
        }

        public DataTable GetAllUsersForContentPermission(int contentId)
        {
            var selectClause = $" select u.* from content_access as ca  inner join users as u on u.user_id = ca.user_id  where ca.content_id = {contentId}";
            return _dbConnector.GetRealData(selectClause);
        }

        public void RemoveAllUsersFromContentPermission(int contentId)
        {
            var deleteClause = "delete from content_access where content_id = " + contentId + " and user_id is not null and group_id is null and user_id!=1";
            _dbConnector.ProcessData(deleteClause);
        }

        public void RemoveAllGroupsFromContentPermission(int contentId)
        {
            var deleteClause = "delete from content_access where content_id = " + contentId + " and group_id is not null and user_id is null and group_id!=1";
            _dbConnector.ProcessData(deleteClause);
        }

        public void RemoveAllEntitiesFromContentPermission(int contentId)
        {
            var deleteClause = "delete from content_access where content_id = " + contentId + " and IsNull(group_id,-1)!=1 and IsNull(user_id,-1)!=1 ";
            _dbConnector.ProcessData(deleteClause);
        }

        public void AddUserToContentPermission(int userId, int contentId, int permissionId)
        {
            AddUserToContentPermission(userId, contentId, permissionId, false);
        }

        public void AddUserToContentPermission(int userId, int contentId, int permissionId, bool propagateToItems)
        {
            var propagate = "0";
            if (propagateToItems)
            {
                propagate = "1";
            }

            var insertClause = $" delete from content_access where content_id = {contentId} and user_id = {userId};  insert into content_access (content_id, user_id, permission_level_id, propagate_to_items) values ({contentId}, {userId}, {permissionId}, {propagate})";
            _dbConnector.ProcessData(insertClause);
        }

        public void AddGroupToContentPermission(int groupId, int contentId, int permissionId)
        {
            AddGroupToContentPermission(groupId, contentId, permissionId, false);
        }

        public void AddGroupToContentPermission(int groupId, int contentId, int permissionId, bool propagateToItems)
        {
            var propagate = "0";
            if (propagateToItems)
            {
                propagate = "1";
            }

            var insertClause = $" delete from content_access where content_id = {contentId} and group_id = {groupId};  insert into content_access (content_id, group_id, permission_level_id, propagate_to_items) values ({contentId}, {groupId}, {permissionId}, {propagate})";
            _dbConnector.ProcessData(insertClause);
        }

        public void RemoveUserFromContentPermission(int userId, int contentId)
        {
            var deleteClause = $"delete from content_access where content_id = {contentId} and user_id = {userId}";
            _dbConnector.ProcessData(deleteClause);
        }

        public void UpdateUserContentPermission(int userId, int contentId, int permissionId, bool propagateToItems)
        {
            var propagate = "0";
            if (propagateToItems)
            {
                propagate = "1";
            }

            var updateClause = $" update content_access set permission_level_id = {permissionId}, propagate_to_items = {propagate} where content_id = {contentId} and user_id = {userId}";
            _dbConnector.ProcessData(updateClause);
        }

        public void RemoveGroupFromContentPermission(int groupId, int contentId)
        {
            var deleteClause = $"delete from content_access where content_id = {contentId} and group_id = {groupId}";
            _dbConnector.ProcessData(deleteClause);
        }

        public void UpdateGroupContentPermission(int groupId, int contentId, int permissionId, bool propagateToItems)
        {
            var propagate = "0";
            if (propagateToItems)
            {
                propagate = "1";
            }

            var updateClause = $" update content_access set permission_level_id = {permissionId}, propagate_to_items = {propagate} where content_id = {contentId} and group_id = {groupId}";
            _dbConnector.ProcessData(updateClause);
        }

        public DataTable GetPermissionLevels()
        {
            const string selectClause = "select * from permission_level";
            return _dbConnector.GetCachedData(selectClause);
        }
    }
}
