using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.StaticFiles;
using Minio;
using Minio.DataModel.Args;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats;

namespace Quantumart.QPublishing.FileSystem;


public class S3FileSystem : IFileSystem
{
    private readonly string _bucket;
    private readonly IMinioClient _client;

    public S3FileSystem(string endpoint, string accessKey, string secretKey, string bucket)
    {
        _bucket = bucket;
        _client = new MinioClient().WithEndpoint(endpoint).WithCredentials(accessKey, secretKey).Build();
    }

    public string CombinePath(string path, string name)
    {
        return FixPathSeparator(Path.Combine(path, name));
    }

    public string FixPathSeparator(string path)
    {
        return path.Replace(@"\", @"/");
    }
    public string RemoveLeadingSeparator(string path)
    {
        return path.StartsWith('/') ? path.Substring(1) : path;
    }

    public string AddTrailingSeparator(string path)
    {
        return path.EndsWith('/') ? path : path + '/';
    }


    public bool FileExists(string path)
    {
        try
        {
            var statObjectArgs = new StatObjectArgs()
                .WithBucket(_bucket)
                .WithObject(FixPathSeparator(path));
            var result = Task.Run(async () => await _client.StatObjectAsync(statObjectArgs)).Result;
            return result.ETag != null && result.Size != 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public void RemoveDirectory(string path)
    {
        path = FixPathSeparator(path);
        path = RemoveLeadingSeparator(path);
        path = AddTrailingSeparator(path);

        var listObjectArgs = new ListObjectsArgs()
            .WithBucket(_bucket)
            .WithPrefix(path)
            .WithVersions(false);

        var result = _client.ListObjectsEnumAsync(listObjectArgs)
            .ToBlockingEnumerable()
            .Where(n => !n.IsDir)
            .Select(n => n.Key)
            .ToList();

        var objectArgs = new RemoveObjectsArgs().WithBucket(_bucket).WithObjects(result);
        Task.Run(async () => await _client.RemoveObjectsAsync(objectArgs)).Wait();
    }

    public void CreateDirectory(string path)
    {
    }

    public void CopyFile(string sourceName, string destName)
    {
        if (!FileExists(sourceName))
        {
            return;
        }
        var sourceArgs = new CopySourceObjectArgs().WithBucket(_bucket).WithObject(FixPathSeparator(sourceName));
        var destArgs = new CopyObjectArgs().WithBucket(_bucket).WithObject(FixPathSeparator(destName))
            .WithCopyObjectSource(sourceArgs);

        Task.Run(async () => await _client.CopyObjectAsync(destArgs)).Wait();
    }

    private Stream GetS3Stream(string path)
    {
        return Task.Run(async () => await GetS3StreamAsync(path)).Result;
    }

    private async Task<Stream> GetS3StreamAsync(string path)
    {
        MemoryStream memoryStream = new MemoryStream();
        GetObjectArgs getObjectArgs = new GetObjectArgs()
            .WithBucket(_bucket)
            .WithObject(FixPathSeparator(path))
            .WithCallbackStream(stream => { stream.CopyTo(memoryStream); });
        await _client.GetObjectAsync(getObjectArgs);
        memoryStream.Position = 0;
        return memoryStream;
    }

    private void SetS3File(Stream stream, string path)
    {
        Task.Run(async () => await SetS3FileAsync(stream, path)).Wait();
    }

    private async Task SetS3FileAsync(Stream stream, string path)
    {
        new FileExtensionContentTypeProvider().TryGetContentType(path, out var contentType);

        var putObjectArgs = new PutObjectArgs()
            .WithBucket(_bucket)
            .WithObject(FixPathSeparator(path))
            .WithContentType(contentType ?? "application/octet-stream")
            .WithStreamData(stream)
            .WithObjectSize(stream.Length);

        var result = await _client.PutObjectAsync(putObjectArgs);
        if (result.Etag == null)
        {
            throw new Exception($"Error while saving file {path} to S3");
        }

    }

    public ImageInfo IdentifyImage(string path)
    {
        using var stream = GetS3Stream(path);
        return Image.Identify(stream);
    }

    public Image LoadImage(string path)
    {
        using var stream = GetS3Stream(path);
        return Image.Load(stream);
    }

    public Stream LoadStream(string path) => GetS3Stream(path);

    public void SaveStream(Stream stream, string path) => SetS3File(stream, path);

    public Task<Stream> LoadStreamAsync(string path) => GetS3StreamAsync(path);

    public Task SaveStreamAsync(Stream stream, string path) => SetS3FileAsync(stream, path);

    public void SaveImage(Image image, string path, IImageEncoder encoder = null)
    {
        using var stream = new MemoryStream();
        image.Save(stream, encoder ?? image.DetectEncoder(path));
        stream.Position = 0;
        SetS3File(stream, path);
    }

    public void SaveXml(XmlDocument xml, string fileName)
    {
        using var stream = new MemoryStream();
        xml.Save(stream);
        stream.Position = 0;
        SetS3File(stream, fileName);
    }

    public XmlDocument LoadXml(string fileName)
    {
        var xmlDocument = new XmlDocument();
        xmlDocument.Load(GetS3Stream(fileName));
        return xmlDocument;
    }
}
