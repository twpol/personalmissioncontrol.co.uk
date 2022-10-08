using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
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

        public static bool TryGetMultipleAuthentication(this HttpContext context, string scheme, out AuthenticationProperties value)
        {
            if (context.Session.TryGetValue($"multiple-authentication-properties-{scheme}", out var propertiesData))
            {
                var json = JsonSerializer.Deserialize<AuthenticationPropertiesJson>(propertiesData);
                value = new AuthenticationProperties(json.Items);
                return true;
            }
            value = null;
            return false;
        }
    }

    public class MultipleAuthenticationOptions : AuthenticationSchemeOptions
    {
    }

    public class MultipleAuthenticationHandler : SignInAuthenticationHandler<MultipleAuthenticationOptions>
    {
        public MultipleAuthenticationHandler(IOptionsMonitor<MultipleAuthenticationOptions> options, ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock) : base(options, logger, encoder, clock)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(GetCurrentPrincipal(), Scheme.Name)));
        }

        protected override Task HandleSignInAsync(ClaimsPrincipal user, AuthenticationProperties properties)
        {
            var principal = GetCurrentPrincipal();
            principal.AddIdentities(user.Identities);
            SetCurrentPrincipal(principal);

            Context.Session.Set($"multiple-authentication-properties-{user.Identity.AuthenticationType}", JsonSerializer.SerializeToUtf8Bytes(properties));

            return Task.CompletedTask;
        }

        ClaimsPrincipal GetCurrentPrincipal()
        {
            if (Context.Session.TryGetValue($"multiple-authentication-principal-{Scheme.Name}", out var ticketData))
            {
                using var stream = new MemoryStream(JsonSerializer.Deserialize<byte[]>(ticketData));
                using var reader = new BinaryReader(stream);
                return new ClaimsPrincipal(reader);
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

        protected override Task HandleSignOutAsync(AuthenticationProperties properties)
        {
            throw new NotImplementedException();
        }
    }

    public class MultipleAuthenticationAuthorizationMiddlewareResultHandler : IAuthorizationMiddlewareResultHandler
    {
        readonly AuthorizationMiddlewareResultHandler DefaultHandler = new AuthorizationMiddlewareResultHandler();

        public async Task HandleAsync(RequestDelegate request, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult result)
        {
            if (result.Forbidden && result.AuthorizationFailure.FailedRequirements.OfType<MultipleAuthenticationRequirement>().Any())
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

    record AuthenticationPropertiesJson(IDictionary<string, string> Items);
}
