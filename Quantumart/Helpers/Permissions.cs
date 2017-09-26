using System;
using System.Data;
using System.Diagnostics;
using Microsoft.VisualBasic;
using Quantumart.QPublishing.Database;

// ReSharper disable once CheckNamespace
namespace Quantumart.QPublishing.Helpers
{
    public class Permissions
    {
        #region "Users management"

        public static DataTable GetUserInfo(int userId)
        {
            var conn = new DBConnector();
            var selectClause = "select * from users where user_id = " + userId;
            return conn.GetCachedData(selectClause);
        }

        public static DataTable GetUserInfo(string login)
        {
            var conn = new DBConnector();
            var selectClause = "select * from users where login = '" + login + "'";
            return conn.GetCachedData(selectClause);
        }

        public static int AddUser(string username, string password, string firstName, string lastName, string email)
        {
            var conn = new DBConnector();
            var insertClause = "insert into users (login, password, disabled, first_name, last_name, email, subscribed, last_modified_by, language_id, vmode) values ('" + username + "', '" + password + "', 1, '" + firstName + "', '" + lastName + "', '" + email + "', 1, 1, 1, 0)";
            return conn.InsertDataWithIdentity(insertClause);
        }

        public static int AddUser(string username, string password, int disabled, string firstName, string lastName, string email)
        {
            var conn = new DBConnector();
            var insertClause = "insert into users (login, password, disabled, first_name, last_name, email, subscribed, last_modified_by, language_id, vmode) values ('" + username + "', '" + password + "', '" + disabled + "', '" + firstName + "', '" + lastName + "', '" + email + "', 1, 1, 1, 0)";
            return conn.InsertDataWithIdentity(insertClause);
        }

        public static void UpdateUser(int userId, string newUserName, string newPassword, string newFirstName, string newLastName, string newEmail)
        {
            var conn = new DBConnector();
            var updateClause = " update users set login = '" + newUserName + "', password = '" + newPassword + "', " + "first_name='" + newFirstName + "', last_name='" + newLastName + "', email='" + newEmail + "' " + " where user_id = " + userId;
            conn.ProcessData(updateClause);
        }

        public static bool RemoveUser(int userId)
        {
            var conn = new DBConnector();
            var deleteClause = "delete users where user_id = " + userId;
            conn.ProcessData(deleteClause);
            return true;
        }

        public static int AuthenticateUser(string username, string password)
        {
            var conn = new DBConnector();
            var selectClause = $"qp_authenticate @login = '{username}', @password = '{password}'";
            DataTable dt;
            try
            {
                dt = conn.GetRealData(selectClause);
            }
            catch (Exception ex)
            {
                var errorMessage = $"Permissions.cs, AuthenticateUser(string username, string password), MESSAGE: {ex.Message} STACK TRACE: {ex.StackTrace}";
                EventLog.WriteEntry("Application", errorMessage);
                return 0;
            }

            return dt.Rows.Count > 0 ? DBConnector.GetNumInt(dt.Rows[0]["user_id"]) : 0;
        }

        #endregion

        #region "User group management"

        public static DataTable GetGroupInfo(int groupId)
        {
            var conn = new DBConnector();
            var selectClause = "select * from user_group where group_id = " + groupId;
            var dt = conn.GetCachedData(selectClause);
            return dt;
        }

        public static DataTable GetGroupInfo(string groupName)
        {
            var conn = new DBConnector();
            var selectClause = "select * from user_group where group_name = '" + groupName + "'";
            var dt = conn.GetCachedData(selectClause);
            return dt;
        }

        public static int AddGroup(string name) => AddGroup(name, false);

        public static int AddGroup(string name, bool allowSharedOwnershipOfItems)
        {
            var conn = new DBConnector();
            var allowOwnership = "0";
            if (allowSharedOwnershipOfItems)
            {
                allowOwnership = "1";
            }
            var insertClause = "insert into user_group (group_name, shared_content_items) values ('" + name + "', " + allowOwnership + ")";
            return conn.InsertDataWithIdentity(insertClause);
        }

        public static void UpdateGroup(int groupId, string newName)
        {
            UpdateGroup(groupId, newName, false);
        }

