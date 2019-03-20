using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Quantumart.QPublishing.Helpers;
using Quantumart.QPublishing.Info;
using Quantumart.QPublishing.Resizer;

#if !ASPNETCORE
using System.IO;
using System.Web;
#endif

// ReSharper disable once CheckNamespace
namespace Quantumart.QPublishing.Database
{
    // ReSharper disable once InconsistentNaming
    public partial class DBConnector
    {
        private int FieldId(int contentId, string fieldName)
        {
            return contentId == 0 ? 0 : GetContentAttributeObjects(contentId).Where(n => n.Name.Equals(fieldName, StringComparison.InvariantCultureIgnoreCase)).Select(n => n.Id).FirstOrDefault();
        }

        // ReSharper disable once InconsistentNaming
        public int FieldID(int siteId, string contentName, string fieldName)
        {
            var contentId = GetDynamicContentId(contentName, 0, siteId);
            return FieldId(contentId, fieldName);
        }

        public string InputName(int siteId, string contentName, string inputName) => "field_" + FieldID(siteId, contentName, inputName);

        public string FieldName(int siteId, string contentName, string fieldName) => FieldName(FieldID(siteId, contentName, fieldName));

        internal string FieldName(int attributeId) => "field_" + attributeId;

#if ASPNETCORE || NETSTANDARD
        public int AddFormToContent(int siteId, string contentName, string statusName, ref Hashtable values, int contentItemId, bool updateEmpty)
        {
            var contentId = GetDynamicContentId(contentName, 0, siteId, out var actualSiteId);
            return AddFormToContent(actualSiteId, contentId, statusName, ref values, contentItemId, updateEmpty, 0);
        }
#else
        public int AddFormToContent(int siteId, string contentName, string statusName, ref Hashtable values, ref HttpFileCollection files, int contentItemId, bool updateEmpty)
        {
            var contentId = GetDynamicContentId(contentName, 0, siteId, out var actualSiteId);
            return AddFormToContent(actualSiteId, contentId, statusName, ref values, ref files, contentItemId, updateEmpty, 0);
        }
#endif

#if ASPNETCORE || NETSTANDARD
        public int AddFormToContent(int siteId, string contentName, string statusName, ref Hashtable values, int contentItemId) =>
            AddFormToContent(siteId, contentName, statusName, ref values, contentItemId, true);
#else
        public int AddFormToContent(int siteId, string contentName, string statusName, ref Hashtable values, ref HttpFileCollection files, int contentItemId) =>
            AddFormToContent(siteId, contentName, statusName, ref values, ref files, contentItemId, true);
#endif

#if ASPNETCORE || NETSTANDARD
        public int AddFormToContent(int siteId, string contentName, string statusName, ref Hashtable values) =>
            AddFormToContent(siteId, contentName, statusName, ref values, 0);
#else
        public int AddFormToContent(int siteId, string contentName, string statusName, ref Hashtable values, ref HttpFileCollection files) =>
            AddFormToContent(siteId, contentName, statusName, ref values, ref files, 0);
#endif

#if !ASPNETCORE && !NETSTANDARD
        public int AddFormToContent(int siteId, string contentName, string statusName, ref Hashtable values, int contentItemId)
        {
            HttpFileCollection files = null;
            return AddFormToContent(siteId, contentName, statusName, ref values, ref files, contentItemId);
        }
#endif

#if !ASPNETCORE && !NETSTANDARD
        public int AddFormToContent(int actualSiteId, int contentId, string statusName, ref Hashtable values, int contentItemId, bool updateEmpty, int attributeId, bool visible, bool archive, bool returnModified, ref DateTime modified)
        {
            HttpFileCollection files = null;
            return AddFormToContent(actualSiteId, contentId, statusName, ref values, ref files, contentItemId, updateEmpty, attributeId, visible, archive, returnModified, ref modified, true);
        }
#endif

#if ASPNETCORE || NETSTANDARD
        public int AddFormToContent(int actualSiteId, int contentId, string statusName, ref Hashtable values, int contentItemId, bool updateEmpty, int attributeId)
        {
            var modified = DateTime.MinValue;
            return AddFormToContent(actualSiteId, contentId, statusName, ref values, contentItemId, updateEmpty, attributeId, true, false, false, ref modified, false);
        }
#else
        public int AddFormToContent(int actualSiteId, int contentId, string statusName, ref Hashtable values, ref HttpFileCollection files, int contentItemId, bool updateEmpty, int attributeId)
        {
            var modified = DateTime.MinValue;
            return AddFormToContent(actualSiteId, contentId, statusName, ref values, ref files, contentItemId, updateEmpty, attributeId, true, false, false, ref modified, false);
        }
#endif

#if ASPNETCORE || NETSTANDARD
        public int AddFormToContent(int actualSiteId, int contentId, string statusName, ref Hashtable values, int contentItemId, bool updateEmpty, int attributeId, bool visible, bool archive, bool returnModified, ref DateTime modified) =>
            AddFormToContent(actualSiteId, contentId, statusName, ref values, contentItemId, updateEmpty, attributeId, visible, archive, returnModified, ref modified, true);
#else
        public int AddFormToContent(int actualSiteId, int contentId, string statusName, ref Hashtable values, ref HttpFileCollection files, int contentItemId, bool updateEmpty, int attributeId, bool visible, bool archive, bool returnModified, ref DateTime modified) =>
            AddFormToContent(actualSiteId, contentId, statusName, ref values, ref files, contentItemId, updateEmpty, attributeId, visible, archive, returnModified, ref modified, true);
#endif

#if ASPNETCORE || NETSTANDARD
        internal int AddFormToContent(int actualSiteId, int contentId, string statusName, ref Hashtable values, int contentItemId, bool updateEmpty, int attributeId, bool visible, bool archive, bool returnModified, ref DateTime modified, bool updateFlags) =>
            AddFormToContent(actualSiteId, contentId, statusName, ref values, contentItemId, updateEmpty, attributeId, visible, archive, LastModifiedBy, false, returnModified, ref modified, updateFlags, false);
#else
        internal int AddFormToContent(int actualSiteId, int contentId, string statusName, ref Hashtable values, ref HttpFileCollection files, int contentItemId, bool updateEmpty, int attributeId, bool visible, bool archive, bool returnModified, ref DateTime modified, bool updateFlags) =>
            AddFormToContent(actualSiteId, contentId, statusName, ref values, ref files, contentItemId, updateEmpty, attributeId, visible, archive, LastModifiedBy, false, returnModified, ref modified, updateFlags, false);
#endif

#if ASPNETCORE || NETSTANDARD
        internal int AddFormToContent(int actualSiteId, int contentId, string statusName, ref Hashtable values, int contentItemId, bool updateEmpty, int attributeId, bool visible, bool archive, int lastModifiedId, bool delayedSchedule, bool returnModified, ref DateTime modified, bool updateFlags, bool updateDelayed)
#else
        internal int AddFormToContent(int actualSiteId, int contentId, string statusName, ref Hashtable values, ref HttpFileCollection files, int contentItemId, bool updateEmpty, int attributeId, bool visible, bool archive, int lastModifiedId, bool delayedSchedule, bool returnModified, ref DateTime modified, bool updateFlags, bool updateDelayed)
#endif
        {
            if (GetContentVirtualType(contentId) > 0)
            {
                throw new Exception($"Cannot modify virtual content (ID = {contentId})");
            }

            int statusTypeId;
            var command = new SqlCommand();

            var sqlStringBuilder = new StringBuilder();
            var dynamicImagesList = new List<DynamicImageInfo>();
            sqlStringBuilder.AppendLine();

            if (!string.IsNullOrEmpty(statusName))
            {
                statusTypeId = GetStatusTypeId(actualSiteId, statusName);
                if (statusTypeId <= 0)
                {
                    throw new Exception($"Status '{statusName}' is not found on the site (ID = {actualSiteId})");
                }
            }
            else
            {
                if (contentItemId == 0)
                {
                    throw new ArgumentException("status_name parameter is null or empty");
                }

                var cmd = new SqlCommand("select status_type_id from content_item where content_item_id = @id")
                {
                    CommandType = CommandType.Text
                };

                cmd.Parameters.AddWithValue("@id", contentItemId);
                statusTypeId = (int)CastDbNull.To<decimal>(GetRealScalarData(cmd));
            }

#if !ASPNETCORE && !NETSTANDARD
            var actualFieldName = string.Empty;
            if (attributeId > 0)
            {
                actualFieldName = FieldName(attributeId);
            }

            //saving uploaded files first
            if (files != null)
            {
                HttpPostedFile file;
                if (attributeId > 0)
                {
                    //only one field is being updated
                    foreach (string item in files)
                    {
                        if (item == actualFieldName)
                        {
                            file = files[item];
                            CheckFileExistence(file, item, values, contentId);
                        }
                    }
                }
                else
                {
                    //all fields being updated
                    foreach (string item in files)
                    {
                        file = files[item];
                        if (!string.IsNullOrEmpty(file?.FileName))
                        {
                            CheckFileExistence(file, item, values, contentId);
                        }
                    }
                }
            }
#endif

            command.Parameters.Add("@statusId", SqlDbType.Decimal).Value = statusTypeId;
            command.Parameters.Add("@archive", SqlDbType.Decimal).Value = Convert.ToInt32(archive);
            command.Parameters.Add("@visible", SqlDbType.Decimal).Value = Convert.ToInt32(visible);
            command.Parameters.Add("@lastModifiedId", SqlDbType.Decimal).Value = Convert.ToInt32(lastModifiedId);
            command.Parameters.Add("@delayed", SqlDbType.Bit).Value = delayedSchedule;
            sqlStringBuilder.AppendLine("declare @splitted numeric;");

            const string strSql = "insert into content_item (CONTENT_ID, STATUS_TYPE_ID, VISIBLE, ARCHIVE, NOT_FOR_REPLICATION, LAST_MODIFIED_BY, SCHEDULE_NEW_VERSION_PUBLICATION) Values(@contentId, @statusId, @visible, @archive, 1, @lastModifiedId, @delayed);";
            if (contentItemId == 0)
            {
                command.Parameters.Add("@contentId", SqlDbType.Decimal).Value = contentId;
                sqlStringBuilder.AppendLine(GetSqlInsertDataWithIdentity(command, strSql));
            }
            else
            {
                command.Parameters.Add("@itemId", SqlDbType.Decimal).Value = contentItemId;
                sqlStringBuilder.AppendLine("exec create_content_item_version @lastModifiedId, @itemId;");

                var flagsString = updateFlags ? ", VISIBLE = @visible, ARCHIVE = @archive" : "";
                var delayedString = updateDelayed ? ", SCHEDULE_NEW_VERSION_PUBLICATION = @delayed" : "";
                sqlStringBuilder.AppendLine($"update content_item set modified = getdate(), last_modified_by = @lastModifiedId, STATUS_TYPE_ID = @statusId{flagsString}{delayedString} where content_item_id = @itemId;");
            }

            sqlStringBuilder.AppendLine("select @splitted = splitted from content_item where content_item_id = @itemId;");

            //inserting sql for updating field values (trigger on content_item already created all field in content_data)
            //also create a list of all dynamic images that need to be created
            //also create sql for updating dynamic image fields
            sqlStringBuilder.AppendLine(attributeId > 0
                ? GetSqlUpdateAttributes(command, contentItemId, new List<ContentAttribute> { GetContentAttributeObject(attributeId) }, values, true, dynamicImagesList, contentId, actualSiteId)
                : GetSqlUpdateAttributes(command, contentItemId, GetContentAttributeObjects(contentId), values, updateEmpty, dynamicImagesList, contentId, actualSiteId));

            //***********************
            // START *** update process
            //***********************
            CreateAllDynamicImages(dynamicImagesList);

            command.CommandText = sqlStringBuilder.ToString();

            var isOldArticle = contentItemId != 0;
            var content = GetContentObject(contentId);
            var isVersionOverflow = isOldArticle && content.UseVersionControl && GetVersionsCount(contentItemId) == content.MaxVersionNumber;
            var oldVersionId = isVersionOverflow ? GetEarliestVersionId(contentItemId) : 0;
            ProcessDataAsNewTransaction(command);

            var result = contentItemId != 0 ? contentItemId : GetIdentityId(command);
            if (returnModified)
            {
                var cmd = new SqlCommand("select modified From content_item where content_item_id = @itemId")
                {
                    CommandType = CommandType.Text
                };

                cmd.Parameters.AddWithValue("@itemId", result);
                modified = (DateTime)GetRealScalarData(cmd);
            }

            if (content.UseVersionControl)
            {
                CreateFilesVersions(result);
                if (isVersionOverflow)
                {
                    FileSystem.RemoveDirectory(GetVersionFolder(result, oldVersionId));
                }
            }

            return result;
        }

#if ASPNETCORE || NETSTANDARD
        public void UpdateContentItemField(int siteId, string contentName, string fieldName, int contentItemId, ref Hashtable values)
        {
            var contentId = GetDynamicContentId(contentName, 0, siteId, out var actualSiteId);
            var attributeId = FieldId(contentId, fieldName);
            if (attributeId > 0)
            {
                AddFormToContent(actualSiteId, contentId, string.Empty, ref values, contentItemId, true, attributeId);
            }
            else
            {
                HandleInvalidAttributeValue(fieldName, "Field '" + fieldName + "' does not exist in content: '" + contentName + "'");
            }
        }
#else
        public void UpdateContentItemField(int siteId, string contentName, string fieldName, int contentItemId, ref Hashtable values, ref HttpFileCollection files)
        {
            var contentId = GetDynamicContentId(contentName, 0, siteId, out var actualSiteId);
            var attributeId = FieldId(contentId, fieldName);
            if (attributeId > 0)
            {
                AddFormToContent(actualSiteId, contentId, string.Empty, ref values, ref files, contentItemId, true, attributeId);
            }
            else
            {
                HandleInvalidAttributeValue(fieldName, "Field '" + fieldName + "' does not exist in content: '" + contentName + "'");
            }
        }
#endif

#if !ASPNETCORE && !NETSTANDARD
        public void UpdateContentItemField(int siteId, string contentName, string fieldName, int contentItemId, ref Hashtable values)
        {
            HttpFileCollection files = null;
            UpdateContentItemField(siteId, contentName, fieldName, contentItemId, ref values, ref files);
        }
#endif

#if !ASPNETCORE && !NETSTANDARD
        public int UpdateContentItem(int siteId, int contentId, ref Hashtable values, int contentItemId)
        {
            HttpFileCollection files = null;
            return UpdateContentItem(siteId, contentId, ref values, ref files, contentItemId, true);
        }

