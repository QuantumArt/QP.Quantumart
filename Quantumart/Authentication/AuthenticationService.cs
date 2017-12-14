using Quantumart.QPublishing.Database;
using System;
using System.Data.SqlClient;

namespace Quantumart.QPublishing.Authentication
{
    public class AuthenticationService : IAuthenticationService
    {
        private const string ClearSIDQuery = "UPDATE sessions_log SET sid = NULL output inserted.USER_ID, inserted.SESSION_ID WHERE sid = @sid";

        private const string AuthenticationQuery = @"declare @date datetime = getdate() + cast(@interval as datetime);
if exists (select null from access_token where UserId = @userId and SessionId = @sessionId and Application = @application)
	update access_token set ExpirationDate = @date output inserted.$rowguid Token, inserted.ExpirationDate where UserId = @userId and SessionId = @sessionId and Application = @application
else
	insert into access_token(UserId, SessionId, Application, ExpirationDate) output inserted.$rowguid Token, inserted.ExpirationDate values(@userId, @sessionId, @application, @date)";

        private const string TokenQuery = "select UserId, ExpirationDate from access_token where $rowguid = @token and Application = @application and getdate() < ExpirationDate";

        private readonly DBConnector _connector;

        public AuthenticationService(DBConnector connector)
        {
            _connector = connector;
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

            var sqlCommand = new SqlCommand(ClearSIDQuery);
            sqlCommand.Parameters.AddWithValue("@sid", sid);
            var dt = _connector.GetRealData(sqlCommand);

            if (dt.Rows.Count > 0)
            {
                var userId = (int)(decimal)dt.Rows[0]["User_id"];
                var sessionId = (int)(decimal)dt.Rows[0]["Session_id"];

                sqlCommand = new SqlCommand(AuthenticationQuery);
                sqlCommand.Parameters.AddWithValue("@sid", sid);
                sqlCommand.Parameters.AddWithValue("@userId", userId);
                sqlCommand.Parameters.AddWithValue("@sessionId", sessionId);
                sqlCommand.Parameters.AddWithValue("@interval", interval);
                sqlCommand.Parameters.AddWithValue("@application", application);
                dt = _connector.GetRealData(sqlCommand);

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

            var sqlCommand = new SqlCommand(TokenQuery);
            sqlCommand.Parameters.AddWithValue("@token", token);
            sqlCommand.Parameters.AddWithValue("@application", application);

            var dt = _connector.GetRealData(sqlCommand);
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
    }
}
