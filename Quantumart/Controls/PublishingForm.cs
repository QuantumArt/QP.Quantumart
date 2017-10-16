using System;
using System.Globalization;
using Quantumart.QPublishing.Database;

// ReSharper disable once CheckNamespace
namespace Quantumart.QPublishing.Controls
{
    public class PublishingForm : QPublishControl
    {
        protected PublishingForm(DBConnector dbConnector)
            : base(dbConnector)
        {
        }

        public string ThankYouPage { get; set; }

        public string PublishedStatusName { get; set; }

        protected new virtual string Field(string fieldName) => Data.Rows.Count != 0 ? FormatField(Data.Rows[0][fieldName].ToString()) : "";

        // ReSharper disable once InconsistentNaming
        protected new virtual string FieldNS(string fieldName) => Field(fieldName);

        protected virtual string FieldCheckboxCheck(string fieldName) => Data.Rows.Count != 0 && Data.Rows[0][fieldName] != DBNull.Value && !string.IsNullOrEmpty(Data.Rows[0][fieldName].ToString()) && DBConnector.GetNumInt(Data.Rows[0][fieldName]) > 0
            ? "checked"
            : string.Empty;

        protected virtual string FieldDateTime(string fieldName, string format)
        {
            var result = string.Empty;
            if (Data.Rows.Count != 0 && !Data.Rows[0].IsNull(fieldName))
            {
                var fieldValue = Convert.ToDateTime(Data.Rows[0][fieldName]);
                switch (format)
                {
                    case "Date":
                        result = fieldValue.ToShortDateString();
                        break;
                    case "Time":
                        result = fieldValue.ToShortTimeString();
                        break;
                    case "DateTime":
                        result = fieldValue.ToString(CultureInfo.InvariantCulture);
                        break;
                }
            }

            return result;
        }

        protected virtual string FieldImage(string fieldName)
        {
            var result = string.Empty;
            if (Data.Rows.Count != 0 && !Data.Rows[0].IsNull(fieldName))
            {
                var fieldValue = Data.Rows[0][fieldName].ToString();
                if (!string.IsNullOrEmpty(fieldValue))
                {
                    result = "<td><img src=\"" + ContentUploadURL + "/" + fieldValue + "\"" + " title=\"" + fieldValue + "\"" + " alt=\"" + fieldValue + "\"" + " >&nbsp;</td>";
                }
            }

            return result;
        }

        protected virtual string FieldFile(string fieldName)
        {
            var result = string.Empty;
            if (Data.Rows.Count != 0 && !Data.Rows[0].IsNull(fieldName))
            {
                var fieldValue = Data.Rows[0][fieldName].ToString();
                if (!string.IsNullOrEmpty(fieldValue))
                {
                    result = "<td><a href=\"" + ContentUploadURL + "/" + fieldValue + "\"" + " target=\"_blank\"" + " >" + fieldValue + "</a>&nbsp;</td>";
                }
            }

            return result;
        }
    }
}
