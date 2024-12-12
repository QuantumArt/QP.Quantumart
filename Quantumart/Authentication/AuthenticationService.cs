using Quantumart.QPublishing.Database;
using System;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using QP.ConfigurationService.Models;

namespace Quantumart.QPublishing.Authentication
{
    public class AuthenticationService : IAuthenticationService
    {
        private readonly string _clearSIDQuery;
        private readonly string _sqlAuthenticationQuery = @"declare @date datetime = getdate() + cast(@interval as datetime);
if exists (select null from access_token where UserId = @userId and SessionId = @sessionId and Application = @application)
	update access_token set ExpirationDate = @date output inserted.$rowguid Token, inserted.ExpirationDate where UserId = @userId and SessionId = @sessionId and Application = @application
else
	insert into access_token(UserId, SessionId, Application, ExpirationDate) output inserted.$rowguid Token, inserted.ExpirationDate values(@userId, @sessionId, @application, @date)";

        private readonly string _pgAuthenticationQuery = @"INSERT INTO access_token(UserId, SessionId, Application, ExpirationDate)
            VALUES(@userId, @sessionId, @application, NOW() + CAST(@interval AS time))
            ON CONFLICT (UserId, SessionId, Application) DO
                UPDATE SET ExpirationDate = NOW() + CAST(@interval AS time)
            RETURNING Token, ExpirationDate;";

        private readonly string _tokenQuery;

        private const string SettingsQuery = "select use_tokens from db";

        private readonly DBConnector _connector;

        public AuthenticationService(DBConnector connector)
        {
            _connector = connector;
            _clearSIDQuery = $@"UPDATE sessions_log SET sid = NULL {
                    SqlQuerySyntaxHelper.Output(_connector.DatabaseType, new[] { "USER_ID", "SESSION_ID" })
                } WHERE sid = @sid {SqlQuerySyntaxHelper.Returning(_connector.DatabaseType, new[] { "USER_ID", "SESSION_ID" })}";
            _tokenQuery = $@"select UserId, ExpirationDate
                    from access_token
                    where Token = @token and Application = @application and {
                            SqlQuerySyntaxHelper.Now(_connector.DatabaseType)
                         } < ExpirationDate";
        }
        public AuthenticationToken Authenticate(string sid, TimeSpan interval, string application)
        {
            if (sid == null)
            {
                throw new ArgumentNullException(nameof(sid));
            }

            if (application == null)
            {
                throw new ArgumentNullException(nameof(application));
            }

            CheckSettings();
            var dbCommand = _connector.CreateDbCommand(_clearSIDQuery);
            dbCommand.Parameters.AddWithValue("@sid", sid);
            var dt = _connector.GetRealData(dbCommand);

            if (dt.Rows.Count > 0)
            {
                var userId = (int)(decimal)dt.Rows[0]["User_id"];
                var sessionId = (int)(decimal)dt.Rows[0]["Session_id"];

                dbCommand = _connector.CreateDbCommand(_connector.DatabaseType == DatabaseType.Postgres ? _pgAuthenticationQuery : _sqlAuthenticationQuery);
                dbCommand.Parameters.AddWithValue("@userId", userId);
                dbCommand.Parameters.AddWithValue("@sessionId", sessionId);
                dbCommand.Parameters.AddWithValue("@interval", interval);
                dbCommand.Parameters.AddWithValue("@application", application);
                dt = _connector.GetRealData(dbCommand);

                if (dt.Rows.Count > 0)
                {
                    var token = (Guid)dt.Rows[0]["Token"];
                    var date = (DateTime)dt.Rows[0]["ExpirationDate"];

                    return new AuthenticationToken
                    {
                        UserId = userId,
                        Token = token,
                        ExpirationDate = date,
                        Application = application
                    };
                }
            }

            return null;
        }

        public AuthenticationToken Authenticate(Guid token, string application)
        {
            if (application == null)
            {
                throw new ArgumentNullException(nameof(application));
            }

            CheckSettings();

            var dbCommand = _connector.CreateDbCommand(_tokenQuery);
            dbCommand.Parameters.AddWithValue("@token", token);
            dbCommand.Parameters.AddWithValue("@application", application);

            var dt = _connector.GetRealData(dbCommand);
            if (dt.Rows.Count > 0)
            {
                var userId = (int)dt.Rows[0]["UserId"];
                var date = (DateTime)dt.Rows[0]["ExpirationDate"];

                return new AuthenticationToken
                {
                    UserId = userId,
                    Token = token,
                    ExpirationDate = date,
                    Application = application
                };
            }

            return null;
        }

        private void CheckSettings()
        {
            var dbCommand = _connector.CreateDbCommand(SettingsQuery);

            if (!(bool)_connector.GetRealScalarData(dbCommand))
            {
                throw new AuthenticationException("Option 'Use authentication tokens' must be on");
            }
        }
    }
}