        public static void UpdateGroup(int groupId, string newName, bool allowSharedOwnershipOfItems)
        {
            var conn = new DBConnector();
            var allowOwnership = "0";
            if (allowSharedOwnershipOfItems)
            {
                allowOwnership = "1";
            }
            var updateClause = "update user_group set group_name = '" + newName + "', shared_content_items=" + allowOwnership + " where group_id = " + groupId;
            conn.ProcessData(updateClause);
        }

        public static void RemoveGroup(int groupId)
        {
            var conn = new DBConnector();
            var deleteClause = "delete user_group where group_id = " + groupId;
            conn.ProcessData(deleteClause);
        }

        #endregion

        #region "Nested Groups management"

        public static DataTable GetAllGroups()
        {
            var conn = new DBConnector();
            const string selectClause = "select * from user_group ";
            return conn.GetCachedData(selectClause);
        }

        public static DataTable GetChildParentGroups()
        {
            var conn = new DBConnector();
            const string selectClause = "select gg.*, pg.group_name as parent_group_name, cg.group_name as child_group_name " + " from group_to_group as gg " + " inner join user_group as pg on pg.group_id = gg.parent_group_id " + " inner join user_group as cg on cg.group_id = gg.child_group_id ";
            return conn.GetCachedData(selectClause);
        }

        public static void AddChildGroupToParentGroup(int parentGroupId, int childGroupId)
        {
            var conn = new DBConnector();
            var insertClause = " if not exists (select * from group_to_group where parent_group_id=" + parentGroupId + " and child_group_id=" + childGroupId + ")" + Constants.vbCrLf + " insert into (parent_group_id, child_group_id) values (" + parentGroupId + ", " + childGroupId + ")";
            conn.ProcessData(insertClause);
        }

        public static void RemoveChildGroupFromParentGroup(int parentGroupId, int childGroupId)
        {
            var conn = new DBConnector();
            var insertClause = "delete from group_to_group where parent_group_id = " + parentGroupId + " AND child_group_id = " + childGroupId;
            conn.ProcessData(insertClause);
        }

        #endregion

        #region "User group bind management"

        public static DataTable GetRootGroupsForUser(int userId)
        {
            var conn = new DBConnector();
            var selectClause = "select ugb.group_id from user_group_bind as ugb " + " where ugb.user_id = " + userId;
            var dt = conn.GetCachedData(selectClause);
            return dt;
        }

        public static DataTable GetUsersForGroup(int groupId)
        {
            var conn = new DBConnector();
            var selectClause = "select u.* from user_group_bind as ugb " + " inner join users as u on u.user_id = ugb.user_id " + " where ugb.group_id = " + groupId;
            var dt = conn.GetCachedData(selectClause);
            return dt;
        }

        public static void AddUserToGroup(int userId, int groupId)
        {
            var conn = new DBConnector();
            var insertClause = "if not exists (select * from user_group_bind where group_id=" + groupId + " and user_id=" + userId + ")" + Constants.vbCrLf + " insert into user_group_bind (group_id, user_id) values (" + groupId + ", " + userId + ")";
            conn.ProcessData(insertClause);
        }

        public static void RemoveUserFromGroup(int userId, int groupId)
        {
            var conn = new DBConnector();
            var deleteClause = "delete user_group_bind where group_id = " + groupId + " and user_id = " + userId;
            conn.ProcessData(deleteClause);
        }

        public static void MoveUsersFromGroupToGroup(int fromGroupId, int toGroupId)
        {
            var conn = new DBConnector();
            var updateClause = " update user_group_bind set group_id = " + toGroupId + " where group_id = " + fromGroupId;
            conn.ProcessData(updateClause);
        }

        public static void CopyUsersFromGroupToGroup(int fromGroupId, int toGroupId)
        {
            var conn = new DBConnector();
            var updateClause = " insert into user_group_bind (group_id, user_id) " + " select " + toGroupId + ", user_id from user_group_bind where group_id=" + fromGroupId + " and user_id not in " + " (select user_id from user_group_bind where group_id = " + toGroupId + ")";
            conn.ProcessData(updateClause);
        }

        #endregion

        #region "Content_item_access management"

