using System.Web.UI;
using Quantumart.QPublishing.Pages;
using Xunit;

namespace Quantumart.Tests
{
    public class QPageEssentialTests
    {
        [Fact]
        public void AddValue_WithNullValue()
        {
            var page = new QPageEssential(new Page());
            page.FillValues();
            page.AddValue("key", null);

            Assert.Null(page.DirtyValue("key"));
            Assert.Null(page.StrValue("key"));
            Assert.Null(page.Value("key"));
            Assert.Empty(page.Value("key1"));
        }
    }
}
