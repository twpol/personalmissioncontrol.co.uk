using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Graph;

namespace app.Pages.Email
{
    [Authorize]
    public class ConversationModel : PageModel
    {
        public const string BaseEmailHtml = @"
            <style>
                html {
                    font-family: sans-serif;
                }
                body {
                    margin: 0;
                }
            </style>
        ";

        public string Conversation = "";

        public IEnumerable<Message> Emails = new List<Message>();

        readonly IMemoryCache _cache;

        public ConversationModel(IMemoryCache cache)
        {
            _cache = cache;
        }

        public async Task OnGetAsync(string conversation)
        {
            Conversation = conversation;

            var authContext = await HttpContext.AuthenticateAsync();
            if (authContext.Succeeded)
            {
                var graph = new GraphServiceClient(new Auth(authContext));

                Emails = await _cache.GetOrCreateAsync($"email/conversation/{Conversation}", entry =>
                {
                    entry.SlidingExpiration = TimeSpan.FromMinutes(60);
                    return graph.Me.Messages.Request()
                        .Filter($"sentDateTime gt 2000-01-01 and (subject eq '{Conversation}' or subject eq 'Re: {Conversation}')")
                        .OrderBy("sentDateTime")
                        .Select(email => new
                        {
                            email.SentDateTime,
                            email.From,
                            email.Subject,
                            email.Flag,
                            email.UniqueBody,
                        })
                        .GetAsync();
                });
            }
        }

        async IAsyncEnumerable<MailFolder> GetAllMailFolder(IUserMailFoldersCollectionRequest request)
        {
            while (request != null)
            {
                var page = await request.GetAsync();
                foreach (var folder in page)
                {
                    yield return folder;
                }
                request = page.NextPageRequest;
            }
        }
    }

    public class Auth : IAuthenticationProvider
    {
        readonly AuthenticateResult _auth;

        public Auth(AuthenticateResult auth)
        {
            _auth = auth;
        }

        public Task AuthenticateRequestAsync(HttpRequestMessage request)
        {
            var type = _auth.Properties.GetTokenValue("token_type");
            var token = _auth.Properties.GetTokenValue("access_token");
            request.Headers.Add("Authorization", $"{type} {token}");
            return Task.CompletedTask;
        }
    }
}
