using System;

namespace Quantumart.QPublishing.Info
{
    public class VersionNotFoundException : ApplicationException
    {
        public VersionNotFoundException()
        {
        }

        public VersionNotFoundException(string message)
            : base(message)
        {
        }

        public VersionNotFoundException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}