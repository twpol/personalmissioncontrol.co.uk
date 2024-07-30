using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using app.Auth;
using app.Models;
using Microsoft.Graph;

namespace app.Pages.Microsoft.Tasks
{
    public class IndexModel : MicrosoftPageModel
    {
        public IEnumerable<TaskListModel> Lists = null!;

        readonly GraphServiceClient Graph;

        public IndexModel(MicrosoftGraphProvider graphProvider)
        {
            Graph = graphProvider.Client;
        }

        public async Task OnGet()
        {
            Lists = GetTaskLists(await Graph.Me.Todo.Lists.Request().Top(1000).GetAsync()).OrderBy(list => list.SortKey);
        }

        static IEnumerable<TaskListModel> GetTaskLists(IEnumerable<TodoTaskList> lists)
        {
            var displayLists = new List<TaskListModel>();
            foreach (var list in lists)
            {
                var (Emoji, Text) = GetSplitEmojiName(list.DisplayName);
                displayLists.Add(new TaskListModel(list.Id, Emoji, Text, list.WellknownListName == WellknownListName.DefaultList ? TaskListSpecial.Default : list.WellknownListName == WellknownListName.FlaggedEmails ? TaskListSpecial.Emails : TaskListSpecial.None));
            }
            return displayLists;
        }

        static (string Emoji, string Text) GetSplitEmojiName(string name)
        {
            var first = StringInfo.GetNextTextElement(name);
            if (IsTextElementEmoji(first)) return (first, name[first.Length..].Trim());
            return ("", name);
        }

        static bool IsTextElementEmoji(string text)
        {
            var runes = text.EnumerateRunes();
            var unicode = runes.Select(rune => Rune.GetUnicodeCategory(rune));
            return runes.Any(rune => rune.Value == 0xFE0F) || unicode.Any(category => category == UnicodeCategory.OtherSymbol);
        }
    }
}