        public int UpdateContentItem(int siteId, int contentId, ref Hashtable values, int contentItemId, bool updateEmpty)
        {
            HttpFileCollection files = null;
            return UpdateContentItem(siteId, contentId, ref values, ref files, contentItemId, updateEmpty, "");
        }

        public int UpdateContentItem(int siteId, int contentId, ref Hashtable values, int contentItemId, bool updateEmpty, string statusName)
        {
            HttpFileCollection files = null;
            return AddFormToContent(siteId, contentId, statusName, ref values, ref files, contentItemId, updateEmpty, 0);
        }
#endif

#if ASPNETCORE || NETSTANDARD
        public int UpdateContentItem(int siteId, int contentId, ref Hashtable values, int contentItemId) =>
            UpdateContentItem(siteId, contentId, ref values, contentItemId, true);
#else
        public int UpdateContentItem(int siteId, int contentId, ref Hashtable values, ref HttpFileCollection files, int contentItemId) =>
            UpdateContentItem(siteId, contentId, ref values, ref files, contentItemId, true);

#endif

#if ASPNETCORE || NETSTANDARD
        public int UpdateContentItem(int siteId, int contentId, ref Hashtable values, int contentItemId, bool updateEmpty) =>
            UpdateContentItem(siteId, contentId, ref values, contentItemId, updateEmpty, string.Empty);
#else
        public int UpdateContentItem(int siteId, int contentId, ref Hashtable values, ref HttpFileCollection files, int contentItemId, bool updateEmpty) =>
            UpdateContentItem(siteId, contentId, ref values, ref files, contentItemId, updateEmpty, string.Empty);
#endif

#if ASPNETCORE || NETSTANDARD
        public int UpdateContentItem(int siteId, int contentId, ref Hashtable values, int contentItemId, bool updateEmpty, string statusName) =>
            AddFormToContent(siteId, contentId, statusName, ref values, contentItemId, updateEmpty, 0);
#else
        public int UpdateContentItem(int siteId, int contentId, ref Hashtable values, ref HttpFileCollection files, int contentItemId, bool updateEmpty, string statusName) =>
            AddFormToContent(siteId, contentId, statusName, ref values, ref files, contentItemId, updateEmpty, 0);
#endif

