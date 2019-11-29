using System;
using NUnit.Framework;
using Quantumart.IntegrationTests.Infrastructure;
using Quantumart.QPublishing.Database;
using Quantumart.QPublishing.Helpers;

#if ASPNETCORE
using Moq;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;

#endif

namespace Quantumart.IntegrationTests
{
    [TestFixture]
    public class PermissionFixture
    {
        private const string Username = "test54321";
        private const string Password = "1Qaz2Wsx";
        private const string FirstName = "testFirstName";
        private const string LastName = "testLastName";
        private const string Email = "testEmail@test.ru";
        private const string ColumnIdName = "user_id";
        private const int GroupId = 1;

        public static DBConnector DbConnector { get; set; }

        [OneTimeSetUp]
        public static void Init()
        {
#if ASPNETCORE
            DbConnector = new DBConnector(
                new DbConnectorSettings { ConnectionString = Global.ConnectionString, DbType = Global.DBType},
                new MemoryCache(new MemoryCacheOptions()),
                new HttpContextAccessor { HttpContext = new DefaultHttpContext { Session = Mock.Of<ISession>() } }
            )
            {
                ForceLocalCache = true
            };
#else
            DbConnector = new DBConnector(Global.ConnectionString, Global.DBType) { ForceLocalCache = true };
#endif
            Clear();
        }

        [Test]
        public void AuthenticateUser()
        {
            var permissions = new Permissions(DbConnector);

            var id = 0;
            Assert.DoesNotThrow(() => { id = permissions.AddUser(Username, Password, 0, FirstName, LastName, Email); }, "Add User");

            //authenticate user
            var auth = 0;
            Assert.DoesNotThrow(() => { auth = permissions.AuthenticateUser(Username, Password); }, "Authenticate");
            Assert.That(auth, Is.Not.EqualTo(0));

            //add user to group
            Assert.DoesNotThrow(() => { permissions.AddUserToGroup(id, GroupId); }, "Add user to group");

            //remove user from group
            Assert.DoesNotThrow(() => { permissions.RemoveUserFromGroup(id, GroupId); }, "Remove user from group");
        }

        [OneTimeTearDown]
        public static void TearDown()
        {
            Clear();
        }

        private static void Clear()
        {
            var permissions = new Permissions(DbConnector);
            var userInfo = permissions.GetUserInfo(Username);
            if (userInfo.Rows.Count > 0)
            {
                var id = Convert.ToInt32(userInfo.Rows[0][ColumnIdName]);
                permissions.RemoveUser(id);
            }
        }
    }
}
