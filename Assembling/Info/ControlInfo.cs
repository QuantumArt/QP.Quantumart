using System;
using System.Collections;
using System.Data;
using System.Globalization;
using System.Text;

namespace Quantumart.QP8.Assembling.Info
{
    public enum ControlType
    {
        Generic,
        PublishingContainer,
        PublishingForm
    }

    public class ControlInfo
    {
        public ControlInfo(DataRow reader, AssembleInfo info)
        {
            Row = reader;
            Info = info;
            if (CurrentType == ControlType.PublishingContainer)
            {
                Container = new ContainerInfo(this);
            }

            TargetFolder = GetTargetFolder();
            CodeBehind = CodeTransformer.Preprocess(Row["CODE_BEHIND"].ToString());
            Presentation = CodeTransformer.Preprocess(Row["FORMAT_BODY"].ToString());

            var code = CodeBehind;
            UserNamespaces = CodeTransformer.CutNamespaceDefinitionsFromCode(ref code);
            CodeBehind = CodeTransformer.AppendIndent(code);
        }

        public DataRow Row { get; }

        public bool GetNumericBoolean(string fieldName)
        {
            return Convert.ToBoolean(GetInt32(fieldName));
        }

        public int GetInt32(string fieldName)
        {
            if (Row.Table.Columns[fieldName] == null)
            {
                throw new DataException(string.Format(CultureInfo.InvariantCulture, "Field {0} is not found in the object-level information table", fieldName));
            }
            return Convert.ToInt32(GetString(fieldName), CultureInfo.InvariantCulture);
        }

        public string GetString(string fieldName)
        {
            return Row.Table.Columns[fieldName] != null ? Row[fieldName].ToString() : "";
        }

        public bool GetBoolean(string fieldName)
        {
            if (Row.Table.Columns[fieldName] == null)
            {
                throw new DataException(string.Format(CultureInfo.InvariantCulture, "Field {0} is not found in the object-level information table", fieldName));
            }
            return (bool)Row[fieldName];
        }

        public object GetObject(string fieldName)
        {
            return Row[fieldName];
        }

        public AssembleInfo Info { get; }

        public ContainerInfo Container { get; }

        public Hashtable UserNamespaces { get; }

        public string Presentation { get; set; }

        public string CodeBehind { get; set; }

        public int TemplateId => GetInt32("PAGE_TEMPLATE_ID");

        public int LanguageId => GetInt32("NET_LANGUAGE_ID");

        public int TypeId => GetInt32("OBJECT_TYPE_ID");

        public bool IsDefault => GetInt32("CURRENT_FORMAT_ID") == GetInt32("DEFAULT_FORMAT_ID");

        public bool IsRoot => (int)Row["ROOT"] == 1;

        public bool ContentSelected => GetObject("CONTENT_ID") != DBNull.Value;

        public string MissedContentExceptionString => string.Format(CultureInfo.InvariantCulture, "Content is not selected in {3} (Object: {0}, Template: {1}, Page Id: {2})", GetString("OBJECT_NAME"), GetString("TEMPLATE_NAME"), GetString("PAGE_ID"), TypeName);

        public string TypeName
        {
            get
            {
                switch (CurrentType)
                {
                    case ControlType.PublishingContainer:
                        return "Publishing Container";
                    case ControlType.PublishingForm:
                        return "Publishing Form";
                    default:
                        return "Generic";
                }
            }
        }

        public bool IsCSharp => LanguageId == 1;

        public string Language => AssembleInfo.GetLangName(LanguageId);

        public string ClassName
        {
            get
            {
                if (IsRoot)
                {
                    return NetObjectName;
                }

                return NetObjectName + "_" + NetFormatName;
            }
        }

        public string NetObjectName => AssembleInfo.GetNetName(GetString("NET_OBJECT_NAME"), GetString("OBJECT_ID"), "o");

        public string NetFormatName => IsRoot
            ? string.Empty
            : AssembleInfo.GetNetName(GetString("NET_FORMAT_NAME"), GetString("CURRENT_FORMAT_ID"), "f");

        public bool EnableViewState => GetBoolean("ENABLE_VIEWSTATE");

        public bool DisableDataBind => GetBoolean("DISABLE_DATABIND");

        public string TagName => GetString("TAG_NAME");

