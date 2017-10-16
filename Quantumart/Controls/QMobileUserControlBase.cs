using System.Web.UI.MobileControls;

// ReSharper disable once CheckNamespace
namespace Quantumart.QPublishing.Controls
{
#pragma warning disable 618
    public class QMobileUserControlBase : MobileUserControl, IQUserControlBase
#pragma warning restore 618
    {
        public string calls { get; set; } = string.Empty;

        public bool simple { get; set; }
    }
}
