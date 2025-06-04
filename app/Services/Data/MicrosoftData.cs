using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using app.Auth;
using app.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;

namespace app.Services.Data
{
    public static class MicrosoftDataExtensions
    {
        public static IServiceCollection AddMicrosoftData(this IServiceCollection services)
        {
            services.AddScoped<MicrosoftData>();
            services.AddScoped<IDataProvider, MicrosoftData>(s => s.GetRequiredService<MicrosoftData>());
            services.AddScoped<ITaskDataProvider, MicrosoftData>(s => s.GetRequiredService<MicrosoftData>());
            return services;
        }
    }

    public class MicrosoftData : ITaskDataProvider
    {
        readonly ILogger<MicrosoftData> Logger;
        readonly GraphServiceClient? Graph;
        readonly string AccountId;
        readonly IModelStore<TaskListModel> TaskLists;
        readonly IModelStore<TaskModel> Tasks;

        public MicrosoftData(ILogger<MicrosoftData> logger, MicrosoftGraphProvider graphProvider, IModelStore<TaskListModel> taskLists, IModelStore<TaskModel> tasks)
        {
            Logger = logger;
            graphProvider.TryGet("Microsoft", out Graph, out AccountId);
            TaskLists = taskLists;
            Tasks = tasks;
            if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug(".ctor({AccountId})", AccountId);
        }

        public async Task UpdateData()
        {
            if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug("UpdateData({AccountId})", AccountId);
            await UpdateTasks();
        }

        public async IAsyncEnumerable<TaskListModel> GetTaskLists()
        {
            if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug("GetTaskLists({AccountId})", AccountId);
            await foreach (var taskList in TaskLists.GetCollectionAsync(AccountId, ""))
            {
                yield return taskList;
            }
        }

        public async IAsyncEnumerable<TaskModel> GetTasks()
        {
            if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug("GetTasks({AccountId})", AccountId);
            if (Graph == null) yield break;
            await foreach (var task in Tasks.GetCollectionAsync(AccountId, null))
            {
                yield return task;
            }
        }

        public async IAsyncEnumerable<TaskModel> GetTasks(string listId)
        {
            if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug("GetTasks({AccountId}, {ListId})", AccountId, listId);
            if (Graph == null) yield break;
            await foreach (var task in Tasks.GetCollectionAsync(AccountId, listId))
            {
                yield return task;
            }
        }

        public async Task<TaskModel> CreateTask(string listId, string title, string body, bool isImportant, DateTimeOffset? completed)
        {
            if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug("CreateTask({AccountId}, {ListId})", AccountId, listId);
            if (Graph == null) throw new InvalidOperationException("Graph client not available");
            var createdTask = await Graph.Me.Todo.Lists[listId].Tasks.Request().AddAsync(new TodoTask
            {
                Title = title,
                Body = new ItemBody { Content = body, ContentType = BodyType.Text },
                Importance = isImportant ? Importance.High : Importance.Normal,
                Status = completed.HasValue ? Microsoft.Graph.TaskStatus.Completed : Microsoft.Graph.TaskStatus.NotStarted,
                CompletedDateTime = completed.HasValue ? DateTimeTimeZone.FromDateTimeOffset(completed.Value) : null,
            });
            var task = FromApi(listId, createdTask);
            await Tasks.SetItemAsync(task);
            return task;
        }

        public async Task UpdateTask(TaskModel task)
        {
            if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug("UpdateTask({AccountId}, {ListId}, {TaskId})", AccountId, task.ParentId, task.ItemId);
            if (Graph == null) throw new InvalidOperationException("Graph client not available");
            // HACK: Microsoft Tasks do not update the completed date unless changing status, so force the issue by marking not-started first
            if (task.IsCompleted)
            {
                var oldTask = await Tasks.GetItemAsync(task.AccountId, task.ParentId, task.ItemId);
                if (oldTask == null || oldTask.Completed != task.Completed)
                {
                    await Graph.Me.Todo.Lists[task.ParentId].Tasks[task.ItemId].Request().UpdateAsync(new TodoTask
                    {
                        Status = Microsoft.Graph.TaskStatus.NotStarted,
                    });
                }
            }
            await Graph.Me.Todo.Lists[task.ParentId].Tasks[task.ItemId].Request().UpdateAsync(new TodoTask
            {
                Title = task.Title,
                Importance = task.IsImportant ? Importance.High : Importance.Normal,
                Status = task.IsCompleted ? Microsoft.Graph.TaskStatus.Completed : Microsoft.Graph.TaskStatus.NotStarted,
                CompletedDateTime = task.IsCompleted ? DateTimeTimeZone.FromDateTimeOffset(task.Completed!.Value) : null,
            });
            await Tasks.SetItemAsync(task);
        }

