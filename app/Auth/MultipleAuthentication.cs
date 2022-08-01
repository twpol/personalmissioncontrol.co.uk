using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace app.Auth
{
    public static class MultipleAuthenticationDefaults
    {
        public const string AuthenticationScheme = "Multiple";
        public const string DisplayName = "Multiple";
    }

    public static class MultipleAuthenticationExtensions
    {
        public static AuthenticationBuilder AddMultiple(this AuthenticationBuilder builder) => builder.AddMultiple(MultipleAuthenticationDefaults.AuthenticationScheme, _ => { });
        public static AuthenticationBuilder AddMultiple(this AuthenticationBuilder builder, Action<MultipleAuthenticationOptions> configureOptions) => builder.AddMultiple(MultipleAuthenticationDefaults.AuthenticationScheme, configureOptions);
        public static AuthenticationBuilder AddMultiple(this AuthenticationBuilder builder, string authenticationScheme, Action<MultipleAuthenticationOptions> configureOptions) => builder.AddMultiple(authenticationScheme, MultipleAuthenticationDefaults.DisplayName, configureOptions);
        public static AuthenticationBuilder AddMultiple(this AuthenticationBuilder builder, string authenticationScheme, string displayName, Action<MultipleAuthenticationOptions> configureOptions) => builder.AddScheme<MultipleAuthenticationOptions, MultipleAuthenticationHandler>(authenticationScheme, displayName, configureOptions);
        public static bool TryGetIdentity(this HttpContext context, string scheme, [NotNullWhen(true)] out ClaimsIdentity? value)
        {
            value = context.User.Identities.FirstOrDefault(id => id.AuthenticationType == scheme);
            return value != null;
        }
        public static string? FindFirstValue(this ClaimsIdentity identity, string type) => identity.FindFirst(type)?.Value;
    }

    public class MultipleAuthenticationOptions : AuthenticationSchemeOptions
    {
    }

    public class MultipleAuthenticationHandler : SignInAuthenticationHandler<MultipleAuthenticationOptions>
    {
        const string IdentitySchemeKey = ".Identity.Scheme";
        const string IdentityAccountKey = ".Identity.Account";

        public MultipleAuthenticationHandler(IOptionsMonitor<MultipleAuthenticationOptions> options, ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock) : base(options, logger, encoder, clock)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(GetCurrentPrincipal(), Scheme.Name)));
        }

        protected override Task HandleSignInAsync(ClaimsPrincipal user, AuthenticationProperties? properties)
        {
            if (user.Identity == null || properties == null) return Task.CompletedTask;

            var scheme = user.Identity.AuthenticationType;
            var account = user.FindFirstValue(ClaimTypes.NameIdentifier);
            properties.SetString(IdentitySchemeKey, scheme);
            properties.SetString(IdentityAccountKey, account);
            if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug($"HandleSignInAsync({scheme}/{account})");

            var principal = GetCurrentPrincipal(identity => identity.AuthenticationType == scheme);
            principal.AddIdentities(user.Identities);
            SetCurrentPrincipal(principal);

            Context.Session.Set($"multiple-authentication-properties-{scheme}", JsonSerializer.SerializeToUtf8Bytes(properties));

            return Task.CompletedTask;
        }

        protected override Task HandleSignOutAsync(AuthenticationProperties? properties)
        {
            if (properties == null) return Task.CompletedTask;

            var scheme = properties.GetString(IdentitySchemeKey) ?? properties.GetString(".AuthScheme");
            var account = properties.GetString(IdentityAccountKey);
            if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug($"HandleSignOutAsync({scheme}/{account})");

            var principal = GetCurrentPrincipal(identity => identity.AuthenticationType == scheme);
            SetCurrentPrincipal(principal);

            Context.Session.Remove($"multiple-authentication-properties-{scheme}");

            return Task.CompletedTask;
        }

        ClaimsPrincipal GetCurrentPrincipal(Func<ClaimsIdentity, bool> existingFilter)
        {
            var principal = GetCurrentPrincipal();
            var existing = principal.Identities.Where(existingFilter);
            return existing.Any() ? new ClaimsPrincipal(principal.Identities.Except(existing)) : principal;
        }

        ClaimsPrincipal GetCurrentPrincipal()
        {
            if (Context.Session.TryGetValue($"multiple-authentication-principal-{Scheme.Name}", out var ticketData))
            {
                var bytes = JsonSerializer.Deserialize<byte[]>(ticketData);
                if (bytes != null)
                {
                    using var stream = new MemoryStream(bytes);
                    using var reader = new BinaryReader(stream);
                    return new ClaimsPrincipal(reader);
                }
            }
            return new ClaimsPrincipal();
        }

        void SetCurrentPrincipal(ClaimsPrincipal principal)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            principal.WriteTo(writer);
            Context.Session.Set($"multiple-authentication-principal-{Scheme.Name}", JsonSerializer.SerializeToUtf8Bytes(stream.ToArray()));
        }
    }

    public class MultipleAuthenticationAuthorizationMiddlewareResultHandler : IAuthorizationMiddlewareResultHandler
    {
        readonly AuthorizationMiddlewareResultHandler DefaultHandler = new AuthorizationMiddlewareResultHandler();

        public async Task HandleAsync(RequestDelegate request, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult result)
        {
            if (result.Forbidden && (result.AuthorizationFailure?.FailedRequirements.OfType<MultipleAuthenticationRequirement>().Any() ?? false))
            {
                result = PolicyAuthorizationResult.Challenge();
            }

            await DefaultHandler.HandleAsync(request, context, policy, result);
        }
    }

    public class MultipleAuthenticationRequirement : IAuthorizationRequirement, IAuthorizationHandler
    {
        public string Scheme { get; init; }

        public MultipleAuthenticationRequirement(string scheme)
        {
            Scheme = scheme;
        }

        public Task HandleAsync(AuthorizationHandlerContext context)
        {
            foreach (var requirement in context.PendingRequirements)
            {
                if (requirement is MultipleAuthenticationRequirement mar && context.User.Identities.Any(id => id.AuthenticationType == mar.Scheme))
                {
                    context.Succeed(requirement);
                }
            }

            return Task.CompletedTask;
        }
    }

    public class MultipleAuthenticationContext<T> where T : OAuthOptions
    {
        readonly IHttpContextAccessor ContextAccessor;
        readonly IOptionsMonitor<T> OptionsMonitor;

        public MultipleAuthenticationContext(IHttpContextAccessor contextAccessor, IOptionsMonitor<T> optionsMonitor)
        {
            ContextAccessor = contextAccessor;
            OptionsMonitor = optionsMonitor;
        }

        public bool TryGetAuthentication(string scheme, [NotNullWhen(true)] out AuthenticationProperties? value)
        {
            value = null;
            if (!(ContextAccessor.HttpContext?.Session.TryGetValue($"multiple-authentication-properties-{scheme}", out var propertiesData) ?? false)) return false;

            var json = JsonSerializer.Deserialize<AuthenticationPropertiesJson>(propertiesData);
            if (json == null) return false;

            value = new AuthenticationProperties(json.Items);
            var expiresAt = value.GetTokenValue("expires_at");
            var refreshToken = value.GetTokenValue("refresh_token");
            if (expiresAt == null || refreshToken == null || (DateTimeOffset.Parse(expiresAt) - DateTimeOffset.Now).TotalMinutes > 5) return true;

            // At this point, we need to refresh the nearly or actually expired token
            var options = OptionsMonitor.Get(scheme);
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "client_id", options.ClientId },
                { "client_secret", options.ClientSecret },
                { "grant_type", "refresh_token" },
                { "refresh_token", refreshToken },
            });
            var response = options.Backchannel.PostAsync(options.TokenEndpoint, content, ContextAccessor.HttpContext.RequestAborted).Result;
            if (response.IsSuccessStatusCode)
            {
                using (var payload = JsonDocument.Parse(response.Content.ReadAsStringAsync().Result))
                {
                    var newAccessToken = payload.RootElement.GetString("access_token");
                    var newRefreshToken = payload.RootElement.GetString("refresh_token");
                    if (newAccessToken != null && newRefreshToken != null)
                    {
                        value.UpdateTokenValue("access_token", newAccessToken);
                        value.UpdateTokenValue("refresh_token", newRefreshToken);
                        if (payload.RootElement.TryGetProperty("expires_in", out var property) && property.TryGetInt32(out var seconds))
                        {
                            var newExpiresAt = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(seconds);
                            value.UpdateTokenValue("expires_at", newExpiresAt.ToString("o", CultureInfo.InvariantCulture));
                        }
                        var user = ContextAccessor.HttpContext.AuthenticateAsync().Result.Principal;
                        if (user != null)
                        {
                            ContextAccessor.HttpContext.SignInAsync(user, value).Wait();
                            return true;
                        }
                    }
                }
            }

            // We failed to refresh the expired token, log out of this account
            ContextAccessor.HttpContext.SignOutAsync(value);
            return false;
        }
    }

    record AuthenticationPropertiesJson(IDictionary<string, string?> Items);
}
