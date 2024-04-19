using System.Xml;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;

namespace Quantumart.QPublishing.FileSystem
{
    public class FakeFileSystem : IFileSystem
    {
        public void RemoveDirectory(string path)
        {
        }

        public void CreateDirectory(string path)
        {
        }

        public void CopyFile(string sourceName, string destName)
        {
        }

        public bool FileExists(string path) => false;

        public XmlDocument LoadXml(string fileName) => throw new System.NotImplementedException();

        public void SaveXml(XmlDocument xml, string fileName)
        {
            throw new System.NotImplementedException();
        }

        public Image LoadImage(string path) => throw new System.NotImplementedException();

        public void SaveImage(Image image, string path, IImageEncoder encoder = null)
        {
            throw new System.NotImplementedException();
        }

        public ImageInfo IdentifyImage(string path) => throw new System.NotImplementedException();
    }
}
