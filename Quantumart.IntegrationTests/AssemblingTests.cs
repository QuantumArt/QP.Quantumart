using Quantumart.QP8.Assembling;
using NUnit.Framework;

using System;
using Quantumart.IntegrationTests.Infrastructure;
using Quantumart.QPublishing.Database;

namespace Quantumart.IntegrationTests
{
    [TestFixture]
    public class AssemblingTests
    {
        [Test]
        public void TestGetMappingFromAssembling()
        {
            var cnt = new AssembleContentsController(Global.SiteId, Global.ConnectionString, Global.DBType) { IsLive = true };
            var map1 = cnt.GetMapping("QPDataContext");

            var cnt2 = new AssembleContentsController(Global.SiteId, Global.ConnectionString, Global.DBType) { IsLive = false };
            var map2 = cnt2.GetMapping("QPDataContext");

            Assert.That(map1, Is.Not.Null);
            Assert.That(map2, Is.Not.Null);
            Assert.That(map1, Is.Not.EqualTo(map2));
        }


        [Test]
        public void TestGetMappingFromQuantumart()
        {
            var dbc1 = new DBConnector(Global.ConnectionString, Global.DBType) { IsStage = false };
            var dbc2 = new DBConnector(Global.ConnectionString, Global.DBType) { IsStage = true };

            var map1 = dbc1.GetDefaultMapFileContents(Global.SiteId, "QPDataContext");
            var map2 = dbc2.GetDefaultMapFileContents(Global.SiteId, "QPDataContext");

            Assert.That(map1, Is.Not.Null);
            Assert.That(map2, Is.Not.Null);
            Assert.That(map1, Is.Not.EqualTo(map2));
        }

        [Test]
        public void TestAssembling()
        {
            var cnt = new AssembleContentsController(Global.SiteId, Global.ConnectionString, Global.DBType);
            Assert.That(() => { cnt.Assemble(); }, Throws.Exception.InstanceOf(typeof(PlatformNotSupportedException)));
        }

        [Test]
        public void TestGetDifferentMappings()
        {
            var dbc = new DBConnector(Global.ConnectionString, Global.DBType);

            var map1 = dbc.GetDefaultMapFileContents(dbc.GetSiteId("Sandbox Net 2"));
            var map2 = dbc.GetDefaultMapFileContents(dbc.GetSiteId("Sandbox Net 2"), "QPSmallDataContext");
            var map3 = dbc.GetDefaultMapFileContents(dbc.GetSiteId("Sandbox Net 2"));

            Assert.That(map1, Is.Not.Null);
            Assert.That(map2, Is.Not.Null);
            Assert.That(map1, Is.Not.EqualTo(map2));
            Assert.That(map3, Is.SameAs(map1));
        }

        [Test]
        public void TestGetNonExstingMapping()
        {
            var dbc = new DBConnector(Global.ConnectionString, Global.DBType);
            Assert.That(() => { dbc.GetDefaultMapFileContents(dbc.GetSiteId("Sandbox Net"), "abc"); }, Throws.Exception.TypeOf<ApplicationException>());
        }
    }
}
