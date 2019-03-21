#if ASPNETCORE || NET4
using System.Web.UI;
using NUnit.Framework;
using Quantumart.QPublishing.Database;
using Quantumart.QPublishing.Pages;

namespace Quantumart.Tests
{
    [TestFixture]
    public class QPageEssentialTests
    {
        [Test]
        public void AddValue_WithNullValue()
        {
            var dbConnector = new DBConnector(Global.ConnectionString);
            var page = new QPageEssential(new Page(), dbConnector);
            page.FillValues();
            page.AddValue("key", null);

            Assert.That(page.DirtyValue("key"), Is.Null);
            Assert.That(page.StrValue("key"), Is.Null);
            Assert.That(page.Value("key"), Is.Null);
            Assert.That(page.Value("key1"), Is.Empty);
        }
    }
}
#endif
