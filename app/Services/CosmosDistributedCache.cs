using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace app.Services
{
    public static class CosmosDistributedCacheExtensions
    {
        public static IServiceCollection AddDistributedCosmosCache(this IServiceCollection services) => services.AddSingleton<IDistributedCache, CosmosDistributedCache>();
        public static IServiceCollection AddDistributedCosmosCache(this IServiceCollection services, Action<CosmosDistributedCacheOptions> setupAction) => services.AddDistributedCosmosCache().Configure(setupAction);
    }

    public class CosmosDistributedCacheOptions
    {
        public string StorageEndpoint { get; set; }
        public string StorageKey { get; set; }
        public string StorageDatabase { get; set; } = "cache";
        public string StorageContainer { get; set; } = "cache";
    }

    public class CosmosDistributedCache : IDistributedCache
    {
        readonly ILogger Logger;
        readonly CosmosClient Client;
        readonly Database Database;
        readonly Container Container;

        public CosmosDistributedCache(IOptions<CosmosDistributedCacheOptions> optionsAccessor, ILoggerFactory loggerFactory)
        {
            Logger = loggerFactory.CreateLogger<CosmosDistributedCache>();
            var options = optionsAccessor.Value;
            Client = new CosmosClient(options.StorageEndpoint, options.StorageKey);
            Database = Client.CreateDatabaseIfNotExistsAsync(options.StorageDatabase).Result;
            Container = Database.CreateContainerIfNotExistsAsync(options.StorageContainer, "/id").Result;
            var _ = Container.ReplaceContainerAsync(new ContainerProperties(Container.Id, "/id")
            {
                DefaultTimeToLive = 43200,
            }).Result;
        }

        public byte[] Get(string key) => GetAsync(key).Result;

        public async Task<byte[]> GetAsync(string key, CancellationToken token = default)
        {
            try
            {
                return await Do($"Get({key})", async () => (await Container.ReadItemAsync<CacheEntry>(key, new PartitionKey(key))).Resource.value);
            }
            catch (CosmosException error) when (error.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        public void Refresh(string key) => RefreshAsync(key).Wait();

        public async Task RefreshAsync(string key, CancellationToken token = default)
        {
            await Do($"Refresh({key})", () => Container.PatchItemAsync<CacheEntry>(key, new PartitionKey(key), new[] { PatchOperation.Set("/patch", 0) }));
        }

        public void Remove(string key) => RemoveAsync(key).Wait();

        public async Task RemoveAsync(string key, CancellationToken token = default)
        {
            await Do($"Remove({key})", () => Container.DeleteItemAsync<CacheEntry>(key, new PartitionKey(key)));
        }

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => SetAsync(key, value, options).Wait();

        public async Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            await Do($"Set({key}, {value.Length} bytes)", () => Container.UpsertItemAsync<CacheEntry>(new CacheEntry(key, 0, value), new PartitionKey(key)));
        }

        async Task<T> Do<T>(string name, Func<Task<T>> action)
        {
            if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug(name);
            for (var i = 0; i < 5; i++)
            {
                try
                {
                    return await action();
                }
                catch (Exception error)
                {
                    if (error is CosmosException ce && ce.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
                    {
                        if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug($"{name} [{i}] <{error.Message}>");
                        Thread.Sleep(100);
                        continue;
                    }
                    throw;
                }
            }
            throw new InvalidOperationException("Exceeded retry limit for operation");
        }
    }

    record CacheEntry(string id, int patch, byte[] value);
}