        async Task UpdateTasks()
        {
            if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug("UpdateTasks({AccountId})", AccountId);
            await TaskLists.UpdateCollectionAsync(AccountId, "", UpdateCollectionTaskLists);
            await foreach (var taskList in TaskLists.GetCollectionAsync(AccountId, ""))
            {
                await Tasks.UpdateCollectionAsync(AccountId, taskList.ItemId, () => UpdateCollectionTasks(taskList.ItemId));
            }
        }

        async IAsyncEnumerable<TaskListModel> UpdateCollectionTaskLists()
        {
            if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug("UpdateCollectionTaskLists({AccountId})", AccountId);
            if (Graph == null) yield break;
            var lists = await Graph.Me.Todo.Lists.Request().Top(1000).GetAsync();
            while (lists != null)
            {
                foreach (var list in lists) yield return FromApi(list);
                lists = lists.NextPageRequest != null ? await lists.NextPageRequest.GetAsync() : null;
            }
        }

        async IAsyncEnumerable<TaskModel> UpdateCollectionTasks(string listId)
        {
            if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug("UpdateCollectionTasks({AccountId}, {ListId})", AccountId, listId);
            if (Graph == null) yield break;
            var tasks = await Graph.Me.Todo.Lists[listId].Tasks.Request().Top(1000).GetAsync();
            while (tasks != null)
            {
                foreach (var task in tasks) yield return FromApi(listId, task);
                tasks = tasks.NextPageRequest != null ? await tasks.NextPageRequest.GetAsync() : null;
            }
        }

        TaskListModel FromApi(TodoTaskList list)
        {
            var (Emoji, Text) = GetSplitEmojiName(list.DisplayName);
            return new TaskListModel(AccountId, "", list.Id, Emoji, Text, list.WellknownListName == WellknownListName.DefaultList ? TaskListSpecial.Default : list.WellknownListName == WellknownListName.FlaggedEmails ? TaskListSpecial.Emails : TaskListSpecial.None);
        }

        static (string Emoji, string Text) GetSplitEmojiName(string name)
        {
            var first = StringInfo.GetNextTextElement(name);
            if (IsTextElementEmoji(first)) return (first, name[first.Length..].Trim());
            return ("", name);
        }

        static bool IsTextElementEmoji(string text)
        {
            var runes = text.EnumerateRunes();
            var unicode = runes.Select(rune => Rune.GetUnicodeCategory(rune));
            return runes.Any(rune => rune.Value == 0xFE0F) || unicode.Any(category => category == UnicodeCategory.OtherSymbol);
        }

        TaskModel FromApi(string listId, TodoTask task)
        {
            return new TaskModel(AccountId, listId, task.Id, task.Title, task.Importance == Importance.High, task.CreatedDateTime ?? DateTimeOffset.MinValue, task.Status == Microsoft.Graph.TaskStatus.Completed ? GetDTO(task.CompletedDateTime) : null);
        }

        static DateTimeOffset? GetDTO(DateTimeTimeZone dateTimeTimeZone)
        {
            if (dateTimeTimeZone == null) return null;
            return dateTimeTimeZone.TimeZone switch
            {
                "UTC" => (DateTimeOffset?)DateTimeOffset.ParseExact(dateTimeTimeZone.DateTime + "Z", "o", CultureInfo.InvariantCulture),
                _ => throw new InvalidDataException($"Unknown time zone: {dateTimeTimeZone.TimeZone}"),
            };
        }
    }
}
