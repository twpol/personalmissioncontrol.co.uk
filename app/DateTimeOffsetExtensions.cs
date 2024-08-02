using System;

namespace app
{
    public static class DateTimeOffsetExtensions
    {
        public static string ToRfc3339(this DateTimeOffset value, DateTimeKind kind)
        {
            switch (kind)
            {
                case DateTimeKind.Utc:
                    return value.ToUniversalTime().ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'Z'");
                case DateTimeKind.Local:
                    return value.ToLocalTime().ToString("yyyy'-'MM'-'dd'T'HH':'mm':'sszzz");
                default:
                    return value.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'sszzz");
            }
        }
    }
}
