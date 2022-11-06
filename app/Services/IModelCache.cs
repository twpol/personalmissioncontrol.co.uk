using System.Threading.Tasks;

namespace app.Services
{
    public interface IModelCache<T> where T : class
    {
        public Task<T?> GetAsync(string key);
        public Task RemoveAsync(string key);
        public Task SetAsync(string key, T value);
    }
}