        public void DeleteContentItem(int contentItemId)
        {
            ProcessData("DELETE FROM CONTENT_ITEM WITH(ROWLOCK) WHERE CONTENT_ITEM_ID = " + contentItemId);
        }

        private static string GetDataValueWithDefault(ContentAttribute attr, string data, bool isNewArticle)
        {
            var result = data;
            if (attr.DbTypeName == "DATETIME")
            {
                if (DateTime.TryParse(result, out DateTime _))
                {
                    var datedata = DateTime.Parse(result);
                    result = datedata.ToString("yyyy-MM-dd HH:mm:ss");
                }
            }
            else if (attr.Type == AttributeType.Numeric)
            {
                result = result.Replace(",", ".");
            }

            if (string.IsNullOrEmpty(result) && isNewArticle)
            {
                result = attr.DefaultValue;
            }

            return result;
        }

        private static void ValidateAttributeValue(ContentAttribute attr, string data, bool updateEmpty)
        {
            if (string.IsNullOrEmpty(data) && !updateEmpty)
            {
                return;
            }

            if (string.IsNullOrEmpty(data))
            {
                if (!updateEmpty)
                {
                    return;
                }

                if (attr.Required)
                {
                    HandleInvalidAttributeValue(attr.Name, "Attribute value is required");
                }
            }
            else
            {
                var ci = new CultureInfo("en-US");
                if (attr.LinkId == null && attr.BackRelation == null && (attr.DbTypeName == "DATETIME" && !DateTime.TryParse(data, out DateTime _) || attr.DbTypeName == "NUMERIC" && !double.TryParse(data, NumberStyles.Float, ci.NumberFormat, out var _)))
                {
                    HandleInvalidAttributeValue(attr.Name, $"Attribute value type is incorrect. Value: {data}");
                }

                if (attr.DbTypeName == "NVARCHAR")
                {
                    if (data.Length > attr.Size)
                    {
                        HandleInvalidAttributeValue(attr.Name,
                            $"Attribute value size is too long. Value: '{data}', allowed size: {attr.Size}");
                    }
                    else if (!string.IsNullOrEmpty(attr.InputMask) && !ValidateInputMask(attr.InputMask, data))
                    {
                        HandleInvalidAttributeValue(attr.Name,
                            $"Attribute value does not comply with input mask. Value: '{data}'");
                    }
                }
            }
        }

