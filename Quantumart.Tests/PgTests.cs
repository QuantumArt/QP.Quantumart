using System;
using System.Collections.Generic;
using Npgsql;
using NUnit.Framework;
using QP.ConfigurationService.Models;
using Quantumart.QPublishing.Database;

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
    }
}
