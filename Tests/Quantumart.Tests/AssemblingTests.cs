using System;
using NUnit.Framework;
using Quantumart.QP8.Assembling;
using Quantumart.QPublishing.Database;

namespace Quantumart.Tests
{
    [TestFixture]
    public class AssemblingTests
    {
        [Test]
        public void TestGetMappingFromAssembling()
        {
            var cnt = new AssembleContentsController(Global.SiteId, Global.ConnectionString) { IsLive = true };
            var map1 = cnt.GetMapping("qpcontext");
            var cnt2 = new AssembleContentsController(Global.SiteId, Global.ConnectionString) { IsLive = false };
            var map2 = cnt2.GetMapping("qpcontext");
            Assert.That(map1, Is.Not.Null);
            Assert.That(map2, Is.Not.Null);
            Assert.That(map1, Is.Not.EqualTo(map2));
        }

        [Test]
        public void TestGetMappingFromQuantumart()
        {
            var dbc = new DBConnector(Global.ConnectionString);
            var map1 = dbc.GetDefaultMapFileContents(Global.SiteId, "qpcontext");
            dbc.IsStage = true;

            var map2 = dbc.GetDefaultMapFileContents(Global.SiteId, "qpcontext");
            Assert.That(map1, Is.Not.Null);
            Assert.That(map2, Is.Not.Null);
            Assert.That(map1, Is.Not.EqualTo(map2));
        }

        [Test]
        public void TestAssembling()
        {
            var cnt = new AssembleContentsController(Global.SiteId, Global.ConnectionString);
            Assert.That(() => { cnt.Assemble(); }, Throws.Nothing);
        }

        [Test]
        public void TestGetDifferentMappings()
        {
            var dbc = new DBConnector(Global.ConnectionString);
            var map1 = dbc.GetDefaultMapFileContents(dbc.GetSiteId("main_site"));
            var map2 = dbc.GetDefaultMapFileContents(dbc.GetSiteId("main_site"), "qpcontext");
            var map3 = dbc.GetDefaultMapFileContents(dbc.GetSiteId("main_site"));
            Assert.That(map1, Is.Not.Null);
            Assert.That(map2, Is.Not.Null);
            Assert.That(map1, Is.Not.EqualTo(map2));
            Assert.That(map3, Is.SameAs(map1));
        }

        [Test]
        public void TestGetNonExstingMapping()
        {
            var dbc = new DBConnector(Global.ConnectionString);
            Assert.That(() => { dbc.GetDefaultMapFileContents(dbc.GetSiteId("main_site"), "abc"); }, Throws.Exception.TypeOf<ApplicationException>());
        }
    }
}
