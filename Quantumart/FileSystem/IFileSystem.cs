using System.Xml;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;

namespace Quantumart.QPublishing.FileSystem
{
    public interface IFileSystem
    {
        void RemoveDirectory(string path);

        void CreateDirectory(string path);

        void CopyFile(string sourceName, string destName);

        bool FileExists(string path);

        XmlDocument LoadXml(string fileName);

        void SaveXml(XmlDocument xml, string fileName);

        Image LoadImage(string path);

        void SaveImage(Image image, string path, IImageEncoder encoder = null);

        ImageInfo IdentifyImage(string path);

    }
}
