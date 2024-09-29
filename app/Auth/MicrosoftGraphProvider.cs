using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using app.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Microsoft.Graph;

namespace app.Auth
{
    public class MicrosoftGraphProvider
    {
        readonly AuthenticationContext AuthenticationContext;

        public MicrosoftGraphProvider(AuthenticationContext authenticationContext)
        {
            AuthenticationContext = authenticationContext;
        }

        public bool TryGet(string scheme, [NotNullWhen(true)] out GraphServiceClient? client, out string accountId)
        {
            client = null;
            accountId = "";
            if (!AuthenticationContext.TryGetOAuthAuthentication<MicrosoftAccountOptions>(scheme, out var auth)) return false;

            var type = auth.GetTokenValue("token_type");
            var token = auth.GetTokenValue("access_token");
            if (type == null || token == null) return false;

            client = new GraphServiceClient(new AuthenticationProvider(type, token));
            accountId = auth.GetAccountId() ?? "";
            return true;
        }

        class AuthenticationProvider : IAuthenticationProvider
        {
            readonly string Type;
            readonly string Token;

            public AuthenticationProvider(string type, string token)
            {
                Type = type;
                Token = token;
            }

            public Task AuthenticateRequestAsync(HttpRequestMessage request)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue(Type, Token);
                return Task.CompletedTask;
            }
        }
    }
}
