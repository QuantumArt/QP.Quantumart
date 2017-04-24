using NUnit.Framework;
using Quantumart.QP8.Assembling;

namespace Quantumart.Tests
{
    [TestFixture]
    public class AssemblingTests
    {

        [Test]
        public void TestGetMapping()
        {
            var cnt = new AssembleContentsController(Global.SiteId, Global.ConnectionString) { IsLive = true };
            var map1 = cnt.GetMapping("qpcontext");
            var cnt2 = new AssembleContentsController(Global.SiteId, Global.ConnectionString) { IsLive = false };
            var map2 = cnt2.GetMapping("qpcontext");
            Assert.That(map1, Is.Not.Null);
            Assert.That(map2, Is.Not.Null);
        }

        [Test]
        public void TestAssembling()
        {
            var cnt = new AssembleContentsController(Global.SiteId, Global.ConnectionString);
            cnt.Assemble();
        }

    }
}
