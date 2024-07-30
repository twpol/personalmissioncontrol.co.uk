using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using app.Models;

namespace app.Services
{
    public interface IModelStore<T> where T : BaseModel
    {
        public IAsyncEnumerable<T> GetCollectionAsync(string accountId);
        public Task UpdateCollectionAsync(string accountId, Func<IAsyncEnumerable<T>> updater);
    }
}
