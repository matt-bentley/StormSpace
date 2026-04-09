using EventStormingBoard.Server.Models;
using System.Collections.Concurrent;

namespace EventStormingBoard.Server.Services
{
    public interface IAutonomousFacilitatorCoordinator
    {
        void SyncBoardSettings(Guid boardId, bool enabled, string? stopReason = null);
        void RecordUserActivity(Guid boardId);
        void BeginManualAgentResponse(Guid boardId, DateTimeOffset now);
        void AcknowledgeManualAgentResponse(Guid boardId, DateTimeOffset now);
        void CancelManualAgentResponse(Guid boardId);
        void ClearBoardState(Guid boardId);
        AutonomousFacilitatorStatusDto GetStatus(Guid boardId, bool enabled);
        string? GetTriggerReason(Guid boardId, DateTimeOffset now);
        bool TryStartRun(Guid boardId, string triggerReason, DateTimeOffset now, out AutonomousFacilitatorStatusDto status);
        AutonomousFacilitatorStatusDto CompleteRun(Guid boardId, AutonomousAgentResult result, DateTimeOffset now);
        int GetTurnsInCurrentPhase(Guid boardId);
        void RecordPhaseAtRun(Guid boardId, string? phase);
    }

    public sealed class AutonomousFacilitatorCoordinator : IAutonomousFacilitatorCoordinator
    {
        private static readonly TimeSpan ActivityDebounce = TimeSpan.FromSeconds(20);
        private static readonly TimeSpan MinimumCooldown = TimeSpan.FromSeconds(45);
        private static readonly TimeSpan PeriodicInterval = TimeSpan.FromMinutes(3);
        private static readonly TimeSpan TurnWindow = TimeSpan.FromMinutes(10);
        private const int MaxTurnsPerWindow = 4;
        private const int FailureLimit = 3;

        private readonly ConcurrentDictionary<Guid, RuntimeState> _states = new();

        public void SyncBoardSettings(Guid boardId, bool enabled, string? stopReason = null)
        {
            var state = _states.GetOrAdd(boardId, _ => new RuntimeState());
            lock (state.SyncRoot)
            {
                state.Enabled = enabled;
                if (!enabled)
                {
                    state.IsRunning = false;
                    state.StopReason = stopReason ?? state.StopReason ?? "disabled";
                }
                else if (stopReason is null)
                {
                    state.StopReason = null;
                }

                state.UpdatedAtUtc = DateTimeOffset.UtcNow;
            }
        }

        public void RecordUserActivity(Guid boardId)
        {
            var state = _states.GetOrAdd(boardId, _ => new RuntimeState());
            lock (state.SyncRoot)
            {
                if (!state.Enabled)
                {
                    return;
                }

                state.LastUserActivityUtc = DateTimeOffset.UtcNow;
                state.IsAwaitingUserActivity = false;
                state.UpdatedAtUtc = state.LastUserActivityUtc.Value;
            }
        }

        public void BeginManualAgentResponse(Guid boardId, DateTimeOffset now)
        {
            var state = _states.GetOrAdd(boardId, _ => new RuntimeState());
            lock (state.SyncRoot)
            {
                if (!state.Enabled)
                {
                    return;
                }

                state.IsManualResponseInFlight = true;
                state.LastTriggeredActivityUtc = state.LastUserActivityUtc;
                state.TriggerReason = "manualResponsePending";
                state.UpdatedAtUtc = now;
            }
        }

        public void AcknowledgeManualAgentResponse(Guid boardId, DateTimeOffset now)
        {
            var state = _states.GetOrAdd(boardId, _ => new RuntimeState());
            lock (state.SyncRoot)
            {
                if (!state.Enabled)
                {
                    return;
                }

                state.IsManualResponseInFlight = false;
                state.LastTriggeredActivityUtc = state.LastUserActivityUtc;
                state.LastRunStartedUtc = now;
                state.LastRunCompletedUtc = now;
                state.TriggerReason = "manualResponse";
                state.UpdatedAtUtc = now;
            }
        }

        public void CancelManualAgentResponse(Guid boardId)
        {
            var state = _states.GetOrAdd(boardId, _ => new RuntimeState());
            lock (state.SyncRoot)
            {
                state.IsManualResponseInFlight = false;
            }
        }

        public void ClearBoardState(Guid boardId)
        {
            _states.TryRemove(boardId, out _);
        }

        public AutonomousFacilitatorStatusDto GetStatus(Guid boardId, bool enabled)
        {
            var state = _states.GetOrAdd(boardId, _ => new RuntimeState());
            lock (state.SyncRoot)
            {
                if (state.Enabled != enabled)
                {
                    state.Enabled = enabled;
                    state.UpdatedAtUtc = DateTimeOffset.UtcNow;
                }

                return BuildStatus(boardId, state);
            }
        }

        public string? GetTriggerReason(Guid boardId, DateTimeOffset now)
        {
            var state = _states.GetOrAdd(boardId, _ => new RuntimeState());
            lock (state.SyncRoot)
            {
                if (!state.Enabled || state.IsRunning || state.IsManualResponseInFlight || !CanRunNow(state, now))
                {
                    return null;
                }

                if (state.IsAwaitingUserActivity)
                {
                    return null;
                }

                if (state.LastUserActivityUtc.HasValue &&
                    state.LastUserActivityUtc > state.LastTriggeredActivityUtc &&
                    now - state.LastUserActivityUtc.Value >= ActivityDebounce)
                {
                    return "userActivityDebounce";
                }

                if (!state.LastRunStartedUtc.HasValue)
                {
                    return "enabled";
                }

                return now - state.LastRunStartedUtc.Value >= PeriodicInterval ? "timer" : null;
            }
        }

