using System.Web.UI;

// ReSharper disable once CheckNamespace
namespace Quantumart.QPublishing.Controls
{
    public abstract class QUserControlBase : UserControl, IQUserControlBase
    {
        public string calls { get; set; } = string.Empty;

        public bool simple { get; set; } = false;
    }
}
