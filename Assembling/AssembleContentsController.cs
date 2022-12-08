using System;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using System.Xml.Xsl;
using QP.ConfigurationService.Models;
using Quantumart.QP8.Assembling.Info;

// ReSharper disable once CheckNamespace
namespace Quantumart.QP8.Assembling
{
    public class AssembleContentsController : AssembleControllerBase
    {
        public int SiteId { get; set; }

        public string SqlMetalPath { get; set; }

        public string NameSpace { get; set; }

        private DataTable _contentsTable;
        private DataTable _contentGroupTable;
        private DataTable _fieldsTable;
        private DataTable _fieldsInfoTable;
        private DataTable _linkTable;
        private DataTable _userQueryTable;
        private DataTable _statusTable;
        private DataRow _siteRow;
        private DataTable _contentToContentTable;

        public void ClearTables()
        {
            _contentsTable = null;
            _contentGroupTable = null;
            _fieldsTable = null;
            _linkTable = null;
            _userQueryTable = null;
            _statusTable = null;
            _siteRow = null;
            _contentToContentTable = null;
        }

        public DataRow SiteRow => _siteRow ?? (_siteRow = Cnn.GetDataTable("select * from site where site_id = " + SiteId).Rows[0]);

        public DataTable StatusTable => _statusTable ?? (_statusTable = Cnn.GetDataTable("select * from status_type order by status_type_id"));

        public DataTable AdditionalContextClassNameTable => _contentGroupTable ?? (_contentGroupTable = Cnn.GetDataTable("select distinct add_context_class_name from content where add_context_class_name is not null and site_id = " + SiteId));

        public DataTable UserQueryTable
        {
            get
            {
                if (null == _userQueryTable)
                {
                    var qb = new StringBuilder();
                    qb.Append(" select uq.*, c.site_id as real_site_id, c2.site_id as virtual_site_id from user_query_contents uq ");
                    qb.Append(" inner join content c on uq.real_content_id = c.CONTENT_ID");
                    qb.Append(" inner join content c2 on uq.virtual_content_id = c2.CONTENT_ID");
                    _userQueryTable = Cnn.GetDataTable(qb.ToString());
                }
                return _userQueryTable;
            }
        }

        public DataTable ContentsTable
        {
            get
            {
                if (null == _contentsTable)
                {
                    var qb = new StringBuilder();
                    var split = Cnn.DbType == DatabaseType.SqlServer ? "cast(coalesce(cwb.is_async, 0) as bit)" : "coalesce(cwb.is_async, false)";
                    qb.Append($"select c.*, {split} as split_articles from content c ");
                    qb.Append(" left join content_workflow_bind cwb on c.content_id = cwb.content_id ");
                    qb.Append($" where site_id = {SiteId}");
                    qb.Append($" order by c.content_id");
                    _contentsTable = Cnn.GetDataTable(qb.ToString());
                }
                return _contentsTable;
            }
        }

        public DataTable FieldsTable
        {
            get
            {
                if (null == _fieldsTable)
                {
                    var qb = new StringBuilder();
                    qb.Append("select * from (");
                    qb.Append(" select row_number() over(PARTITION BY ca.attribute_id order by ca.attribute_id asc) as count, ");
                    qb.Append(" c.virtual_type, ca.*, at.type_name, ua.union_attr_id, ca2.attribute_id as uq_attr_id, ca3.attribute_id as related_m2o_id from content_attribute ca ");
                    qb.Append(" inner join attribute_type at on ca.attribute_type_id = at.attribute_type_id");
                    qb.Append(" inner join content c on ca.content_id = c.content_id ");
                    qb.Append(" left join union_attrs ua on ua.virtual_attr_id = ca.attribute_id ");
                    qb.Append(" left join user_query_contents uqa on ca.content_id = uqa.virtual_content_id ");
                    qb.Append(" left join content_attribute ca2 on ca2.content_id = uqa.real_content_id and ca.attribute_name = ca2.attribute_name");
                    qb.Append(" left join content_attribute ca3 on ca.attribute_id = ca3.back_related_attribute_id");

                    qb.Append($" where c.site_id = {SiteId} ");
                    qb.Append(" ) cc where cc.COUNT = 1");
                    _fieldsTable = Cnn.GetDataTable(qb.ToString());
                }
                return _fieldsTable;
            }
        }

        public DataTable FieldsInfoTable => _fieldsInfoTable ?? (_fieldsInfoTable = Cnn.GetDataTable("select COLUMN_NAME, TABLE_NAME, DATA_TYPE from INFORMATION_SCHEMA.COLUMNS"));

