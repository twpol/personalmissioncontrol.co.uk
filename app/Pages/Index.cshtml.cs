using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using app.Models;
using app.Services.Data;
using app.Services;

namespace app.Pages
{
    [ResponseCache(Duration = 1, Location = ResponseCacheLocation.Client)]
    public class IndexModel : PageModel
    {
        public IEnumerable<TaskModel> Tasks = null!;
        public IEnumerable<HabitModel> Habits = null!;

        readonly ILogger<IndexModel> Logger;
        readonly IList<IHabitProvider> HabitProviders;
        readonly MicrosoftData Microsoft;

        public IndexModel(ILogger<IndexModel> logger, IEnumerable<IHabitProvider> habitProviders, MicrosoftData microsoft)
        {
            Logger = logger;
            HabitProviders = habitProviders.ToList();
            Microsoft = microsoft;
        }

        public async Task OnGet()
        {
            Habits = await HabitProviders.SelectManyAsync(data => data.GetHabits()).ToListAsync();
            var tasks = new List<TaskModel>();
            foreach (var list in await Microsoft.GetLists())
            {
                tasks.AddRange((await Microsoft.GetTasks(list.Id)).Where(task => task.IsImportant && !task.IsCompleted));
            }
            Tasks = tasks.OrderByDescending(task => task.Created);
        }
    }
}
