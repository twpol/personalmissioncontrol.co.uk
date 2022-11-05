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

        GraphServiceClient Graph;
        IMemoryCache Cache;

        public MicrosoftData(MicrosoftGraphProvider provider, DataMemoryCache cache)
        {
            Graph = provider.Client;
            Cache = cache.Cache;
        }

        public async Task<IList<TodoTaskList>> GetLists()
        {
            if (Graph == null) return new TodoTaskList[0];
            return await GetOrCreateAsync("tasks:lists", async () =>
                await Graph.Me.Todo.Lists.Request().Top(1000).GetAsync()
            );
        }

        public async Task<IList<TaskModel>> GetTasks(string list)
        {
            if (Graph == null) return new TaskModel[0];
            return await GetOrCreateAsync($"tasks:list:{list}", async () =>
            {
                return (await GetAllPages(Graph.Me.Todo.Lists[list].Tasks.Request().Top(1000)))
                    .Select(task => new TaskModel(task.Id, task.Title, task.Status ?? Microsoft.Graph.TaskStatus.NotStarted, task.Importance ?? Importance.Normal, GetDTO(task.CompletedDateTime)))
                    .OrderBy(task => task.SortKey).ToList();
            });
        }

        async Task<T> GetOrCreateAsync<T>(string subKey, Func<Task<T>> asyncFactory)
        {
            var key = $"{nameof(MicrosoftData)}:{subKey}";
            if (!Cache.TryGetValue<T>(key, out var obj))
            {
                obj = await asyncFactory();
                Cache.Set(key, obj, new MemoryCacheEntryOptions()
                {
                    Size = 1,
                });
            }
            return obj;
        }

        async Task<IList<TodoTask>> GetAllPages(ITodoTaskListTasksCollectionRequest request)
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

        DateTimeOffset? GetDTO(DateTimeTimeZone dateTimeTimeZone)
        {
            if (dateTimeTimeZone == null) return null;
            switch (dateTimeTimeZone.TimeZone)
            {
                case "UTC":
                    return DateTimeOffset.ParseExact(dateTimeTimeZone.DateTime + "Z", "o", CultureInfo.InvariantCulture);
                default:
                    throw new InvalidDataException($"Unknown time zone: {dateTimeTimeZone.TimeZone}");
            }
        }
    }
}
