using Microsoft.Extensions.Caching.Memory;

public class DataMemoryCache
{
    public MemoryCache Cache { get; } = new MemoryCache(new MemoryCacheOptions
    {
        // Arbitrary, but seems good for now
        SizeLimit = 1024
    });
}
