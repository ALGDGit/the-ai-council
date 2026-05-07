namespace TheIACouncil.Helpers;

/// <summary>Evita saturar GPU (CUDA) o Ollama con demasiadas inferencias a la vez.</summary>
public static class LlmConcurrency
{
    public static async Task<TResult[]> RunParallelLimitedAsync<TResult>(
        int maxConcurrent,
        IReadOnlyList<Func<Task<TResult>>> factories,
        CancellationToken cancellationToken = default)
    {
        if (factories.Count == 0)
            return [];

        var n = Math.Clamp(maxConcurrent, 1, 64);
        using var sem = new SemaphoreSlim(n, n);
        var wrapped = factories.Select(async factory =>
        {
            await sem.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await factory().ConfigureAwait(false);
            }
            finally
            {
                sem.Release();
            }
        });

        return await Task.WhenAll(wrapped).ConfigureAwait(false);
    }
}
