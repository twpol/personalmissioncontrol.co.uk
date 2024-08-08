using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using app.Auth;
using app.Models;
using app.Services;
using app.Services.Data;

namespace app.Pages.Microsoft.Tasks
{
    public class IndexModel : MicrosoftPageModel
    {
        public IEnumerable<TaskListModel> Lists = null!;

        readonly ITaskProvider TaskProvider;

        public IndexModel(MicrosoftData taskProvider)
        {
            TaskProvider = taskProvider;
        }

        public async Task OnGet()
        {
            Lists = await TaskProvider.GetTaskLists().ToListAsync();
        }
    }
}
