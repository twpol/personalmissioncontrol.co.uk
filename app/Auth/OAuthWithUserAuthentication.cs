using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.Extensions.DependencyInjection;

namespace app.Auth
{
    public static class OAuthWithUserAuthenticationExtensions
    {
        public static AuthenticationBuilder AddOAuthWithUser(this AuthenticationBuilder builder, string authenticationScheme, Action<OAuthOptions> configureOptions) => builder.AddOAuthWithUser(authenticationScheme, OAuthDefaults.DisplayName, configureOptions);
        public static AuthenticationBuilder AddOAuthWithUser(this AuthenticationBuilder builder, string authenticationScheme, string displayName, Action<OAuthOptions> configureOptions) => builder.AddOAuth(authenticationScheme, displayName, options =>
        {
            // Configure OAuth normally
            configureOptions(options);

            // Now wrap `OnCreatingTicket` so that it'll fetch `UserInformationEndpoint` automatically
            var oldCreatingTicket = options.Events.OnCreatingTicket;
            options.Events.OnCreatingTicket = async context =>
            {
                var request = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.AccessToken);
                var response = await context.Backchannel.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, context.HttpContext.RequestAborted);
                response.EnsureSuccessStatusCode();
                var user = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                context.RunClaimActions(user.RootElement);

                // And make sure to run original `OnCreatingTicket`
                await oldCreatingTicket(context);
            };
        });
    }
}
