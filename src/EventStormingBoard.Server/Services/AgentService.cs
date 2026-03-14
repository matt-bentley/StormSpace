using EventStormingBoard.Server.Agents;
using EventStormingBoard.Server.Entities;
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
    }

    public sealed class AgentService : IAgentService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly AzureOpenAIOptions _options;
        private readonly AgentToolCallFilter _toolCallFilter;
        private readonly Azure.AI.OpenAI.AzureOpenAIClient _azureOpenAIClient;
        private readonly ConcurrentDictionary<Guid, List<ChatMessage>> _conversationHistories = new();
        private readonly ConcurrentDictionary<Guid, List<AgentChatMessageDto>> _displayHistories = new();

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
            var conversationHistory = _conversationHistories.GetOrAdd(boardId, static _ => new List<ChatMessage>());
            var displayHistory = _displayHistories.GetOrAdd(boardId, static _ => new List<AgentChatMessageDto>());

            lock (conversationHistory)
            {
                conversationHistory.Add(new ChatMessage(ChatRole.User, userMessage));
            }

            var displayUserMessage = new AgentChatMessageDto
            {
                Role = "user",
                UserName = userName,
                Content = userMessage,
                Timestamp = DateTime.UtcNow
            };
            lock (displayHistory)
            {
                displayHistory.Add(displayUserMessage);
            }

            var steps = await InvokeAgentAsync(boardId, allowDestructiveChanges: true, CancellationToken.None).ConfigureAwait(false);
            var messages = AppendAgentSteps(boardId, steps);
            return messages;
        }

        public async Task<AutonomousAgentResult> RunAutonomousTurnAsync(Guid boardId, string triggerReason, CancellationToken cancellationToken)
        {
            EnsureInitialAutonomousPhase(boardId);

            var conversationHistory = _conversationHistories.GetOrAdd(boardId, static _ => new List<ChatMessage>());
            lock (conversationHistory)
            {
                conversationHistory.Add(new ChatMessage(ChatRole.User, BuildAutonomousPrompt(boardId, triggerReason)));
            }

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(45));

            var steps = await InvokeAgentAsync(boardId, allowDestructiveChanges: false, timeoutCts.Token).ConfigureAwait(false);
            var board = _serviceProvider.GetRequiredService<IBoardsRepository>().GetById(boardId);
            var allToolCalls = steps.SelectMany(s => s.ToolCalls).ToList();
            var facilitatorReply = steps.FirstOrDefault(s => s.AgentName == "Facilitator")?.Content ?? string.Empty;
            var trimmedReply = facilitatorReply.Trim();
            var usedCompletionTool = allToolCalls.Any(call => string.Equals(call.Name, nameof(BoardPlugin.CompleteAutonomousSession), StringComparison.Ordinal));

            if (board?.AutonomousEnabled == false && usedCompletionTool)
            {
                var visibleMessage = string.IsNullOrWhiteSpace(trimmedReply)
                    ? "The session looks complete, so I’m pausing autonomous facilitation here."
                    : trimmedReply;
                PatchFacilitatorContent(steps, visibleMessage);                var completedMessages = AppendAgentSteps(boardId, steps);

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
                return new AutonomousAgentResult
                {
                    Status = AutonomousAgentStatus.Idle,
                    TriggerReason = triggerReason,
                    ToolCalls = allToolCalls,
                    Diagnostics = "No visible reply and no tool activity."
                };
            }

            var visibleActedMessage = string.IsNullOrWhiteSpace(trimmedReply)
                ? "I made a small facilitation update to keep the session moving."
                : trimmedReply;

            PatchFacilitatorContent(steps, visibleActedMessage);
            var actedMessages = AppendAgentSteps(boardId, steps);

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
            _conversationHistories.TryRemove(boardId, out _);
            _displayHistories.TryRemove(boardId, out _);
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

            return await groupChat.RunAsync(
                boardId, board, boardPlugin, messages,
                allowDestructiveChanges, cancellationToken).ConfigureAwait(false);
        }

        private static void PatchFacilitatorContent(List<AgentStep> steps, string content)
        {
            var facilitator = steps.FirstOrDefault(s => s.AgentName == "Facilitator");
            if (facilitator != null && string.IsNullOrWhiteSpace(facilitator.Content))
            {
                // AgentStep.Content has init-only setter; replace the step
                var index = steps.IndexOf(facilitator);
                steps[index] = new AgentStep
                {
                    AgentName = facilitator.AgentName,
                    Content = content,
                    ToolCalls = facilitator.ToolCalls
                };
            }
        }

        private List<AgentChatMessageDto> AppendAgentSteps(Guid boardId, List<AgentStep> steps)
        {
            var conversationHistory = _conversationHistories.GetOrAdd(boardId, static _ => new List<ChatMessage>());
            var displayHistory = _displayHistories.GetOrAdd(boardId, static _ => new List<AgentChatMessageDto>());

            // Append the facilitator's reply to the LLM conversation history
            var facilitatorReply = steps.FirstOrDefault(s => s.AgentName == "Facilitator")?.Content ?? string.Empty;
            lock (conversationHistory)
            {
                conversationHistory.Add(new ChatMessage(ChatRole.Assistant, facilitatorReply));
            }

            var result = new List<AgentChatMessageDto>();
            lock (displayHistory)
            {
                foreach (var step in steps)
                {
                    if (string.IsNullOrWhiteSpace(step.Content) && step.ToolCalls.Count == 0)
                        continue;

                    var dto = new AgentChatMessageDto
                    {
                        Role = "assistant",
                        AgentName = step.AgentName,
                        Content = step.Content,
                        ToolCalls = step.ToolCalls.Count > 0 ? step.ToolCalls : null,
                        Timestamp = DateTime.UtcNow
                    };
                    displayHistory.Add(dto);
                    result.Add(dto);
                }

                // Ensure the client always receives at least one response to clear the loading state
                if (result.Count == 0)
                {
                    var fallback = new AgentChatMessageDto
                    {
                        Role = "assistant",
                        AgentName = "Facilitator",
                        Content = string.Empty,
                        Timestamp = DateTime.UtcNow
                    };
                    displayHistory.Add(fallback);
                    result.Add(fallback);
                }
            }

            return result;
        }

        private string BuildAutonomousPrompt(Guid boardId, string triggerReason)
        {
            var board = _serviceProvider.GetRequiredService<IBoardsRepository>().GetById(boardId);
            var isEarlyFlowLayoutPhase = board?.Phase is EventStormingPhase.IdentifyEvents or EventStormingPhase.AddCommandsAndPolicies;
            var needsSessionIntroduction = board is not null &&
                string.IsNullOrWhiteSpace(board.Domain) &&
                string.IsNullOrWhiteSpace(board.SessionScope) &&
                board.Notes.Count == 0 &&
                board.Connections.Count == 0 &&
                board.Phase is null or EventStormingPhase.SetContext;
            var phaseGuidance = board?.Phase switch
            {
                EventStormingPhase.SetContext => "If this phase has just started, explain that participants should clarify the domain boundary, business goal, actors, and scope before modeling the flow.",
                EventStormingPhase.IdentifyEvents => "If this phase has just started, explain that participants should brainstorm domain events in past tense, place them chronologically, and keep adding more events themselves after your starter example (max 3). They should think of as many events as possible before moving on to adding commands or policies.",
                EventStormingPhase.AddCommandsAndPolicies => "If this phase has just started, explain that participants should pick one existing Event at a time and connect it back to what triggered it using a Command or Policy. Your first starter example must cover exactly one Event, not the whole happy path. Prefer a user-invoked Command with a User note when the domain plausibly supports one, and explain the difference between a user-invoked Command, an automated Command, and a Policy before asking participants to continue the rest themselves.",
                EventStormingPhase.DefineAggregates => "If this phase has just started, explain that participants should identify the core aggregates that own behavior around existing commands and events. Do not add ReadModels unless someone explicitly asks for them.",
                EventStormingPhase.BreakItDown => "If this phase has just started, explain that participants should identify bounded contexts, subdomains, and integration boundaries rather than adding lots of new notes.",
                _ => "If the current phase has just started, explain the phase goal and what participants should do before or alongside your first example."
            };

            var sessionBootstrap = needsSessionIntroduction
                ? "This is a brand new blank session. Ensure the session starts in SetContext. Your very first reply must do four things in this order: (1) briefly explain the high-level Event Storming process and the main phases, (2) explain the house rules for how participants should contribute, (3) explain why the team is working this way, and only then (4) ask one concrete question to capture the missing domain and scope context. Do not skip the introduction and do not jump straight to the question."
                : string.Empty;

            var layoutGuidance = isEarlyFlowLayoutPhase
                ? "When adding or rearranging notes in this phase, leave generous room between events or clusters so users can keep extending the board. If notes are getting cramped, use MoveNotes to spread them out."
                : string.Empty;

            return $"""
                Autonomous facilitator loop trigger: {triggerReason}.

                Review the board and recent activity before acting.
                Call GetRecentEvents as well as GetBoardState so you can tell whether a phase has just started or changed.
                {phaseGuidance}
                {sessionBootstrap}
                {layoutGuidance}
                Ask at most one concise question or make one small facilitation move unless users have explicitly asked for broader changes.
                By default, autonomous facilitation should prefer spreading out existing events to make collaboration easier, asking questions that get participants thinking, suggesting improvements, or suggesting that the group moves to the next phase if the current phase looks complete.
                When a phase has just started (or you have started it), or when the users have asked for help with the phase, you may delegate a very small starter example via RequestSpecialistProposal to teach how the phase works. Keep that example intentionally small, then stop and hand back to the users.
                In AddCommandsAndPolicies, one small facilitation move means one Event starter example only. Do not continue into neighboring Events after that example.
                Autonomous facilitation must not delete existing notes or connections. If something looks wrong, mention it, add a Concern, or ask the users before making destructive changes.
                Avoid rewriting existing work unless the users explicitly ask you to; prefer suggestions over large unsolicited changes.
                Do not create ReadModel notes unless a user explicitly asks for them or the facilitator instructions clearly require them.
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
                OldAgentInstructions = board.AgentInstructions,
                NewAgentInstructions = board.AgentInstructions,
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