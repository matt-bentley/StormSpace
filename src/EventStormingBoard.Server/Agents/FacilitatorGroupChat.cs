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
        public string? Prompt { get; init; }
        public List<AgentToolCallDto> ToolCalls { get; init; } = new();
    }

    /// <summary>
    /// Orchestrates a group chat between the Facilitator and delegated agents.
    /// The Facilitator is the main entry point. When it calls DelegateToAgent,
    /// the target agent runs with its own tools and returns a result.
    /// When it calls RequestBoardReview, a read-only review agent runs.
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
            List<AgentConfiguration> agentConfigurations,
            List<ChatMessage> conversationHistory,
            bool allowDestructiveChanges,
            CancellationToken cancellationToken,
            Func<AgentStep, Task>? onStepCompleted = null)
        {
            var steps = new List<AgentStep>();

            _toolCallFilter.BeginCapture(boardId.ToString());

            try
            {
                var delegationRanges = new List<(int Start, int End)>();

                async Task<string> DelegationHandler(string agentName, string instructions)
                {
                    var start = _toolCallFilter.PeekCaptureCount(boardId.ToString());
                    var result = await RunDelegatedAgentAsync(
                        boardId, board, boardPlugin, agentConfigurations,
                        agentName, instructions, allowDestructiveChanges,
                        steps, onStepCompleted, cancellationToken).ConfigureAwait(false);
                    var end = _toolCallFilter.PeekCaptureCount(boardId.ToString());
                    delegationRanges.Add((start, end));
                    return result;
                }

                async Task<string> ReviewHandler(string? agentName, string instructions)
                {
                    var start = _toolCallFilter.PeekCaptureCount(boardId.ToString());
                    var result = await RunReviewAgentAsync(
                        boardId, board, boardPlugin, agentConfigurations,
                        agentName, instructions,
                        steps, onStepCompleted, cancellationToken).ConfigureAwait(false);
                    var end = _toolCallFilter.PeekCaptureCount(boardId.ToString());
                    delegationRanges.Add((start, end));
                    return result;
                }

                async Task<string> QuestionHandler(string agentName, string question)
                {
                    var start = _toolCallFilter.PeekCaptureCount(boardId.ToString());
                    var facilitatorConfig2 = agentConfigurations.FirstOrDefault(a => a.IsFacilitator);
                    var result = await RunQuestionAgentAsync(
                        boardId, board, boardPlugin, agentConfigurations,
                        facilitatorConfig2, agentName, question,
                        steps, onStepCompleted, cancellationToken).ConfigureAwait(false);
                    var end = _toolCallFilter.PeekCaptureCount(boardId.ToString());
                    delegationRanges.Add((start, end));
                    return result;
                }

                var delegationPlugin = new DelegationPlugin(DelegationHandler, ReviewHandler, QuestionHandler);

                var facilitatorConfig = agentConfigurations.FirstOrDefault(a => a.IsFacilitator)
                    ?? throw new InvalidOperationException("No facilitator agent configured for this board.");

                var facilitator = _agentFactory.CreateAgent(
                    facilitatorConfig, board, boardPlugin, delegationPlugin,
                    boardId, allowDestructiveChanges, agentConfigurations);

                var response = await facilitator.RunAsync(conversationHistory, cancellationToken: cancellationToken).ConfigureAwait(false);

                var allToolCalls = _toolCallFilter.EndCapture(boardId.ToString());

                // Facilitator's tool calls = everything outside delegation ranges.
                var delegatedIndices = new HashSet<int>();
                foreach (var (start, end) in delegationRanges)
                {
                    for (var i = start; i < end; i++)
                        delegatedIndices.Add(i);
                }

                var facilitatorToolCalls = new List<AgentToolCallDto>();
                for (var i = 0; i < allToolCalls.Count; i++)
                {
                    if (!delegatedIndices.Contains(i))
                        facilitatorToolCalls.Add(allToolCalls[i]);
                }

                var facilitatorText = response.Text ?? string.Empty;
                AgentStep? facilitatorStep = null;
                if (!string.IsNullOrWhiteSpace(facilitatorText))
                {
                    facilitatorStep = new AgentStep
                    {
                        AgentName = facilitatorConfig.Name,
                        Content = facilitatorText,
                        ToolCalls = facilitatorToolCalls
                    };
                }
                else if (facilitatorToolCalls.Count > 0)
                {
                    facilitatorStep = new AgentStep
                    {
                        AgentName = facilitatorConfig.Name,
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

        private async Task<string> RunDelegatedAgentAsync(
            Guid boardId,
            Board? board,
            BoardPlugin boardPlugin,
            List<AgentConfiguration> agentConfigurations,
            string agentName,
            string instructions,
            bool allowDestructiveChanges,
            List<AgentStep> steps,
            Func<AgentStep, Task>? onStepCompleted,
            CancellationToken cancellationToken)
        {
            var agentConfig = FindActiveAgent(agentConfigurations, agentName, board?.Phase);
            if (agentConfig == null)
            {
                var available = string.Join(", ", agentConfigurations
                    .Where(a => !a.IsFacilitator && IsActiveInPhase(a, board?.Phase))
                    .Select(a => $"\"{a.Name}\""));
                return $"Agent '{agentName}' not found or not active in the current phase. Available agents: {available}";
            }

            var boardKey = boardId.ToString();
            var preAgentCount = _toolCallFilter.PeekCaptureCount(boardKey);

            // If the agent has AskAgentQuestion in its tools, give it a question-only DelegationPlugin
            DelegationPlugin? agentDelegationPlugin = null;
            List<AgentConfiguration>? agentVisibleConfigs = null;
            if (agentConfig.AllowedTools.Contains(nameof(DelegationPlugin.AskAgentQuestion)))
            {
                async Task<string> AgentQuestionHandler(string targetName, string question)
                {
                    // Validate CanAskAgents
                    if (agentConfig.CanAskAgents != null &&
                        !agentConfig.CanAskAgents.Any(n => string.Equals(n, targetName, StringComparison.OrdinalIgnoreCase)))
                    {
                        var allowed = string.Join(", ", agentConfig.CanAskAgents.Select(n => $"\"{n}\""));
                        return $"You are not allowed to ask '{targetName}'. You can ask: {allowed}";
                    }
                    return await RunQuestionAgentAsync(
                        boardId, board, boardPlugin, agentConfigurations,
                        agentConfig, targetName, question,
                        steps, onStepCompleted, cancellationToken).ConfigureAwait(false);
                }

                agentDelegationPlugin = new DelegationPlugin(
                    (_, _) => Task.FromResult("Delegation is not available for this agent."),
                    (_, _) => Task.FromResult("Board review is not available for this agent."),
                    AgentQuestionHandler);
                agentVisibleConfigs = agentConfigurations;
            }

            var agent = _agentFactory.CreateAgent(
                agentConfig, board, boardPlugin, agentDelegationPlugin, boardId, allowDestructiveChanges, agentVisibleConfigs);

            var messages = new List<ChatMessage>
            {
                new(ChatRole.User, instructions)
            };

            var response = await agent.RunAsync(messages, cancellationToken: cancellationToken).ConfigureAwait(false);
            var agentText = response.Text ?? string.Empty;
            var agentToolCalls = _toolCallFilter.PeekCaptureSlice(boardKey, preAgentCount);

            var agentStep = new AgentStep
            {
                AgentName = agentConfig.Name,
                Content = agentText,
                Prompt = instructions,
                ToolCalls = agentToolCalls
            };
            steps.Add(agentStep);
            if (onStepCompleted != null)
                await onStepCompleted(agentStep).ConfigureAwait(false);

            return agentText;
        }

        private async Task<string> RunReviewAgentAsync(
            Guid boardId,
            Board? board,
            BoardPlugin boardPlugin,
            List<AgentConfiguration> agentConfigurations,
            string? agentName,
            string instructions,
            List<AgentStep> steps,
            Func<AgentStep, Task>? onStepCompleted,
            CancellationToken cancellationToken)
        {
            // If no agent specified, pick an appropriate one for the phase
            AgentConfiguration? reviewerConfig = null;
            if (!string.IsNullOrWhiteSpace(agentName))
            {
                reviewerConfig = FindActiveAgent(agentConfigurations, agentName, board?.Phase);
            }

            // Fallback: pick first non-facilitator agent active in current phase
            reviewerConfig ??= agentConfigurations
                .Where(a => !a.IsFacilitator && IsActiveInPhase(a, board?.Phase))
                .OrderBy(a => a.Order)
                .FirstOrDefault();

            if (reviewerConfig == null)
                return "No suitable agent found for review in the current phase.";

            var boardKey = boardId.ToString();
            var preReviewCount = _toolCallFilter.PeekCaptureCount(boardKey);

            // For reviews, only give read-only tools regardless of config
            var reviewConfig = new AgentConfiguration
            {
                Id = reviewerConfig.Id,
                Name = reviewerConfig.Name,
                IsFacilitator = false,
                SystemPrompt = reviewerConfig.SystemPrompt + "\n\nYou are acting as a **Board Reviewer**. Provide advisory feedback only. Do NOT modify the board.",
                Icon = reviewerConfig.Icon,
                Color = reviewerConfig.Color,
                ActivePhases = reviewerConfig.ActivePhases,
                AllowedTools = new List<string> { nameof(BoardPlugin.GetBoardState), nameof(BoardPlugin.GetRecentEvents) },
                Order = reviewerConfig.Order
            };

            var reviewer = _agentFactory.CreateAgent(
                reviewConfig, board, boardPlugin, null, boardId);

            var messages = new List<ChatMessage>
            {
                new(ChatRole.User, instructions)
            };

            var response = await reviewer.RunAsync(messages, cancellationToken: cancellationToken).ConfigureAwait(false);
            var reviewText = response.Text ?? string.Empty;
            var reviewToolCalls = _toolCallFilter.PeekCaptureSlice(boardKey, preReviewCount);

            var reviewStep = new AgentStep
            {
                AgentName = reviewerConfig.Name,
                Content = reviewText,
                Prompt = instructions,
                ToolCalls = reviewToolCalls
            };
            steps.Add(reviewStep);
            if (onStepCompleted != null)
                await onStepCompleted(reviewStep).ConfigureAwait(false);

            return reviewText;
        }

        private async Task<string> RunQuestionAgentAsync(
            Guid boardId,
            Board? board,
            BoardPlugin boardPlugin,
            List<AgentConfiguration> agentConfigurations,
            AgentConfiguration? callerConfig,
            string agentName,
            string question,
            List<AgentStep> steps,
            Func<AgentStep, Task>? onStepCompleted,
            CancellationToken cancellationToken)
        {
            // Validate CanAskAgents for the caller
            if (callerConfig?.CanAskAgents != null &&
                !callerConfig.CanAskAgents.Any(n => string.Equals(n, agentName, StringComparison.OrdinalIgnoreCase)))
            {
                var allowed = string.Join(", ", callerConfig.CanAskAgents.Select(n => $"\"{n}\""));
                return $"You are not allowed to ask '{agentName}'. You can ask: {allowed}";
            }

            var targetConfig = FindActiveAgent(agentConfigurations, agentName, board?.Phase);
            if (targetConfig == null)
            {
                var available = string.Join(", ", agentConfigurations
                    .Where(a => !a.IsFacilitator && IsActiveInPhase(a, board?.Phase))
                    .Select(a => $"\"{a.Name}\""));
                return $"Agent '{agentName}' not found or not active in the current phase. Available agents: {available}";
            }

            var boardKey = boardId.ToString();
            var preQuestionCount = _toolCallFilter.PeekCaptureCount(boardKey);

            // Give the answering agent read-only board access and a question-answering prompt
            var questionConfig = new AgentConfiguration
            {
                Id = targetConfig.Id,
                Name = targetConfig.Name,
                IsFacilitator = false,
                SystemPrompt = targetConfig.SystemPrompt +
                    "\n\nYou are being asked a question by another agent. Answer based on your expertise and the board context. Do NOT modify the board.",
                Icon = targetConfig.Icon,
                Color = targetConfig.Color,
                ActivePhases = targetConfig.ActivePhases,
                AllowedTools = new List<string> { nameof(BoardPlugin.GetBoardState) },
                Order = targetConfig.Order
            };

            var answerer = _agentFactory.CreateAgent(
                questionConfig, board, boardPlugin, null, boardId);

            var messages = new List<ChatMessage>
            {
                new(ChatRole.User, question)
            };

            var response = await answerer.RunAsync(messages, cancellationToken: cancellationToken).ConfigureAwait(false);
            var answerText = response.Text ?? string.Empty;
            var answerToolCalls = _toolCallFilter.PeekCaptureSlice(boardKey, preQuestionCount);

            var answerStep = new AgentStep
            {
                AgentName = targetConfig.Name,
                Content = answerText,
                Prompt = question,
                ToolCalls = answerToolCalls
            };
            steps.Add(answerStep);
            if (onStepCompleted != null)
                await onStepCompleted(answerStep).ConfigureAwait(false);

            return answerText;
        }

        private static AgentConfiguration? FindActiveAgent(
            List<AgentConfiguration> configs, string agentName, EventStormingPhase? currentPhase)
        {
            // Try exact match first (case-insensitive)
            var config = configs.FirstOrDefault(a =>
                string.Equals(a.Name, agentName, StringComparison.OrdinalIgnoreCase));

            // Fallback: normalized match (remove spaces, hyphens, underscores)
            if (config == null)
            {
                var normalized = NormalizeName(agentName);
                config = configs.FirstOrDefault(a =>
                    string.Equals(NormalizeName(a.Name), normalized, StringComparison.OrdinalIgnoreCase));
            }

            if (config == null) return null;
            if (!IsActiveInPhase(config, currentPhase)) return null;

            return config;
        }

        private static string NormalizeName(string name) =>
            name.Replace(" ", "").Replace("-", "").Replace("_", "");

        private static bool IsActiveInPhase(AgentConfiguration config, EventStormingPhase? currentPhase)
        {
            // null ActivePhases means active in all phases
            if (config.ActivePhases == null || config.ActivePhases.Count == 0) return true;
            // If no phase is set on the board, all agents are active
            if (!currentPhase.HasValue) return true;
            return config.ActivePhases.Contains(currentPhase.Value);
        }
    }
}
