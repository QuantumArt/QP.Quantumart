using System;
using System.Runtime.Serialization;

// ReSharper disable once CheckNamespace
namespace Quantumart.QPublishing.Database
{
    public class QpInvalidAttributeException : ArgumentException
    {
        public QpInvalidAttributeException()
        {
        }

        public QpInvalidAttributeException(string message)
            : base(message)
        {
        }

        public QpInvalidAttributeException(string message, Exception inner)
            : base(message, inner)
        {
        }

        [Obsolete("Obsolete")]
        public QpInvalidAttributeException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
