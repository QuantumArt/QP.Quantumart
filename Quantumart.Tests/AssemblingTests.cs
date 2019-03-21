using System;
using System.Collections.Generic;
using NUnit.Framework;
using Quantumart.QPublishing.Database;

#if NET4
using Quantumart.QP8.Assembling;
#endif

#if ASPNETCORE
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
#endif

namespace Quantumart.Tests
{
    [TestFixture]
    public class AssemblingTests
    {
#if NET4
        [Test]
        public void TestGetMappingFromAssembling()
        {
            var cnt = new AssembleContentsController(Global.SiteId, Global.ConnectionString) { IsLive = true };
            var map1 = cnt.GetMapping("QPDataContext");

            var cnt2 = new AssembleContentsController(Global.SiteId, Global.ConnectionString) { IsLive = false };
            var map2 = cnt2.GetMapping("QPDataContext");

            Assert.That(map1, Is.Not.Null);
            Assert.That(map2, Is.Not.Null);
            Assert.That(map1, Is.Not.EqualTo(map2));
        }
#endif


#if !ASPNETCORE && NET4
        [Test]
        public void TestGetMappingFromQuantumart()
        {
            var dbc1 = new DBConnector(Global.ConnectionString) { IsStage = false };
            var dbc2 = new DBConnector(Global.ConnectionString) { IsStage = true };

            var map1 = dbc1.GetDefaultMapFileContents(Global.SiteId, "QPDataContext");
            var map2 = dbc2.GetDefaultMapFileContents(Global.SiteId, "QPDataContext");

            Assert.That(map1, Is.Not.Null);
            Assert.That(map2, Is.Not.Null);
            Assert.That(map1, Is.Not.EqualTo(map2));
        }
#endif

#if NET4
        [Test]
        public void TestAssembling()
        {
            var cnt = new AssembleContentsController(Global.SiteId, Global.ConnectionString);
            Assert.That(() => { cnt.Assemble(); }, Throws.Nothing);
        }
#endif

#if !ASPNETCORE && NET4
        [Test]
        public void TestGetDifferentMappings()
        {
            var dbc = new DBConnector(Global.ConnectionString);

            var map1 = dbc.GetDefaultMapFileContents(dbc.GetSiteId("Sandbox Net"));
            var map2 = dbc.GetDefaultMapFileContents(dbc.GetSiteId("Sandbox Net"), "QPDataContext");
            var map3 = dbc.GetDefaultMapFileContents(dbc.GetSiteId("Sandbox Net"));

            Assert.That(map1, Is.Not.Null);
            Assert.That(map2, Is.Not.Null);
            Assert.That(map1, Is.Not.EqualTo(map2));
            Assert.That(map3, Is.SameAs(map1));
        }
#endif

#if !ASPNETCORE && NET4
        [Test]
        public void TestGetNonExstingMapping()
        {
            var dbc = new DBConnector(Global.ConnectionString);
            Assert.That(() => { dbc.GetDefaultMapFileContents(dbc.GetSiteId("Sandbox Net"), "abc"); }, Throws.Exception.TypeOf<ApplicationException>());
        }
#endif
    }
}
