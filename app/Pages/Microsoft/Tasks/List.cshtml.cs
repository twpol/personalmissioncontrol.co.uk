using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using app.Auth;
using Microsoft.Graph;
using TaskStatus = Microsoft.Graph.TaskStatus;

namespace app.Pages.Microsoft.Tasks
{
    public class ListModel : MicrosoftPageModel
    {
        public string Title;
        public TodoTaskList List;
        public IEnumerable<DisplayTask> Tasks;
        public bool Nested;

        GraphServiceClient Graph;

        public ListModel(MicrosoftGraphProvider graphProvider)
        {
            Graph = graphProvider.Client;
        }

        public async Task OnGet(string list)
        {
            List = await Graph.Me.Todo.Lists[list].Request().GetAsync();
            Title = List.DisplayName;
            var tasks = await GetAllPages(Graph.Me.Todo.Lists[list].Tasks.Request()
                .Top(1000));
            Tasks = tasks
                .Select(task => new DisplayTask(task.Id, task.Title, task.Status ?? TaskStatus.NotStarted, task.Importance ?? Importance.Normal, GetDTO(task.CompletedDateTime)))
                .OrderBy(task => task.SortKey);
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

            var tasks = new List<TodoTask>();
            foreach (var list in await Graph.Me.Todo.Lists.Request().Top(1000).GetAsync())
            {
                tasks.AddRange(await GetAllPages(Graph.Me.Todo.Lists[list.Id].Tasks.Request().Filter($"contains(title, '{Uri.EscapeDataString(Uri.EscapeDataString(text))}')")));
            }
            Tasks = tasks
                .Select(task => new DisplayTask(task.Id, task.Title, task.Status ?? TaskStatus.NotStarted, task.Importance ?? Importance.Normal, GetDTO(task.CompletedDateTime)))
                .OrderBy(task => task.SortKey);
        }

        public async Task OnGetChildren(string hashtag)
        {
            Title = $"#{hashtag}";
            Nested = HttpContext.Request.Query["layout"] == "nested";

            var tasks = new List<TodoTask>();
            foreach (var list in await Graph.Me.Todo.Lists.Request().Top(1000).GetAsync())
            {
                tasks.AddRange(await GetAllPages(Graph.Me.Todo.Lists[list.Id].Tasks.Request().Filter($"contains(title, '{Uri.EscapeDataString(Uri.EscapeDataString(Title))}')")));
            }
            var pattern = new Regex($@". #{hashtag}(?: |$)");
            Tasks = tasks
                .Where(task => pattern.IsMatch(task.Title))
                .Select(task => new DisplayTask(task.Id, task.Title, task.Status ?? TaskStatus.NotStarted, task.Importance ?? Importance.Normal, GetDTO(task.CompletedDateTime)))
                .OrderBy(task => task.SortKey);
        }

        async Task<IList<TodoTask>> GetAllPages(ITodoTaskListTasksCollectionRequest request)
        {
            var list = new List<TodoTask>();
            do
            {
                var task = await request.GetAsync();
                list.AddRange(task);
                request = task.NextPageRequest;
            } while (request != null);
            return list;
        }

        DateTimeOffset? GetDTO(DateTimeTimeZone dateTimeTimeZone)
        {
            if (dateTimeTimeZone == null) return null;
            switch (dateTimeTimeZone.TimeZone)
            {
                case "UTC":
                    return DateTimeOffset.ParseExact(dateTimeTimeZone.DateTime + "Z", "o", CultureInfo.InvariantCulture);
                default:
                    throw new InvalidDataException($"Unknown time zone: {dateTimeTimeZone.TimeZone}");
            }
        }

        public record DisplayTask(string Id, string Title, TaskStatus Status, Importance Importance, DateTimeOffset? Completed)
        {
            static Dictionary<TaskStatus, string> StatusSort = new()
            {
                { TaskStatus.NotStarted, "1" },
                { TaskStatus.InProgress, "2" },
                { TaskStatus.Completed, "3" },
                { TaskStatus.WaitingOnOthers, "4" },
                { TaskStatus.Deferred, "5" },
            };

            static Dictionary<Importance, string> ImportanceSort = new()
            {
                { Importance.High, "1" },
                { Importance.Normal, "2" },
                { Importance.Low, "3" },
            };

            public string Classes => Status == TaskStatus.Completed ? "task task--completed text-black-50" : "task";
            public bool IsComplete => Status == TaskStatus.Completed;
            public bool IsImportant => Importance == Importance.High;
            public string SortKey => $"{StatusSort[Status]}{ImportanceSort[Importance]} {Title}";
            public string NestedTag => Title.StartsWith("#") ? Title.Split(' ')[0].Substring(1) : null;
            public string NestedUrl => $"/Microsoft/Tasks/List/Children?hashtag={NestedTag}&layout=nested";
        }
    }
}
