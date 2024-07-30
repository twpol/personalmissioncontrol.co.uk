using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using app.Models;
using System.Diagnostics;

namespace app.Services
{
    public static class CosmosModelStoreOptionsExtensions
    {
        public static IServiceCollection AddCosmosModelStore(this IServiceCollection services) => services.AddSingleton(typeof(IModelStore<>), typeof(CosmosModelStore<>));
        public static IServiceCollection AddCosmosModelStore(this IServiceCollection services, Action<CosmosModelStoreOptions> setupAction) => services.AddCosmosModelStore().Configure(setupAction);
        public static IAsyncEnumerable<T> GetItemsAsync<T>(this Container container, System.Linq.Expressions.Expression<Func<T, bool>> predicate) => container.GetItemLinqQueryable<T>().Where(predicate).ToAsyncEnumerable();
        static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(this IQueryable<T> query)
        {
            using var feed = query.ToFeedIterator();
            while (feed.HasMoreResults)
            {
                foreach (var item in await feed.ReadNextAsync())
                {
                    yield return item;
                }
            }
        }
    }

    public class CosmosModelStoreOptions
    {
        public string StorageDatabase { get; set; } = "Models";
        public TimeSpan? StorageTTL { get; set; }
        public TimeSpan? UpdateTTL { get; set; }
    }

    public class CosmosModelStore<T> : IModelStore<T> where T : BaseModel
    {
        readonly ILogger<CosmosModelStore<T>> Logger;
        readonly TimeSpan? UpdateTTL;
        readonly Container Container;

        public CosmosModelStore(ILogger<CosmosModelStore<T>> logger, IOptions<CosmosModelStoreOptions> options, ICosmosStorage storage)
        {
            Logger = logger;
            UpdateTTL = options.Value.UpdateTTL;
            Container = storage.GetContainerAsync(options.Value.StorageDatabase, typeof(T).Name, (int?)options.Value.StorageTTL?.TotalSeconds).Result;
        }

        public async IAsyncEnumerable<T> GetCollectionAsync(string accountId)
        {
            var startTime = Logger.IsEnabled(LogLevel.Debug) ? Stopwatch.GetTimestamp() : 0;
            if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug($"<{typeof(T).Name}> GetCollectionAsync({accountId})");

            await foreach (var item in Container.GetItemsAsync<T>(model => model.AccountId == accountId && model.ItemId != ""))
            {
                if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug($"<{typeof(T).Name}> GetCollectionAsync({accountId}) <{item.Id}> Get");
                yield return item;
            }

            var stopTime = Logger.IsEnabled(LogLevel.Debug) ? Stopwatch.GetTimestamp() : 0;
            if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug($"<{typeof(T).Name}> GetCollectionAsync({accountId}) {(float)(stopTime - startTime) / Stopwatch.Frequency:F3} s");
        }

        public async Task UpdateCollectionAsync(string accountId, Func<IAsyncEnumerable<T>> updater)
        {
            if (UpdateTTL == null) return;

            var startTime = Logger.IsEnabled(LogLevel.Debug) ? Stopwatch.GetTimestamp() : 0;
            var containerId = $"{accountId}~~";
            var container = await GetContainer(containerId);
            var update = container == null || container.Change.Add(UpdateTTL.Value) < DateTimeOffset.Now;
            if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug($"<{typeof(T).Name}> UpdateCollectionAsync({accountId}) Change = {container?.Change}, Update = {update}");
            if (!update) return;

            // Capture date/time before updater starts, to ensure overlap of time periods
            container = new CollectionModel(accountId, "", DateTimeOffset.Now);

            await foreach (var item in updater())
            {
                if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug($"<{typeof(T).Name}> UpdateCollectionAsync({accountId}) <{item.Id}> Upsert");
                await Container.UpsertItemAsync(item, new PartitionKey(item.Id));
            }

            await foreach (var item in Container.GetItemsAsync<T>(model => model.AccountId == accountId && model.ItemId != "" && model.TimeStamp < container.TimeStamp))
            {
                if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug($"<{typeof(T).Name}> UpdateCollectionAsync({accountId}) <{item.Id}> Delete");
                await Container.DeleteItemAsync<T>(item.Id, new PartitionKey(item.Id));
            }

            if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug($"<{typeof(T).Name}> UpdateCollectionAsync({accountId}) <{container.Id}> Upsert");
            await Container.UpsertItemAsync(container, new PartitionKey(container.Id));

            var stopTime = Logger.IsEnabled(LogLevel.Debug) ? Stopwatch.GetTimestamp() : 0;
            if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug($"<{typeof(T).Name}> UpdateCollectionAsync({accountId}) {(float)(stopTime - startTime) / Stopwatch.Frequency:F3} s");
        }

        async Task<CollectionModel?> GetContainer(string containerId)
        {
            try
            {
                return (await Container.ReadItemAsync<CollectionModel>(containerId, new PartitionKey(containerId))).Resource;
            }
            catch (CosmosException error) when (error.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
        }
    }
}
