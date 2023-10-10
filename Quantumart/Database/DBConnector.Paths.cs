using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

// ReSharper disable once CheckNamespace
namespace Quantumart.QPublishing.Database
{
    // ReSharper disable once InconsistentNaming
    public partial class DBConnector
    {
        internal static string CombineWithoutDoubleSlashes(string first, string second)
        {
            if (string.IsNullOrEmpty(second))
            {
                return first;
            }

            var sb = new StringBuilder();
            sb.Append(first.Replace(@":/", @"://").Replace(@":///", @"://").TrimEnd('/'));
            sb.Append("/");
            sb.Append(second.Replace("//", "/").TrimStart('/'));

            return sb.ToString();
        }

        private static string ConvertUrlToSchemaInvariant(string prefix) => Regex.Replace(
            prefix,
            "^http(s?):",
            string.Empty,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        public string GetImagesUploadUrlRel(int siteId) => GetUploadUrlRel(siteId) + "images";

        public string GetImagesUploadUrl(int siteId) => GetUploadUrl(siteId) + "images";

        public string GetImagesUploadUrl(int siteId, bool asShortAsPossible) => GetUploadUrl(siteId, asShortAsPossible, false) + "images";

        public string GetImagesUploadUrl(int siteId, bool asShortAsPossible, bool removeSchema) => GetUploadUrl(siteId, asShortAsPossible, removeSchema) + "images";

        public string GetUploadDir(int siteId)
        {
            var site = GetSite(siteId);
            return site == null ? string.Empty : site.UploadDir;
        }

        public string GetUploadUrl(int siteId) => GetUploadUrl(siteId, false);

        public string GetUploadUrlRel(int siteId)
        {
            var site = GetSite(siteId);
            return site == null ? string.Empty : site.UploadUrl;
        }

        public string GetUploadUrl(int siteId, bool asShortAsPossible) => GetUploadUrl(siteId, asShortAsPossible, false);

        public string GetUploadUrl(int siteId, bool asShortAsPossible, bool removeSchema)
        {
            var site = GetSite(siteId);
            var sb = new StringBuilder();
            if (site != null)
            {
                var prefix = GetUploadUrlPrefix(siteId);
                if (!string.IsNullOrEmpty(prefix))
                {
                    if (removeSchema)
                    {
                        prefix = ConvertUrlToSchemaInvariant(prefix);
                    }

                    sb.Append(prefix);
                }
                else
                {
                    if (!asShortAsPossible)
                    {
                        sb.Append(!removeSchema ? "http://" : "//");

                        sb.Append(GetDns(siteId, true));
                    }
                }

                sb.Append(GetUploadUrlRel(siteId));
            }

            return sb.ToString();
        }

        public string GetUploadUrlPrefix(int siteId)
        {
            var site = GetSite(siteId);
            return site != null && site.UseAbsoluteUploadUrl ? site.UploadUrlPrefix : string.Empty;
        }

        public string GetActualSiteUrl(int siteId) => GetSiteUrl(siteId, IsLive(siteId));

        public string GetSiteUrl(int siteId, bool isLive)
        {
            var site = GetSite(siteId);
            var sb = new StringBuilder();
            if (site != null)
            {
                sb.Append("http://");
                sb.Append(GetDns(siteId, isLive));
                sb.Append(GetSiteUrlRel(siteId, isLive));
            }

            return sb.ToString();
        }

        public string GetSiteUrlRel(int siteId, bool isLive)
        {
            var site = GetSite(siteId);
            return site == null ? string.Empty : (isLive ? site.LiveVirtualRoot : site.StageVirtualRoot);
        }

        public string GetActualDns(int siteId) => GetDns(siteId, IsLive(siteId));

        public string GetDns(int siteId, bool isLive)
        {
            var site = GetSite(siteId);
            return site == null ? string.Empty : (isLive || string.IsNullOrEmpty(site.StageDns) ? site.Dns : site.StageDns);
        }

        public string GetSiteLibraryDirectory(int siteId) => GetUploadDir(siteId) + Path.DirectorySeparatorChar + "images";

        public string GetSiteDirectory(int siteId, bool isLive) => GetSiteDirectory(siteId, isLive, false);

        public string GetSiteDirectory(int siteId, bool isLive, bool isTest)
        {
            var site = GetSite(siteId);
            if (site == null)
            {
                return string.Empty;
            }

            if (isLive && isTest)
            {
                return string.IsNullOrEmpty(site.TestDirectory) ? site.TestDirectory : string.Empty;
            }

            return isLive ? site.LiveDirectory : site.StageDirectory;
        }

        public string GetSiteLiveDirectory(int siteId) => GetSiteDirectory(siteId, true);

        public string GetContentLibraryDirectory(int siteId, int contentId)
        {
            return GetUploadDir(siteId) + Path.DirectorySeparatorChar + "contents" + Path.DirectorySeparatorChar + contentId;
        }

        public string GetContentLibraryDirectory(int contentId) => GetContentLibraryDirectory(GetSiteIdByContentId(contentId), contentId);

        public string GetContentUploadUrl(int siteId, string contentName)
        {
            var contentId = GetDynamicContentId(contentName, 0, siteId, out var targetSiteId);
            if (targetSiteId == 0)
            {
                targetSiteId = siteId;
            }

            return GetContentUploadUrlByID(targetSiteId, contentId);
        }

        // ReSharper disable once InconsistentNaming
        public string GetContentUploadUrlByID(int siteId, long contentId) => GetContentUploadUrlByID(siteId, contentId, true);

        // ReSharper disable once InconsistentNaming
        public string GetContentUploadUrlByID(int siteId, long contentId, bool asShortAsPossible) => GetContentUploadUrlByID(siteId, contentId, asShortAsPossible, false);

        // ReSharper disable once InconsistentNaming
        public string GetContentUploadUrlByID(int siteId, long contentId, bool asShortAsPossible, bool removeSchema)
        {
            var site = GetSite(siteId);
            var sb = new StringBuilder();
            if (site != null)
            {
                sb.Append(GetUploadUrl(siteId, asShortAsPossible, removeSchema));
                if (sb[sb.Length - 1] != '/')
                {
                    sb.Append("/");
                }

                sb.Append("contents/");
                sb.Append(contentId);
            }

            return sb.ToString();
        }

        private string GetFieldSubFolder(int attrId, bool revertSlashes)
        {
            var result = GetContentAttributeObject(attrId).SubFolder;
            if (!string.IsNullOrEmpty(result))
            {
                result = @"\" + result;
                if (revertSlashes)
                {
                    result = result.Replace(@"\", @"/");
                }
            }

            return result;
        }

        public string GetFieldSubFolder(int attrId) => GetFieldSubFolder(attrId, !RuntimeInformation.IsOSPlatform(OSPlatform.Windows));

        public string GetDirectoryForFileAttribute(int attrId)
        {
            var attr = GetContentAttributeObject(attrId);
            if (attr == null)
            {
                throw new Exception("No File/Image Attribute found with attribute_id=" + attrId);
            }

            var baseDir = attr.UseSiteLibrary ? GetSiteLibraryDirectory(attr.SiteId) : GetContentLibraryDirectory(attr.SiteId, attr.ContentId);
            return baseDir + GetFieldSubFolder(attrId);
        }

        public string GetFieldSubUrl(int attrId) => GetFieldSubFolder(attrId, true);

        public string GetFieldUploadUrl(string fieldName, int contentId) => GetFieldUploadUrl(0, fieldName, contentId);

        public string GetFieldUploadUrl(int siteId, string fieldName, int contentId) => GetUrlForFileAttribute(FieldId(contentId, fieldName));

        public string GetUrlForFileAttribute(int fieldId) => GetUrlForFileAttribute(fieldId, true);

        public string GetUrlForFileAttribute(int fieldId, bool asShortAsPossible) => GetUrlForFileAttribute(fieldId, asShortAsPossible, false);

        public string GetUrlForFileAttribute(int fieldId, bool asShortAsPossible, bool removeSchema)
        {
            if (fieldId == 0)
            {
                return string.Empty;
            }

            var attr = GetContentAttributeObject(fieldId);
            if (attr == null)
            {
                return string.Empty;
            }

            int sourceContentId, sourceFieldId;
            bool useSiteLibrary;
            if (attr.SourceAttribute == null)
            {
                sourceContentId = attr.ContentId;
                sourceFieldId = attr.Id;
                useSiteLibrary = attr.UseSiteLibrary;
            }
            else
            {
                sourceContentId = attr.SourceAttribute.ContentId;
                sourceFieldId = attr.SourceAttribute.Id;
                useSiteLibrary = attr.SourceAttribute.UseSiteLibrary;
            }

            var baseUrl = useSiteLibrary ? GetImagesUploadUrl(attr.SiteId, asShortAsPossible, removeSchema) : GetContentUploadUrlByID(attr.SiteId, sourceContentId, asShortAsPossible, removeSchema);
            return CombineWithoutDoubleSlashes(baseUrl, GetFieldSubUrl(sourceFieldId));
        }
    }
}
