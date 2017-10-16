using System;
using System.Collections;
using System.Globalization;
using System.Text.RegularExpressions;
using Quantumart.QPublishing.Database;
#if !ASPNETCORE
using System.Web;

#endif

// ReSharper disable once CheckNamespace
namespace Quantumart.QPublishing.Helpers
{
    public class QpTrace
    {
        private readonly DBConnector _dbConnector;

        public QpTrace(DBConnector dbConnector)
        {
            _dbConnector = dbConnector;
        }

        public string TraceString { get; set; }

        public int TraceId { get; set; }

        public string TraceStartText { get; set; }

        public DateTime TraceStartTime { get; set; }

        public int InitTrace(int pageId)
        {
            int functionReturnValue;
#if ASPNETCORE
            var query = _dbConnector.HttpContext.Request.QueryString.Value.Replace("'", "''");
#else
            var query = HttpContext.Current.Request.ServerVariables["QUERY_STRING"].Replace("'", "''");
#endif
            var traceSql = $"select * from page_trace where query_string = \'{query}\' and page_id = {pageId}";
            var dt = _dbConnector.GetRealData(traceSql);
            if (dt.Rows.Count == 0)
            {
                traceSql = $"insert into page_trace(query_string, page_id, traced) values (\'{query}\', {pageId}, \'{DateTime.Now:yyyy-MM-dd HH:mm:ss}\')";
                functionReturnValue = _dbConnector.InsertDataWithIdentity(traceSql);
            }
            else
            {
                traceSql = $"update page_trace set query_string = \'{query}\', page_id = {pageId}, traced = \'{DateTime.Now:yyyy-MM-dd HH:mm:ss}\' where page_id = {pageId}";
                _dbConnector.ProcessData(traceSql);

                traceSql = $"select trace_id from page_trace where query_string = \'{query}\' and page_id = {pageId}";
                dt = _dbConnector.GetRealData(traceSql);
                functionReturnValue = dt.Rows.Count != 0 ? DBConnector.GetNumInt(dt.Rows[0]["trace_id"]) : 0;
            }

            return functionReturnValue;
        }

        public void DoneTrace(TimeSpan duration, bool allowUserSessions, Hashtable values)
        {
#if ASPNETCORE
            var dp = new DebugPrint(_dbConnector);
#else
            var dp = new DebugPrint();
#endif
            var traceSession = allowUserSessions ? string.Empty : dp.GetSessionString();
            var traceCookies = dp.GetCookiesString();
            var traceValues = dp.GetSimpleDictionaryString(ref values);
            var traceSql = "update page_trace set SESSION = '" + traceSession.Replace("'", "''") + "', COOKIES = '" + traceCookies.Replace("'", "''") + "', [VALUES] = '" + traceValues.Replace("'", "''") + "', DURATION = " + Math.Round(duration.TotalMilliseconds).ToString(CultureInfo.InvariantCulture) + " where TRACE_ID = " + TraceId;
            _dbConnector.ProcessData(traceSql);
        }

        public void SaveTraceToDb(string trace, int traceId)
        {
            _dbConnector.ProcessData("delete from page_trace_format where trace_id = " + TraceId);
        }

        public void ExtractFirstLine(ref string traceString, ref string trace)
        {
            const string divider = "<br>";
            if (trace.IndexOf(divider, StringComparison.Ordinal) > 0)
            {
                var substrLength = trace.IndexOf(divider, StringComparison.Ordinal) + divider.Length;
                traceString = trace.Substring(1, substrLength);
                trace = trace.Substring(substrLength + 1, trace.Length - substrLength);
            }
            else
            {
                throw new Exception("Cannot complete saving trace. Remainder: " + trace);
            }
        }

        public bool MatchLine(string line, string pattrn, ref Match firstMatch)
        {
            var regEx = new Regex(pattrn, RegexOptions.IgnoreCase | RegexOptions.Multiline);
            var matches = regEx.Matches(line);
            var functionReturnValue = matches.Count > 0;
            foreach (Match match in matches)
            {
                firstMatch = match;
            }

            return functionReturnValue;
        }

