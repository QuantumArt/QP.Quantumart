using System;

namespace Quantumart.QPublishing.Authentication
{
    public class AuthenticationToken
    {
        public int UserId { get; set; }
        public Guid Token { get; set; }
        public string Application { get; set; }
        public DateTime ExpirationDate { get; set; }
    }
}
