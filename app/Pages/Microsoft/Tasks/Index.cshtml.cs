using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using app.Auth;
using Microsoft.Graph;

namespace app.Pages.Microsoft.Tasks
{
    public class IndexModel : MicrosoftPageModel
    {
        public IEnumerable<DisplayTaskList> Lists;

        GraphServiceClient Graph;

        public IndexModel(MicrosoftGraphProvider graphProvider)
        {
            Graph = graphProvider.Client;
        }

        public async Task OnGet()
        {
            Lists = GetTaskLists(await Graph.Me.Todo.Lists.Request().Top(1000).GetAsync()).OrderBy(list => list.SortKey);
        }

        IEnumerable<DisplayTaskList> GetTaskLists(IEnumerable<TodoTaskList> lists)
        {
            var displayLists = new List<DisplayTaskList>();
            foreach (var list in lists)
            {
                var split = GetSplitEmojiName(list.DisplayName);
                displayLists.Add(new DisplayTaskList(list.Id, split.Emoji, split.Text, list.WellknownListName, list.IsOwner ?? false, list.IsShared ?? true));
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
            Debug.WriteLine($"{text} = {String.Join(" ", runes.Select(rune => rune.Value.ToString("X")))} = {String.Join(" ", unicode)}");
            return runes.Any(rune => rune.Value == 0xFE0F) || unicode.Any(category => category == UnicodeCategory.OtherSymbol);
        }

        public record DisplayTaskList(string Id, string NameEmoji, string NameText, WellknownListName? KnownList, bool IsOwner, bool Isshared)
        {
            public string SortKey
            {
                get
                {
                    if (this.KnownList == WellknownListName.DefaultList) return "01 ";
                    if (this.KnownList == WellknownListName.FlaggedEmails) return "02 ";
                    return "99 " + this.NameText;
                }
            }
        }
    }
}
