using System.Text;

// ReSharper disable once CheckNamespace
namespace Quantumart.QPublishing.Helpers
{
    public static class StringBuilderExtensions
    {
        public static StringBuilder AppendFormatLine(this StringBuilder sb, string format, params object[] args) => sb.AppendFormat(format, args).AppendLine();
    }
}
