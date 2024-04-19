using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using Quantumart.QPublishing.FileSystem;
using Quantumart.QPublishing.Info;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;

// ReSharper disable InconsistentNaming
// ReSharper disable once CheckNamespace
namespace Quantumart.QPublishing.Resizer
{
    public class DynamicImage
    {
        private const string JPG_EXTENSION = "JPG";
        private const string PNG_EXTENSION = "PNG";
        private const string GIF_EXTENSION = "GIF";
        private const string SVG_EXTENSION = "SVG";

        private readonly DynamicImageInfo _info;
        private readonly IFileSystem _fileSystem;

        public DynamicImage(DynamicImageInfo info, IFileSystem fileSystem)
        {
            _info = info;
            _fileSystem = fileSystem;
        }

        private enum ImageResizeMode
        {
            None,
            ByWidth,
            ByHeight,
            Absolute,
            Fit
        }

        private ImageResizeMode ResizeMode
        {
            get
            {
                if (_info.Width != 0 && _info.Height == 0)
                {
                    return ImageResizeMode.ByWidth;
                }

                if (_info.Width == 0 && _info.Height != 0)
                {
                    return ImageResizeMode.ByHeight;
                }

                if (_info.Width != 0 && _info.Height != 0)
                {
                    return _info.MaxSize ? ImageResizeMode.Fit : ImageResizeMode.Absolute;
                }

                return ImageResizeMode.None;
            }
        }

        private string GetDynamicImageRelPath()
        {
            var newName = _info.ImageName.Replace("/", Path.DirectorySeparatorChar.ToString());
            var fileNameParts = newName.Split('.');
            if (!fileNameParts[fileNameParts.Length - 1].Equals(SVG_EXTENSION, StringComparison.InvariantCultureIgnoreCase))
            {
                fileNameParts[fileNameParts.Length - 1] = _info.FileType;
            }
            return "field_" + _info.AttrId + Path.DirectorySeparatorChar + string.Join(".", fileNameParts);
        }


        private Size GetDesiredImageSize(Size currentSize)
        {
            if (ResizeMode == ImageResizeMode.Absolute)
            {
                return new Size(_info.Width, _info.Height);
            }

            var widthCoefficient = _info.Width / (double)currentSize.Width;
            var heightCoefficient = _info.Height / (double)currentSize.Height;
            double targetCoefficient;

            // ReSharper disable once SwitchStatementMissingSomeCases
            switch (ResizeMode)
            {
                case ImageResizeMode.ByWidth:
                    targetCoefficient = widthCoefficient;
                    break;
                case ImageResizeMode.ByHeight:
                    targetCoefficient = heightCoefficient;
                    break;
                case ImageResizeMode.Fit:
                    targetCoefficient = heightCoefficient >= 1 && widthCoefficient >= 1 ? 1 : Math.Min(heightCoefficient, widthCoefficient);
                    break;
                default:
                    throw new Exception($"Incorrect resize mode: {ResizeMode}");
            }

            return new Size((int)(currentSize.Width * targetCoefficient), (int)(currentSize.Height * targetCoefficient));
        }

        private IImageEncoder Encoder
        {
            get
            {
                switch (_info.FileType)
                {
                    case JPG_EXTENSION:
                        return new JpegEncoder() { Quality = _info.Quality };
                    case GIF_EXTENSION:
                        return new GifEncoder();
                    case PNG_EXTENSION:
                        return new PngEncoder();
                    default:
                        return null;
                }
            }
        }


        public void Create()
        {
            var baseImagePath = (_info.ImagePath + Path.DirectorySeparatorChar + _info.ImageName).Replace("/", Path.DirectorySeparatorChar.ToString());
            if (!_fileSystem.FileExists(baseImagePath))
            {
                return;
            }

            var resultPath = Path.Combine(_info.ContentLibraryPath, GetDynamicImageRelPath());
            var resultDir = Path.GetDirectoryName(resultPath);

            if (!_info.ImageName.ToUpper().EndsWith(SVG_EXTENSION))
            {
                using (var image = _fileSystem.LoadImage(baseImagePath))
                {
                    var desiredSize = GetDesiredImageSize(new Size(image.Width, image.Height));
                    image.Mutate(x => x.Resize(desiredSize.Width, desiredSize.Height));

                    _fileSystem.CreateDirectory(resultDir);
                    _fileSystem.SaveImage(image, resultPath, Encoder);
                }
            }
            else
            {
                var xmlDocument = _fileSystem.LoadXml(baseImagePath);
                var documentElement = xmlDocument.DocumentElement;
                if (documentElement == null)
                {
                    return;
                }

                var width = 0;
                var height = 0;
                var widthAttr = documentElement.Attributes.GetNamedItem("width");
                if (widthAttr != null)
                {
                    width = int.Parse(Regex.Match(widthAttr.Value, "\\d+").Value);
                }

                var heightAttr = documentElement.Attributes.GetNamedItem("height");
                if (heightAttr != null)
                {
                    height = int.Parse(Regex.Match(heightAttr.Value, "\\d+").Value);
                }

                documentElement.SetAttribute("preserveAspectRatio", ResizeMode == ImageResizeMode.Fit ? "none" : "XMinYMin meet");

                var desiredImageSize = GetDesiredImageSize(new Size(width, height));
                if (widthAttr != null)
                {
                    widthAttr.Value = desiredImageSize.Width.ToString();
                }

                if (heightAttr != null)
                {
                    heightAttr.Value = desiredImageSize.Height.ToString();
                }

                _fileSystem.SaveXml(xmlDocument,resultPath);

            }
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
    }
}