        public bool TryStartRun(Guid boardId, string triggerReason, DateTimeOffset now, out AutonomousFacilitatorStatusDto status)
        {
            var state = _states.GetOrAdd(boardId, _ => new RuntimeState());
            lock (state.SyncRoot)
            {
                if (!state.Enabled || state.IsRunning || !CanRunNow(state, now))
                {
                    status = BuildStatus(boardId, state);
                    return false;
                }

                state.IsRunning = true;
                state.TriggerReason = triggerReason;
                state.LastRunStartedUtc = now;
                state.StopReason = null;
                PruneRecentRuns(state, now);
                state.RecentRunTimestamps.Enqueue(now);
                if (string.Equals(triggerReason, "userActivityDebounce", StringComparison.Ordinal))
                {
                    state.LastTriggeredActivityUtc = state.LastUserActivityUtc;
                }

                state.UpdatedAtUtc = now;
                status = BuildStatus(boardId, state);
                return true;
            }
        }

        public AutonomousFacilitatorStatusDto CompleteRun(Guid boardId, AutonomousAgentResult result, DateTimeOffset now)
        {
            var state = _states.GetOrAdd(boardId, _ => new RuntimeState());
            lock (state.SyncRoot)
            {
                state.IsRunning = false;
                state.LastRunCompletedUtc = now;
                state.LastResultStatus = result.Status.ToString().ToLowerInvariant();
                state.TriggerReason = result.TriggerReason;
                state.StopReason = result.StopReason;
                state.UpdatedAtUtc = now;

                if (result.Status == AutonomousAgentStatus.Failed)
                {
                    state.ConsecutiveFailures++;
                    if (state.ConsecutiveFailures >= FailureLimit)
                    {
                        state.Enabled = false;
                        state.StopReason ??= "failureLimitExceeded";
                    }
                }
                else
                {
                    state.ConsecutiveFailures = 0;
                }

                if (result.Status == AutonomousAgentStatus.Complete)
                {
                    state.Enabled = false;
                    state.StopReason ??= "sessionComplete";
                }

                state.IsAwaitingUserActivity = result.Status == AutonomousAgentStatus.Acted;

                return BuildStatus(boardId, state);
            }
        }

        public int GetTurnsInCurrentPhase(Guid boardId)
        {
            var state = _states.GetOrAdd(boardId, _ => new RuntimeState());
            lock (state.SyncRoot)
            {
                return state.TurnsInCurrentPhase;
            }
        }

        public void RecordPhaseAtRun(Guid boardId, string? phase)
        {
            var state = _states.GetOrAdd(boardId, _ => new RuntimeState());
            lock (state.SyncRoot)
            {
                if (string.Equals(state.PhaseAtLastRun, phase, StringComparison.Ordinal))
                {
                    state.TurnsInCurrentPhase++;
                }
                else
                {
                    state.PhaseAtLastRun = phase;
                    state.TurnsInCurrentPhase = 1;
                }
            }
        }

        private static bool CanRunNow(RuntimeState state, DateTimeOffset now)
        {
            if (state.LastRunStartedUtc.HasValue && now - state.LastRunStartedUtc.Value < MinimumCooldown)
            {
                return false;
            }

            PruneRecentRuns(state, now);
            return state.RecentRunTimestamps.Count < MaxTurnsPerWindow;
        }

        private static void PruneRecentRuns(RuntimeState state, DateTimeOffset now)
        {
            while (state.RecentRunTimestamps.Count > 0 && now - state.RecentRunTimestamps.Peek() > TurnWindow)
            {
                state.RecentRunTimestamps.Dequeue();
            }
        }

        private static AutonomousFacilitatorStatusDto BuildStatus(Guid boardId, RuntimeState state)
        {
            var derivedState = state.IsRunning
                ? "running"
                : state.Enabled
                    ? "waiting"
                    : string.IsNullOrWhiteSpace(state.StopReason)
                        ? "disabled"
                        : "stopped";

            return new AutonomousFacilitatorStatusDto
            {
                BoardId = boardId,
                IsEnabled = state.Enabled,
                IsRunning = state.IsRunning,
                State = derivedState,
                LastResultStatus = state.LastResultStatus,
                StopReason = state.StopReason,
                TriggerReason = state.TriggerReason,
                UpdatedAt = state.UpdatedAtUtc.UtcDateTime
            };
        }

        private sealed class RuntimeState
        {
            public object SyncRoot { get; } = new();
            public bool Enabled { get; set; }
            public bool IsRunning { get; set; }
            public bool IsManualResponseInFlight { get; set; }
            public bool IsAwaitingUserActivity { get; set; }
            public int ConsecutiveFailures { get; set; }
            public DateTimeOffset? LastUserActivityUtc { get; set; }
            public DateTimeOffset? LastTriggeredActivityUtc { get; set; } = DateTimeOffset.MinValue;
            public DateTimeOffset? LastRunStartedUtc { get; set; }
            public DateTimeOffset? LastRunCompletedUtc { get; set; }
            public Queue<DateTimeOffset> RecentRunTimestamps { get; } = new();
            public string? LastResultStatus { get; set; }
            public string? StopReason { get; set; }
            public string? TriggerReason { get; set; }
            public string? PhaseAtLastRun { get; set; }
            public int TurnsInCurrentPhase { get; set; }
            public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
        }
    }
}