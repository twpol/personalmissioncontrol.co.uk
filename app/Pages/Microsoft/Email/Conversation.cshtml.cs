using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using app.Auth;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using Microsoft.Graph;

namespace app.Pages.Microsoft.Email
{
    public class ConversationModel : MicrosoftPageModel
    {
        public const string HtmlBodyPrefix = @"
            <style>
                body {
                    margin: 1rem;
                    font-family: sans-serif;
                }
                img {
                    max-width: 100%;
                }
                pre {
                    white-space: pre-wrap;
                }
                body.pmc-plain-text pre {
                    font-family: inherit;
                }
            </style>
        ";
        public const string HtmlBodyPostfix = "";

        public IEnumerable<DisplayMessage> Messages = null!;
        public string ConversationName = null!;

        static readonly Regex HttpLink = new(@"https?://[^\r\n> ]+");
        const string OutlookProtection = ".safelinks.protection.outlook.com";

        readonly GraphServiceClient Graph;

        public ConversationModel(MicrosoftGraphProvider graphProvider)
        {
            if (!graphProvider.TryGet("Microsoft", out Graph!, out _)) throw new InvalidOperationException("Microsoft Graph client not initialised");
        }

        public async Task OnGet(string conversation)
        {
            var message = await Graph.Me.Messages.Request()
                .Filter($"conversationId eq '{conversation}'")
                .Top(1)
                .Select(message => new
                {
                    message.Subject,
                })
                .GetAsync();
            ConversationName = CleanSubject(message.First().Subject ?? "(no subject)");

            Messages = (await GetAllPages(Graph.Me.Messages.Request()
                .Filter($"conversationId eq '{conversation}'")
                .Top(1000)
                .Select(message => new
                {
                    message.SentDateTime,
                    message.From,
                    message.ToRecipients,
                    message.IsRead,
                    message.Flag,
                    message.Body,
                })))
                .OrderBy(message => message.SentDateTime)
                .Select(message => new DisplayMessage(
                    message.Id,
                    message.SentDateTime ?? DateTimeOffset.MinValue,
                    message.From.EmailAddress,
                    message.ToRecipients.First().EmailAddress,
                    message.IsRead == false,
                    message.Flag.FlagStatus == FollowupFlagStatus.Flagged,
                    message.Flag.FlagStatus == FollowupFlagStatus.Complete,
                    GetHtmlBody(message.Body)
                ));
        }

        static async Task<IList<Message>> GetAllPages(IUserMessagesCollectionRequest request)
        {
            var list = new List<Message>();
            do
            {
                var messages = await request.GetAsync();
                list.AddRange(messages);
                request = messages.NextPageRequest;
            } while (request != null);
            return list;
        }

        static string CleanSubject(string subject)
        {
            int length;
            do
            {
                length = subject.Length;
                subject = subject.Trim();
                if (subject.StartsWith("re:", StringComparison.OrdinalIgnoreCase)) subject = subject[3..];
                if (subject.StartsWith("fw:", StringComparison.OrdinalIgnoreCase)) subject = subject[3..];
                if (subject.StartsWith("fwd:", StringComparison.OrdinalIgnoreCase)) subject = subject[4..];
            } while (length != subject.Length);
            return subject;
        }

        static string GetHtmlBody(ItemBody body)
        {
            if (body.ContentType == BodyType.Text)
            {
                return "<body class=pmc-plain-text><pre>"
                    + HttpLink.Replace(WebUtility.HtmlEncode(body.Content), match =>
                    {
                        var uri = new Uri(WebUtility.HtmlDecode(match.Value));
                        if (uri.Host.EndsWith(OutlookProtection))
                        {
                            var query = QueryHelpers.ParseQuery(uri.Query);
                            var original = query.GetValueOrDefault("url", query.GetValueOrDefault("amp;url", StringValues.Empty)).FirstOrDefault();
                            if (original != null) return $"<a href=\"{match.Value}\">{WebUtility.HtmlEncode(original)}</a>";
                        }
                        return $"<a href=\"{match.Value}\">{match.Value}</a>";
                    })
                    + "</pre></body>";
            }
            return body.Content;
        }

        public record DisplayMessage(string Id, DateTimeOffset Date, EmailAddress From, EmailAddress To, bool Unread, bool Flagged, bool Completed, string HtmlBody)
        {
            public string HtmlBodyForIFrame
            {
                get
                {
                    // TODO: Replace this with HTML parser
                    return HtmlBodyPrefix + HtmlBody.Replace("<img src=", "<img data-src=") + HtmlBodyPostfix;
                }
            }
        }
    }
}
