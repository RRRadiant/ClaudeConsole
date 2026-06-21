using ClaudeCodePanel.Windows.Models;

namespace ClaudeCodePanel.Windows.Tests.Models;

public class DashboardSummaryTests
{
    [Fact]
    public void AddEvent_InsertsAtBeginning()
    {
        var summary = new DashboardSummary();
        summary.AddEvent("event1", DashboardEventType.Info);
        summary.AddEvent("event2", DashboardEventType.Success);

        Assert.Equal(2, summary.RecentEvents.Count);
        Assert.Equal("event2", summary.RecentEvents[0].Message); // newest first
        Assert.Equal("event1", summary.RecentEvents[1].Message);
    }

    [Fact]
    public void AddEvent_UpdatesLastUpdated()
    {
        var summary = new DashboardSummary();
        var before = summary.LastUpdated;
        System.Threading.Thread.Sleep(10);
        summary.AddEvent("test", DashboardEventType.Success);

        Assert.True(summary.LastUpdated > before);
    }

    [Fact]
    public void AddEvent_LimitsTo50()
    {
        var summary = new DashboardSummary();
        for (int i = 0; i < 60; i++)
            summary.AddEvent($"event{i}", DashboardEventType.Info);

        Assert.Equal(50, summary.RecentEvents.Count);
        Assert.Equal("event59", summary.RecentEvents[0].Message); // newest
        Assert.Equal("event10", summary.RecentEvents[49].Message); // oldest kept
    }

    [Fact]
    public void AddEvent_CorrectEventType()
    {
        var summary = new DashboardSummary();
        summary.AddEvent("ok", DashboardEventType.Success);
        summary.AddEvent("err", DashboardEventType.Error);
        summary.AddEvent("info", DashboardEventType.Info);

        Assert.Equal(DashboardEventType.Info, summary.RecentEvents[0].Type);
        Assert.Equal(DashboardEventType.Error, summary.RecentEvents[1].Type);
        Assert.Equal(DashboardEventType.Success, summary.RecentEvents[2].Type);
    }
}
