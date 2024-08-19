using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using app.Models;
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

        readonly IList<ITaskProvider> TaskProviders;

        public StatsModel(IEnumerable<ITaskProvider> taskProviders)
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

            var completedTasks = await GetCompletedTasks();
            var date = DateTime.Now.Date.AddDays(1 - (int)DateTime.Now.DayOfWeek);
            for (var week = 0; week < 26; week++)
            {
                var weekStart = date.AddDays(-7 * week);
                var weekEnd = weekStart.AddDays(7);
                var weekTasks = completedTasks.Where(task => weekStart <= task.Completed && task.Completed < weekEnd);
                weeks.Add(new DisplayWeek(new DateTimeOffset(weekStart), weekTasks.Count()));
            }

            return weeks;
        }

        async Task<IEnumerable<TaskModel>> GetCompletedTasks()
        {
            var tasks = await TaskProviders.SelectManyAsync(provider => provider.GetTasks()).ToListAsync();
            return tasks.Where(task => task.Completed != null);
        }

        public record DisplayWeek(DateTimeOffset Date, int Completed);
    }
}