        private void ValidateUniqueConstraint(int constraintId, int id, IReadOnlyDictionary<string, string> dataValues, int attributeId)
        {
            if (attributeId != 0)
            {
                var dv = GetConstraints("ATTRIBUTE_ID = " + attributeId);
                if (dv.Count == 0)
                {
                    return;
                }

                constraintId = GetNumInt(dv[0]["CONSTRAINT_ID"]);
            }

            var dv2 = GetConstraints($"CONSTRAINT_ID = {constraintId}");
            var contentId = 0;
            if (dv2.Count > 0)
            {
                contentId = GetNumInt(dv2[0]["CONTENT_ID"]);
            }

            var cmd = new SqlCommand { CommandType = CommandType.Text };
            cmd.Parameters.Add("@itemId", SqlDbType.Decimal).Value = id;

            var sqls = new List<string>();
            var msgs = new List<string>();
            foreach (DataRowView drv in dv2)
            {
                var currentId = GetNumInt(drv["ATTRIBUTE_ID"]);
                var attr = GetContentAttributeObject(currentId);

                string value;
                if (attributeId > 0 && !dataValues.ContainsKey(FieldName(currentId)))
                {
                    var data = GetRealData($"EXEC sp_executesql N'select [{attr.Name}] as DATA from content_{contentId}_united where content_item_id = @itemId', N'@itemId NUMERIC', @itemId = {id}");
                    value = data.Rows[0]["DATA"].ToString();
                }
                else
                {
                    value = dataValues[FieldName(currentId)];
                }

                var paramName = "@" + FieldName(currentId);
                if (string.IsNullOrEmpty(value))
                {
                    sqls.Add($"([{attr.Name}] IS NULL)");
                }
                else
                {
                    sqls.Add($"([{attr.Name}] = {paramName})");
                    cmd.Parameters.Add(GetSqlParameter(paramName, attr, value));
                }

                msgs.Add($"[{attr.Name}] = '{value}'");
            }

            cmd.CommandText = $"SELECT CONTENT_ITEM_ID FROM CONTENT_{contentId}_UNITED WHERE {string.Join(" AND ", sqls.ToArray())} AND CONTENT_ITEM_ID <> @itemId";

            var dt = GetRealData(cmd);
            if (dt.Rows.Count != 0)
            {
                var conflictIds = new List<string> { id.ToString() };
                conflictIds.AddRange(from DataRow row in dt.Rows select row["CONTENT_ITEM_ID"].ToString());
                throw new QpInvalidAttributeException($"Unique constraint violation: {string.Join(", ", msgs.ToArray())}. Article IDs: {string.Join(", ", conflictIds.ToArray())}");
            }
        }

