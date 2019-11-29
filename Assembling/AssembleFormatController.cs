using System.Data;
using QP.ConfigurationService.Models;
using Quantumart.QP8.Assembling.Info;

// ReSharper disable once CheckNamespace
namespace Quantumart.QP8.Assembling
{
    public class AssembleFormatController : AssembleControllerBase
    {
        public int FormatId { get; protected set; }

        public AssembleFormatController(int formatId, AssembleMode mode, string connectionParameter, bool isCustomerCode, AssembleLocation fixedLocation, DatabaseType dbType = DatabaseType.SqlServer)
            : base(connectionParameter, isCustomerCode, dbType)
        {
            UseFixedLocation = true;
            FixedLocation = fixedLocation;
            FillController(formatId, mode);
        }

        public AssembleFormatController(int formatId, AssembleMode mode, string connectionParameter, bool isCustomerCode, DatabaseType dbType = DatabaseType.SqlServer)
            : base(connectionParameter, isCustomerCode, dbType)
        {
            FillController(formatId, mode);
        }

        public AssembleFormatController(int formatId, AssembleMode mode, string connectionParameter, DatabaseType dbType = DatabaseType.SqlServer)
            : base(connectionParameter, dbType)
        {
            FillController(formatId, mode);
        }

        public AssembleFormatController(int formatId, AssembleMode mode, DataTable data)
        {
            FillController(formatId, mode, string.Empty, data);
        }

        private void FillController(int formatId, AssembleMode mode)
        {
            var sqlQuery =
                " SELECT pt.*, p.*, s.*, obj.object_id, obj.object_name, objf.format_name " + Renames +
                " FROM object_format objf " +
                " INNER JOIN object obj on objf.object_id = obj.object_id" +
                " INNER JOIN page_template AS pt ON pt.page_template_id=obj.page_template_id" +
                " LEFT JOIN page AS p ON p.page_id=obj.page_id" +
                " INNER JOIN site AS s ON pt.site_id = s.site_id" +
                " WHERE objf.object_format_id=" + formatId;

            FillController(formatId, mode, sqlQuery, null);
        }

        public void FillController(int formatId, AssembleMode mode, string sqlQuery, DataTable data)
        {
            FormatId = formatId;
            CurrentAssembleMode = mode;
            if (data != null)
            {
                Info = new AssembleInfo(this, data);
            }
            else if (!string.IsNullOrEmpty(sqlQuery))
            {
                Info = new AssembleInfo(this, sqlQuery);
            }
        }

        public override void Assemble()
        {
            if (string.IsNullOrEmpty(Info.PageId))
            {
                InvalidateTemplateCache();
            }
            else
            {
                InvalidatePageCache();
            }
            AssembleControlSet();
            AssemblePageFiles();
        }

        internal override string GetFilter() => "and objf.object_format_id = " + FormatId;
    }
}
