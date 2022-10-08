using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using app.Auth;
using app.Data;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Graph;

namespace app.Services.Data
{
    public class MicrosoftData
    {
        readonly TimeSpan CacheTimeout = TimeSpan.FromSeconds(30);

        GraphServiceClient Graph;
        IDistributedCache Cache;

        public MicrosoftData(MicrosoftGraphProvider provider, IDistributedCache cache)
        {
            Graph = provider.Client;
            Cache = cache;
        }

        public async Task<IList<TodoTaskList>> GetLists()
        {
            if (Graph == null) return new TodoTaskList[0];
            return await GetOrCreateAsync("tasks:lists", async () =>
                await Graph.Me.Todo.Lists.Request().Top(1000).GetAsync()
            );
        }

        public async Task<IList<DisplayTask>> GetTasks(string list)
        {
            if (Graph == null) return new DisplayTask[0];
            return await GetOrCreateAsync($"tasks:list:{list}", async () =>
            {
                return (await GetAllPages(Graph.Me.Todo.Lists[list].Tasks.Request().Top(1000)))
                    .Select(task => new DisplayTask(task.Id, task.Title, task.Status ?? Microsoft.Graph.TaskStatus.NotStarted, task.Importance ?? Importance.Normal, GetDTO(task.CompletedDateTime)))
                    .OrderBy(task => task.SortKey).ToList();
            });
        }

        async Task<T> GetOrCreateAsync<T>(string subKey, Func<Task<T>> asyncFactory)
        {
            var key = $"{nameof(MicrosoftData)}:{subKey}";
            var waitKey = $"{key}:wait";
            await WaitForCache(waitKey);
            var json = await Cache.GetStringAsync(key);
            if (json != null) return JsonSerializer.Deserialize<T>(json);
            await Cache.SetStringAsync(waitKey, nameof(GetOrCreateAsync));
            try
            {
                var obj = await asyncFactory();
                await Cache.SetStringAsync(key, JsonSerializer.Serialize<T>(obj));
                return obj;
            }
            finally
            {
                await Cache.RemoveAsync(waitKey);
            }
        }

        async Task WaitForCache(string waitKey)
        {
            var end = DateTimeOffset.Now + CacheTimeout;
            while (DateTimeOffset.Now < end)
            {
                if (await Cache.GetAsync(waitKey) == null) return;
                await Task.Delay(100);
            }
            throw new InvalidOperationException($"Timeout waiting for {waitKey} to be removed");
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
