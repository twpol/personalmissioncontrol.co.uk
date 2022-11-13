using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth;

namespace app.Auth
{
    public class OAuthProvider
    {
        readonly MultipleAuthenticationContext<OAuthOptions> AuthenticationContext;

        public OAuthProvider(MultipleAuthenticationContext<OAuthOptions> authenticationContext)
        {
            AuthenticationContext = authenticationContext;
        }

        public HttpClient? GetChannel(string scheme)
        {
            if (AuthenticationContext.TryGetAuthentication(scheme, out var auth))
            {
                var type = auth.GetTokenValue("token_type");
                var token = auth.GetTokenValue("access_token");
                if (type != null && token != null)
                {
                    var client = new HttpClient();
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(type, token);
                    return client;
                }
            }
            return null;
        }
    }
}
