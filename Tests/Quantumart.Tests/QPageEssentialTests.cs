using System.Web.UI;
using NUnit.Framework;
using Quantumart.QPublishing.Pages;

namespace Quantumart.Tests
{
    [TestFixture]
    public class QPageEssentialTests
    {
        [Test]
        public void AddValue_WithNullValue()
        {
            var page = new QPageEssential(new Page());
            page.FillValues();
            page.AddValue("key", null);

            Assert.That(page.DirtyValue("key"), Is.Null);
            Assert.That(page.StrValue("key"), Is.Null);
            Assert.That(page.Value("key"), Is.Null);
            Assert.That(page.Value("key1"), Is.Empty);
        }
    }
}