        private static bool ValidateInputMask(string inputMask, string data)
        {
            var regEx = new Regex(inputMask, RegexOptions.IgnoreCase);
            var matches = regEx.Matches(data);
            return matches.Count > 0;
        }

        private static void HandleInvalidAttributeValue(string attributeName, string comment)
        {
            throw new QpInvalidAttributeException($"Error updating attribute '{attributeName}': {comment}");
        }

        private static SqlParameter GetSqlParameter(string name, ContentAttribute attr, string value)
        {
            var result = new SqlParameter
            {
                ParameterName = name,
                SqlDbType = GetSqlParameterType(attr.DbTypeName),
                Value = value
            };

            if (attr.DbTypeName == "NVARCHAR")
            {
                result.Size = attr.Size;
            }

            if (attr.DbTypeName == "NUMERIC")
            {
                result.Precision = 18;
                result.Scale = (byte)attr.Size;
                result.Value = decimal.Parse(value, CultureInfo.InvariantCulture);
            }

            if (attr.DbTypeName == "DATETIME")
            {
                result.Value = DateTime.Parse(value, CultureInfo.InvariantCulture);
            }

            return result;
        }

        private static SqlDbType GetSqlParameterType(string dbType)
        {
            if (dbType == "NUMERIC")
            {
                return SqlDbType.Decimal;
            }

            if (dbType == "NVARCHAR")
            {
                return SqlDbType.NVarChar;
            }

            if (dbType == "NTEXT")
            {
                return SqlDbType.NText;
            }

            if (dbType == "DATETIME")
            {
                return SqlDbType.DateTime;
            }

            throw new Exception("Unknown DB type");
        }

#if !ASPNETCORE && !NETSTANDARD
        public string ShortFileName(string fileName)
        {
            var pos = fileName.IndexOf("\\", StringComparison.Ordinal);
            return fileName.Substring(pos + 1, fileName.Length - pos);
        }

