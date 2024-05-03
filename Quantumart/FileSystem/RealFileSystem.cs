using System.IO;
using System.Xml;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats;

// ReSharper disable once CheckNamespace
namespace Quantumart.QPublishing.FileSystem
{
    public class RealFileSystem : IFileSystem
    {
        public void RemoveDirectory(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }

        public void CreateDirectory(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        public void CopyFile(string sourceName, string destName)
        {
            if (!File.Exists(sourceName))
            {
                return;
            }

            if (File.Exists(destName))
            {
                File.Delete(destName);
            }

            File.Copy(sourceName, destName);
        }

        public bool FileExists(string path)
        {
            return File.Exists(path);
        }

        public ImageInfo IdentifyImage(string path)
        {
            return Image.Identify(path);
        }

        public Image LoadImage(string path)
        {
            return Image.Load(path);
        }

        public void SaveImage(Image image, string path, IImageEncoder encoder = null)
        {
            image.Save(path, encoder ?? image.DetectEncoder(path));
        }

        public XmlDocument LoadXml(string fileName)
        {
            var xmlDocument = new XmlDocument();
            xmlDocument.Load(fileName);
            return xmlDocument;
        }

        public Stream LoadStream(string fileName)
        {
            return new MemoryStream(File.ReadAllBytes(fileName));
        }

        public void SaveStream(Stream stream, string path)
        {
            using var fileStream = File.Create(path);
            stream.CopyTo(fileStream);
        }

        public void SaveXml(XmlDocument xml, string fileName)
        {
            xml.Save(fileName);
        }
    }
}
