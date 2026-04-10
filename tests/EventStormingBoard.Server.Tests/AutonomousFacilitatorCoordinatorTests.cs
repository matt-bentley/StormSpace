using EventStormingBoard.Server.Models;
using EventStormingBoard.Server.Services;

namespace EventStormingBoard.Server.Tests;

public class AutonomousFacilitatorCoordinatorTests
{
    [Fact]
    public void GivenBoardIsEnabledForFirstTime_WhenGettingTriggerReason_ThenReturnsEnabled()
    {
        // Arrange
        var coordinator = new AutonomousFacilitatorCoordinator();
        var boardId = Guid.NewGuid();

        coordinator.SyncBoardSettings(boardId, enabled: true);

        // Act
        var triggerReason = coordinator.GetTriggerReason(boardId, DateTimeOffset.UtcNow.AddMinutes(1));

        // Assert
        triggerReason.Should().Be("enabled");
    }

    [Fact]
    public void GivenRecentUserActivity_WhenGettingTriggerReasonAfterDebounceWindow_ThenReturnsUserActivityDebounce()
    {
        // Arrange
        var coordinator = new AutonomousFacilitatorCoordinator();
        var boardId = Guid.NewGuid();

        coordinator.SyncBoardSettings(boardId, enabled: true);
        coordinator.RecordUserActivity(boardId);

        // Act
        var triggerReason = coordinator.GetTriggerReason(boardId, DateTimeOffset.UtcNow.AddSeconds(30));

        // Assert
        triggerReason.Should().Be("userActivityDebounce");
    }

    [Fact]
    public void GivenManualAgentResponseAcknowledged_WhenCheckingTriggerAcrossCooldown_ThenImmediateFollowUpIsSuppressed()
    {
        // Arrange
        var coordinator = new AutonomousFacilitatorCoordinator();
        var boardId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        coordinator.SyncBoardSettings(boardId, enabled: true);
        coordinator.RecordUserActivity(boardId);
        coordinator.AcknowledgeManualAgentResponse(boardId, now);

        // Act
        var immediateTriggerReason = coordinator.GetTriggerReason(boardId, now.AddSeconds(30));
        var postCooldownTriggerReason = coordinator.GetTriggerReason(boardId, now.AddSeconds(50));
        var postTimerTriggerReason = coordinator.GetTriggerReason(boardId, now.AddMinutes(4));

        // Assert
        immediateTriggerReason.Should().BeNull();
        postCooldownTriggerReason.Should().BeNull();
        postTimerTriggerReason.Should().Be("timer");
    }

    [Fact]
    public void GivenManualResponseIsInFlight_WhenGettingTriggerReason_ThenAutonomousRunIsBlocked()
    {
        // Arrange
        var coordinator = new AutonomousFacilitatorCoordinator();
        var boardId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        coordinator.SyncBoardSettings(boardId, enabled: true);
        coordinator.RecordUserActivity(boardId);
        coordinator.BeginManualAgentResponse(boardId, now);

        // Act
        var triggerReasonWhileManualResponseRuns = coordinator.GetTriggerReason(boardId, now.AddMinutes(1));

        // Assert
        triggerReasonWhileManualResponseRuns.Should().BeNull();
    }

    [Fact]
    public void GivenAutonomousRunActed_WhenNoNewUserActivity_ThenCoordinatorWaitsBeforeRunningAgain()
    {
        // Arrange
        var coordinator = new AutonomousFacilitatorCoordinator();
        var boardId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        coordinator.SyncBoardSettings(boardId, enabled: true);
        coordinator.TryStartRun(boardId, "enabled", now, out _).Should().BeTrue();
        coordinator.CompleteRun(boardId, new AutonomousAgentResult
        {
            Status = AutonomousAgentStatus.Acted,
            TriggerReason = "enabled"
        }, now.AddSeconds(5));

        // Act
        var timerTriggerWithoutUserActivity = coordinator.GetTriggerReason(boardId, now.AddMinutes(4));

        coordinator.RecordUserActivity(boardId);
        var activityTriggerAfterUserInput = coordinator.GetTriggerReason(boardId, now.AddMinutes(4).AddSeconds(25));

        // Assert
        timerTriggerWithoutUserActivity.Should().BeNull();
        activityTriggerAfterUserInput.Should().Be("userActivityDebounce");
    }

