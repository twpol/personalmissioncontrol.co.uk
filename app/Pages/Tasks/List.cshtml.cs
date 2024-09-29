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
    public class ListModel : PageModel
    {
        public string Title = null!;
        public TaskListModel List = null!;
        public IEnumerable<TaskModel> Tasks = null!;

        readonly IList<ITaskDataProvider> TaskProviders;

        public ListModel(IEnumerable<ITaskDataProvider> taskProviders)
        {
            TaskProviders = taskProviders.ToList();
        }

        public async Task<IActionResult> OnGetList(string list)
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
            await OnGetList(list);
            var taskParents = Tasks.Where(task => task.Tag != null).ToDictionary(task => task.Tag!, task => task);
            var tasks = await TaskProviders.SelectManyAsync(provider => provider.GetTasks()).ToListAsync();
            tasks = tasks.Where(task => task.Tags.Count > 0).ToList();
            foreach (var depth in Enumerable.Range(0, 10))
            {
                var remainingTasks = new List<TaskModel>();
                foreach (var task in tasks)
                {
                    if (taskParents.ContainsKey(task.Tags[0]))
                    {
                        taskParents[task.Tags[0]].Children.Add(task);
                        if (task.Tag != null) taskParents[task.Tag] = task;
                    }
                    else
                    {
                        remainingTasks.Add(task);
                    }
                }
                tasks = remainingTasks;
            }
        }

        public async Task OnGetSearch(string text)
        {
            Title = text;

            var tasks = await TaskProviders.SelectManyAsync(provider => provider.GetTasks()).ToListAsync();
            Tasks = tasks.Where(task => task.Title.Contains(Title)).OrderBy(task => task.SortKey);
        }
    }
}