        public DataTable LinkTable
        {
            get
            {
                if (null == _linkTable)
                {
                    var qb = new StringBuilder();
                    qb.Append("SELECT link_id, l_content_id AS content_id, r_content_id as linked_content_id FROM content_to_content");
                    qb.Append(" union ");
                    qb.Append("SELECT link_id, r_content_id AS content_id, l_content_id as linked_content_id FROM content_to_content");

                    _linkTable = Cnn.GetDataTable(qb.ToString());
                }

                return _linkTable;
            }
        }

        public DataTable ContentToContentTable => _contentToContentTable ?? (_contentToContentTable = Cnn.GetDataTable($"select cc.* from content_to_content cc inner join CONTENT c on l_content_id = c.CONTENT_ID INNER JOIN CONTENT c2 on r_content_id = c2.CONTENT_ID WHERE c.SITE_ID = {SiteId} and c2.SITE_ID = {SiteId} and cc.link_id in (select link_id from content_attribute ca) order by cc.link_id"));

        public AssembleContentsController(int siteId, string connectionParameter, DatabaseType dbType = DatabaseType.SqlServer)
            : base(connectionParameter, dbType)
        {
            FillController(siteId, null);
        }

        public AssembleContentsController(int siteId, string sqlMetalPath, string connectionParameter, DatabaseType dbType = DatabaseType.SqlServer)
            : base(connectionParameter, dbType)
        {
            FillController(siteId, sqlMetalPath);
        }

        public AssembleContentsController(int siteId, string sqlMetalPath, DbConnector cnn)
            : base(cnn)
        {
            FillController(siteId, sqlMetalPath);
        }

        public void FillController(int siteId, string sqlMetalPath)
        {
            CurrentAssembleMode = AssembleMode.Contents;
            SiteId = siteId;
            SqlMetalPath = sqlMetalPath;
            UseT4 = string.IsNullOrEmpty(sqlMetalPath);
        }

        private string _siteRoot;

        public string SiteRoot
        {
            get
            {
                var path = SiteRow["is_live"].ToString() == "1" ? "assembly_path" : "stage_assembly_path";
                return _siteRoot ?? (_siteRoot = SiteRow[path].ToString().Replace(@"\bin", ""));
            }
            set => _siteRoot = value;
        }

        private bool? _isLive;

        public new bool IsLive
        {
            get => _isLive ?? (_isLive = Convert.ToBoolean(int.Parse(SiteRow["is_live"].ToString()))).Value;
            set => _isLive = value;
        }

        public bool DisableClassGeneration { get; set; }

        private bool UseT4 { get; set; }

        public bool ProceedMappingWithDb => GetFlag("proceed_mapping_with_db", false);

        public bool ImportMappingToDb => GetFlag("import_mapping_to_db", false);

        public bool GenerateMapFileOnly => GetFlag("generate_map_file_only", false);

        public bool ProceedDbIndependentGeneration => GetFlag("proceed_db_independent_generation", false);

        public bool GetFlag(string key, bool defaultValue) => !SiteRow.Table.Columns.Contains(key) ? defaultValue : (bool)SiteRow[key];

        public string DataContextClass
        {
            get
            {
                var contextClass = Convert.ToString(SiteRow["context_class_name"]);
                return string.IsNullOrEmpty(contextClass) ? "QPDataContext" : contextClass;
            }
        }

        private FileNameHelper _fileNameHelper;

        public FileNameHelper FileNameHelper => _fileNameHelper ?? (_fileNameHelper = new FileNameHelper { SiteRoot = SiteRoot, DataContextClass = DataContextClass, ProceedMappingWithDb = ProceedMappingWithDb });

        public XmlPreprocessor XmlPreprocessor { get; private set; }

        public XmlDocument DbmlFile { get; private set; }

        public XDocument MapFile { get; private set; }

        public string GetMapping(string name)
        {
            XmlPreprocessor = new XmlPreprocessor(this, true);
            string result = null;
            var schemaInfo = XmlPreprocessor.GenerateMainMapping(FileNameHelper);
            if (string.Equals(schemaInfo.ClassName, name, StringComparison.InvariantCultureIgnoreCase))
            {
                GenerateDbmlFile();
                GenerateMapFile(schemaInfo);
                result = MapFile.ToString();
            }

            foreach (DataRow row in AdditionalContextClassNameTable.Rows)
            {
                var info = ContextClassInfo.Parse(Convert.ToString(row["add_context_class_name"]));
                if (string.Equals(info.ClassName, name, StringComparison.InvariantCultureIgnoreCase))
                {
                    FileNameHelper.DataContextClass = info.ClassName;
                    schemaInfo = XmlPreprocessor.GeneratePartialMapping(FileNameHelper, info);
                    GenerateDbmlFile();
                    GenerateMapFile(schemaInfo);
                    result = MapFile.ToString();
                }
            }

            return result;
        }

