using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using app.Models;

namespace app.Services
{
    public interface IModelStore<T> where T : BaseModel
    {
        public IAsyncEnumerable<T> GetCollectionAsync(string accountId, string? parentId);
        public Task UpdateCollectionAsync(string accountId, string? parentId, Func<IAsyncEnumerable<T>> updater);
    }
}
