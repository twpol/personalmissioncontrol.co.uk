using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using app.Models;
using app.Services;

namespace app.Pages
{
    [ResponseCache(Duration = 1, Location = ResponseCacheLocation.Client)]
    public class IndexModel : PageModel
    {
        public IEnumerable<HabitModel> Habits = null!;
        public IEnumerable<TaskModel> Tasks = null!;

        readonly IList<IHabitProvider> HabitProviders;
        readonly IList<ITaskProvider> TaskProviders;

        public IndexModel(IEnumerable<IHabitProvider> habitProviders, IEnumerable<ITaskProvider> taskProviders)
        {
            HabitProviders = habitProviders.ToList();
            TaskProviders = taskProviders.ToList();
        }

        public async Task OnGet()
        {
            Habits = await HabitProviders.SelectManyAsync(data => data.GetHabits()).ToListAsync();
            Tasks = await TaskProviders.SelectManyAsync(data => data.GetTasks().Where(task => task.IsImportant && !task.IsCompleted)).ToListAsync();
        }
    }
}