        public void CheckFileExistence(HttpPostedFile fileToSave, string valueFieldName, Hashtable values, int contentId)
        {
            var attrId = GetValidContentAttributeId(valueFieldName, contentId);
            if (attrId == 0)
            {
                return;
            }

            var fileName = ShortFileName(fileToSave.FileName);
            var actualFieldName = FieldName(attrId);
            if (values[actualFieldName].ToString().ToLowerInvariant() != fileName.ToLowerInvariant())
            {
                return;
            }

            var fileDir = GetDirectoryForFileAttribute(attrId);
            if (File.Exists(fileDir + "\\" + fileName))
            {
                var dotPos = fileName.IndexOf(".", StringComparison.Ordinal);
                var fileNameWithoutExtension = fileName.Substring(1, dotPos - 1);
                var fileExtension = fileName.Substring(dotPos + 1, fileName.Length - dotPos);
                var index = 1;
                fileName = fileNameWithoutExtension + "[" + index + "]" + "." + fileExtension;

                while (true)
                {
                    if (!File.Exists(fileDir + "\\" + fileName))
                    {
                        break;
                    }

                    index = index + 1;
                    fileName = fileNameWithoutExtension + "[" + index + "]" + "." + fileExtension;
                }

                values[actualFieldName] = fileName;
            }

            fileToSave.SaveAs(fileDir + "\\" + fileName);
        }
#endif

        private void GetDynamicImagesForImage(int attributeId, int contentItemId, string imageName, ICollection<DynamicImageInfo> imagesList)
        {
            var imageAttr = GetContentAttributeObject(attributeId);
            var attrDir = GetDirectoryForFileAttribute(imageAttr.Id);
            var contentDir = GetContentLibraryDirectory(imageAttr.SiteId, imageAttr.ContentId);
            var attrs = GetContentAttributeObjects(imageAttr.ContentId).Where(n => n.RelatedImageId == imageAttr.Id);
            foreach (var attr in attrs)
            {
                var image = new DynamicImageInfo
                {
                    AttrId = attr.Id,
                    FileType = attr.DynamicImage.Type,
                    ImageName = imageName,
                    ImagePath = attrDir,
                    ContentLibraryPath = contentDir,
                    Width = attr.DynamicImage.Width,
                    Height = attr.DynamicImage.Height,
                    Quality = attr.DynamicImage.Quality,
                    MaxSize = attr.DynamicImage.MaxSize
                };

                image.DynamicUrl = !string.IsNullOrEmpty(image.ImageName) ? DynamicImage.GetDynamicImageRelUrl(image.ImageName, image.AttrId, image.FileType).Replace("'", "''") : "NULL";
                imagesList.Add(image);
            }
        }

