using System;
using System.Diagnostics;
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
            Activity.Current?.SetBaggage("session.id", context.Session.Id);
            if (DateTimeOffset.TryParse(context.Session.GetString("SessionSlidingExpiry"), out var expiry) &&
                DateTimeOffset.TryParse(context.Session.GetString("SessionSlidingRefresh"), out var refresh))
            {
                if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug($"Got expiry of {expiry} and refresh of {refresh}");
                Activity.Current?.SetTag("session.expiry", expiry.ToString("o"));
                Activity.Current?.SetTag("session.refresh", refresh.ToString("o"));
                if (DateTimeOffset.Now > refresh) ExtendExpiry(context);
            }
            else
            {
                if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug($"Missing or invalid expiry/refresh");
                ExtendExpiry(context);
            }
            await Next(context);
        }

        void ExtendExpiry(HttpContext context)
        {
            var expiry = DateTimeOffset.UtcNow.Add(IdleTimeout);
            var refresh = DateTimeOffset.UtcNow.Add(IdleTimeout / 2);
            var cookieOptions = CookieBuilder.Build(context);
            if (CookieBuilder.Name == null) return;

            var value = context.Request.Cookies[CookieBuilder.Name];
            if (value == null) return;

            context.Response.Cookies.Append(CookieBuilder.Name, value, cookieOptions);
            context.Session.SetString("SessionSlidingExpiry", expiry.ToString("o"));
            context.Session.SetString("SessionSlidingRefresh", refresh.ToString("o"));
            if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug($"Extended expiry to {expiry} and refresh to {refresh}");
            Activity.Current?.SetTag("session.expiry_new", expiry.ToString("o"));
            Activity.Current?.SetTag("session.refresh_new", refresh.ToString("o"));
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
