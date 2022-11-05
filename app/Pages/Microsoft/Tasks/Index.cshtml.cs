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
        public IEnumerable<TaskListModel> Lists;

        GraphServiceClient Graph;

        public IndexModel(MicrosoftGraphProvider graphProvider)
        {
            Graph = graphProvider.Client;
        }

        public async Task OnGet()
        {
            Lists = GetTaskLists(await Graph.Me.Todo.Lists.Request().Top(1000).GetAsync()).OrderBy(list => list.SortKey);
        }

        IEnumerable<TaskListModel> GetTaskLists(IEnumerable<TodoTaskList> lists)
        {
            var displayLists = new List<TaskListModel>();
            foreach (var list in lists)
            {
                var split = GetSplitEmojiName(list.DisplayName);
                displayLists.Add(new TaskListModel(list.Id, split.Emoji, split.Text, list.WellknownListName == WellknownListName.DefaultList ? TaskListSpecial.Default : list.WellknownListName == WellknownListName.FlaggedEmails ? TaskListSpecial.Emails : TaskListSpecial.None));
            }
            return displayLists;
        }

        (string Emoji, string Text) GetSplitEmojiName(string name)
        {
            var first = StringInfo.GetNextTextElement(name);
            if (IsTextElementEmoji(first)) return (first, name.Substring(first.Length).Trim());
            return ("", name);
        }

        bool IsTextElementEmoji(string text)
        {
            var runes = text.EnumerateRunes();
            var unicode = runes.Select(rune => Rune.GetUnicodeCategory(rune));
            return runes.Any(rune => rune.Value == 0xFE0F) || unicode.Any(category => category == UnicodeCategory.OtherSymbol);
        }
    }
}
