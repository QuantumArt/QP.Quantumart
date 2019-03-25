#if NET4
using System;
using System.Drawing;
using System.IO;
using Quantumart.QPublishing.FileSystem;
using Quantumart.QPublishing.Info;

// ReSharper disable once CheckNamespace
namespace Quantumart.QPublishing.Resizer
{
    public class DynamicImage
    {
        private readonly DynamicImageInfo _info;
        private readonly IFileSystem _fs;
        public DynamicImage(DynamicImageInfo info, IFileSystem fs)
        {
            _info = info;
            _fs = fs;
        }

        public void Create()
        {
            var curWidth = GetValidValue(_info.Width);
            var curHeight = GetValidValue(_info.Height);
            var curQuality = GetValidValue(_info.Quality);
            var path = (_info.ImagePath + "\\" + _info.ImageName).Replace("/", "\\");
            if (File.Exists(path))
            {
                Bitmap img = null;
                Bitmap img2 = null;
                try
                {
                    img = new Bitmap(path);
                    img2 = curWidth != 0 || curHeight != 0
                        ? ResizeImage(img, curWidth, curHeight, _info.MaxSize)
                        : new Bitmap(path);

                    var filePath = _info.ContentLibraryPath + "\\" + GetDynamicImageRelPath(_info.ImageName, _info.AttrId, _info.FileType);
                    CreateFolderForFile(filePath);
                    Resizer.SaveImage(img2, _info.FileType, filePath, curQuality);
                }
                finally
                {
                    img?.Dispose();
                    img2?.Dispose();
                }
            }
        }

        public Bitmap ResizeImage(Bitmap img, int width, int height, bool fit)
        {
            var factor = GetResizeFactor(img.Width, img.Height, width, height, fit);
            var resizeWidth = (int)factor == 0 ? width : Convert.ToInt32(img.Width * factor);
            var resizeHeight = (int)factor == 0 ? height : Convert.ToInt32(img.Height * factor);
            return Resizer.Resize(img, resizeWidth, resizeHeight);
        }

        private static double GetResizeFactor(int width, int height, int targetWidth, int targetHeight, bool fit)
        {
            var heightFactor = Convert.ToDouble(targetHeight) / Convert.ToDouble(height);
            var widthFactor = Convert.ToDouble(targetWidth) / Convert.ToDouble(width);
            if (fit)
            {
                var result = heightFactor <= widthFactor ? heightFactor : widthFactor;
                result = result > 1 ? 1 : result;
                return result;
            }

            if ((int)heightFactor == 0)
            {
                return widthFactor;
            }

            return (int)widthFactor == 0 ? heightFactor : 0;
        }

        private static int GetValidValue(object value)
        {
            var res = 0;
            if (value != null)
            {
                var strValue = Convert.ToString(value);
                if (strValue != string.Empty)
                {
                    res = int.Parse(strValue);
                }
            }

            return res;
        }

        public string GetDynamicImageRelPath(string fileName, decimal attributeId, string outFileType)
        {
            var newName = fileName.Replace("/", "\\");
            var fileNameParts = newName.Split('.');
            fileNameParts[fileNameParts.Length - 1] = outFileType;
            return "field_" + attributeId + "\\" + string.Join(".", fileNameParts);
        }

        public static string GetDynamicImageRelUrl(string fileName, decimal attributeId, string outFileType)
        {
            if (fileName == null)
            {
                return null;
            }
            else
            {
                var fileNameParts = fileName.Split('.');
                fileNameParts[fileNameParts.Length - 1] = outFileType;
                return "field_" + attributeId + "/" + string.Join(".", fileNameParts);
            }
        }

        private void CreateFolderForFile(string filePath)
        {
            var fileDirectoryParts = filePath.Split('\\');
            fileDirectoryParts[fileDirectoryParts.Length - 1] = string.Empty;
            var fileDirectoryPath = string.Join("\\", fileDirectoryParts);
            _fs.CreateDirectory(fileDirectoryPath);
        }
    }
}
#endif
