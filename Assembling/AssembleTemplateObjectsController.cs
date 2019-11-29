using System.Data;
using QP.ConfigurationService.Models;
using Quantumart.QP8.Assembling.Info;

// ReSharper disable once CheckNamespace
namespace Quantumart.QP8.Assembling
{
    public class AssembleTemplateObjectsController : AssembleControllerBase
    {
        public int TemplateId { get; private set; }

        public void FillController(int templateId)
        {
            var sqlQuery =
                " SELECT pt.*, s.* " + RenamesWithoutPage +
                " FROM page_template AS pt " +
                " INNER JOIN site AS s ON pt.site_id = s.site_id" +
                " where pt.page_template_id = " + templateId;

            FillController(templateId, sqlQuery, null);
        }

        public void FillController(int templateId, string sqlQuery, DataTable data)
        {
            CurrentAssembleMode = AssembleMode.AllTemplateObjects;
            TemplateId = templateId;
            if (data != null)
            {
                Info = new AssembleInfo(this, data);
            }
            else if (!string.IsNullOrEmpty(sqlQuery))
            {
                Info = new AssembleInfo(this, sqlQuery);
            }
        }

        public AssembleTemplateObjectsController(int templateId, DataTable data)
        {
            FillController(templateId, string.Empty, data);
        }

        public AssembleTemplateObjectsController(int templateId, DataRow row)
        {
            FillController(templateId, string.Empty, ConvertToDataTable(row));
        }

        public AssembleTemplateObjectsController(int templateId, string connectionParameter)
            : base(connectionParameter, DatabaseType.SqlServer)
        {
            FillController(templateId);
        }

        public AssembleTemplateObjectsController(int templateId, DbConnector cnn)
            : base(cnn)
        {
            FillController(templateId);
        }

        internal override string GetFilter() => " and obj.page_id is null and obj.page_template_id = " + TemplateId;

        public override void Assemble()
        {
            AssembleControlSet();
            InvalidateTemplateCache();
        }
    }
}
