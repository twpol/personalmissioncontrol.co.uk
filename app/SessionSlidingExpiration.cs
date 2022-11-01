using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace app
{
    public class SessionSlidingExpirationMiddleware
    {
        readonly RequestDelegate Next;
        readonly ILogger<SessionSlidingExpirationMiddleware> Logger;
        readonly TimeSpan IdleTimeout;
        readonly CookieBuilder CookieBuilder;

        public SessionSlidingExpirationMiddleware(RequestDelegate next, ILogger<SessionSlidingExpirationMiddleware> logger, IOptions<SessionOptions> options)
        {
            Next = next;
            Logger = logger;
            IdleTimeout = options.Value.IdleTimeout;
            CookieBuilder = options.Value.Cookie;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            EnsureExpiry(context);
            if (DateTimeOffset.TryParse(context.Session.GetString("SessionSlidingExpiry"), out var expiry))
            {
                if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug($"Got expiry of {expiry}");
                if (DateTimeOffset.Now > expiry) ExtendExpiry(context);
            }
            await Next(context);
        }

        void EnsureExpiry(HttpContext context)
        {
            if (!context.Session.TryGetValue("SessionSlidingExpiry", out var _))
            {
                var expiry = DateTimeOffset.UtcNow.Add(IdleTimeout / 2);
                context.Session.SetString("SessionSlidingExpiry", expiry.ToString("o"));
                if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug($"Set expiry to {expiry}");
            }
        }

        void ExtendExpiry(HttpContext context)
        {
            var expiry = DateTimeOffset.UtcNow.Add(IdleTimeout / 2);
            var cookieOptions = CookieBuilder.Build(context);
            context.Response.Cookies.Append(CookieBuilder.Name, context.Request.Cookies[CookieBuilder.Name], cookieOptions);
            context.Session.SetString("SessionSlidingExpiry", expiry.ToString("o"));
            if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug($"Extended expiry to {expiry}");
        }
    }

    public static class SessionSlidingExpirationMiddlewareExtensions
    {
        public static IApplicationBuilder UseSessionSlidingExpiration(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<SessionSlidingExpirationMiddleware>();
        }
    }
}
