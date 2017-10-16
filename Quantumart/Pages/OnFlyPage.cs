using Quantumart.QPublishing.Database;
using Quantumart.QPublishing.OnScreen;

// ReSharper disable once CheckNamespace
namespace Quantumart.QPublishing.Pages
{
    public class OnFlyPage : RSPage
    {
        private readonly DBConnector _dbConnector;

        public OnFlyPage(DBConnector dbConnector)
        {
            _dbConnector = dbConnector;
        }

        [RemoteScriptingMethod]
        public string DecreaseStatus(string itemId) => new OnFly(_dbConnector).DecreaseStatus(int.Parse(itemId));

        [RemoteScriptingMethod]
        public string UpdateArticle(string itemId, string attrName, string uploadUrl, string siteUrl, string attrValue) => new OnFly(_dbConnector).UpdateArticle(int.Parse(itemId), attrName, uploadUrl, siteUrl, attrValue);

        [RemoteScriptingMethod]
        public string CreateLikeArticle(string itemId, string contentId, string siteId) => new OnFly(_dbConnector).CreateLikeArticle(int.Parse(itemId), int.Parse(contentId), int.Parse(siteId));
    }
}
