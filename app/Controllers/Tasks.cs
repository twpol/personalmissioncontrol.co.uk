using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using app.Models;
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

    [HttpGet("lists/{listId}")]
    public async Task<IActionResult> GetTaskList(string listId)
    {
        var (provider, list) = await GetProviderTaskList(listId);
        if (provider == null || list == null) return NotFound("List not found");
        return Ok(list);
    }

    [HttpGet("lists/{listId}/tasks")]
    public async Task<IActionResult> GetTasks(string listId)
    {
        var (provider, list) = await GetProviderTaskList(listId);
        if (provider == null || list == null) return NotFound("List not found");
        return Ok(await provider.GetTasks(list.ItemId).ToListAsync());
    }

    [HttpPost("lists/{listId}/tasks")]
    public async Task<IActionResult> CreateTask(string listId, [FromHeader(Name = "If-None-Match")] string? ifNoneMatch, [FromForm] string? title, [FromForm] string? body, [FromForm] bool? isImportant, [FromForm] bool? isCompleted)
    {
        if (ifNoneMatch == null && title == null) return BadRequest("Title or If-None-Match is required");
        var (provider, list) = await GetProviderTaskList(listId);
        if (provider == null || list == null) return NotFound("List not found");
        if (ifNoneMatch == null) return Ok(await provider.CreateTask(list.ItemId, title ?? "", body ?? "", isImportant ?? false, isCompleted ?? false));

        if (ifNoneMatch.StartsWith("\"") && ifNoneMatch.EndsWith("\"")) ifNoneMatch = ifNoneMatch[1..^1];
        if (title != null && !title.Contains(ifNoneMatch)) return BadRequest("Title does not contain If-None-Match");
        var task = await provider.GetTasks(list.ItemId).FirstOrDefaultAsync(t => t.Title.Contains(ifNoneMatch));
        if (task == null) return NotFound("Task not found");
        if (title != null) task = task with { Title = title };
        if (body != null) return BadRequest("Body cannot be updated");
        if (isImportant.HasValue) task = task with { IsImportant = isImportant.Value };
        if (isCompleted.HasValue) task = task with { Completed = isCompleted.Value ? DateTimeOffset.UtcNow : null };
        await provider.UpdateTask(task);
        return Ok(task);
    }

    async Task<(ITaskDataProvider? Provider, TaskListModel? List)> GetProviderTaskList(string listId)
    {
        foreach (var provider in TaskProviders)
        {
            await foreach (var list in provider.GetTaskLists())
            {
                if (list.Id != listId) continue;
                return (provider, list);
            }
        }
        return (null, null);
    }
}
