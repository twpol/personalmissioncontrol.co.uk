using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using app.Auth;
using app.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Graph;

namespace app.Services.Data
{
    public class MicrosoftData
    {
        readonly TimeSpan CacheTimeout = TimeSpan.FromSeconds(30);

        readonly GraphServiceClient? Graph;
        readonly IModelCache<IList<TodoTaskList>> TaskListCache;
        readonly IModelCache<IList<TaskModel>> TaskCache;

        public MicrosoftData(MicrosoftGraphProvider provider, IModelCache<IList<TodoTaskList>> taskListCache, IModelCache<IList<TaskModel>> taskCache)
        {
            Graph = provider.MaybeClient;
            TaskListCache = taskListCache;
            TaskCache = taskCache;
        }

        public async Task<IList<TodoTaskList>> GetLists()
        {
            if (Graph == null) return Array.Empty<TodoTaskList>();
            return await GetOrCreateAsync<IList<TodoTaskList>>(TaskListCache, "tasks:lists", async () =>
                await Graph.Me.Todo.Lists.Request().Top(1000).GetAsync()
            );
        }

        public async Task<IList<TaskModel>> GetTasks(string list)
        {
            if (Graph == null) return Array.Empty<TaskModel>();
            return await GetOrCreateAsync<IList<TaskModel>>(TaskCache, $"tasks:list:{list}", async () =>
            {
                return (await GetAllPages(Graph.Me.Todo.Lists[list].Tasks.Request().Top(1000)))
                    .Select(task => FromGraph(task))
                    .OrderBy(task => task.SortKey).ToList();
            });
        }

        async Task<T> GetOrCreateAsync<T>(IModelCache<T> cache, string subKey, Func<Task<T>> asyncFactory) where T : class
        {
            var key = $"{nameof(MicrosoftData)}:{subKey}";
            return (await cache.GetAsync(key)) ?? (await SetAsync(cache, key, asyncFactory));
        }

        static async Task<T> SetAsync<T>(IModelCache<T> cache, string key, Func<Task<T>> asyncFactory) where T : class
        {
            var obj = await asyncFactory();
            await cache.SetAsync(key, obj);
            return obj;
        }

        static async Task<IList<TodoTask>> GetAllPages(ITodoTaskListTasksCollectionRequest request)
        {
            var list = new List<TodoTask>();
            do
            {
                var task = await request.GetAsync();
                list.AddRange(task);
                request = task.NextPageRequest;
            } while (request != null);
            return list;
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

        static TaskModel FromGraph(TodoTask task)
        {
            return new TaskModel(task.Id, task.Title, task.Importance == Importance.High, task.CreatedDateTime ?? DateTimeOffset.MinValue, task.Status == Microsoft.Graph.TaskStatus.Completed ? GetDTO(task.CompletedDateTime) : null);
        }
    }
}