        public static DataTable GetAllGroupsForItemPermission(int itemId)
        {
            var conn = new DBConnector();
            var selectClause = " select ug.* from content_item_access as cia " + " inner join user_group as ug on ug.group_id = cia.group_id " + " where cia.content_item_id = " + itemId;
            var dt = conn.GetRealData(selectClause);
            return dt;
        }

        public static DataTable GetAllUsersForItemPermission(int itemId)
        {
            var conn = new DBConnector();
            var selectClause = " select u.* from content_item_access as cia " + " inner join users as u on u.user_id = cia.user_id " + " where cia.content_item_id = " + itemId;
            var dt = conn.GetRealData(selectClause);
            return dt;
        }

        public static void RemoveAllUsersFromItemPermission(int itemId)
        {
            var conn = new DBConnector();
            var deleteClause = "delete content_item_access where content_item_id = " + itemId + " and user_id is not null and group_id is null and user_id!=1";
            conn.ProcessData(deleteClause);
        }

        public static void RemoveAllGroupsFromItemPermission(int itemId)
        {
            var conn = new DBConnector();
            var deleteClause = "delete content_item_access where content_item_id = " + itemId + " and group_id is not null and user_id is null and group_id!=1";
            conn.ProcessData(deleteClause);
        }

        public static void RemoveAllEntitiesFromItemPermission(int itemId)
        {
            var conn = new DBConnector();
            var deleteClause = "delete content_item_access where content_item_id = " + itemId + " and IsNull(group_id,-1)!=1 and IsNull(user_id,-1)!=1 ";
            conn.ProcessData(deleteClause);
        }

        public static void AddUserToItemPermission(int userId, int itemId, int permissionId)
        {
            var conn = new DBConnector();
            var insertClause = " delete content_item_access where user_id=" + userId + " and content_item_id =" + itemId + "; " + " insert into content_item_access (content_item_id, user_id, permission_level_id, last_modified_by) values (" + itemId + ", " + userId + ", " + permissionId + ", 1)";
            conn.ProcessData(insertClause);
        }

        public static void AddGroupToItemPermission(int groupId, int itemId, int permissionId)
        {
            var conn = new DBConnector();
            var insertClause = " delete content_item_access where group_id=" + groupId + " and content_item_id =" + itemId + "; " + " insert into content_item_access (content_item_id, group_id, permission_level_id, last_modified_by) values (" + itemId + ", " + groupId + ", " + permissionId + ", 1)";
            conn.ProcessData(insertClause);
        }

        public static void RemoveUserFromItemPermission(int userId, int itemId)
        {
            var conn = new DBConnector();
            var deleteClause = "delete content_item_access where content_item_id = " + itemId + " and user_id = " + userId;
            conn.ProcessData(deleteClause);
        }

        public static void UpdateUserItemPermission(int userId, int itemId, int permissionId)
        {
            var conn = new DBConnector();
            var updateClause = " update content_item_access set permission_level_id = " + permissionId + " where content_item_id = " + itemId + " and user_id = " + userId;
            conn.ProcessData(updateClause);
        }

        public static void RemoveGroupFromItemPermission(int groupId, int itemId)
        {
            var conn = new DBConnector();
            var deleteClause = "delete content_item_access where content_item_id = " + itemId + " and group_id = " + groupId;
            conn.ProcessData(deleteClause);
        }

        public static void UpdateGroupItemPermission(int groupId, int itemId, int permissionId)
        {
            var conn = new DBConnector();
            var updateClause = " update content_item_access set permission_level_id = " + permissionId + " where content_item_id = " + itemId + " and group_id = " + groupId;
            conn.ProcessData(updateClause);
        }

        #endregion

        #region "Content_access management"

        public static DataTable GetAllGroupsForContentPermission(int contentId)
        {
            var conn = new DBConnector();
            var selectClause = " select ug.* from content_access as ca " + " inner join user_group as ug on ug.group_id = ca.group_id " + " where ca.content_id = " + contentId;
            var dt = conn.GetRealData(selectClause);
            return dt;
        }

