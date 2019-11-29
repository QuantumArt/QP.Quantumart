using System.Data;
using QP.ConfigurationService.Models;
using Quantumart.QP8.Assembling.Info;

// ReSharper disable once CheckNamespace
namespace Quantumart.QP8.Assembling
{
    public class AssemblePageObjectsController : AssembleControllerBase
    {
        public int PageId { get; private set; }

        public void FillController(int pageId)
        {
            var sqlQuery =
                " SELECT p.*, pt.*, s.* " + Renames +
                " FROM page AS p  " +
                " INNER JOIN page_template AS pt ON pt.page_template_id = p.page_template_id" +
                " INNER JOIN site AS s ON pt.site_id = s.site_id" +
                " where p.page_id = " + pageId;

            FillController(pageId, sqlQuery, null);
        }

        public void FillController(int pageId, string sqlQuery, DataTable data)
        {
            CurrentAssembleMode = AssembleMode.AllPageObjects;
            PageId = pageId;
            if (data != null)
            {
                Info = new AssembleInfo(this, data);
            }
            else if (!string.IsNullOrEmpty(sqlQuery))
            {
                Info = new AssembleInfo(this, sqlQuery);
            }
        }

        public AssemblePageObjectsController(int pageId, string connectionParameter)
            : base(connectionParameter, DatabaseType.SqlServer)
        {
            FillController(pageId);
        }

        public AssemblePageObjectsController(int pageId, DbConnector cnn)
            : base(cnn)
        {
            FillController(pageId);
        }

        public AssemblePageObjectsController(int pageId, DataTable data)
        {
            FillController(pageId, string.Empty, data);
        }

        internal override string GetFilter() => " and obj.page_id = " + PageId;

        public override void Assemble()
        {
            AssembleControlSet();
            InvalidatePageCache();
        }
    }
}