        public override void Assemble()
        {
            XmlPreprocessor = new XmlPreprocessor(this, false);
            if (ImportMappingToDb)
            {
                XmlPreprocessor.ImportMapping(FileNameHelper);
                ClearTables();
            }
            var schemaInfo = XmlPreprocessor.GenerateMainMapping(FileNameHelper);
            GenerateClasses(schemaInfo);
            if (ProceedDbIndependentGeneration)
            {
                foreach (DataRow row in AdditionalContextClassNameTable.Rows)
                {
                    var info = ContextClassInfo.Parse(Convert.ToString(row["add_context_class_name"]));
                    FileNameHelper.DataContextClass = info.ClassName;
                    schemaInfo = XmlPreprocessor.GeneratePartialMapping(FileNameHelper, info);
                    GenerateClasses(schemaInfo);
                }
            }
        }

        private void GenerateClasses(SchemaInfo info)
        {
            if (DisableClassGeneration)
            {
                return;
            }

            GenerateDbmlFile();

            if (!GenerateMapFileOnly)
            {
                DbmlFile.Save(FileNameHelper.DbmlFilePath);
            }

            GenerateMapFile(info);

            MapFile.Save(FileNameHelper.MapFilePath);

            if (!GenerateMapFileOnly)
            {
                GenerateMain();
                GenerateManyToMany();
                GenerateModifications();
                GenerateExtensions();
            }
        }

        private static Stream GetResourceStream(string folder, string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var fullResourceName = $"{assembly.GetName().Name}.{folder}.{resourceName}";
            return assembly.GetManifestResourceStream(fullResourceName);
        }

        private static XmlWriter GetXmlWriter(IXPathNavigable doc) => doc.CreateNavigator()?.AppendChild();

        private IXPathNavigable GetMappingResultNavigator() => XmlPreprocessor.MappingResultXml.CreateNavigator();

        private void GenerateDbmlFile()
        {
            DbmlFile = new XmlDocument();
            using (var stream = GetResourceStream("xslt", FileNameHelper.MappingXsltFileName))
            {
                using (var xmlReader = XmlReader.Create(stream))
                {
                    using (var xmlWriter = GetXmlWriter(DbmlFile))
                    {
                        var xslTran = new XslCompiledTransform();
                        xslTran.Load(xmlReader);
                        xslTran.Transform(GetMappingResultNavigator(), xmlWriter);
                    }
                }
            }
        }

        private void GenerateManyToMany()
        {
            ProceedXsltToText(FileNameHelper.ManyXsltFileName, FileNameHelper.ManyCodeFilePath);
        }

        private void ProceedXsltToText(string xsltFileName, string outFilePath)
        {
            using (var stream = GetResourceStream("xslt", xsltFileName))
            {
                using (var xmlReader = XmlReader.Create(stream))
                {
                    using (var writer = new StreamWriter(outFilePath))
                    {
                        var xslTran = new XslCompiledTransform();
                        xslTran.Load(xmlReader);
                        xslTran.Transform(GetMappingResultNavigator(), null, writer);
                    }
                }
            }
        }

        private void GenerateModifications()
        {
            ProceedXsltToText(FileNameHelper.ModificationXsltFileName, FileNameHelper.ModificationCodeFilePath);
        }

        private void GenerateExtensions()
        {
            if (File.Exists(FileNameHelper.OldExtensionsCodeFilePath))
            {
                File.Delete(FileNameHelper.OldExtensionsCodeFilePath);
            }

            ProceedXsltToText(FileNameHelper.ExtensionsXsltFileName, FileNameHelper.ExtensionsCodeFilePath);
        }

        private readonly StringBuilder _output = new StringBuilder();

        private string Output => _output.ToString();

        private static Encoding OutputEncoding => Encoding.GetEncoding("cp866");

        private void OutputHandler(object sendingProcess, DataReceivedEventArgs outLine)
        {
            if (!string.IsNullOrEmpty(outLine.Data))
            {
                _output.AppendLine(OutputEncoding.GetString(Encoding.Default.GetBytes(outLine.Data)));
            }
        }

