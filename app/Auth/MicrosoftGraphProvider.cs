using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Graph;

namespace app.Auth
{
    public class MicrosoftGraphProvider : IAuthenticationProvider
    {
        public GraphServiceClient Client { get; init; }

        string Authorization { get; init; }

        public MicrosoftGraphProvider(IHttpContextAccessor contextAccessor)
        : this(contextAccessor.HttpContext)
        {
        }

        public MicrosoftGraphProvider(HttpContext context)
        {
            if (context.TryGetMultipleAuthentication("Microsoft", out var auth))
            {
                var type = auth.GetTokenValue("token_type");
                var token = auth.GetTokenValue("access_token");
                Authorization = $"{type} {token}";
                Client = new GraphServiceClient(this);
            }
        }

        public Task AuthenticateRequestAsync(HttpRequestMessage request)
        {
            if (Authorization.Length > 0 && !request.Headers.Contains("Authorization"))
            {
                request.Headers.Add("Authorization", Authorization);
            }
            return Task.CompletedTask;
        }
    }
}
