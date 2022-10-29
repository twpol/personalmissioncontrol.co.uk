using System;
using System.Collections.Generic;
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
            if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug($"HandleSignInAsync({user.Identity.AuthenticationType}/{user.Identity.Name})");

            var principal = GetCurrentPrincipal();
            var nameId = user.Claims.FirstOrDefault(claim => claim.Type == ClaimTypes.NameIdentifier)?.Value ?? "";
            var existing = principal.Identities.Where(identity => identity.AuthenticationType == user.Identity.AuthenticationType && identity.HasClaim(ClaimTypes.NameIdentifier, nameId));
            if (existing.Any()) principal = new ClaimsPrincipal(principal.Identities.Except(existing));
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

    public class MultipleAuthenticationContext<T> where T : OAuthOptions
    {
        readonly IHttpContextAccessor ContextAccessor;
        readonly IOptionsMonitor<T> OptionsMonitor;

        public MultipleAuthenticationContext(IHttpContextAccessor contextAccessor, IOptionsMonitor<T> optionsMonitor)
        {
            ContextAccessor = contextAccessor;
            OptionsMonitor = optionsMonitor;
        }

        public bool TryGetAuthentication(string scheme, out AuthenticationProperties value)
        {
            if (ContextAccessor.HttpContext.Session.TryGetValue($"multiple-authentication-properties-{scheme}", out var propertiesData))
            {
                var json = JsonSerializer.Deserialize<AuthenticationPropertiesJson>(propertiesData);
                value = new AuthenticationProperties(json.Items);

                if ((DateTimeOffset.Parse(value.GetTokenValue("expires_at")) - DateTimeOffset.Now).TotalMinutes < 5)
                {
                    var options = OptionsMonitor.Get(scheme);
                    var content = new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        { "client_id", options.ClientId },
                        { "client_secret", options.ClientSecret },
                        { "grant_type", "refresh_token" },
                        { "refresh_token", value.GetTokenValue("refresh_token") },
                    });
                    var response = options.Backchannel.PostAsync(options.TokenEndpoint, content, ContextAccessor.HttpContext.RequestAborted).Result;
                    response.EnsureSuccessStatusCode();

                    using (var payload = JsonDocument.Parse(response.Content.ReadAsStringAsync().Result))
                    {
                        value.UpdateTokenValue("access_token", payload.RootElement.GetString("access_token"));
                        value.UpdateTokenValue("refresh_token", payload.RootElement.GetString("refresh_token"));
                        if (payload.RootElement.TryGetProperty("expires_in", out var property) && property.TryGetInt32(out var seconds))
                        {
                            var expiresAt = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(seconds);
                            value.UpdateTokenValue("expires_at", expiresAt.ToString("o", CultureInfo.InvariantCulture));
                        }
                        var user = ContextAccessor.HttpContext.AuthenticateAsync().Result.Principal;
                        ContextAccessor.HttpContext.SignInAsync(user, value).Wait();
                    }
                }

                return true;
            }

            value = null;
            return false;
        }
    }

    record AuthenticationPropertiesJson(IDictionary<string, string> Items);
}
