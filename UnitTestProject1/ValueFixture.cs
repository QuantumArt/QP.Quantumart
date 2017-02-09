using System;
using NUnit.Framework;
using Quantumart.QPublishing;
using Quantumart.QPublishing.Pages;
using System.Web.UI;

namespace UnitTestProject1
{
    [TestFixture]
    public class ValueFixture
    {
        [Test]
        public void AddValue_WithNullValue()
        {
            var p = new Page();
            var page = new QPageEssential(p);
            page.FillValues();

            page.AddValue("key", null);

            Assert.That(page.DirtyValue("key"), Is.Null);
            Assert.That(page.StrValue("key"), Is.Null);
            Assert.That(page.Value("key"), Is.Null);

            Assert.That(page.Value("key1"), Is.Empty);
        }
    }
}
