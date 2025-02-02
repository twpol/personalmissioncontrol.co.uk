using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using app.Filters;
using app.Models;
using app.Services;
using Microsoft.AspNetCore.Mvc;

namespace app.Controllers;

[ApiController]
[ApiModelFilter]
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
    public async Task<IActionResult> CreateTask(string listId, [FromHeader(Name = "Task-Key")] string? taskKey, [FromForm] string name, [FromForm] string? description, [FromForm] bool? important, [FromForm] DateTimeOffset? completed)
    {
        if (string.IsNullOrWhiteSpace(name)) return BadRequest("Name is required");
        if (taskKey != null && !name.Contains(taskKey)) return BadRequest("Name does not contain Task-Key");

        var (provider, list) = await GetProviderTaskList(listId);
        if (provider == null || list == null) return NotFound("List not found");

        var task = await provider.GetTasks(list.ItemId).FirstOrDefaultAsync(t => taskKey == null ? t.Name == name && !t.Completed.HasValue : t.Name.Contains(taskKey));
        if (task != null)
        {
            if (name != null) task = task with { Title = name };
            // TODO: if (description != null) task = task with { Description = description };
            if (important.HasValue) task = task with { IsImportant = important.Value };
            if (Request.Form.ContainsKey("completed")) task = task with { Completed = completed };
            await provider.UpdateTask(task);
            return Ok(task);
        }
        return Ok(await provider.CreateTask(list.ItemId, name, description ?? "", important ?? false, completed));
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
