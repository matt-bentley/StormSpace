using EventStormingBoard.Server.Models;
using EventStormingBoard.Server.Services;

namespace EventStormingBoard.Server.Tests;

public class AutonomousFacilitatorCoordinatorTests
{
    [Fact]
    public void GetTriggerReason_WhenEnabledForFirstTime_ReturnsEnabled()
    {
        var coordinator = new AutonomousFacilitatorCoordinator();
        var boardId = Guid.NewGuid();

        coordinator.SyncBoardSettings(boardId, enabled: true);

        var triggerReason = coordinator.GetTriggerReason(boardId, DateTimeOffset.UtcNow.AddMinutes(1));

        Assert.Equal("enabled", triggerReason);
    }

    [Fact]
    public void GetTriggerReason_AfterDebouncedUserActivity_ReturnsUserActivityDebounce()
    {
        var coordinator = new AutonomousFacilitatorCoordinator();
        var boardId = Guid.NewGuid();

        coordinator.SyncBoardSettings(boardId, enabled: true);
        coordinator.RecordUserActivity(boardId);

        var triggerReason = coordinator.GetTriggerReason(boardId, DateTimeOffset.UtcNow.AddSeconds(30));

        Assert.Equal("userActivityDebounce", triggerReason);
    }

    [Fact]
    public void AcknowledgeManualAgentResponse_SuppressesImmediateAutonomousFollowUp()
    {
        var coordinator = new AutonomousFacilitatorCoordinator();
        var boardId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        coordinator.SyncBoardSettings(boardId, enabled: true);
        coordinator.RecordUserActivity(boardId);
        coordinator.AcknowledgeManualAgentResponse(boardId, now);

        var immediateTriggerReason = coordinator.GetTriggerReason(boardId, now.AddSeconds(30));
        var postCooldownTriggerReason = coordinator.GetTriggerReason(boardId, now.AddSeconds(50));
        var postTimerTriggerReason = coordinator.GetTriggerReason(boardId, now.AddMinutes(4));

        Assert.Null(immediateTriggerReason);
        Assert.Null(postCooldownTriggerReason);
        Assert.Equal("timer", postTimerTriggerReason);
    }

    [Fact]
    public void BeginManualAgentResponse_BlocksAutonomousRunWhileManualReplyIsInFlight()
    {
        var coordinator = new AutonomousFacilitatorCoordinator();
        var boardId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        coordinator.SyncBoardSettings(boardId, enabled: true);
        coordinator.RecordUserActivity(boardId);
        coordinator.BeginManualAgentResponse(boardId, now);

        var triggerReasonWhileManualResponseRuns = coordinator.GetTriggerReason(boardId, now.AddMinutes(1));

        Assert.Null(triggerReasonWhileManualResponseRuns);
    }

    [Fact]
    public void CompleteRun_AfterAutonomousActed_WaitsForUserActivityBeforeRunningAgain()
    {
        var coordinator = new AutonomousFacilitatorCoordinator();
        var boardId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        coordinator.SyncBoardSettings(boardId, enabled: true);
        Assert.True(coordinator.TryStartRun(boardId, "enabled", now, out _));
        coordinator.CompleteRun(boardId, new AutonomousAgentResult
        {
            Status = AutonomousAgentStatus.Acted,
            TriggerReason = "enabled"
        }, now.AddSeconds(5));

        var timerTriggerWithoutUserActivity = coordinator.GetTriggerReason(boardId, now.AddMinutes(4));

        coordinator.RecordUserActivity(boardId);
        var activityTriggerAfterUserInput = coordinator.GetTriggerReason(boardId, now.AddMinutes(4).AddSeconds(25));

        Assert.Null(timerTriggerWithoutUserActivity);
        Assert.Equal("userActivityDebounce", activityTriggerAfterUserInput);
    }

    [Fact]
    public void CompleteRun_AfterThreeFailures_DisablesAutonomy()
    {
        var coordinator = new AutonomousFacilitatorCoordinator();
        var boardId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        coordinator.SyncBoardSettings(boardId, enabled: true);

        for (var attempt = 0; attempt < 3; attempt++)
        {
            Assert.True(coordinator.TryStartRun(boardId, "timer", now.AddMinutes(attempt + 1), out _));
            coordinator.CompleteRun(boardId, new AutonomousAgentResult
            {
                Status = AutonomousAgentStatus.Failed,
                TriggerReason = "timer"
            }, now.AddMinutes(attempt + 1).AddSeconds(5));
        }

        var status = coordinator.GetStatus(boardId, enabled: false);

        Assert.False(status.IsEnabled);
        Assert.Equal("stopped", status.State);
        Assert.Equal("failureLimitExceeded", status.StopReason);
        Assert.Equal("failed", status.LastResultStatus);
    }
}