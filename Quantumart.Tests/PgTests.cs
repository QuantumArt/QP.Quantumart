using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using System.Linq;
using Npgsql;
using NUnit.Framework;
using QP.ConfigurationService.Models;
using Quantumart.QPublishing.Database;
using Quantumart.QPublishing.Helpers;

namespace Quantumart.Tests
{
    [TestFixture]
    public class PgTests
    {
        [Test]
        public void TestGetRegion()
        {
            var cnn = new NpgsqlConnection(Global.ConnectionString);
            var dbc = new DBConnector(cnn);
            dbc.GetStatusTypeId(35, "Published");
            dbc.GetContentIdByNetName(Global.SiteId, "Region");
            dbc.GetAttributeIdByNetNames(Global.SiteId, "Region", "Title1");
            dbc.GetRealData("select site_id from site");
            dbc.GetVersionIdsToRemove(new[] { 1741257, 1935847 }, 5);

             var dict = new Dictionary<string, string>() { {"CONTENT_ITEM_ID" , "0"}, {"Title", "abcc"}, {"Alias", "SubscriptionFee3"}, {"ApplyToTypes", "8422,23518"}};
             dbc.MassUpdate(350, new[] {dict}, 1);
             var a = dict["CONTENT_ITEM_ID"];
        }

        [Test]
        public void TestGetSiteData()
        {
            var cnn = new NpgsqlConnection(Global.ConnectionString);
            var dbc = new DBConnector(cnn);
            var plugins = new Plugins(dbc);
            var siteInfo = plugins.GetSiteMetaData("main_site", "Graphql", "test");
            var key = siteInfo != null ? siteInfo["apikey"] : "";
            var contentInfo = plugins.GetContentMetaData("main_site", "AllField", "Graphql", "test");
            var exposed = contentInfo != null && (bool)contentInfo["isexposed"];
            var contentsInfo = plugins.GetContentListMetaData("main_site", "Graphql", "test");
            var cnt = contentsInfo.Count();
            var fieldsInfo = plugins.GetContentAttributeListMetaData("main_site", "AllField","Graphql", "test");
            var cnt2 = fieldsInfo.Count();
            var fieldInfo = plugins.GetContentAttributeMetaData("main_site", "AllField", "VisualEdit","Graphql", "test");
            var hidden = fieldInfo != null && (bool)fieldInfo["ishidden"];
        }

    }
}
