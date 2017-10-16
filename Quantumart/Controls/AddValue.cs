using System;
using System.Web.UI;
using Quantumart.QPublishing.Pages;

// ReSharper disable once CheckNamespace
// ReSharper disable InconsistentNaming
namespace Quantumart.QPublishing.Controls
{
    public class AddValue : UserControl
    {
        public string key { get; set; }

        public string value { get; set; }

        protected override void OnLoad(EventArgs e)
        {
            ((QPage)Page).AddValue(key, value);
        }
    }
}
