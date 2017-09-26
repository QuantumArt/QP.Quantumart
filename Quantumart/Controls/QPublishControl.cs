using System.Data;

// ReSharper disable once CheckNamespace
namespace Quantumart.QPublishing.Controls
{
    public abstract class QPublishControl : QUserControl, IQPublishControl
    {
        protected QPublishControl()
        {
            QPublishControlEssential = new QPublishControlEssential(this);
        }

        public QPublishControlEssential QPublishControlEssential { get; set; }

        public DataTable Data
        {
            get => QPublishControlEssential.Data;
            set => QPublishControlEssential.Data = value;
        }

        public long TotalRecords
        {
            get => QPublishControlEssential.TotalRecords;
            set => QPublishControlEssential.TotalRecords = value;
        }

        public long AbsoluteTotalRecords
        {
            get => QPublishControlEssential.AbsoluteTotalRecords;
            set => QPublishControlEssential.AbsoluteTotalRecords = value;
        }

        public int Duration
        {
            get => QPublishControlEssential.Duration;
            set => QPublishControlEssential.Duration = value;
        }

        public long RecordsPerPage
        {
            get => QPublishControlEssential.RecordsPerPage;
            set => QPublishControlEssential.RecordsPerPage = value;
        }

        public bool EnableCacheInvalidation
        {
            get => QPublishControlEssential.EnableCacheInvalidation;
            set => QPublishControlEssential.EnableCacheInvalidation = value;
        }

        public bool ForceUnited
        {
            get => QPublishControlEssential.ForceUnited;
            set => QPublishControlEssential.ForceUnited = value;
        }

        public bool UseSchedule
        {
            get => QPublishControlEssential.UseSchedule;
            set => QPublishControlEssential.UseSchedule = value;
        }

        public bool ShowArchive
        {
            get => QPublishControlEssential.ShowArchive;
            set => QPublishControlEssential.ShowArchive = value;
        }

        public string Statuses
        {
            get => QPublishControlEssential.Statuses;
            set => QPublishControlEssential.Statuses = value;
        }

        public string CustomFilter
        {
            get => QPublishControlEssential.CustomFilter;
            set => QPublishControlEssential.CustomFilter = value;
        }

        public string StaticOrder
        {
            get => QPublishControlEssential.StaticOrder;
            set => QPublishControlEssential.StaticOrder = value;
        }

        public string DynamicOrder
        {
            get => QPublishControlEssential.DynamicOrder;
            set => QPublishControlEssential.DynamicOrder = value;
        }

        public string StartRow
        {
            get => QPublishControlEssential.StartRow;
            set => QPublishControlEssential.StartRow = value;
        }

        public string PageSize
        {
            get => QPublishControlEssential.PageSize;
            set => QPublishControlEssential.PageSize = value;
        }

        public bool UseSecurity
        {
            get => QPublishControlEssential.UseSecurity;
            set => QPublishControlEssential.UseSecurity = value;
        }

        public bool UseLevelFiltration
        {
            get => QPublishControlEssential.UseLevelFiltration;
            set => QPublishControlEssential.UseLevelFiltration = value;
        }

        public string StartLevel
        {
            get => QPublishControlEssential.StartLevel;
            set => QPublishControlEssential.StartLevel = value;
        }

        public string EndLevel
        {
            get => QPublishControlEssential.EndLevel;
            set => QPublishControlEssential.EndLevel = value;
        }

        public bool RotateContent
        {
            get => QPublishControlEssential.RotateContent;
            set => QPublishControlEssential.RotateContent = value;
        }

        public bool IsRoot
        {
            get => QPublishControlEssential.IsRoot;
            set => QPublishControlEssential.IsRoot = value;
        }

        public string DynamicVariable
        {
            get => QPublishControlEssential.DynamicVariable;
            set => QPublishControlEssential.DynamicVariable = value;
        }

        public int ContentID
        {
            get => QPublishControlEssential.ContentId;
            set => QPublishControlEssential.ContentId = value;
        }

        public string ContentName
        {
            get => QPublishControlEssential.ContentName;
            set => QPublishControlEssential.ContentName = value;
        }

        public string ContentUploadURL
        {
            get => QPublishControlEssential.ContentUploadUrl;
            set => QPublishControlEssential.ContentUploadUrl = value;
        }

        public string CacheKey
        {
            get => QPublishControlEssential.CacheKey;
            set => QPublishControlEssential.CacheKey = value;
        }

        public string GetBackendUrlForNotification(string defaultBackendUrl) => QPublishControlEssential.GetBackendUrlForNotification(defaultBackendUrl);

        public string GetFieldUploadUrl(string fieldName) => QPublishControlEssential.GetFieldUploadUrl(fieldName);

        public void FillData()
        {
            QPublishControlEssential.FillData();
        }
    }
}
