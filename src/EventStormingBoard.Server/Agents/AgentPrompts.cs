using EventStormingBoard.Server.Entities;
using EventStormingBoard.Server.Models;
using System.Text;

namespace EventStormingBoard.Server.Agents
{
    public static class AgentPrompts
    {
        /// <summary>
        /// Appends runtime board context (domain, scope, phase) and available agents to the agent's configured system prompt.
        /// </summary>
        public static string BuildRuntimeContext(Board? board, List<AgentConfiguration>? agentConfigurations = null, AgentConfiguration? callingAgent = null)
        {
            if (board == null && (agentConfigurations == null || agentConfigurations.Count == 0))
                return string.Empty;

            var sb = new StringBuilder();

            if (board != null)
            {
                if (!string.IsNullOrWhiteSpace(board.Domain))
                {
                    sb.AppendLine();
                    sb.AppendLine("--- DOMAIN CONTEXT ---");
                    sb.AppendLine(board.Domain);
                }

                if (!string.IsNullOrWhiteSpace(board.SessionScope))
                {
                    sb.AppendLine();
                    sb.AppendLine("--- SESSION SCOPE ---");
                    sb.AppendLine(board.SessionScope);
                }

                if (board.Phase.HasValue)
                {
                    sb.AppendLine();
                    sb.AppendLine($"--- CURRENT PHASE: {board.Phase.Value} ---");
                    sb.AppendLine(board.Phase.Value switch
                    {
                        EventStormingPhase.SetContext =>
                            "Focus on understanding the domain and session scope. Ask clarifying questions about boundaries, actors, and processes before adding notes.",
                        EventStormingPhase.IdentifyEvents =>
                            "Focus on brainstorming domain Events. Events in past tense, chronological left-to-right. Add Concerns for open questions. Do not add Commands, Policies, or Aggregates yet.",
                        EventStormingPhase.AddCommandsAndPolicies =>
                            "Focus on adding Commands and Policies for each Event. Work on one Event at a time. Leave enough space and use MoveNotes if crowded. Do not add Aggregates or ReadModels.",
                        EventStormingPhase.DefineAggregates =>
                            "Focus on defining Aggregates. Place them between Commands and Events. Do not add ReadModels unless explicitly requested.",
                        EventStormingPhase.BreakItDown =>
                            "Focus on grouping flows into Bounded Contexts and Subdomains. Identify Integration Events between contexts.",
                        _ => string.Empty
                    });
                }
            }

            // Inject available agents for delegation
            if (agentConfigurations is { Count: > 0 })
            {
                var delegatable = agentConfigurations
                    .Where(a => !a.IsFacilitator)
                    .OrderBy(a => a.Order)
                    .ToList();

                if (delegatable.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("--- AVAILABLE AGENTS FOR DELEGATION ---");
                    sb.AppendLine("Use these EXACT names when calling DelegateToAgent, RequestBoardReview, or AskAgentQuestion.");
                    foreach (var agent in delegatable)
                    {
                        var phases = agent.ActivePhases == null || agent.ActivePhases.Count == 0
                            ? "all phases"
                            : string.Join(", ", agent.ActivePhases);
                        var tools = agent.AllowedTools.Count > 0
                            ? string.Join(", ", agent.AllowedTools)
                            : "none";
                        sb.AppendLine($"- \"{agent.Name}\" — Active in: {phases}. Tools: {tools}");
                    }
                }

                // Inject askable agents for the calling agent
                if (callingAgent != null && callingAgent.AllowedTools.Contains(nameof(DelegationPlugin.AskAgentQuestion)))
                {
                    var askable = callingAgent.CanAskAgents == null
                        ? delegatable.Where(a => !string.Equals(a.Name, callingAgent.Name, StringComparison.OrdinalIgnoreCase)).ToList()
                        : delegatable.Where(a => callingAgent.CanAskAgents.Any(n =>
                            string.Equals(n, a.Name, StringComparison.OrdinalIgnoreCase))).ToList();

                    if (askable.Count > 0)
                    {
                        sb.AppendLine();
                        sb.AppendLine("--- AGENTS YOU CAN ASK QUESTIONS TO ---");
                        sb.AppendLine("Use these EXACT names when calling AskAgentQuestion.");
                        foreach (var agent in askable)
                        {
                            sb.AppendLine($"- \"{agent.Name}\"");
                        }
                    }
                }
            }

            return sb.ToString();
        }
    }
}