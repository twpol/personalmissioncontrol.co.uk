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
            services.AddScoped<ITaskProvider, MicrosoftData>(s => s.GetRequiredService<MicrosoftData>());
            return services;
        }
    }

    public class MicrosoftData : ITaskProvider
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

        public async IAsyncEnumerable<TaskListModel> GetTaskLists()
        {
            if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug("GetTaskLists({AccountId})", AccountId);
            await foreach (var taskList in TaskLists.GetCollectionAsync(AccountId, ""))
            {
                yield return taskList;
            }
            // Do update in the background
            _ = UpdateTasks();
        }

        public async IAsyncEnumerable<TaskModel> GetTasks()
        {
            if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug("GetTasks({AccountId})", AccountId);
            if (Graph == null) yield break;
            await foreach (var task in Tasks.GetCollectionAsync(AccountId, null))
            {
                yield return task;
            }
            // Do update in the background
            _ = UpdateTasks();
        }

        public async IAsyncEnumerable<TaskModel> GetTasks(string listId)
        {
            if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug("GetTasks({AccountId}, {ListId})", AccountId, listId);
            if (Graph == null) yield break;
            await foreach (var task in Tasks.GetCollectionAsync(AccountId, listId))
            {
                yield return task;
            }
            // Do update in the background
            _ = UpdateTasks();
        }

        public async Task UpdateTasks()
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
            foreach (var list in lists.Select(list => FromApi(list)))
            {
                yield return list;
            }
        }

        async IAsyncEnumerable<TaskModel> UpdateCollectionTasks(string listId)
        {
            if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug("UpdateCollectionTasks({AccountId}, {ListId})", AccountId, listId);
            if (Graph == null) yield break;
            var tasks = await Graph.Me.Todo.Lists[listId].Tasks.Request().Top(1000).GetAsync();
            foreach (var task in tasks.Select(task => FromApi(listId, task)))
            {
                yield return task;
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
