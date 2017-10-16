using System.Text.RegularExpressions;

namespace Quantumart.Helpers
{
    public static class Utils
    {
        private static readonly Regex CleanRegex = new Regex("(create |delete |update |grant |revoke |drop |alter |create |backup |restore |sp_|truncate |set |exec |execute |insert |dbcc |deny |union )", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static string CleanSql(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            int count;
            do
            {
                text = CleanRegex.Replace(text, string.Empty);

                var matches = CleanRegex.Matches(text);
                count = matches.Count;
            } while (count != 0);

            return text;
        }
    }
}
