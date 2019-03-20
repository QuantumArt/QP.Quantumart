#if ASPNETCORE || NET4

using Quantumart.QPublishing.Database;
#if ASPNETCORE
using Microsoft.AspNetCore.Http;
#else
using System.Web;

#endif

// ReSharper disable once CheckNamespace
namespace Quantumart.QPublishing.Helpers
{
    public class TranslateManager
    {
        private readonly DBConnector _dbConnector;

        public TranslateManager(DBConnector dbConnector)
        {
            _dbConnector = dbConnector;
        }

        public string Translate(string phrase) => Translate(phrase, false);

#if ASPNETCORE // ReSharper disable once PossibleInvalidOperationException
        public string Translate(string phrase, bool forJavaScript) => _dbConnector.HttpContext.Session.GetInt32("CurrentLanguageID") == 1
            ? phrase
            : GetTranslation(int.Parse(GetPhraseId(phrase)), _dbConnector.HttpContext.Session.GetInt32("CurrentLanguageID").Value, phrase);
#else
        public string Translate(string phrase, bool forJavaScript) => HttpContext.Current.Session["CurrentLanguageID"].ToString() == "1"
            ? phrase
            : GetTranslation(int.Parse(GetPhraseId(phrase)), (int)HttpContext.Current.Session["CurrentLanguageID"], phrase);
#endif

        public string ReplaceForJavaScript(string input, bool forJavaScript) => forJavaScript
            ? input.Replace("\"", "\\\"").Replace("'", "\\'")
            : input;

        public string GetPhraseId(string phrase)
        {
            var dt = _dbConnector.GetCachedData($"select * from phrases where phrase_text = '{phrase.Replace("'", "''")}'");
            return dt.Rows.Count == 0 ? "0" : dt.Rows[0]["phraseId"].ToString();
        }

        public string GetTranslation(int phraseId, int languageId, string phraseText)
        {
            if (phraseId == 0)
            {
                return phraseText;
            }

            var dt = _dbConnector.GetCachedData($"select * from translations where phrase_id = {phraseId} and language_id = {languageId}");
            return dt.Rows.Count == 0 ? phraseText : dt.Rows[0]["phrase_translation"].ToString();
        }
    }
}
#endif
