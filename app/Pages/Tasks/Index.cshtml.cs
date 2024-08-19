using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using app.Models;
using app.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace app.Pages.Tasks
{
    [ResponseCache(Duration = 1, Location = ResponseCacheLocation.Client)]
    public class IndexModel : PageModel
    {
        public IEnumerable<TaskListModel> Lists = null!;

        readonly IList<ITaskProvider> TaskProviders;

        public IndexModel(IEnumerable<ITaskProvider> taskProviders)
        {
            TaskProviders = taskProviders.ToList();
        }

        public async Task OnGet()
        {
            Lists = await TaskProviders.SelectManyAsync(provider => provider.GetTaskLists()).ToListAsync();
        }
    }
}
