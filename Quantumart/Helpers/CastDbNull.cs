using System;

// ReSharper disable once CheckNamespace
namespace Quantumart.QPublishing.Helpers
{
    internal static class CastDbNull
    {
        public static T To<T>(object value, T defaultValue) => value != DBNull.Value ? (T)value : defaultValue;

        public static T To<T>(object value) => To(value, default(T));
    }
}