        public static DataTable GetAllUsersForContentPermission(int contentId)
        {
            var conn = new DBConnector();
            var selectClause = " select u.* from content_access as ca " + " inner join users as u on u.user_id = ca.user_id " + " where ca.content_id = " + contentId;
            var dt = conn.GetRealData(selectClause);
            return dt;
        }

        public static void RemoveAllUsersFromContentPermission(int contentId)
        {
            var conn = new DBConnector();
            var deleteClause = "delete content_access where content_id = " + contentId + " and user_id is not null and group_id is null and user_id!=1";
            conn.ProcessData(deleteClause);
        }

        public static void RemoveAllGroupsFromContentPermission(int contentId)
        {
            var conn = new DBConnector();
            var deleteClause = "delete content_access where content_id = " + contentId + " and group_id is not null and user_id is null and group_id!=1";
            conn.ProcessData(deleteClause);
        }

        public static void RemoveAllEntitiesFromContentPermission(int contentId)
        {
            var conn = new DBConnector();
            var deleteClause = "delete content_access where content_id = " + contentId + " and IsNull(group_id,-1)!=1 and IsNull(user_id,-1)!=1 ";
            conn.ProcessData(deleteClause);
        }

        public static void AddUserToContentPermission(int userId, int contentId, int permissionId)
        {
            AddUserToContentPermission(userId, contentId, permissionId, false);
        }

        public static void AddUserToContentPermission(int userId, int contentId, int permissionId, bool propagateToItems)
        {
            var conn = new DBConnector();
            var propagate = "0";
            if (propagateToItems)
            {
                propagate = "1";
            }
            var insertClause = " delete content_access where content_id = " + contentId + " and user_id = " + userId + "; " + " insert into content_access (content_id, user_id, permission_level_id, propagate_to_items) values (" + contentId + ", " + userId + ", " + permissionId + ", " + propagate + ")";
            conn.ProcessData(insertClause);
        }

        public static void AddGroupToContentPermission(int groupId, int contentId, int permissionId)
        {
            AddGroupToContentPermission(groupId, contentId, permissionId, false);
        }

        public static void AddGroupToContentPermission(int groupId, int contentId, int permissionId, bool propagateToItems)
        {
            var conn = new DBConnector();
            var propagate = "0";
            if (propagateToItems)
            {
                propagate = "1";
            }
            var insertClause = " delete content_access where content_id = " + contentId + " and group_id = " + groupId + "; " + " insert into content_access (content_id, group_id, permission_level_id, propagate_to_items) values (" + contentId + ", " + groupId + ", " + permissionId + ", " + propagate + ")";
            conn.ProcessData(insertClause);
        }

        public static void RemoveUserFromContentPermission(int userId, int contentId)
        {
            var conn = new DBConnector();
            var deleteClause = "delete content_access where content_id = " + contentId + " and user_id = " + userId;
            conn.ProcessData(deleteClause);
        }

        public static void UpdateUserContentPermission(int userId, int contentId, int permissionId, bool propagateToItems)
        {
            var conn = new DBConnector();
            var propagate = "0";
            if (propagateToItems)
            {
                propagate = "1";
            }
            var updateClause = " update content_access set permission_level_id = " + permissionId + ", propagate_to_items = " + propagate + " where content_id = " + contentId + " and user_id = " + userId;
            conn.ProcessData(updateClause);
        }

        public static void RemoveGroupFromContentPermission(int groupId, int contentId)
        {
            var conn = new DBConnector();
            var deleteClause = "delete content_access where content_id = " + contentId + " and group_id = " + groupId;
            conn.ProcessData(deleteClause);
        }

        public static void UpdateGroupContentPermission(int groupId, int contentId, int permissionId, bool propagateToItems)
        {
            var conn = new DBConnector();
            var propagate = "0";
            if (propagateToItems)
            {
                propagate = "1";
            }
            var updateClause = " update content_access set permission_level_id = " + permissionId + ", propagate_to_items = " + propagate + " where content_id = " + contentId + " and group_id = " + groupId;
            conn.ProcessData(updateClause);
        }

        #endregion

        public static DataTable GetPermissionLevels()
        {
            var conn = new DBConnector();
            const string selectClause = "select * from permission_level";
            return conn.GetCachedData(selectClause);
        }
    }
}
