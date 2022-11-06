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
        public IEnumerable<TaskModel> Tasks;

        private readonly ILogger<IndexModel> _logger;
        private readonly MicrosoftData _data;

        public IndexModel(ILogger<IndexModel> logger, MicrosoftData data)
        {
            _logger = logger;
            _data = data;
        }

        public async Task OnGet()
        {
            var tasks = new List<TaskModel>();
            foreach (var list in await _data.GetLists())
            {
                tasks.AddRange((await _data.GetTasks(list.Id)).Where(task => task.IsImportant && !task.IsCompleted));
            }
            Tasks = tasks.OrderByDescending(task => task.Created);
        }
    }
}
