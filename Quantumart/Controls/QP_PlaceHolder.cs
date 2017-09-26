using System;
using Quantumart.QPublishing.Pages;

// ReSharper disable once CheckNamespace
namespace Quantumart.QPublishing.Controls
{
    public sealed class PlaceHolder : QUserControlBase
    {
        protected override void OnInit(EventArgs e)
        {
            if (Page is QPage page)
            {
                if (simple)
                {
                    page.ShowObjectSimple(calls, this);
                }
                else
                {
                    page.ShowObject(calls, this);
                }
            }
            else
            {
                if (Page is QMobilePage mobilePage)
                {
                    if (simple)
                    {
                        mobilePage.ShowObjectSimple(calls, this);
                    }
                    else
                    {
                        mobilePage.ShowObject(calls, this);
                    }
                }
            }
        }
    }
}
