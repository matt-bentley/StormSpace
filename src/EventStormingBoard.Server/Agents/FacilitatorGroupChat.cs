using EventStormingBoard.Server.Entities;
using EventStormingBoard.Server.Filters;
using EventStormingBoard.Server.Models;
using EventStormingBoard.Server.Plugins;
using EventStormingBoard.Server.Services;
using Microsoft.Extensions.AI;
using System.Text;

namespace EventStormingBoard.Server.Agents
{
    /// <summary>
    /// A single step in the multi-agent conversation, representing one agent's contribution.
    /// </summary>
    public sealed class AgentStep
    {
        public required string AgentName { get; init; }
        public required string Content { get; init; }
        public List<AgentToolCallDto> ToolCalls { get; init; } = new();
    }

    /// <summary>
    /// Orchestrates a group chat between the Lead Facilitator and specialist agents.
    /// The Facilitator is the main entry point. When it calls RequestSpecialistProposal,
    /// the specialist pipeline runs: Specialist → Wall Scribe.
    /// When it calls RequestBoardReview, a specialist runs in advisory review mode.
    /// All agent steps are collected for UI display.
    /// </summary>
    public sealed class FacilitatorGroupChat
    {
        private readonly BoardAgentFactory _agentFactory;
        private readonly AgentToolCallFilter _toolCallFilter;

        public FacilitatorGroupChat(
            BoardAgentFactory agentFactory,
            AgentToolCallFilter toolCallFilter)
        {
            _agentFactory = agentFactory;
            _toolCallFilter = toolCallFilter;
        }

        public async Task<List<AgentStep>> RunAsync(
            Guid boardId,
            Board? board,
            BoardPlugin boardPlugin,
            List<ChatMessage> conversationHistory,
            bool allowDestructiveChanges,
            IBoardEventLog boardEventLog,
            CancellationToken cancellationToken,
            Func<AgentStep, Task>? onStepCompleted = null)
        {
            var steps = new List<AgentStep>();
            bool organiserRanViaDelegation = false;

            _toolCallFilter.BeginCapture(boardId.ToString());

            try
            {
                // Track pipeline boundaries to correctly attribute tool calls.
                // The Facilitator may call tools both before and after delegation.
                int delegationStartIndex = -1;
                int delegationEndIndex = -1;

                async Task<string> DelegationHandler(SpecialistAgent specialist, string instructions)
                {
                    delegationStartIndex = _toolCallFilter.PeekCaptureCount(boardId.ToString());
                    var pipelineResult = await RunSpecialistPipelineAsync(
                        boardId, board, boardPlugin, specialist, instructions,
                        allowDestructiveChanges, steps, onStepCompleted, cancellationToken).ConfigureAwait(false);
                    delegationEndIndex = _toolCallFilter.PeekCaptureCount(boardId.ToString());
                    return pipelineResult;
                }

                async Task<string> ReviewHandler(SpecialistAgent? specialist, string instructions)
                {
                    delegationStartIndex = _toolCallFilter.PeekCaptureCount(boardId.ToString());
                    var reviewResult = await RunReviewPipelineAsync(
                        boardId, board, boardPlugin, specialist, instructions,
                        steps, onStepCompleted, cancellationToken).ConfigureAwait(false);
                    delegationEndIndex = _toolCallFilter.PeekCaptureCount(boardId.ToString());
                    return reviewResult;
                }

                async Task<string> OrganisationHandler(string instructions)
                {
                    organiserRanViaDelegation = true;
                    delegationStartIndex = _toolCallFilter.PeekCaptureCount(boardId.ToString());
                    var result = await RunOrganiserAsync(
                        boardId, board, boardPlugin, instructions,
                        steps, onStepCompleted, cancellationToken).ConfigureAwait(false);
                    delegationEndIndex = _toolCallFilter.PeekCaptureCount(boardId.ToString());
                    return result;
                }

                var facilitator = _agentFactory.CreateFacilitator(board, boardPlugin, DelegationHandler, ReviewHandler, OrganisationHandler, boardId);
                var response = await facilitator.RunAsync(conversationHistory, cancellationToken: cancellationToken).ConfigureAwait(false);

                var allToolCalls = _toolCallFilter.EndCapture(boardId.ToString());

                // Facilitator's tool calls = everything outside the pipeline range.
                // Pre-delegation calls: [0, delegationStartIndex)
                // Post-delegation calls: [delegationEndIndex, allToolCalls.Count)
                List<AgentToolCallDto> facilitatorToolCalls;
                if (delegationStartIndex >= 0 && delegationEndIndex >= 0)
                {
                    facilitatorToolCalls = allToolCalls.GetRange(0, delegationStartIndex);
                    if (delegationEndIndex < allToolCalls.Count)
                    {
                        facilitatorToolCalls.AddRange(allToolCalls.GetRange(delegationEndIndex, allToolCalls.Count - delegationEndIndex));
                    }
                }
                else
                {
                    // No delegation happened — all tool calls belong to the Facilitator
                    facilitatorToolCalls = allToolCalls;
                }

                var facilitatorText = response.Text ?? string.Empty;
                AgentStep? facilitatorStep = null;
                if (!string.IsNullOrWhiteSpace(facilitatorText))
                {
                    facilitatorStep = new AgentStep
                    {
                        AgentName = "Facilitator",
                        Content = facilitatorText,
                        ToolCalls = facilitatorToolCalls
                    };
                }
                else if (facilitatorToolCalls.Count > 0)
                {
                    facilitatorStep = new AgentStep
                    {
                        AgentName = "Facilitator",
                        Content = string.Empty,
                        ToolCalls = facilitatorToolCalls
                    };
                }

                if (facilitatorStep != null)
                {
                    steps.Add(facilitatorStep);
                    if (onStepCompleted != null)
                        await onStepCompleted(facilitatorStep).ConfigureAwait(false);
                }

                // --- Organiser: run if notes were recently created (by agent or user) ---
                if (!organiserRanViaDelegation)
                {
                    bool agentCreatedNotes = allToolCalls.Any(tc =>
                        tc.Name == nameof(BoardPlugin.CreateNote) ||
                        tc.Name == nameof(BoardPlugin.CreateNotes));

                    bool hasRecentUserNotes = HasRecentNoteCreations(boardEventLog, boardId);

                    if (agentCreatedNotes || hasRecentUserNotes)
                    {
                        await RunOrganiserAsync(
                            boardId, board, boardPlugin,
                            "Organise the board according to positioning rules for the current phase.",
                            steps, onStepCompleted, cancellationToken).ConfigureAwait(false);
                    }
                }

                return steps;
            }
            catch
            {
                _toolCallFilter.EndCapture(boardId.ToString());
                throw;
            }
        }

