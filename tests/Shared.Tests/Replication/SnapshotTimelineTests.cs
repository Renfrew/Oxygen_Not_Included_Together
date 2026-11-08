using Shared.Replication;
using Xunit;

namespace Shared.Tests.Replication;

public sealed class SnapshotTimelineTests
{
    [Fact]
    public void FirstSampleAnchorsToLocalArrival()
    {
        var timeline = new SnapshotTimeline();

        double mappedTime = timeline.ToLocalTime(5_000, 32_000);

        Assert.Equal(32_000, mappedTime);
        Assert.Equal(0, timeline.EstimatedJitterMilliseconds);
    }

    [Fact]
    public void SubsequentSamplesPreserveRemoteElapsedTime()
    {
        var timeline = new SnapshotTimeline();
        timeline.ToLocalTime(100_000, 2_500);

        double mappedTime = timeline.ToLocalTime(100_120, 2_620);

        Assert.Equal(2_620, mappedTime);
        Assert.Equal(120, mappedTime - 2_500);
    }

    [Fact]
    public void ArrivalDeviationUpdatesJitterAndAdaptiveBuffer()
    {
        var timeline = new SnapshotTimeline();
        timeline.ToLocalTime(1_000, 10_000);

        timeline.ToLocalTime(1_100, 10_120);

        Assert.Equal(5, timeline.EstimatedJitterMilliseconds);
        Assert.Equal(60, timeline.GetAdaptiveBufferMilliseconds(50, 100));
        Assert.Equal(55, timeline.GetAdaptiveBufferMilliseconds(50, 55));
        Assert.Equal(50, timeline.GetAdaptiveBufferMilliseconds(50, 40));
    }

    [Fact]
    public void ReorderedRemoteTimestampDoesNotChangeJitterEstimate()
    {
        var timeline = new SnapshotTimeline();
        timeline.ToLocalTime(1_000, 5_000);
        timeline.ToLocalTime(1_100, 5_110);

        double reorderedTime = timeline.ToLocalTime(1_050, 9_999);

        Assert.Equal(5_050, reorderedTime);
        Assert.Equal(2.5, timeline.EstimatedJitterMilliseconds);

        timeline.ToLocalTime(1_200, 5_210);

        Assert.Equal(1.875, timeline.EstimatedJitterMilliseconds);
    }

    [Fact]
    public void ResetClearsJitterAndCreatesNewAnchor()
    {
        var timeline = new SnapshotTimeline();
        timeline.ToLocalTime(1_000, 5_000);
        timeline.ToLocalTime(1_100, 5_120);
        Assert.Equal(5, timeline.EstimatedJitterMilliseconds);

        timeline.Reset();

        Assert.Equal(0, timeline.EstimatedJitterMilliseconds);
        Assert.Equal(100, timeline.ToLocalTime(50_000, 100));
        Assert.Equal(0, timeline.EstimatedJitterMilliseconds);
        Assert.Equal(120, timeline.ToLocalTime(50_020, 120));
        Assert.Equal(0, timeline.EstimatedJitterMilliseconds);
    }
}
