using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using app.Services;
using Microsoft.AspNetCore.Authentication;
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
        public static string GetId(this ClaimsIdentity identity) => identity.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        public static string GetName(this ClaimsIdentity identity) => identity.FindFirstValue(ClaimTypes.Name) ?? $"{identity.FindFirstValue(ClaimTypes.GivenName)} {identity.FindFirstValue(ClaimTypes.Surname)}";
        public static string? GetAccountId(this ClaimsIdentity identity) => identity.AuthenticationType != null ? $"{identity.AuthenticationType}:{identity.FindFirstValue(ClaimTypes.NameIdentifier)}" : null;

        const string AccountIdKey = ".AccountId";
        internal static void SetAccountId(this AuthenticationProperties properties, string account) => properties.SetString(AccountIdKey, account);
        public static string? GetAccountId(this AuthenticationProperties properties) => properties.GetString(AccountIdKey);
    }

    public class MultipleAuthenticationOptions : AuthenticationSchemeOptions
    {
    }

    public class MultipleAuthenticationHandler : SignInAuthenticationHandler<MultipleAuthenticationOptions>
    {
        AuthenticationContext AuthenticationContext;

        public MultipleAuthenticationHandler(AuthenticationContext authenticationContext, IOptionsMonitor<MultipleAuthenticationOptions> options, ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock) : base(options, logger, encoder, clock)
        {
            AuthenticationContext = authenticationContext;
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var principal = GetCurrentPrincipal();
            Activity.Current?.SetTag("auth.account_ids", String.Join(" ", principal.Identities.Select(identity => identity.GetAccountId())));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }

        protected override async Task HandleSignInAsync(ClaimsPrincipal user, AuthenticationProperties? properties)
        {
            Debug.Assert(user.Identities.Count() == 1, "Cannot HandleSignInAsync with more than one identity in user principal");
            if (user.Identity is not ClaimsIdentity identity) return;
            if (properties == null) return;
            var accountId = identity.GetAccountId();
            if (accountId == null) return;

            properties.SetAccountId(accountId);
            Activity.Current?.AddEvent(new("HandleSignInAsync", tags: new()
            {
                { "auth.account_id", accountId },
            }));

            var principal = GetCurrentPrincipal(identity => identity.GetAccountId() == accountId);
            principal.AddIdentities(user.Identities);
            SetCurrentPrincipal(principal);

            await AuthenticationContext.SetAccount(accountId, properties);
        }

        protected override async Task HandleSignOutAsync(AuthenticationProperties? properties)
        {
            if (properties == null) return;
            var accountId = properties.GetAccountId();
            if (accountId == null) return;

            Activity.Current?.AddEvent(new("HandleSignOutAsync", tags: new()
            {
                { "auth.account_id", accountId },
            }));

            var principal = GetCurrentPrincipal(identity => identity.GetAccountId() == accountId);
            SetCurrentPrincipal(principal);

            await AuthenticationContext.RemoveAccount(accountId);
        }

        ClaimsPrincipal GetCurrentPrincipal(Func<ClaimsIdentity, bool> existingFilter)
        {
            var principal = GetCurrentPrincipal();
            var existing = principal.Identities.Where(existingFilter);
            return existing.Any() ? new ClaimsPrincipal(principal.Identities.Except(existing)) : principal;
        }

        ClaimsPrincipal GetCurrentPrincipal()
        {
            if (Context.Session.TryGetValue($"{nameof(MultipleAuthenticationHandler)}:Principal", out var ticketData))
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
            Context.Session.Set($"{nameof(MultipleAuthenticationHandler)}:Principal", JsonSerializer.SerializeToUtf8Bytes(stream.ToArray()));
        }
    }

    public class MultipleAuthenticationAuthorizationMiddlewareResultHandler : IAuthorizationMiddlewareResultHandler
    {
        readonly AuthorizationMiddlewareResultHandler DefaultHandler = new();

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
}