        public bool MatchesLine(string line, string pattrn, out MatchCollection firstMatch)
        {
            var regEx = new Regex(pattrn, RegexOptions.IgnoreCase | RegexOptions.Multiline);
            var matches = regEx.Matches(line);
            var functionReturnValue = matches.Count > 0;
            firstMatch = matches;

            return functionReturnValue;
        }

        public int SaveLine(int traceId, int formatId, int parentId, int order, int traced, string defValuesString, string undefValuesString)
        {
            var parent = parentId == 0 ? "NULL" : parentId.ToString();
            var traceSql = "insert into page_trace_format(parent_trace_format_id, format_id, number, duration, trace_id) values (" + parent + ", " + formatId + ", " + order + ", " + traced + ", " + traceId + ")";
            var id = _dbConnector.InsertDataWithIdentity(traceSql);
            var functionReturnValue = id;

            SaveDefValues(defValuesString, id);
            SaveUndefValues(undefValuesString, id);

            return functionReturnValue;
        }

        public void SaveTraceLines(string trace, int traceId, int parent)
        {
            var traceString = string.Empty;
            Match firstMatch = null;

            var order = 1;
            while (!string.IsNullOrEmpty(trace))
            {
                var found = false;
                var childLines = "";
                ExtractFirstLine(ref traceString, ref trace);

                if (MatchLine(traceString, "(?<depth>[\\d])-(?<fid>[\\d]+)Def:(?<def>.*?)Undef:(?<undef>.*?)[\\d\\w]+<br>", ref firstMatch))
                {
                    var currentLevel = firstMatch.Groups["depth"].ToString();
                    var formatId = firstMatch.Groups["fid"].ToString();
                    var defValuesString = firstMatch.Groups["def"].ToString();
                    var undefValuesString = firstMatch.Groups["undef"].ToString();
                    string traced;

                    if (MatchLine(traceString, "started<br>", ref firstMatch))
                    {
                        while (!found)
                        {
                            ExtractFirstLine(ref traceString, ref trace);
                            if (MatchLine(traceString, currentLevel + "-" + formatId + "-(?<dur>[\\d]+)", ref firstMatch))
                            {
                                traced = firstMatch.Groups["dur"].ToString();
                                found = true;
                                var traceFormatId = SaveLine(traceId, int.Parse(formatId), parent, order, int.Parse(traced), defValuesString, undefValuesString);
                                SaveTraceLines(childLines, traceId, traceFormatId);
                            }
                            else
                            {
                                childLines = childLines + traceString;
                            }
                        }
                    }
                    else
                    {
                        MatchLine(traceString, "(?<dur>[\\d]+)ms", ref firstMatch);
                        traced = firstMatch.Groups["dur"].ToString();
                        SaveLine(traceId, int.Parse(formatId), parent, order, int.Parse(traced), defValuesString, undefValuesString);
                    }
                }

                order = order + 1;
            }
        }

        public void SaveDefValues(string defValuesString, int traceFormatId)
        {
            MatchesLine(defValuesString, "Value\\((?<key>.*?)\\)=(?<value>.*?);", out var matches);
            foreach (Match match in matches)
            {
                var key = match.Groups["key"].ToString().Replace("'", "''");
                var value = match.Groups["value"].ToString().Replace("'", "''");
                if (key.Length <= 50 && value.Length <= 255)
                {
                    var strSql = " INSERT INTO PAGE_TRACE_FORMAT_VALUES(trace_format_id, name, value, defined) ";
                    strSql = strSql + " VALUES(" + traceFormatId + ",'" + key + "', '" + value + "', " + 1 + ") ";
                    _dbConnector.ProcessData(strSql);
                }
            }
        }

        public void SaveUndefValues(string undefValuesString, int traceFormatId)
        {
            MatchesLine(undefValuesString, "Value\\((?<key>.*?)\\);", out var matches);
            foreach (Match match in matches)
            {
                var key = match.Groups["key"].ToString().Replace("'", "''");
                if (key.Length <= 50)
                {
                    var strSql = " INSERT INTO PAGE_TRACE_FORMAT_VALUES(trace_format_id, name, value, defined) ";
                    strSql = strSql + " VALUES(" + traceFormatId + ", '" + key + "', NULL , " + 0 + ") ";
                    _dbConnector.ProcessData(strSql);
                }
            }
        }
    }
}