        private void CreateAllDynamicImages(IEnumerable<DynamicImageInfo> imagesList)
        {
            foreach (var image in imagesList)
            {
                CreateDynamicImage(image);
            }
        }

        private void CreateDynamicImage(DynamicImageInfo image)
        {
            {
                if (!string.IsNullOrEmpty(image.ImageName))
                {
                    DynamicImageCreator.CreateDynamicImage(image);
                }
            }
        }

        public string GetSqlInsertDataWithIdentity(SqlCommand command, string queryString)
        {
            var idParam = command.Parameters.Add(IdentityParamString, SqlDbType.Decimal);
            idParam.Direction = ParameterDirection.Output;
            if (queryString.Trim().Substring(queryString.Length - 1) != ";")
            {
                queryString = queryString + ";";
            }

            return queryString + Environment.NewLine + "SELECT " + IdentityParamString + " = SCOPE_IDENTITY();" + Environment.NewLine;
        }

        private string GetSqlUpdateAttributes(SqlCommand command, int contentItemId, IEnumerable<ContentAttribute> attrs, Hashtable values, bool updateEmpty, List<DynamicImageInfo> dynamicImagesList, int contentId, int siteId)
        {
            var oSb = new StringBuilder();
            string inputName;

            var counter = 0;
            var isNewArticle = contentItemId == 0;
            var dataValues = new Dictionary<string, string>();
            var contentAttributes = attrs as ContentAttribute[] ?? attrs.ToArray();
            foreach (var attr in contentAttributes)
            {
                inputName = FieldName(attr.Id);
                var dataValue = GetDataValue(values, inputName);
                dataValue = GetDataValueWithDefault(attr, dataValue, isNewArticle);
                ValidateAttributeValue(attr, dataValue, updateEmpty);
                dataValues.Add(inputName, dataValue);
            }

            if (contentAttributes.Length == 1)
            {
                ValidateUniqueConstraint(0, contentItemId, dataValues, contentAttributes.Single().Id);
            }
            else
            {
                foreach (var constraintId in GetConstraints(contentId))
                {
                    ValidateUniqueConstraint(constraintId, contentItemId, dataValues, 0);
                }
            }

            var longUploadUrl = GetImagesUploadUrl(siteId);
            var longSiteLiveUrl = GetSiteUrl(siteId, true);
            var longSiteStageUrl = GetSiteUrl(siteId, false);
            if (UpdateManyToOne)
            {
                oSb.AppendLine("create table #resultIds (id numeric, attribute_id numeric not null, to_remove bit not null default 0);");
            }

            //create sql statements
            foreach (var attr in contentAttributes)
            {
                inputName = FieldName(attr.Id);
                var data = dataValues[inputName];
                if (updateEmpty || !IsEmptyData(data))
                {
                    if (attr.Type == AttributeType.String || attr.Type == AttributeType.Textbox || attr.Type == AttributeType.VisualEdit)
                    {
                        data = data
                            .Replace(longUploadUrl, UploadPlaceHolder)
                            .Replace(longSiteStageUrl, SitePlaceHolder)
                            .Replace(longSiteLiveUrl, SitePlaceHolder);
                    }

                    if (attr.Type != AttributeType.DynamicImage)
                    {
                        var dataParamName = $"@qp_data{counter}";
                        var blobDataParamName = $"@qp_blob_data{counter}";
                        var idParamName = $"@field{counter}";
                        var linkParamName = $"@link{counter}";
                        var linkValueParamName = $"@linkValue{counter}";
                        var backFieldParamName = $"@backField{counter}";
                        var backFieldValueParamName = $"@backFieldValue{counter}";

                        oSb.Append(" update content_data set modified = getdate(),");
                        oSb.Append($" data = {dataParamName}, blob_data = {blobDataParamName}, not_for_replication = 1 where content_item_id = @itemId and attribute_id = {idParamName};");
                        oSb.AppendLine(string.Empty);
                        if (attr.Type == AttributeType.Image)
                        {
                            GetDynamicImagesForImage(attr.Id, contentItemId, data, dynamicImagesList);
                        }

                        if (attr.LinkId.HasValue && UpdateManyToMany)
                        {
                            command.Parameters.AddWithValue(linkParamName, attr.LinkId);
                            command.Parameters.Add(new SqlParameter(linkValueParamName, SqlDbType.NVarChar, -1) { Value = !string.IsNullOrEmpty(data) ? (object)data : DBNull.Value });
                            oSb.AppendLine($"exec qp_update_m2m @itemId, {linkParamName}, {linkValueParamName}, @splitted;");
                        }

                        if (attr.BackRelation != null && UpdateManyToOne)
                        {
                            command.Parameters.AddWithValue(backFieldParamName, attr.BackRelation.Id);
                            command.Parameters.AddWithValue(backFieldParamName, attr.BackRelation.Id);
                            command.Parameters.Add(new SqlParameter(backFieldValueParamName, SqlDbType.NVarChar, -1) { Value = !string.IsNullOrEmpty(data) ? (object)data : DBNull.Value });
                            oSb.AppendLine($"exec qp_update_m2o @itemId, {backFieldParamName}, {backFieldValueParamName};");
                        }

                        command.Parameters.AddWithValue(idParamName, attr.Id);
                        object dataValue = DBNull.Value;
                        object blobDataValue = DBNull.Value;
                        if (attr.LinkId.HasValue)
                        {
                            dataValue = attr.LinkId.Value;
                        }
                        else if (attr.BackRelation != null)
                        {
                            dataValue = attr.BackRelation.Id;
                        }
                        else if (attr.IsBlob && !IsEmptyData(data))
                        {
                            blobDataValue = data;
                        }
                        else if (!IsEmptyData(data))
                        {
                            dataValue = data;
                        }

                        command.Parameters.Add(new SqlParameter(dataParamName, SqlDbType.NVarChar, 3500) { Value = dataValue });
                        command.Parameters.Add(new SqlParameter(blobDataParamName, SqlDbType.NVarChar, -1) { Value = blobDataValue });
                    }
                }

                counter = counter + 1;
            }

            oSb.AppendLine(GetSqlDynamicImages(command, dynamicImagesList));
            oSb.AppendLine(" exec qp_replicate @itemId;");

            if (UpdateManyToOne)
            {
                oSb.AppendLine("exec qp_update_m2o_final @itemId;");
                oSb.AppendLine("drop table #resultIds;");
            }

            return oSb.ToString();
        }

