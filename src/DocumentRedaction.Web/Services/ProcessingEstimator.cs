using System.Diagnostics;

namespace DocumentRedaction.Web.Services;

/// <summary>
/// Predicts how long a redaction will take from the throughput of recent runs, so the page can
/// show an estimated time remaining. Keeps a small rolling window in memory; no document data.
/// </summary>
public sealed class ProcessingEstimator
{
    private const int WindowSize = 20;
    private const double DefaultBytesPerSecond = 2_000_000;
    private static readonly TimeSpan MinimumEstimate = TimeSpan.FromSeconds(1);

    private readonly Lock _gate = new();
    private readonly Queue<(long Bytes, TimeSpan Elapsed)> _samples = new();

    /// <summary>Estimated duration for a document of the given size.</summary>
    public TimeSpan Estimate(long bytes)
    {
        double bytesPerSecond;
        lock (_gate)
        {
            long totalBytes = _samples.Sum(sample => sample.Bytes);
            double totalSeconds = _samples.Sum(sample => sample.Elapsed.TotalSeconds);
            bytesPerSecond = totalBytes > 0 && totalSeconds > 0 ? totalBytes / totalSeconds : DefaultBytesPerSecond;
        }

        TimeSpan estimate = TimeSpan.FromSeconds(Math.Max(0, bytes) / bytesPerSecond);
        return estimate < MinimumEstimate ? MinimumEstimate : estimate;
    }

    public void Record(long bytes, TimeSpan elapsed)
    {
        if (bytes <= 0 || elapsed <= TimeSpan.Zero)
        {
            return;
        }

        lock (_gate)
        {
            _samples.Enqueue((bytes, elapsed));
            while (_samples.Count > WindowSize)
            {
                _samples.Dequeue();
            }
        }
    }

    /// <summary>Runs the operation, records its duration on success, and returns its result.</summary>
    public async Task<T> MeasureAsync<T>(long bytes, Func<Task<T>> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        long start = Stopwatch.GetTimestamp();
        T result = await operation();
        Record(bytes, Stopwatch.GetElapsedTime(start));
        return result;
    }
}
