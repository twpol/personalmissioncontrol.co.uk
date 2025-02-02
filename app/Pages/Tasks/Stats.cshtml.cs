using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using app.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Task = System.Threading.Tasks.Task;

namespace app.Pages.Tasks
{
    [ResponseCache(Duration = 1, Location = ResponseCacheLocation.Client)]
    public class StatsModel : PageModel
    {
        public IEnumerable<DisplayWeek> Weeks = null!;

        readonly IList<ITaskDataProvider> TaskProviders;

        public StatsModel(IEnumerable<ITaskDataProvider> taskProviders)
        {
            TaskProviders = taskProviders.ToList();
        }

        public async Task OnGet()
        {
            Weeks = await GetCompletedWeeks();
        }

        async Task<IEnumerable<DisplayWeek>> GetCompletedWeeks()
        {
            var weeks = new List<DisplayWeek>();

            var tasks = await TaskProviders.SelectManyAsync(provider => provider.GetTasks()).ToListAsync();
            var date = DateTime.Now.Date.AddDays(-((int)DateTime.Now.DayOfWeek + 6) % 7);
            for (var week = 0; week < 26; week++)
            {
                var weekStart = date.AddDays(-7 * week);
                var weekEnd = weekStart.AddDays(7);
                var weekTasksCreated = tasks.Where(task => weekStart <= task.EarliestDate && task.EarliestDate < weekEnd);
                var weekTasksCompleted = tasks.Where(task => task.Completed != null && weekStart <= task.Completed && task.Completed < weekEnd);
                var weekTasksCreatedImportant = weekTasksCreated.Where(task => task.Important);
                var weekTasksCompletedImportant = weekTasksCompleted.Where(task => task.Important);
                weeks.Add(new DisplayWeek(
                    new DateTimeOffset(weekStart),
                    weekTasksCreated.Count(),
                    weekTasksCompleted.Count(),
                    weekTasksCreatedImportant.Count(),
                    weekTasksCompletedImportant.Count()
                ));
            }

            return weeks;
        }

        public record DisplayWeek(DateTimeOffset Date, int Created, int Completed, int CreatedImportant, int CompletedImportant)
        {
            public int CreatedUnimportant => Created - CreatedImportant;
            public int CompletedUnimportant => Completed - CompletedImportant;
            public int Delta => Created - Completed;
        }
    }
}
