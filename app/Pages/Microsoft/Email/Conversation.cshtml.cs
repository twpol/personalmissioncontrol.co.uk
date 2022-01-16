using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using app.Auth;
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
            </style>
        ";
        public const string HtmlBodyPostfix = "";

        public IEnumerable<DisplayMessage> Messages;
        public string ConversationName;

        GraphServiceClient Graph;

        public ConversationModel(MicrosoftGraphProvider graphProvider)
        {
            Graph = graphProvider.Client;
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
            ConversationName = CleanSubject(message.First().Subject);

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
                    message.Body.Content
                ));
        }

        async Task<IList<Message>> GetAllPages(IUserMessagesCollectionRequest request)
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

        string CleanSubject(string subject)
        {
            var length = 0;
            do
            {
                length = subject.Length;
                subject = subject.Trim();
                if (subject.StartsWith("re:", StringComparison.OrdinalIgnoreCase)) subject = subject.Substring(3);
                if (subject.StartsWith("fw:", StringComparison.OrdinalIgnoreCase)) subject = subject.Substring(3);
                if (subject.StartsWith("fwd:", StringComparison.OrdinalIgnoreCase)) subject = subject.Substring(4);
            } while (length != subject.Length);
            return subject;
        }

        public record DisplayMessage(string Id, DateTimeOffset Date, EmailAddress From, EmailAddress To, bool Unread, bool Flagged, bool Completed, string HtmlBody)
        {
            public string HtmlBodyForIFrame
            {
                get
                {
                    return HtmlBodyPrefix + HtmlBody + HtmlBodyPostfix;
                }
            }
        }
    }
}
