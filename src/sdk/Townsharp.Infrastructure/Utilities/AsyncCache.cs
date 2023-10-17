namespace Townsharp.Infrastructure.Utilities;

internal record CacheState<T>(T Value, DateTimeOffset ExpirationTime);

internal class AsyncCache<T>
{
    private readonly TimeSpan marginOfRefresh;
    private readonly Func<CancellationToken, ValueTask<CacheState<T>>> fetchDataAsync;
    private readonly SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);
    private ValueTask<CacheState<T>> cacheTask;
    private DateTimeOffset expirationTime;
    private T cachedValue;

    internal AsyncCache(TimeSpan marginOfRefresh, Func<CancellationToken, ValueTask<CacheState<T>>> fetchDataAsync, CacheState<T> initialCacheState)
    {
        this.marginOfRefresh = marginOfRefresh;
        this.fetchDataAsync = fetchDataAsync;
        this.cacheTask = ValueTask.FromResult(initialCacheState);
        this.expirationTime = initialCacheState.ExpirationTime;
        this.cachedValue = initialCacheState.Value;
    }

    internal async ValueTask<T> GetAsync(CancellationToken cancellationToken = default)
    {
        // Fast path: if the cache is not nearly expired, return the value immediately.
        if (DateTimeOffset.UtcNow + this.marginOfRefresh < this.expirationTime)
        {
            return this.cachedValue;
        }

        await this.semaphore.WaitAsync(cancellationToken);

        try
        {
            var state = await this.cacheTask;
            if (DateTimeOffset.UtcNow + this.marginOfRefresh > this.expirationTime)
            {
                this.cacheTask = fetchDataAsync(cancellationToken);
                state = await this.cacheTask;
                this.expirationTime = state.ExpirationTime;
                this.cachedValue = state.Value;
            }
            return state.Value;
        }
        finally
        {
            this.semaphore.Release();
        }
    }
}