        public string AddTag => string.IsNullOrEmpty(TagName)
            ? string.Empty
            : string.Format(CultureInfo.InvariantCulture, "<add tagPrefix=\"qpc\" tagName=\"{0}\" src=\"{1}\" />", TagName, NetControlUrl);

        public string NetControlUrl => "/~" + TargetFolder.Replace(Info.Paths.BaseAssemblePath, "").Replace("\\", "/");

        public ControlType CurrentType => AssembleInfo.GetControlType(TypeId);

        public string TargetFolder { get; }

        public static string GetFileName(string objectName, string formatName)
        {
            var sb = new StringBuilder();
            sb.Append(objectName);
            if (NotRoot(formatName))
            {
                sb.Append("_");
                sb.Append(formatName);
            }

            sb.Append(".ascx");
            return sb.ToString();
        }

        public static string GetSimpleFileName(string objectName)
        {
            var sb = new StringBuilder();
            sb.Append(objectName);
            sb.Append(".ascx");
            return sb.ToString();
        }

        private static bool NotRoot(string formatName)
        {
            return !string.IsNullOrEmpty(formatName);
        }

        public string CommonFileName => GetFileName(NetObjectName, NetFormatName);

        public string CommonCodeFileName => CommonFileName + "." + AssembleInfo.GetLangExtension(LanguageId);

        public string CommonSystemCodeFileName => CommonFileName + "_partial." + AssembleInfo.GetLangExtension(LanguageId);

        public string SimpleFileName => GetSimpleFileName(NetObjectName);

        public string SimpleCodeFileName => SimpleFileName + "." + AssembleInfo.GetLangExtension(LanguageId);

        private string GetTargetFolder()
        {
            if (IsRoot)
            {
                return Info.IsAssembleFormatMode ? Info.Paths.PageControlsPath : Info.Paths.TemplateControlsPath;
            }

            if (IsPageControl)
            {
                return Info.Paths.PageControlsPath;
            }

            return TemplateId == Info.TemplateId ? Info.Paths.TemplateControlsPath : Info.Paths.GetExternalPath(TemplateId);
        }

        private bool IsPageControl => Row["PAGE_ID"] != DBNull.Value;

        internal string GetThankYouPage()
        {
            var sb = new StringBuilder();
            var dv = new DataView(Info.ThankYouPages) { RowFilter = "OBJECT_ID = " + GetString("OBJECT_ID") };
            if (dv.Count > 0)
            {
                var templateFolder = dv[0]["TEMPLATE_FOLDER"].ToString();
                if (!string.IsNullOrEmpty(templateFolder))
                {
                    sb.Append(templateFolder);
                    sb.Append("/");
                }

                var pageFolder = dv[0]["PAGE_FOLDER"].ToString();
                if (!string.IsNullOrEmpty(pageFolder))
                {
                    sb.Append(pageFolder);
                    sb.Append("/");
                }

                sb.Append(dv[0]["PAGE_FILENAME"]);
            }

            return sb.ToString();
        }

        private string GetCustomClassField()
        {
            switch (CurrentType)
            {
                case ControlType.PublishingContainer:
                    return "custom_class_for_containers";
                case ControlType.PublishingForm:
                    return "custom_class_for_forms";
                default:
                    return "custom_class_for_generics";
            }

        }

        private string CustomClassName
        {
            get
            {
                if (IsRoot)
                {
                    return GetString("CONTROL_CUSTOM_CLASS");
                }

                return !string.IsNullOrEmpty(GetString("CONTROL_CUSTOM_CLASS")) ? GetString("CONTROL_CUSTOM_CLASS") : Info.GetString(GetCustomClassField());
            }
        }

        public string SystemClassName
        {
            get
            {
                switch (CurrentType)
                {
                    case ControlType.PublishingContainer:
                        return "Quantumart.QPublishing." + (Info.AssembleForMobile ? "QMobilePublishControl" : "QPublishControl");
                    case ControlType.PublishingForm:
                        return "Quantumart.QPublishing.PublishingForm";
                }

                return "Quantumart.QPublishing." + (Info.AssembleForMobile ? "QMobileQUserControl" : "QUserControl");
            }

        }

        public string BaseClassName => !string.IsNullOrEmpty(CustomClassName) ? CustomClassName : SystemClassName;
    }
}