        private async Task<string> RunSpecialistPipelineAsync(
            Guid boardId,
            Board? board,
            BoardPlugin boardPlugin,
            SpecialistAgent specialist,
            string instructions,
            bool allowDestructiveChanges,
            List<AgentStep> steps,
            Func<AgentStep, Task>? onStepCompleted,
            CancellationToken cancellationToken)
        {
            var sb = new StringBuilder();

            // Track tool call count before each agent runs so we can attribute them correctly
            var boardKey = boardId.ToString();

            // --- Step 1: Specialist proposes changes ---
            var specialistRole = specialist switch
            {
                SpecialistAgent.EventExplorer => AgentRole.EventExplorer,
                SpecialistAgent.TriggerMapper => AgentRole.TriggerMapper,
                SpecialistAgent.DomainDesigner => AgentRole.DomainDesigner,
                _ => throw new ArgumentOutOfRangeException(nameof(specialist))
            };

            var specialistName = specialist.ToString();
            var preSpecialistCount = CountCapturedToolCalls(boardKey);
            var specialistAgent = _agentFactory.CreateSpecialist(specialistRole, board, boardPlugin, boardId);
            var specialistMessages = new List<ChatMessage>
            {
                new(ChatRole.User, instructions)
            };
            var specialistResponse = await specialistAgent
                .RunAsync(specialistMessages, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            var proposal = specialistResponse.Text ?? string.Empty;
            var specialistToolCalls = SliceCapturedToolCalls(boardKey, preSpecialistCount);

            var specialistStep = new AgentStep
            {
                AgentName = specialistName,
                Content = proposal,
                ToolCalls = specialistToolCalls
            };
            steps.Add(specialistStep);
            if (onStepCompleted != null)
                await onStepCompleted(specialistStep).ConfigureAwait(false);

            sb.AppendLine($"## {specialistName} Proposal");
            sb.AppendLine(proposal);

            // --- Step 2: Wall Scribe executes the proposal ---
            {
                var preScribeCount = CountCapturedToolCalls(boardKey);
                var scribe = _agentFactory.CreateWallScribe(board, boardPlugin, allowDestructiveChanges, boardId);
                var scribeMessages = new List<ChatMessage>
                {
                    new(ChatRole.User, $"Execute the following proposal:\n\n{proposal}")
                };
                var scribeResponse = await scribe
                    .RunAsync(scribeMessages, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                var scribeText = scribeResponse.Text ?? "Changes applied.";
                var scribeToolCalls = SliceCapturedToolCalls(boardKey, preScribeCount);

                var scribeStep = new AgentStep
                {
                    AgentName = "WallScribe",
                    Content = scribeText,
                    ToolCalls = scribeToolCalls
                };
                steps.Add(scribeStep);
                if (onStepCompleted != null)
                    await onStepCompleted(scribeStep).ConfigureAwait(false);

                sb.AppendLine();
                sb.AppendLine("## Execution Result");
                sb.AppendLine(scribeText);
            }

            return sb.ToString();
        }

        private async Task<string> RunReviewPipelineAsync(
            Guid boardId,
            Board? board,
            BoardPlugin boardPlugin,
            SpecialistAgent? specialist,
            string instructions,
            List<AgentStep> steps,
            Func<AgentStep, Task>? onStepCompleted,
            CancellationToken cancellationToken)
        {
            var boardKey = boardId.ToString();

            // Resolve which specialist should perform the review.
            // If not specified, pick based on the current board phase.
            var resolvedSpecialist = specialist ?? ResolveReviewerForPhase(board);
            var reviewerRole = resolvedSpecialist switch
            {
                SpecialistAgent.EventExplorer => AgentRole.EventExplorer,
                SpecialistAgent.TriggerMapper => AgentRole.TriggerMapper,
                SpecialistAgent.DomainDesigner => AgentRole.DomainDesigner,
                _ => throw new ArgumentOutOfRangeException(nameof(specialist))
            };

            var reviewerName = resolvedSpecialist.ToString();
            var preReviewCount = CountCapturedToolCalls(boardKey);
            var reviewer = _agentFactory.CreateReviewer(reviewerRole, board, boardPlugin, boardId);
            var reviewMessages = new List<ChatMessage>
            {
                new(ChatRole.User, instructions)
            };
            var reviewResponse = await reviewer
                .RunAsync(reviewMessages, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            var reviewText = reviewResponse.Text ?? string.Empty;
            var reviewToolCalls = SliceCapturedToolCalls(boardKey, preReviewCount);

            var reviewStep = new AgentStep
            {
                AgentName = reviewerName,
                Content = reviewText,
                ToolCalls = reviewToolCalls
            };
            steps.Add(reviewStep);
            if (onStepCompleted != null)
                await onStepCompleted(reviewStep).ConfigureAwait(false);

            return reviewText;
        }

        private static SpecialistAgent ResolveReviewerForPhase(Board? board)
        {
            return board?.Phase switch
            {
                Models.EventStormingPhase.SetContext => SpecialistAgent.EventExplorer,
                Models.EventStormingPhase.IdentifyEvents => SpecialistAgent.EventExplorer,
                Models.EventStormingPhase.AddCommandsAndPolicies => SpecialistAgent.TriggerMapper,
                Models.EventStormingPhase.DefineAggregates => SpecialistAgent.DomainDesigner,
                Models.EventStormingPhase.BreakItDown => SpecialistAgent.DomainDesigner,
                _ => SpecialistAgent.EventExplorer
            };
        }

        /// <summary>
        /// Peeks at the current captured tool call count without ending the capture.
        /// </summary>
        private int CountCapturedToolCalls(string boardKey)
        {
            return _toolCallFilter.PeekCaptureCount(boardKey);
        }

        /// <summary>
        /// Returns tool calls captured since the given offset and marks them as claimed.
        /// </summary>
        private List<AgentToolCallDto> SliceCapturedToolCalls(string boardKey, int fromIndex)
        {
            return _toolCallFilter.PeekCaptureSlice(boardKey, fromIndex);
        }

        private async Task<string> RunOrganiserAsync(
            Guid boardId,
            Board? board,
            BoardPlugin boardPlugin,
            string instructions,
            List<AgentStep> steps,
            Func<AgentStep, Task>? onStepCompleted,
            CancellationToken cancellationToken)
        {
            var boardKey = boardId.ToString();
            _toolCallFilter.BeginCapture(boardKey);
            try
            {
                var organiser = _agentFactory.CreateOrganiser(board, boardPlugin, boardId);
                var organiserMessages = new List<ChatMessage>
                {
                    new(ChatRole.User, instructions)
                };
                var organiserResponse = await organiser
                    .RunAsync(organiserMessages, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                var organiserToolCalls = _toolCallFilter.EndCapture(boardKey);

                var organiserText = organiserResponse.Text ?? string.Empty;
                var organiserStep = new AgentStep
                {
                    AgentName = "Organiser",
                    Content = organiserText,
                    ToolCalls = organiserToolCalls
                };
                steps.Add(organiserStep);
                if (onStepCompleted != null)
                    await onStepCompleted(organiserStep).ConfigureAwait(false);

                return organiserText;
            }
            catch
            {
                _toolCallFilter.EndCapture(boardKey);
                throw;
            }
        }

        private static readonly TimeSpan RecentNoteWindow = TimeSpan.FromSeconds(120);

        private static bool HasRecentNoteCreations(IBoardEventLog eventLog, Guid boardId)
        {
            var cutoff = DateTimeOffset.UtcNow - RecentNoteWindow;
            var recentEvents = eventLog.GetRecent(boardId, 50);
            return recentEvents.Any(e =>
                e.EventType == "NoteCreatedEvent" &&
                e.Timestamp >= cutoff);
        }


    }
}
