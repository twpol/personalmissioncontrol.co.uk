using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using app.Auth;
using app.Models;
using app.Services.Data;
using Microsoft.Graph;

namespace app.Pages.Microsoft.Tasks
{
    public class ListModel : MicrosoftPageModel
    {
        public string Title = null!;
        public TodoTaskList List = null!;
        public IEnumerable<TaskModel> Tasks = null!;
        public bool Nested;

        readonly GraphServiceClient Graph;
        readonly MicrosoftData Data;

        public ListModel(MicrosoftGraphProvider graphProvider, MicrosoftData data)
        {
            Graph = graphProvider.Client;
            Data = data;
        }

        public async Task OnGet(string list)
        {
            List = await Graph.Me.Todo.Lists[list].Request().GetAsync();
            Title = List.DisplayName;
            Tasks = await Data.GetTasks(list);
        }

        public async Task OnGetTree(string list)
        {
            Nested = true;
            await OnGet(list);
        }

        public async Task OnGetSearch(string text)
        {
            Title = text;
            Nested = HttpContext.Request.Query["layout"] == "nested";

            var tasks = new List<TaskModel>();
            foreach (var list in await Data.GetLists())
            {
                tasks.AddRange((await Data.GetTasks(list.Id)).Where(task => task.Title.Contains(text)));
            }
            Tasks = tasks.OrderBy(task => task.SortKey);
        }

        public async Task OnGetChildren(string hashtag)
        {
            Title = $"#{hashtag}";
            Nested = HttpContext.Request.Query["layout"] == "nested";

            var tasks = new List<TaskModel>();
            foreach (var list in await Data.GetLists())
            {
                tasks.AddRange((await Data.GetTasks(list.Id)).Where(task => task.Title.Contains(Title)));
            }
            var pattern = new Regex($@". #{hashtag}(?: |$)");
            Tasks = tasks.Where(task => pattern.IsMatch(task.Title)).OrderBy(task => task.SortKey);
        }
    }
}
