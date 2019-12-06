using System.IO;

// ReSharper disable once CheckNamespace
namespace Quantumart.QP8.Assembling.Info
{
    public class FileNameHelper
    {
        public string SiteRoot { get; set; }

        public string DataContextClass { get; set; }

        public bool ProceedMappingWithDb { get; set; }

        public string AppDataFolder => $@"{SiteRoot}{Path.DirectorySeparatorChar}App_Data";

        public string AppCodeFolder => $@"{SiteRoot}{Path.DirectorySeparatorChar}App_Code";

        private string AppDataFile(string fileName) => $@"{AppDataFolder}{Path.DirectorySeparatorChar}{fileName}";

        private string AppCodeFile(string fileName) => $@"{AppCodeFolder}{Path.DirectorySeparatorChar}{fileName}";

        private string GetPrefixedFileName(string fileName) => $"{DataContextClass}{fileName}";

        public string OldGeneratedMappingXmlFileName => OldDefaultMappingXmlFilePath;

        public string ImportedMappingXmlFileName => File.Exists(OldMappingXmlFilePath) ? OldMappingXmlFilePath : OldDefaultMappingXmlFilePath;

        public string UsableMappingXmlFileName => ProceedMappingWithDb ? MappingXmlFilePath : OldMappingXmlFilePath;

        public string OldDefaultMappingXmlFilePath => AppDataFile("DefaultMapping.xml");

        public string OldMappingXmlFilePath => AppDataFile("Mapping.xml");

        public string OldMappingResultXmlFilePath => AppDataFile("MappingResult.xml");

        public string MappingXmlFilePath => AppDataFile(GetPrefixedFileName("Mapping.xml"));

        public string MappingResultXmlFileName => AppDataFile(GetPrefixedFileName("MappingResult.xml"));

        public string MappingXsltFileName => "Mapping.xslt";

        public string MappingXsltFilePath => AppDataFile(MappingXsltFileName);

        public string ManyXsltFileName => "Many.xslt";

        public string ManyXsltFilePath => AppDataFile(ManyXsltFileName);

        public string ModificationXsltFileName => "Modifications.xslt";

        public string ModificationXsltFilePath => AppDataFile(ModificationXsltFileName);

        public string ExtensionsXsltFileName => "Extensions.xslt";

        public string ExtensionsXsltFilePath => AppDataFile(ExtensionsXsltFileName);

        public string DbmlFilePath => AppDataFile(GetPrefixedFileName(".dbml"));

        public string MapFilePath => AppDataFile(GetPrefixedFileName(".map"));

        public string SqlMetalLogFilePath => AppDataFile(GetPrefixedFileName(".log"));

        public string MainCodeFilePath => AppCodeFile(GetPrefixedFileName(".cs"));

        public string ManyCodeFilePath => AppCodeFile(GetPrefixedFileName("Many.cs"));

        public string ModificationCodeFilePath => AppCodeFile(GetPrefixedFileName("Modifications.cs"));

        public string ExtensionsCodeFilePath => AppCodeFile(GetPrefixedFileName("Extensions.cs"));

        public string OldExtensionsCodeFilePath => AppCodeFile("UserExtensions.cs");
    }
}
