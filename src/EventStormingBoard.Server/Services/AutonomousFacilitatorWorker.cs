using EventStormingBoard.Server.Events;
using EventStormingBoard.Server.Hubs;
using EventStormingBoard.Server.Models;
using EventStormingBoard.Server.Repositories;
using Microsoft.AspNetCore.SignalR;

namespace EventStormingBoard.Server.Services
{
    public sealed class AutonomousFacilitatorWorker : BackgroundService
    {
        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);

        private readonly IBoardsRepository _boardsRepository;
        private readonly IBoardPresenceService _boardPresenceService;
        private readonly IAutonomousFacilitatorCoordinator _coordinator;
        private readonly IAgentService _agentService;
        private readonly IBoardEventPipeline _boardEventPipeline;
        private readonly IHubContext<BoardsHub> _hubContext;

        public AutonomousFacilitatorWorker(
            IBoardsRepository boardsRepository,
            IBoardPresenceService boardPresenceService,
            IAutonomousFacilitatorCoordinator coordinator,
            IAgentService agentService,
            IBoardEventPipeline boardEventPipeline,
            IHubContext<BoardsHub> hubContext)
        {
            _boardsRepository = boardsRepository;
            _boardPresenceService = boardPresenceService;
            _coordinator = coordinator;
            _agentService = agentService;
            _boardEventPipeline = boardEventPipeline;
            _hubContext = hubContext;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(PollInterval);

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                foreach (var board in _boardsRepository.GetAll().Where(b => b.AutonomousEnabled).ToList())
                {
                    _coordinator.SyncBoardSettings(board.Id, board.AutonomousEnabled);

                    if (!_boardPresenceService.HasActiveUsers(board.Id))
                    {
                        await DisableAutonomousModeAsync(board.Id, "noActiveUsers", stoppingToken);
                        continue;
                    }

                    var now = DateTimeOffset.UtcNow;
                    var triggerReason = _coordinator.GetTriggerReason(board.Id, now);
                    if (triggerReason is null || !_coordinator.TryStartRun(board.Id, triggerReason, now, out var runningStatus))
                    {
                        continue;
                    }

                    await BroadcastStatusAsync(runningStatus, stoppingToken);

                    AutonomousAgentResult result;
                    try
                    {
                        result = await _agentService.RunAutonomousTurnAsync(board.Id, triggerReason, stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        result = new AutonomousAgentResult
                        {
                            Status = AutonomousAgentStatus.Failed,
                            TriggerReason = triggerReason,
                            Diagnostics = ex.Message
                        };
                    }

                    var completedStatus = _coordinator.CompleteRun(board.Id, result, DateTimeOffset.UtcNow);
                    if (!completedStatus.IsEnabled)
                    {
                        await DisableAutonomousModeAsync(board.Id, completedStatus.StopReason ?? "stopped", stoppingToken);
                        completedStatus = _coordinator.GetStatus(board.Id, false);
                    }

                    if (!string.IsNullOrWhiteSpace(result.VisibleMessage) && result.Status != AutonomousAgentStatus.Idle)
                    {
                        if (result.AgentMessages.Count > 0)
                        {
                            foreach (var agentMsg in result.AgentMessages)
                            {
                                await _hubContext.Clients.Group(board.Id.ToString())
                                    .SendAsync("AgentResponse", agentMsg, stoppingToken);
                            }
                        }
                        else
                        {
                            await _hubContext.Clients.Group(board.Id.ToString()).SendAsync("AgentResponse", new AgentChatMessageDto
                            {
                                Role = "assistant",
                                AgentName = "Facilitator",
                                UserName = "AI Facilitator",
                                Content = result.VisibleMessage,
                                ToolCalls = result.ToolCalls.Count > 0 ? result.ToolCalls : null,
                                Timestamp = DateTime.UtcNow
                            }, stoppingToken);
                        }
                    }

                    await BroadcastStatusAsync(completedStatus, stoppingToken);
                }
            }
        }

        private async Task BroadcastStatusAsync(AutonomousFacilitatorStatusDto status, CancellationToken cancellationToken)
        {
            await _hubContext.Clients.Group(status.BoardId.ToString())
                .SendAsync("AutonomousFacilitatorStatusChanged", status, cancellationToken);
        }

        private async Task DisableAutonomousModeAsync(Guid boardId, string reason, CancellationToken cancellationToken)
        {
            var board = _boardsRepository.GetById(boardId);
            if (board == null || !board.AutonomousEnabled)
            {
                _coordinator.SyncBoardSettings(boardId, false, reason);
                return;
            }

            var @event = new BoardContextUpdatedEvent
            {
                BoardId = boardId,
                OldDomain = board.Domain,
                NewDomain = board.Domain,
                OldSessionScope = board.SessionScope,
                NewSessionScope = board.SessionScope,
                OldAgentInstructions = board.AgentInstructions,
                NewAgentInstructions = board.AgentInstructions,
                OldPhase = board.Phase,
                NewPhase = board.Phase,
                OldAutonomousEnabled = board.AutonomousEnabled,
                NewAutonomousEnabled = false
            };

            _boardEventPipeline.ApplyAndLog(@event, "System");
            _coordinator.SyncBoardSettings(boardId, false, reason);

            await _hubContext.Clients.Group(boardId.ToString()).SendAsync("BoardContextUpdated", @event, cancellationToken);
        }
    }
}