        private static string GetSqlDynamicImages(SqlCommand command, IEnumerable<DynamicImageInfo> imagesList)
        {
            var i = 0;
            var sb = new StringBuilder(string.Empty);
            foreach (var image in imagesList)
            {
                var dataParamName = $"@url{i}";
                var fieldParamName = $"@dynamic{i}";
                if (string.Equals(image.DynamicUrl, "NULL"))
                {
                    command.Parameters.Add(dataParamName, SqlDbType.NVarChar).Value = DBNull.Value;
                }
                else
                {
                    command.Parameters.Add(dataParamName, SqlDbType.NVarChar).Value = image.DynamicUrl;
                }

                command.Parameters.Add(fieldParamName, SqlDbType.Decimal).Value = image.AttrId;
                sb.Append($"update content_data set data = {dataParamName}, modified = getdate(), not_for_replication = 1 where content_item_id = @itemId and attribute_id = {fieldParamName};");
                sb.AppendLine();
                i = i + 1;
            }

            return sb.ToString();
        }

        private static string GetDataValue(Hashtable values, string name)
        {
            var data = values.ContainsKey(name) ? (values[name] ?? string.Empty) : string.Empty;
            return data.ToString();
        }

        private static bool IsEmptyData(object data)
        {
            if (data == null)
            {
                return true;
            }

            var dataString = data.ToString();
            return string.IsNullOrEmpty(dataString) || string.Equals(dataString, "NULL");
        }

        internal List<int> GetConstraints(int contentId)
        {
            var dv = GetConstraints($"CONTENT_ID = {contentId}");
            var list = new List<int>();
            foreach (DataRowView drv in dv)
            {
                var id = GetNumInt(drv["CONSTRAINT_ID"]);
                if (!list.Contains(id))
                {
                    list.Add(id);
                }
            }

            return list;
        }
    }
}
