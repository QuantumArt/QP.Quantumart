using System;

namespace Quantumart.QPublishing.Authentication
{
    public interface IAuthenticationService
    {
        AuthenticationToken Authenticate(string sid, TimeSpan interval, string application);
        AuthenticationToken Authenticate(Guid token, string application);
      
    }
}
