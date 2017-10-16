using System.Collections;
using System.Text;
#if ASPNETCORE
using Microsoft.AspNetCore.Http;
using Quantumart.QPublishing.Database;
#else
using System.Collections.Specialized;
using System.Web;

#endif

// ReSharper disable once CheckNamespace
namespace Quantumart.QPublishing.Helpers
{
    public class DebugPrint
    {
#if ASPNETCORE
        private readonly DBConnector _dbConnector;

        public DebugPrint(DBConnector dbConnector)
        {
            _dbConnector = dbConnector;
        }
#endif

#if ASPNETCORE
        public string GetSessionString()
        {
            var result = new StringBuilder();
            foreach (var key in _dbConnector.HttpContext.Session.Keys)
            {
                result.Append(GetElementString(key, _dbConnector.HttpContext.Session.Get(key)));
            }

            return result.ToString();
        }
#else
        public string GetSessionString()
        {
            var result = new StringBuilder();
            foreach (string key in HttpContext.Current.Session.Contents)
            {
                result.Append(GetElementString(key, HttpContext.Current.Session[key]));
            }

            return result.ToString();
        }

#endif

        public string GetElementString(string key, object value) => key + "=" + value.GetType().FullName + "; ";

#if ASPNETCORE
        public string GetCookiesString()
        {
            var result = new StringBuilder();
            foreach (var key in _dbConnector.HttpContext.Request.Cookies.Keys)
            {
                result.Append(key + ": ");
                var cookie = _dbConnector.HttpContext.Request.Cookies[key];
                if (cookie != null)
                {
                    result.Append($"{key}={cookie};<br>");
                }
            }

            return result.ToString();
        }
#else
        public string GetCookiesString()
        {
            var result = new StringBuilder();
            foreach (string key in HttpContext.Current.Request.Cookies)
            {
                result.Append(key + ": ");
                var cookie = HttpContext.Current.Request.Cookies[key];
                if (cookie != null)
                {
                    if (cookie.HasKeys)
                    {
                        var subCookieValues = new NameValueCollection(cookie.Values);
                        foreach (string subkey in subCookieValues)
                        {
                            result.Append(subkey + "=" + cookie[subkey] + "; ");
                        }

                        result.Append("<br>");
                    }
                    else
                    {
                        result.Append(key + "=" + cookie.Value + ";<br>");
                    }
                }
            }

            return result.ToString();
        }
#endif

        public string GetSimpleDictionaryString(ref Hashtable values)
        {
            var result = new StringBuilder();
            foreach (string key in values.Keys)
            {
                result.Append(GetElementString(key, values[key]));
            }

            result.Append("<br>");
            return result.ToString();
        }
    }
}
