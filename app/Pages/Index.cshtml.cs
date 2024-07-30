using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using app.Models;
using app.Services.Data;

namespace app.Pages
{
    [ResponseCache(Duration = 1, Location = ResponseCacheLocation.Client)]
    public class IndexModel : PageModel
    {
        public IEnumerable<TaskModel> Tasks = null!;
        public IEnumerable<HabitModel> Habits = null!;

        readonly ILogger<IndexModel> Logger;
        readonly ExistData Exist;
        readonly MicrosoftData Microsoft;

        public IndexModel(ILogger<IndexModel> logger, ExistData exist, MicrosoftData microsoft)
        {
            Logger = logger;
            Exist = exist;
            Microsoft = microsoft;
        }

        public async Task OnGet()
        {
            Habits = await Exist.GetHabits();
            var tasks = new List<TaskModel>();
            foreach (var list in await Microsoft.GetLists())
            {
                tasks.AddRange((await Microsoft.GetTasks(list.Id)).Where(task => task.IsImportant && !task.IsCompleted));
            }
            Tasks = tasks.OrderByDescending(task => task.Created);
        }
    }
}
