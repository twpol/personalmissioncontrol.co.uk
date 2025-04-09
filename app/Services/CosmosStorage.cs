using System;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace app.Services
{
    public static class CosmosStorageOptionsExtensions
    {
        public static IServiceCollection AddCosmosStorage(this IServiceCollection services) => services.AddSingleton<ICosmosStorage, CosmosStorage>();
        public static IServiceCollection AddCosmosStorage(this IServiceCollection services, Action<CosmosStorageOptions> setupAction) => services.AddCosmosStorage().Configure(setupAction);
    }

    public class CosmosStorageOptions
    {
        public string? StorageEndpoint { get; set; }
        public string? StorageKey { get; set; }
    }

    public class CosmosStorage : ICosmosStorage
    {
        readonly CosmosClient Client;

        public CosmosStorage(IOptions<CosmosStorageOptions> options)
        {
            Client = new CosmosClient(options.Value.StorageEndpoint, options.Value.StorageKey);
        }

        public async Task<Container> GetContainerAsync(string databaseName, string containerName, int? ttl = null)
        {
            Database database = await Client.CreateDatabaseIfNotExistsAsync(databaseName);
            var containerProperties = new ContainerProperties(containerName, "/id")
            {
                DefaultTimeToLive = ttl,
            };
            Container container = await database.CreateContainerIfNotExistsAsync(containerProperties);
            if (ttl != null) await container.ReplaceContainerAsync(containerProperties);
            return container;
        }
    }
}
