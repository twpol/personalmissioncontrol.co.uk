using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using app.Auth;
using app.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace app.Services
{
    public static class AuthenticationContextExtensions
    {
        public static IServiceCollection AddAuthenticationContext(this IServiceCollection services) => services.AddScoped<AuthenticationContext>();
    }

    public class AuthenticationContext
    {
        public Dictionary<string, AccountModel> AccountModels { get; } = new();

        readonly ILogger<AuthenticationContext> Logger;
        readonly IModelStore<AccountModel> Accounts;
        readonly IServiceProvider Services;

        public AuthenticationContext(ILogger<AuthenticationContext> logger, IModelStore<AccountModel> accounts, IServiceProvider services)
        {
            Logger = logger;
            Accounts = accounts;
            Services = services;
            if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug(".ctor()");
        }

        public async Task LoadAccount(string accountId)
        {
            if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug($"LoadAccount({accountId})");
            var account = await Accounts.GetItemAsync(accountId, "", "");
            if (account == null) return;
            AccountModels[accountId] = account;
        }

        public async Task SetAccount(string accountId, AuthenticationProperties properties)
        {
            if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug($"SetAccount({accountId})");
            var account = new AccountModel(accountId, "", "", properties);
            await Accounts.SetItemAsync(account);
            AccountModels[accountId] = account;
        }

        public async Task RemoveAccount(string accountId)
        {
            if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug($"RemoveAccount({accountId})");
            var account = AccountModels[accountId];
            await Accounts.DeleteItemAsync(account);
            AccountModels.Remove(accountId);
        }

        public bool TryGetOAuthAuthentication<TOptions>(string scheme, [NotNullWhen(true)] out AuthenticationProperties? value) where TOptions : OAuthOptions
        {
            if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug($"TryGetOAuthAuthentication({scheme})");
            value = null;

            using var activity = Startup.ActivitySource.StartActivity();
            activity?.SetTag("auth.scheme", scheme);
            activity?.SetTag("auth.refresh", false);
            activity?.SetTag("auth.success", false);

            // TODO: This will need refactoring to support multiple authentications to the same provider
            value = AccountModels.FirstOrDefault(account => account.Key.StartsWith($"{scheme}:")).Value?.AuthenticationProperties;
            if (value == null) return false;

            var accountId = value.GetAccountId();
            if (accountId == null) return false;

            var expiresAt = value.GetTokenValue("expires_at");
            var refreshToken = value.GetTokenValue("refresh_token");
            activity?.SetTag("auth.account_id", accountId);
            activity?.SetTag("auth.expires_at", expiresAt);
            activity?.SetTag("auth.refresh_token_exists", refreshToken != null);
            if (expiresAt == null || refreshToken == null || (DateTimeOffset.Parse(expiresAt) - DateTimeOffset.Now).TotalMinutes > 5)
            {
                activity?.SetTag("auth.success", true);
                return true;
            }
            activity?.SetTag("auth.refresh", true);

            // At this point, we need to refresh the nearly or actually expired token
            var options = Services.GetRequiredService<IOptionsMonitor<TOptions>>().Get(scheme);
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "client_id", options.ClientId },
                { "client_secret", options.ClientSecret },
                { "grant_type", "refresh_token" },
                { "refresh_token", refreshToken },
            });
            var response = options.Backchannel.PostAsync(options.TokenEndpoint, content).Result;
            activity?.SetTag("auth.refresh_status_code", (int)response.StatusCode);
            if (response.IsSuccessStatusCode)
            {
                using var payload = JsonDocument.Parse(response.Content.ReadAsStringAsync().Result);
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
                        activity?.SetTag("auth.expires_at_new", value.GetTokenValue("expires_at"));
                    }
                    SetAccount(accountId, value).Wait();
                    activity?.SetTag("auth.success", true);
                    return true;
                }
            }

            // We failed to refresh the expired token, log out of this account
            RemoveAccount(accountId).Wait();
            return false;
        }
    }

    public static class AuthenticationContextMiddlewareExtensions
    {
        public static IApplicationBuilder UseAuthenticationContext(this IApplicationBuilder builder) => builder.UseMiddleware<AuthenticationContextMiddleware>();
    }

    public class AuthenticationContextMiddleware
    {
        readonly RequestDelegate Next;
        readonly ILogger<AuthenticationContextMiddleware> Logger;

        public AuthenticationContextMiddleware(RequestDelegate next, ILogger<AuthenticationContextMiddleware> logger)
        {
            Next = next;
            Logger = logger;
            if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug(".ctor()");
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug("InvokeAsync()");
            var authenticationContext = context.RequestServices.GetRequiredService<AuthenticationContext>();
            foreach (var identity in context.User.Identities)
            {
                var accountId = identity.GetAccountId();
                if (accountId == null) continue;
                await authenticationContext.LoadAccount(accountId);
            }
            await Next(context);
        }
    }
}
