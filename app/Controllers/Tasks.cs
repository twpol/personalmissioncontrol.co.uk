using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using app.Services;
using Microsoft.AspNetCore.Mvc;

namespace app.Controllers;

[ApiController]
[Route("api/tasks")]
[Produces("application/json")]
public class TasksController : ControllerBase
{
    readonly IList<ITaskDataProvider> TaskProviders;

    public TasksController(IEnumerable<ITaskDataProvider> taskProviders)
    {
        TaskProviders = taskProviders.ToList();
    }

    [HttpGet("lists")]
    public async Task<IActionResult> GetTaskLists()
    {
        return Ok(await TaskProviders.SelectManyAsync(provider => provider.GetTaskLists()).ToListAsync());
    }

    [HttpGet("lists/{list}")]
    public async Task<IActionResult> GetTaskList(string list)
    {
        var matchingList = await TaskProviders.SelectManyAsync(provider => provider.GetTaskLists()).FirstOrDefaultAsync(taskList => taskList.Id == list);
        if (matchingList == null) return NotFound("List not found");
        return Ok(matchingList);
    }

    [HttpGet("lists/{list}/tasks")]
    public async Task<IActionResult> GetTasks(string list)
    {
        var matchingList = await TaskProviders.SelectManyAsync(provider => provider.GetTaskLists()).FirstOrDefaultAsync(taskList => taskList.Id == list);
        if (matchingList == null) return NotFound("List not found");
        return Ok(await TaskProviders.SelectManyAsync(provider => provider.GetTasks(matchingList.ItemId)).ToListAsync());
    }
}
