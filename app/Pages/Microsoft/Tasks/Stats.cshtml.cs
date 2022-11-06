using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using app.Auth;
using Microsoft.Graph;
using Task = System.Threading.Tasks.Task;

namespace app.Pages.Microsoft.Tasks
{
    public class StatsModel : MicrosoftPageModel
    {
        public IEnumerable<DisplayWeek> Weeks = null!;

        GraphServiceClient Graph;

        public StatsModel(MicrosoftGraphProvider graphProvider)
        {
            Graph = graphProvider.Client;
        }

        public async Task OnGet()
        {
            Weeks = await GetCompletedWeeks();
        }

        async Task<IEnumerable<DisplayWeek>> GetCompletedWeeks()
        {
            var weeks = new List<DisplayWeek>();

            var completedTasks = await GetCompletedTasks();
            var date = DateTime.Now.Date.AddDays(1 - (int)DateTime.Now.DayOfWeek);
            for (var week = 0; week < 26; week++)
            {
                var weekStart = date.AddDays(-7 * week);
                var weekEnd = weekStart.AddDays(7);
                var weekTasks = completedTasks.Where(task =>
                {
                    var date = DateTime.Parse(task.CompletedDateTime.DateTime);
                    return weekStart <= date && date < weekEnd;
                });
                weeks.Add(new DisplayWeek(new DateTimeOffset(weekStart), weekTasks.Count()));
            }

            return weeks;
        }

        async Task<IEnumerable<TodoTask>> GetCompletedTasks()
        {
            var result = new List<TodoTask>();
            var lists = await Graph.Me.Todo.Lists.Request().Top(1000).GetAsync();
            foreach (var list in lists)
            {
                result.AddRange(await Graph.Me.Todo.Lists[list.Id].Tasks.Request()
                    .OrderBy("completedDateTime/dateTime desc")
                    .Filter("completedDateTime/dateTime ne null")
                    .Top(100)
                    .GetAsync());
            }
            return result;
        }

        public record DisplayWeek(DateTimeOffset Date, int Completed);
    }
}
