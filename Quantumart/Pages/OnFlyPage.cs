using Quantumart.QPublishing.OnScreen;

// ReSharper disable once CheckNamespace
namespace Quantumart.QPublishing.Pages
{
    public class OnFlyPage : RSPage
    {
        [RemoteScriptingMethod]
        public string DecreaseStatus(string itemId) => OnFly.DecreaseStatus(int.Parse(itemId));

        [RemoteScriptingMethod]
        public string UpdateArticle(string itemId, string attrName, string uploadUrl, string siteUrl, string attrValue) => OnFly.UpdateArticle(int.Parse(itemId), attrName, uploadUrl, siteUrl, attrValue);

        [RemoteScriptingMethod]
        public string CreateLikeArticle(string itemId, string contentId, string siteId) => OnFly.CreateLikeArticle(int.Parse(itemId), int.Parse(contentId), int.Parse(siteId));
    }
}
