using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;

namespace app.Services
{
    public interface ICosmosStorage
    {
        public Task<Container> GetContainerAsync(string databaseName, string containerName, int? ttl = null);
    }
}
