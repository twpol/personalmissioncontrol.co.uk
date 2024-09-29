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
            if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug(".ctor()");
        }

        public async Task<T?> GetItemAsync(string accountId, string parentId, string itemId)
        {
            if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug("<{Type}> GetItemAsync({AccountId}, {ParentId}, {ItemId})", typeof(T).Name, accountId, parentId, itemId);
            await foreach (var item in Container.GetItemsAsync<T>(model => model.AccountId == accountId && model.ParentId == parentId && model.ItemId == itemId))
            {
                return item;
            }
            return null;
        }

        public async Task SetItemAsync(T item)
        {
            if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug("<{Type}> SetItemAsync({AccountId}, {ParentId}, {ItemId})", typeof(T).Name, item.AccountId, item.ParentId, item.ItemId);
            await Container.UpsertItemAsync(item, new PartitionKey(item.Id));
        }

        public async Task DeleteItemAsync(T item)
        {
            if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug("<{Type}> DeleteItemAsync({AccountId}, {ParentId}, {ItemId})", typeof(T).Name, item.AccountId, item.ParentId, item.ItemId);
            await Container.DeleteItemAsync<T>(item.Id, new PartitionKey(item.Id));
        }

        public async IAsyncEnumerable<T> GetCollectionsAsync(string? accountId, string? parentId)
        {
            var startTime = Logger.IsEnabled(LogLevel.Debug) ? Stopwatch.GetTimestamp() : 0;
            if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug("<{Type}> GetCollectionsAsync({AccountId}, {ParentId})", typeof(T).Name, accountId, parentId);

            await foreach (var item in Container.GetItemsAsync<T>(model => (accountId == null || model.AccountId == accountId) && (parentId == null || model.ParentId == parentId) && model.ItemId == ""))
            {
                if (Logger.IsEnabled(LogLevel.Trace)) Logger.LogTrace("<{Type}> GetCollectionsAsync({AccountId}, {ParentId}) <{ItemId}> Get", typeof(T).Name, accountId, parentId, item.Id);
                yield return item;
            }

            var stopTime = Logger.IsEnabled(LogLevel.Debug) ? Stopwatch.GetTimestamp() : 0;
            if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug("<{Type}> GetCollectionsAsync({AccountId}, {ParentId}) {Duration:F3} s", typeof(T).Name, accountId, parentId, (float)(stopTime - startTime) / Stopwatch.Frequency);
        }

        public async IAsyncEnumerable<T> GetCollectionAsync(string accountId, string? parentId)
        {
            var startTime = Logger.IsEnabled(LogLevel.Debug) ? Stopwatch.GetTimestamp() : 0;
            if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug("<{Type}> GetCollectionAsync({AccountId}, {ParentId})", typeof(T).Name, accountId, parentId);

            await foreach (var item in Container.GetItemsAsync<T>(model => model.AccountId == accountId && (parentId == null || model.ParentId == parentId) && model.ItemId != ""))
            {
                if (Logger.IsEnabled(LogLevel.Trace)) Logger.LogTrace("<{Type}> GetCollectionAsync({AccountId}, {ParentId}) <{ItemId}> Get", typeof(T).Name, accountId, parentId, item.Id);
                yield return item;
            }

            var stopTime = Logger.IsEnabled(LogLevel.Debug) ? Stopwatch.GetTimestamp() : 0;
            if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug("<{Type}> GetCollectionAsync({AccountId}, {ParentId}) {Duration:F3} s", typeof(T).Name, accountId, parentId, (float)(stopTime - startTime) / Stopwatch.Frequency);
        }

        public async Task UpdateCollectionAsync(string accountId, string? parentId, Func<IAsyncEnumerable<T>> updater)
        {
            if (UpdateTTL == null) return;

            var startTime = Logger.IsEnabled(LogLevel.Debug) ? Stopwatch.GetTimestamp() : 0;
            var containerId = $"{accountId}~{parentId}~";
            CollectionModel? container;
            do
            {
                container = await GetContainer(containerId);
                var update = container == null || DateTimeOffset.Parse(container.Change).Add(UpdateTTL.Value) < DateTimeOffset.UtcNow;
                if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug("<{Type}> UpdateCollectionAsync({AccountId}, {ParentId}) Change = {Change}, Update = {Update}", typeof(T).Name, accountId, parentId, container?.Change, update);
                if (!update) return;

                if (container == null)
                {
                    container = new CollectionModel(accountId, parentId ?? "", "");
                    container = await Container.UpsertItemAsync(container, new PartitionKey(container.Id));
                }

                try
                {
                    // Capture date/time before updater starts, to ensure overlap of time periods
                    container = await Container.PatchItemAsync<CollectionModel>(container.Id, new PartitionKey(container.Id), new[] { PatchOperation.Set("/Change", DateTimeOffset.Now.ToRfc3339(DateTimeKind.Utc)) }, new PatchItemRequestOptions { FilterPredicate = $"FROM c WHERE c.Change = '{container.Change}'" });
                }
                catch (CosmosException error) when (error.StatusCode == System.Net.HttpStatusCode.PreconditionFailed)
                {
                    continue;
                }
            } while (false);

            await foreach (var item in updater())
            {
                if (Logger.IsEnabled(LogLevel.Trace)) Logger.LogTrace("<{Type}> UpdateCollectionAsync({AccountId}, {ParentId}) <{ItemId}> Upsert", typeof(T).Name, accountId, parentId, item.Id);
                await Container.UpsertItemAsync(item, new PartitionKey(item.Id));
            }

            await foreach (var item in Container.GetItemsAsync<T>(model => model.AccountId == accountId && (parentId == null || model.ParentId == parentId) && model.ItemId != "" && model.TimeStamp < container.TimeStamp))
            {
                if (Logger.IsEnabled(LogLevel.Trace)) Logger.LogTrace("<{Type}> UpdateCollectionAsync({AccountId}, {ParentId}) <{ItemId}> Delete", typeof(T).Name, accountId, parentId, item.Id);
                await Container.DeleteItemAsync<T>(item.Id, new PartitionKey(item.Id));
            }

            var stopTime = Logger.IsEnabled(LogLevel.Debug) ? Stopwatch.GetTimestamp() : 0;
            if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug("<{Type}> UpdateCollectionAsync({AccountId}, {ParentId}) {Duration:F3} s", typeof(T).Name, accountId, parentId, (float)(stopTime - startTime) / Stopwatch.Frequency);
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