        private void GenerateMapFile(SchemaInfo info)
        {
            MapFile = XDocument.Parse(DbmlFile.OuterXml.Replace("dbml/2007", "mapping/2007"));
            XNamespace xn = "http://schemas.microsoft.com/linqtosql/mapping/2007";
            MapFile.Descendants(xn + "Database").Select(n => n.Attribute("Class")).Remove();
            MapFile.Descendants(xn + "Connection").Remove();
            MapFile.Descendants().Select(n => n.Attribute("Type")).Where(m => m != null).Remove();
            MapFile.Descendants().Select(n => n.Attribute("CanBeNull")).Where(m => m != null).Remove();
            var ids = MapFile.Descendants(xn + "Column").Where(n => n.Attribute("Name")?.Value == "CONTENT_ITEM_ID").ToArray();
            foreach (var idElem in ids)
            {
                idElem.SetAttributeValue("AutoSync", "OnInsert");
            }

            var types = MapFile.Descendants(xn + "Type").ToArray();
            foreach (var typeElem in types)
            {
                typeElem.SetAttributeValue("Name", $"{info.NamespaceName}.{typeElem.Attribute("Name")?.Value}");
            }

            var parameters = MapFile.Descendants(xn + "Parameter").ToArray();
            foreach (var paramElem in parameters)
            {
                paramElem.SetAttributeValue("Parameter", paramElem.Attribute("Name")?.Value);
            }

            var columns = MapFile.Descendants(xn + "Column").Union(MapFile.Descendants(xn + "Association")).ToArray();
            foreach (var c in columns)
            {
                var member = c.Attribute("Member")?.Value;
                var name = c.Attribute("Name")?.Value;
                var value = "_" + member;
                if (c.Attribute("OtherKey")?.Value == "Id")
                {
                    var thisKey = c.Attribute("ThisKey")?.Value;
                    if (thisKey?.EndsWith("_ID2") ?? false)
                    {
                        value = "_" + thisKey.Replace("_ID2", "12");
                    }
                    else if (thisKey != "StatusTypeId")
                    {
                        value = value + "1";
                    }
                }
                else if (c.Name == xn + "Column" && (member?.EndsWith("_ID") ?? false) && name != "LINKED_ITEM_ID" && name != "ITEM_ID")
                {
                    value = value.Substring(0, value.Length - 3);
                }
                else if (name == "LINKED_ITEM_ID" || name == "ITEM_ID")
                {
                    value = "_" + name;
                }

                c.SetAttributeValue("Storage", value);
            }
        }

        private void GenerateMain()
        {
            if (UseT4)
            {
                throw new PlatformNotSupportedException();
            }

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                throw new PlatformNotSupportedException();
            }

            RunSqlMetal();
        }

        private void RunSqlMetal()
        {
            var process = new Process
            {
                StartInfo =
                {
                    FileName = SqlMetalPath,
                    Arguments = GenerateCommandLineParams(FileNameHelper),
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                }
            };
            process.OutputDataReceived += OutputHandler;
            process.StartInfo.CreateNoWindow = true;

            if (process.Start())
            {
                process.BeginOutputReadLine();
                process.WaitForExit();
                File.WriteAllText(FileNameHelper.SqlMetalLogFilePath, Output);
                if (process.ExitCode != 0)
                {
                    var message = string.Join("\r\n",
                        Regex.Split(Output, "\r\n")
                            .Where(n => n.Contains("Error "))
                            .Select(n =>
                                {
                                    var line = Regex.Match(n, @".dbml\(([\d]+)\)").Groups[1].Value;
                                    line = string.IsNullOrEmpty(line) ? "" : $"Line {line}: ";
                                    return line + n.Substring(n.IndexOf("Error ", StringComparison.Ordinal));
                                }
                            )
                            .ToArray()
                    );
                    throw new ApplicationException(
                        $"Some errors has been found while processing the file {FileNameHelper.DbmlFilePath}:\r\n{message}");
                }
            }
        }

        private string GenerateCommandLineParams(FileNameHelper helper)
        {
            var cmdBuilder = new StringBuilder();
            cmdBuilder.Append($@"""{helper.DbmlFilePath}"" /code:""{helper.MainCodeFilePath}"" ");
            if (ProceedDbIndependentGeneration)
            {
                cmdBuilder.Append($@"/map:""{helper.MapFilePath}"" ");
            }
            if (!string.IsNullOrEmpty(NameSpace))
            {
                cmdBuilder.Append($@"/namespace:{NameSpace} ");
            }
            return cmdBuilder.ToString();
        }
    }
}
