using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace app.Services
{
    public class ModelMemoryCache<T> : IModelCache<T> where T : class
    {
        readonly ILogger<ModelMemoryCache<T>> Logger;

        readonly MemoryCache Cache = new(new MemoryCacheOptions
        {
            // 200 MB to match default distributed memory cache
            SizeLimit = 200 * 1024 * 1024,
        });

        public ModelMemoryCache(ILogger<ModelMemoryCache<T>> logger)
        {
            Logger = logger;
            if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug(".ctor()");
        }

        public Task<T?> GetAsync(string key)
        {
            if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug("Get({Key})", key);
            if (Cache.TryGetValue<T?>(key, out var value)) return Task.FromResult(value);
            return Task.FromResult<T?>(null);
        }

        public Task RemoveAsync(string key)
        {
            if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug("Remove({Key})", key);
            Cache.Remove(key);
            return Task.CompletedTask;
        }

        public async Task SetAsync(string key, T value)
        {
            using var stream = new MemoryStream();
            await JsonSerializer.SerializeAsync(stream, value);
            if (Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebug("Set({Key}, {Bytes} bytes)", key, stream.Length);
            Cache.Set(key, value, new MemoryCacheEntryOptions()
            {
                Size = stream.Length,
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1),
            });
        }
    }
}
