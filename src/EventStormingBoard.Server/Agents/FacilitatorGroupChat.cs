using EventStormingBoard.Server.Entities;
using EventStormingBoard.Server.Filters;
using EventStormingBoard.Server.Models;
using EventStormingBoard.Server.Plugins;
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
    /// the specialist pipeline runs: Specialist → Challenger → Wall Scribe (if approved).
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
            CancellationToken cancellationToken,
            Func<AgentStep, Task>? onStepCompleted = null)
        {
            var steps = new List<AgentStep>();

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

                var facilitator = _agentFactory.CreateFacilitator(board, boardPlugin, DelegationHandler, boardId);
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

            // --- Step 2: Challenger reviews the proposal ---
            var preChallengerCount = CountCapturedToolCalls(boardKey);
            var challenger = _agentFactory.CreateChallenger(board, boardPlugin, boardId);
            var challengerMessages = new List<ChatMessage>
            {
                new(ChatRole.User, $"Review the following proposal from {specialistName}:\n\n{proposal}")
            };
            var challengerResponse = await challenger
                .RunAsync(challengerMessages, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            var review = challengerResponse.Text ?? string.Empty;
            var challengerToolCalls = SliceCapturedToolCalls(boardKey, preChallengerCount);

            var challengerStep = new AgentStep
            {
                AgentName = "Challenger",
                Content = review,
                ToolCalls = challengerToolCalls
            };
            steps.Add(challengerStep);
            if (onStepCompleted != null)
                await onStepCompleted(challengerStep).ConfigureAwait(false);

            sb.AppendLine();
            sb.AppendLine("## Challenger Review");
            sb.AppendLine(review);

            // --- Step 3: If approved, Wall Scribe executes ---
            if (IsApproved(review))
            {
                var preScribeCount = CountCapturedToolCalls(boardKey);
                var scribe = _agentFactory.CreateWallScribe(board, boardPlugin, allowDestructiveChanges, boardId);
                var scribeMessages = new List<ChatMessage>
                {
                    new(ChatRole.User,
                        $"Execute the following approved proposal:\n\n{proposal}" +
                        (string.IsNullOrWhiteSpace(review) ? string.Empty : $"\n\nChallenger notes:\n{review}"))
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
            else
            {
                sb.AppendLine();
                sb.AppendLine("## Execution");
                sb.AppendLine("Proposal was REJECTED by the Challenger. No changes were made to the board.");
                sb.AppendLine("Consider revising the approach based on the Challenger's feedback.");
            }

            return sb.ToString();
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

        private static bool IsApproved(string reviewText)
        {
            // Use word boundary check to avoid false positives on "DISAPPROVED"
            bool hasApproved = System.Text.RegularExpressions.Regex.IsMatch(
                reviewText, @"\bAPPROVED\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            bool hasRejected = reviewText.Contains("REJECTED", StringComparison.OrdinalIgnoreCase)
                            || reviewText.Contains("NOT APPROVED", StringComparison.OrdinalIgnoreCase)
                            || reviewText.Contains("DISAPPROVED", StringComparison.OrdinalIgnoreCase);

            return hasApproved && !hasRejected;
        }
    }
}
