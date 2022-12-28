using System.Collections;
using System.Text;
using Microsoft.AspNetCore.Http;
using Quantumart.QPublishing.Database;

// ReSharper disable once CheckNamespace
namespace Quantumart.QPublishing.Helpers
{
    public class DebugPrint
    {
        private readonly DBConnector _dbConnector;

        public DebugPrint(DBConnector dbConnector)
        {
            _dbConnector = dbConnector;
        }

        public string GetSessionString()
        {
            var result = new StringBuilder();
            if (_dbConnector.HttpContext != null)
            {
                foreach (var key in _dbConnector.HttpContext.Session.Keys)
                {
                    result.Append(GetElementString(key, _dbConnector.HttpContext.Session.Get(key)));
                }
            }

            return result.ToString();
        }

        public string GetElementString(string key, object value) => key + "=" + value.GetType().FullName + "; ";

        public string GetCookiesString()
        {
            var result = new StringBuilder();
            if (_dbConnector.HttpContext != null)
            {
                foreach (var key in _dbConnector.HttpContext.Request.Cookies.Keys)
                {
                    result.Append(key + ": ");
                    var cookie = _dbConnector.HttpContext.Request.Cookies[key];
                    if (cookie != null)
                    {
                        result.Append($"{key}={cookie};<br>");
                    }
                }
            }

            return result.ToString();
        }

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
