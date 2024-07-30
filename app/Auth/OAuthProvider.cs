using System.Diagnostics.CodeAnalysis;
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

        public bool TryGet(string scheme, [NotNullWhen(true)] out HttpClient? client, out string accountId)
        {
            client = null;
            accountId = "";
            if (!AuthenticationContext.TryGetAuthentication(scheme, out var auth)) return false;

            var type = auth.GetTokenValue("token_type");
            var token = auth.GetTokenValue("access_token");
            if (type == null || token == null) return false;

            client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(type, token);
            accountId = auth.GetAccountId() ?? "";
            return true;
        }
    }
}
