using DocumentRedaction.Web.Services;

namespace DocumentRedaction.Web.Tests.Services;

public class ProcessingEstimatorTests
{
    [Fact]
    public void Without_samples_uses_default_throughput_with_a_floor()
    {
        ProcessingEstimator estimator = new();
        Assert.Equal(TimeSpan.FromSeconds(1), estimator.Estimate(10));
        Assert.Equal(TimeSpan.FromSeconds(10), estimator.Estimate(20_000_000));
    }

    [Fact]
    public void Learns_from_recorded_runs()
    {
        ProcessingEstimator estimator = new();
        estimator.Record(1_000, TimeSpan.FromSeconds(2)); // 500 B/s
        Assert.Equal(TimeSpan.FromSeconds(4), estimator.Estimate(2_000));
    }

    [Fact]
    public void Ignores_empty_or_instant_samples()
    {
        ProcessingEstimator estimator = new();
        estimator.Record(0, TimeSpan.FromSeconds(5));
        estimator.Record(100, TimeSpan.Zero);
        Assert.Equal(TimeSpan.FromSeconds(1), estimator.Estimate(1_000));
    }

    [Fact]
    public void Window_keeps_only_recent_samples()
    {
        ProcessingEstimator estimator = new();
        for (int i = 0; i < 50; i++)
        {
            estimator.Record(1_000, TimeSpan.FromSeconds(100)); // very slow, all evicted below
        }

        for (int i = 0; i < 20; i++)
        {
            estimator.Record(1_000, TimeSpan.FromSeconds(1)); // 1000 B/s
        }

        Assert.Equal(TimeSpan.FromSeconds(2), estimator.Estimate(2_000));
    }

    [Fact]
    public async Task Measure_records_and_returns_result()
    {
        ProcessingEstimator estimator = new();
        int value = await estimator.MeasureAsync(5_000, async () =>
        {
            await Task.Delay(20, TestContext.Current.CancellationToken);
            return 42;
        });

        Assert.Equal(42, value);
        Assert.True(estimator.Estimate(5_000) >= TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Measure_does_not_record_failures()
    {
        ProcessingEstimator estimator = new();
        await Assert.ThrowsAsync<InvalidOperationException>(() => estimator.MeasureAsync<int>(1_000, () => throw new InvalidOperationException()));
        Assert.Equal(TimeSpan.FromSeconds(1), estimator.Estimate(1_000));
    }
}
