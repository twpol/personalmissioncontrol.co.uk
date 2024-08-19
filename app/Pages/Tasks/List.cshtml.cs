using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using app.Models;
using app.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace app.Pages.Tasks
{
    [ResponseCache(Duration = 1, Location = ResponseCacheLocation.Client)]
    public class ListModel : PageModel
    {
        public string Title = null!;
        public TaskListModel List = null!;
        public IEnumerable<TaskModel> Tasks = null!;
        public bool Nested;

        readonly IList<ITaskProvider> TaskProviders;

        public ListModel(IEnumerable<ITaskProvider> taskProviders)
        {
            TaskProviders = taskProviders.ToList();
        }

        public async Task<IActionResult> OnGet(string list)
        {
            var matchingList = await TaskProviders.SelectManyAsync(provider => provider.GetTaskLists()).FirstOrDefaultAsync(taskList => taskList.Id == list);
            if (matchingList == null) return NotFound("List not found");

            List = matchingList;
            Title = List.Name;
            Tasks = await TaskProviders.SelectManyAsync(provider => provider.GetTasks(List.ItemId)).ToListAsync();
            return Page();
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

            var tasks = await TaskProviders.SelectManyAsync(provider => provider.GetTasks()).ToListAsync();
            Tasks = tasks.Where(task => task.Title.Contains(Title)).OrderBy(task => task.SortKey);
        }

        public async Task OnGetChildren(string hashtag)
        {
            Title = $"#{hashtag}";
            Nested = HttpContext.Request.Query["layout"] == "nested";

            var tasks = await TaskProviders.SelectManyAsync(provider => provider.GetTasks()).ToListAsync();
            var pattern = new Regex($@". #{hashtag}(?: |$)");
            Tasks = tasks.Where(task => pattern.IsMatch(task.Title)).OrderBy(task => task.SortKey);
        }
    }
}
