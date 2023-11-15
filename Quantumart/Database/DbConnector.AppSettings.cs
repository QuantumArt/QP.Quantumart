using System;
using System.ComponentModel;
using System.Data;
using System.Globalization;

namespace Quantumart.QPublishing.Database;

public partial class DBConnector
{
    public T GetSettingByName<T>(string name)
    {
        DataTable result = GetCachedData($"select VALUE from APP_SETTINGS where {SqlQuerySyntaxHelper.EscapeEntityName(DatabaseType, "KEY")} = '{name}'");

        switch (result.Rows.Count)
        {
            case 0:
                throw new InvalidOperationException($"Unable to find qp setting with name {name}");
            case > 1:
                throw new InvalidOperationException($"There was found {result.Rows.Count} values for key {name} in qp settings. Can't decide which one to use");
            default:
            {
                TypeConverter converter = TypeDescriptor.GetConverter(typeof(T));

                return (T)converter.ConvertFromString(null, CultureInfo.InvariantCulture, result.Rows[0]["VALUE"].ToString());
            }
        }
    }
}