    [Fact]
    public void GivenThreeConsecutiveFailures_WhenCompletingRuns_ThenAutonomyIsDisabled()
    {
        // Arrange
        var coordinator = new AutonomousFacilitatorCoordinator();
        var boardId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        coordinator.SyncBoardSettings(boardId, enabled: true);

        for (var attempt = 0; attempt < 3; attempt++)
        {
            coordinator.TryStartRun(boardId, "timer", now.AddMinutes(attempt + 1), out _).Should().BeTrue();
            coordinator.CompleteRun(boardId, new AutonomousAgentResult
            {
                Status = AutonomousAgentStatus.Failed,
                TriggerReason = "timer"
            }, now.AddMinutes(attempt + 1).AddSeconds(5));
        }

        // Act
        var status = coordinator.GetStatus(boardId, enabled: false);

        // Assert
        status.IsEnabled.Should().BeFalse();
        status.State.Should().Be("stopped");
        status.StopReason.Should().Be("failureLimitExceeded");
        status.LastResultStatus.Should().Be("failed");
    }

    [Fact]
    public void GivenMultipleBoards_WhenStartingRuns_ThenEachBoardRunsIndependently()
    {
        // Arrange
        var coordinator = new AutonomousFacilitatorCoordinator();
        var boardA = Guid.NewGuid();
        var boardB = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        coordinator.SyncBoardSettings(boardA, enabled: true);
        coordinator.SyncBoardSettings(boardB, enabled: true);

        // Act
        var startedA = coordinator.TryStartRun(boardA, "enabled", now, out var statusA);
        var startedB = coordinator.TryStartRun(boardB, "enabled", now, out var statusB);

        // Assert
        startedA.Should().BeTrue();
        startedB.Should().BeTrue();
        statusA.IsRunning.Should().BeTrue();
        statusB.IsRunning.Should().BeTrue();

        // Complete Board A — Board B should still be running.
        coordinator.CompleteRun(boardA, new AutonomousAgentResult
        {
            Status = AutonomousAgentStatus.Acted,
            TriggerReason = "enabled"
        }, now.AddSeconds(5));

        var statusAAfter = coordinator.GetStatus(boardA, enabled: true);
        var statusBDuring = coordinator.GetStatus(boardB, enabled: true);

        statusAAfter.IsRunning.Should().BeFalse();
        statusBDuring.IsRunning.Should().BeTrue();
    }

    [Fact]
    public void GivenManualResponseWasCancelled_WhenGettingTriggerReason_ThenEnabledTriggerIsReturned()
    {
        // Arrange
        var coordinator = new AutonomousFacilitatorCoordinator();
        var boardId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        coordinator.SyncBoardSettings(boardId, enabled: true);
        coordinator.RecordUserActivity(boardId);
        coordinator.BeginManualAgentResponse(boardId, now);

        // Manual response is in flight — autonomous should be blocked.
        coordinator.GetTriggerReason(boardId, now.AddMinutes(1)).Should().BeNull();

        // Act
        // Cancel (simulating an exception path).
        coordinator.CancelManualAgentResponse(boardId);

        // Should unblock autonomous runs.
        var triggerReason = coordinator.GetTriggerReason(boardId, now.AddSeconds(25));

        // Assert
        triggerReason.Should().Be("enabled");
    }

    [Fact]
    public void GivenBoardHasState_WhenClearingBoardState_ThenAllBoardStateIsRemoved()
    {
        // Arrange
        var coordinator = new AutonomousFacilitatorCoordinator();
        var boardId = Guid.NewGuid();

        coordinator.SyncBoardSettings(boardId, enabled: true);
        coordinator.RecordUserActivity(boardId);

        // State exists.
        var statusBefore = coordinator.GetStatus(boardId, enabled: true);
        statusBefore.IsEnabled.Should().BeTrue();

        // Act
        // Clear it.
        coordinator.ClearBoardState(boardId);

        // State should be fresh (default disabled).
        var statusAfter = coordinator.GetStatus(boardId, enabled: false);

        // Assert
        statusAfter.IsEnabled.Should().BeFalse();
    }
}