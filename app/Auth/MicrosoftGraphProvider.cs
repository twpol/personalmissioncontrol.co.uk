using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Microsoft.Graph;

namespace app.Auth
{
    public class MicrosoftGraphProvider : IAuthenticationProvider
    {
        public GraphServiceClient Client
        {
            get
            {
                if (RealClient == null) throw new InvalidOperationException("Microsoft Graph client not initialised");
                return RealClient;
            }
        }

        readonly GraphServiceClient? RealClient;
        readonly string? Authorization;

        public MicrosoftGraphProvider(MultipleAuthenticationContext<MicrosoftAccountOptions> authenticationContext)
        {
            if (authenticationContext.TryGetAuthentication("Microsoft", out var auth))
            {
                var type = auth.GetTokenValue("token_type");
                var token = auth.GetTokenValue("access_token");
                Authorization = $"{type} {token}";
                RealClient = new GraphServiceClient(this);
            }
        }

        public Task AuthenticateRequestAsync(HttpRequestMessage request)
        {
            if (Authorization != null && !request.Headers.Contains("Authorization"))
            {
                request.Headers.Add("Authorization", Authorization);
            }
            return Task.CompletedTask;
        }
    }
}
