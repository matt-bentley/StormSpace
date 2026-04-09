using EventStormingBoard.Server.Agents;
using EventStormingBoard.Server.Events;
using EventStormingBoard.Server.Filters;
using EventStormingBoard.Server.Hubs;
using EventStormingBoard.Server.Models;
using EventStormingBoard.Server.Plugins;
using EventStormingBoard.Server.Repositories;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace EventStormingBoard.Server.Services
{
    public interface IAgentService
    {
        Task<List<AgentChatMessageDto>> ChatAsync(Guid boardId, string userMessage, string userName);
        Task<AutonomousAgentResult> RunAutonomousTurnAsync(Guid boardId, string triggerReason, CancellationToken cancellationToken);
        List<AgentChatMessageDto> GetHistory(Guid boardId);
        void ClearHistory(Guid boardId);
        void ClearBoardData(Guid boardId);
    }

    public sealed class AgentService : IAgentService
    {
        private const int MaxHistoryMessages = 200;
        private const string AutonomousPromptSentinel = "\uE000[autonomous-facilitator-prompt]";

        private readonly IServiceProvider _serviceProvider;
        private readonly AzureOpenAIOptions _options;
        private readonly AgentToolCallFilter _toolCallFilter;
        private readonly Azure.AI.OpenAI.AzureOpenAIClient _azureOpenAIClient;
        private readonly ConcurrentDictionary<Guid, List<ChatMessage>> _conversationHistories = new();
        private readonly ConcurrentDictionary<Guid, List<AgentChatMessageDto>> _displayHistories = new();
        private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _boardSemaphores = new();

        public AgentService(
            IServiceProvider serviceProvider,
            IOptions<AzureOpenAIOptions> options,
            AgentToolCallFilter toolCallFilter)
        {
            _serviceProvider = serviceProvider;
            _options = options.Value;
            _toolCallFilter = toolCallFilter;
            _azureOpenAIClient = BoardAgentFactory.CreateAzureOpenAIClient(_options);
        }

        public async Task<List<AgentChatMessageDto>> ChatAsync(Guid boardId, string userMessage, string userName)
        {
            var semaphore = _boardSemaphores.GetOrAdd(boardId, static _ => new SemaphoreSlim(1, 1));
            if (!await semaphore.WaitAsync(TimeSpan.FromSeconds(60)).ConfigureAwait(false))
            {
                throw new TimeoutException("Another agent turn is in progress for this board. Please try again shortly.");
            }

            try
            {
                var conversationHistory = _conversationHistories.GetOrAdd(boardId, static _ => new List<ChatMessage>());
                var displayHistory = _displayHistories.GetOrAdd(boardId, static _ => new List<AgentChatMessageDto>());

                lock (conversationHistory)
                {
                    conversationHistory.Add(new ChatMessage(ChatRole.User, userMessage));
                    TrimHistory(conversationHistory);
                }

                var displayUserMessage = new AgentChatMessageDto
                {
                    StepId = Guid.NewGuid(),
                    BoardId = boardId,
                    Role = "user",
                    UserName = userName,
                    Content = userMessage,
                    Timestamp = DateTime.UtcNow
                };
                lock (displayHistory)
                {
                    displayHistory.Add(displayUserMessage);
                    TrimHistory(displayHistory);
                }

                var steps = await InvokeAgentAsync(boardId, allowDestructiveChanges: true, CancellationToken.None).ConfigureAwait(false);
                var messages = AppendAgentSteps(boardId, steps, isManualTurn: true);
                return messages;
            }
            finally
            {
                semaphore.Release();
            }
        }

        public async Task<AutonomousAgentResult> RunAutonomousTurnAsync(Guid boardId, string triggerReason, CancellationToken cancellationToken)
        {
            var semaphore = _boardSemaphores.GetOrAdd(boardId, static _ => new SemaphoreSlim(1, 1));
            if (!await semaphore.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false))
            {
                return new AutonomousAgentResult
                {
                    Status = AutonomousAgentStatus.Idle,
                    TriggerReason = triggerReason,
                    Diagnostics = "Skipped: another turn is in progress for this board."
                };
            }

            try
            {
                return await RunAutonomousTurnCoreAsync(boardId, triggerReason, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                semaphore.Release();
            }
        }

        private async Task<AutonomousAgentResult> RunAutonomousTurnCoreAsync(Guid boardId, string triggerReason, CancellationToken cancellationToken)
        {
            EnsureInitialAutonomousPhase(boardId);

            var coordinator = _serviceProvider.GetRequiredService<IAutonomousFacilitatorCoordinator>();
            var preRunBoard = _serviceProvider.GetRequiredService<IBoardsRepository>().GetById(boardId);
            coordinator.RecordPhaseAtRun(boardId, preRunBoard?.Phase?.ToString());
            var turnsInPhase = coordinator.GetTurnsInCurrentPhase(boardId);

            var conversationHistory = _conversationHistories.GetOrAdd(boardId, static _ => new List<ChatMessage>());
            lock (conversationHistory)
            {
                // Remove previous autonomous prompts — keep exactly one at a time (item 8).
                conversationHistory.RemoveAll(m =>
                    m.Role == ChatRole.User &&
                    m.Text?.Contains(AutonomousPromptSentinel) == true);

                conversationHistory.Add(new ChatMessage(ChatRole.User, BuildAutonomousPrompt(boardId, triggerReason, turnsInPhase)));
                TrimHistory(conversationHistory);
            }

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromMinutes(5));

            var steps = await InvokeAgentAsync(boardId, allowDestructiveChanges: false, timeoutCts.Token).ConfigureAwait(false);
            var board = _serviceProvider.GetRequiredService<IBoardsRepository>().GetById(boardId);
            var allToolCalls = steps.SelectMany(s => s.ToolCalls).ToList();
            var facilitatorConfig = board?.AgentConfigurations.FirstOrDefault(a => a.IsFacilitator);
            var facilitatorName = facilitatorConfig?.Name ?? "Facilitator";
            var facilitatorReply = steps.FirstOrDefault(s => s.AgentName == facilitatorName)?.Content ?? string.Empty;
            var trimmedReply = facilitatorReply.Trim();
            var usedCompletionTool = allToolCalls.Any(call => string.Equals(call.Name, nameof(BoardPlugin.CompleteAutonomousSession), StringComparison.Ordinal));

            if (board?.AutonomousEnabled == false && usedCompletionTool)
            {
                var visibleMessage = string.IsNullOrWhiteSpace(trimmedReply)
                    ? "The session looks complete, so I'm pausing autonomous facilitation here."
                    : trimmedReply;
                PatchFacilitatorContent(steps, facilitatorName, visibleMessage);
                var completedMessages = AppendAgentSteps(boardId, steps, isManualTurn: false);
                await BroadcastPatchedStepAsync(boardId, steps, facilitatorName).ConfigureAwait(false);

                return new AutonomousAgentResult
                {
                    Status = AutonomousAgentStatus.Complete,
                    TriggerReason = triggerReason,
                    VisibleMessage = visibleMessage,
                    ToolCalls = allToolCalls,
                    AgentMessages = completedMessages,
                    StopReason = "sessionComplete"
                };
            }

            if (string.IsNullOrWhiteSpace(trimmedReply) && allToolCalls.Count == 0)
            {
                var idleMessages = AppendAgentSteps(boardId, steps, isManualTurn: false);
                return new AutonomousAgentResult
                {
                    Status = AutonomousAgentStatus.Idle,
                    TriggerReason = triggerReason,
                    ToolCalls = allToolCalls,
                    AgentMessages = idleMessages,
                    Diagnostics = "No visible reply and no tool activity."
                };
            }

            var visibleActedMessage = string.IsNullOrWhiteSpace(trimmedReply)
                ? "I made a small facilitation update to keep the session moving."
                : trimmedReply;

            PatchFacilitatorContent(steps, facilitatorName, visibleActedMessage);
            var actedMessages = AppendAgentSteps(boardId, steps, isManualTurn: false);
            await BroadcastPatchedStepAsync(boardId, steps, facilitatorName).ConfigureAwait(false);

            return new AutonomousAgentResult
            {
                Status = AutonomousAgentStatus.Acted,
                TriggerReason = triggerReason,
                VisibleMessage = visibleActedMessage,
                ToolCalls = allToolCalls,
                AgentMessages = actedMessages
            };
        }

        public List<AgentChatMessageDto> GetHistory(Guid boardId)
        {
            if (_displayHistories.TryGetValue(boardId, out var history))
            {
                lock (history)
                {
                    return new List<AgentChatMessageDto>(history);
                }
            }

            return new List<AgentChatMessageDto>();
        }

        public void ClearHistory(Guid boardId)
        {
            // Only removes histories — does NOT touch the semaphore (safe for live boards).
            // In-flight turns holding list references will append to orphaned lists;
            // new turns create fresh empty lists via GetOrAdd. Acceptable for clear-history.
            _conversationHistories.TryRemove(boardId, out _);
            _displayHistories.TryRemove(boardId, out _);
        }

        public void ClearBoardData(Guid boardId)
        {
            // Full cleanup for board deletion — removes histories AND the per-board semaphore.
            // Only called from BoardsController.Delete after the board is disabled.
            _conversationHistories.TryRemove(boardId, out _);
            _displayHistories.TryRemove(boardId, out _);
            _boardSemaphores.TryRemove(boardId, out _);
        }

        private async Task<List<AgentStep>> InvokeAgentAsync(Guid boardId, bool allowDestructiveChanges, CancellationToken cancellationToken)
        {
            var board = _serviceProvider.GetRequiredService<IBoardsRepository>().GetById(boardId);
            var boardPlugin = new BoardPlugin(
                _serviceProvider.GetRequiredService<IBoardsRepository>(),
                _serviceProvider.GetRequiredService<IBoardEventPipeline>(),
                _serviceProvider.GetRequiredService<IBoardEventLog>(),
                _serviceProvider.GetRequiredService<IHubContext<BoardsHub>>(),
                boardId);

            var factory = new BoardAgentFactory(
                _azureOpenAIClient, _options, _toolCallFilter, _serviceProvider);
            var groupChat = new FacilitatorGroupChat(factory, _toolCallFilter);

            var hubContext = _serviceProvider.GetRequiredService<IHubContext<BoardsHub>>();
            var boardGroup = boardId.ToString();

            async Task BroadcastStep(AgentStep step)
            {
                if (!IsVisibleStep(step))
                    return;

                var dto = new AgentChatMessageDto
                {
                    StepId = step.Id,
                    BoardId = boardId,
                    Role = "assistant",
                    AgentName = step.AgentName,
                    Content = step.Content,
                    Prompt = step.Prompt,
                    ToolCalls = step.ToolCalls.Count > 0 ? step.ToolCalls : null,
                    Timestamp = DateTime.UtcNow
                };
                await hubContext.Clients.Group(boardGroup)
                    .SendAsync("AgentStepUpdate", dto).ConfigureAwait(false);
            }

            List<ChatMessage> messages;
            if (_conversationHistories.TryGetValue(boardId, out var history))
            {
                lock (history)
                {
                    messages = new List<ChatMessage>(history);
                }
            }
            else
            {
                messages = new List<ChatMessage>();
            }

            var agentConfigurations = board?.AgentConfigurations ?? DefaultAgentConfigurations.CreateDefaults();

            return await groupChat.RunAsync(
                boardId, board, boardPlugin, agentConfigurations, messages,
                allowDestructiveChanges, cancellationToken,
                onStepCompleted: BroadcastStep).ConfigureAwait(false);
        }

        private static void PatchFacilitatorContent(List<AgentStep> steps, string facilitatorName, string content)
        {
            var facilitator = steps.FirstOrDefault(s => s.AgentName == facilitatorName);
            if (facilitator != null && string.IsNullOrWhiteSpace(facilitator.Content))
            {
                var index = steps.IndexOf(facilitator);
                steps[index] = new AgentStep
                {
                    Id = facilitator.Id,
                    AgentName = facilitator.AgentName,
                    Content = content,
                    Prompt = facilitator.Prompt,
                    ToolCalls = facilitator.ToolCalls
                };
            }
        }

        private static bool IsVisibleStep(AgentStep step)
        {
            return !string.IsNullOrWhiteSpace(step.Content) || step.ToolCalls.Count > 0;
        }

        private async Task BroadcastPatchedStepAsync(Guid boardId, List<AgentStep> steps, string facilitatorName)
        {
            var patchedStep = steps.FirstOrDefault(s => s.AgentName == facilitatorName);
            if (patchedStep == null || string.IsNullOrWhiteSpace(patchedStep.Content))
                return;

            var hubContext = _serviceProvider.GetRequiredService<IHubContext<BoardsHub>>();
            var dto = new AgentChatMessageDto
            {
                StepId = patchedStep.Id,
                BoardId = boardId,
                Role = "assistant",
                AgentName = patchedStep.AgentName,
                Content = patchedStep.Content,
                Prompt = patchedStep.Prompt,
                ToolCalls = patchedStep.ToolCalls.Count > 0 ? patchedStep.ToolCalls : null,
                Timestamp = DateTime.UtcNow
            };
            await hubContext.Clients.Group(boardId.ToString())
                .SendAsync("AgentStepUpdate", dto).ConfigureAwait(false);
        }

        private List<AgentChatMessageDto> AppendAgentSteps(Guid boardId, List<AgentStep> steps, bool isManualTurn)
        {
            var conversationHistory = _conversationHistories.GetOrAdd(boardId, static _ => new List<ChatMessage>());
            var displayHistory = _displayHistories.GetOrAdd(boardId, static _ => new List<AgentChatMessageDto>());

            var board = _serviceProvider.GetRequiredService<IBoardsRepository>().GetById(boardId);
            var facilitatorConfig = board?.AgentConfigurations.FirstOrDefault(a => a.IsFacilitator);
            var facilitatorName = facilitatorConfig?.Name ?? "Facilitator";

            // Add all agent messages to conversation history so the LLM has
            // full context of what delegated agents did on subsequent turns.
            lock (conversationHistory)
            {
                foreach (var step in steps)
                {
                    if (!IsVisibleStep(step))
                        continue;

                    var prefix = step.AgentName == facilitatorName ? "" : $"[{step.AgentName}] ";
                    conversationHistory.Add(new ChatMessage(ChatRole.Assistant, $"{prefix}{step.Content}"));
                }

                TrimHistory(conversationHistory);
            }

            var result = new List<AgentChatMessageDto>();
            lock (displayHistory)
            {
                foreach (var step in steps)
                {
                    if (!IsVisibleStep(step))
                        continue;

                    var dto = new AgentChatMessageDto
                    {
                        StepId = step.Id,
                        BoardId = boardId,
                        Role = "assistant",
                        AgentName = step.AgentName,
                        Content = step.Content,
                        Prompt = step.Prompt,
                        ToolCalls = step.ToolCalls.Count > 0 ? step.ToolCalls : null,
                        Timestamp = DateTime.UtcNow
                    };
                    displayHistory.Add(dto);
                    result.Add(dto);
                }

                if (result.Count == 0 && isManualTurn)
                {
                    var fallback = new AgentChatMessageDto
                    {
                        StepId = Guid.NewGuid(),
                        BoardId = boardId,
                        Role = "assistant",
                        AgentName = facilitatorName,
                        Content = string.Empty,
                        Timestamp = DateTime.UtcNow
                    };
                    displayHistory.Add(fallback);
                    result.Add(fallback);
                }

                TrimHistory(displayHistory);
            }

            return result;
        }

        private static void TrimHistory<T>(List<T> history)
        {
            if (history.Count > MaxHistoryMessages)
            {
                history.RemoveRange(0, history.Count - MaxHistoryMessages);
            }
        }
        private string BuildAutonomousPrompt(Guid boardId, string triggerReason, int turnsInCurrentPhase)
        {
            var board = _serviceProvider.GetRequiredService<IBoardsRepository>().GetById(boardId);
            var needsSessionIntroduction = board is not null &&
                string.IsNullOrWhiteSpace(board.Domain) &&
                string.IsNullOrWhiteSpace(board.SessionScope) &&
                board.Notes.Count == 0 &&
                board.Connections.Count == 0 &&
                board.Phase is null or EventStormingPhase.SetContext;
            var phaseGuidance = board?.Phase switch
            {
                EventStormingPhase.SetContext => "If this phase has just started, explain that participants should clarify the domain boundary, business goal, actors, and scope before modeling the flow.",
                EventStormingPhase.IdentifyEvents => "If this phase has just started, explain that participants should brainstorm domain events in past tense, place them chronologically, and keep adding more events themselves after you delegate a starter example (max 3). If the phase didn't just start, ask if there are any important events missing or if the flow looks complete enough to move on.",
                EventStormingPhase.AddCommandsAndPolicies => "If this phase has just started, explain that participants should pick one existing Event at a time and connect it back to what triggered it. Delegate a starter example for one Event. If the phase didn't just start, delegate reviewing the board if it is needed.",
                EventStormingPhase.DefineAggregates => "If this phase has just started, explain that participants should identify the core aggregates. Delegate adding one example aggregate.",
                EventStormingPhase.BreakItDown => "If this phase has just started, explain that participants should identify bounded contexts, subdomains, and integration boundaries.",
                _ => "If the current phase has just started, explain the phase goal and what participants should do."
            };

            var sessionBootstrap = needsSessionIntroduction
                ? "This is a brand new blank session. Ensure the session starts in SetContext. Your very first reply must: (1) briefly explain Event Storming, (2) explain house rules, (3) explain why the team is working this way, (4) ask one concrete question to capture domain and scope."
                : string.Empty;

            var reviewGuidance = turnsInCurrentPhase >= 2
                ? "This phase has had multiple autonomous turns. Use RequestBoardReview before making further changes."
                : string.Empty;

            return $"""
                {AutonomousPromptSentinel}
                Autonomous facilitator loop trigger: {triggerReason}.

                Review the board and recent activity before acting.
                Call GetRecentEvents as well as GetBoardState so you can tell whether a phase has just started or changed.
                {phaseGuidance}
                {sessionBootstrap}
                {reviewGuidance}
                Ask at most one concise question or delegate one small facilitation move.
                When a phase has just started, you may delegate a small starter example via DelegateToAgent.
                Autonomous facilitation must not delete existing notes or connections.
                If there is nothing meaningful to add right now, do not call any tools and return an empty response.
                If the workshop is clearly complete, use the CompleteAutonomousSession tool.
                """;
        }

        private void EnsureInitialAutonomousPhase(Guid boardId)
        {
            var repository = _serviceProvider.GetRequiredService<IBoardsRepository>();
            var board = repository.GetById(boardId);
            if (board == null || board.Phase.HasValue ||
                !string.IsNullOrWhiteSpace(board.Domain) ||
                !string.IsNullOrWhiteSpace(board.SessionScope))
            {
                return;
            }

            var @event = new BoardContextUpdatedEvent
            {
                BoardId = boardId,
                OldDomain = board.Domain,
                NewDomain = board.Domain,
                OldSessionScope = board.SessionScope,
                NewSessionScope = board.SessionScope,
                OldPhase = board.Phase,
                NewPhase = EventStormingPhase.SetContext,
                OldAutonomousEnabled = board.AutonomousEnabled,
                NewAutonomousEnabled = board.AutonomousEnabled
            };

            _serviceProvider.GetRequiredService<IBoardEventPipeline>().ApplyAndLog(@event, "AI Agent");
            _serviceProvider.GetRequiredService<IHubContext<BoardsHub>>()
                .Clients.Group(boardId.ToString())
                .SendAsync("BoardContextUpdated", @event);
        }
    }
}