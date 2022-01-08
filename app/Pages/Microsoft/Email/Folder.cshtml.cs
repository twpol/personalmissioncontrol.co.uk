using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using app.Auth;
using Microsoft.Graph;

namespace app.Pages.Microsoft.Email
{
    public class FolderModel : MicrosoftPageModel
    {
        public MailFolder Folder;
        public IEnumerable<DisplayConversation> Conversations;

        GraphServiceClient Graph;

        public FolderModel(MicrosoftGraphProvider graphProvider)
        {
            Graph = graphProvider.Client;
        }

        public async Task OnGet(string folder)
        {
            Folder = await Graph.Me.MailFolders[folder].Request().GetAsync();
            var messages = await GetAllPages(Graph.Me.MailFolders[folder].Messages.Request()
                .Top(1000)
                .Select(message => new
                {
                    message.ConversationId,
                    message.SentDateTime,
                    message.Subject,
                    message.IsRead,
                    message.Flag,
                }));
            Conversations = messages
                .GroupBy(message => message.ConversationId)
                .Select(group => new DisplayConversation(
                    group.Key,
                    CleanSubject(group.First().Subject),
                    group.Max(message => message.SentDateTime) ?? DateTimeOffset.MinValue,
                    group.Any(message => message.IsRead == false),
                    group.Any(message => message.Flag.FlagStatus == FollowupFlagStatus.Flagged),
                    group.Any(message => message.Flag.FlagStatus == FollowupFlagStatus.Complete)
                ))
                .OrderBy(conversation => conversation.SortKey)
                .Reverse();
        }

        async Task<IList<Message>> GetAllPages(IMailFolderMessagesCollectionRequest request)
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

        public record DisplayConversation(string Id, string Name, DateTimeOffset Date, bool Unread, bool Flagged, bool Completed)
        {
            public string SortKey
            {
                get
                {
                    return $"{(Unread ? 1 : 0)} {(Flagged ? 1 : 0)} {Date.ToString("u")}";
                }
            }
        }
    }
}
