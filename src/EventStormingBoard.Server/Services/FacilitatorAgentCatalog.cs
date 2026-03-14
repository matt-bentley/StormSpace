using EventStormingBoard.Server.Plugins;

namespace EventStormingBoard.Server.Services
{
    public static class FacilitatorAgentIds
    {
        public const string Planner = "planner";
        public const string BoardAnalyst = "board-analyst";
        public const string FacilitationCoach = "facilitation-coach";
        public const string BoardEditor = "board-editor";
    }

    public sealed record FacilitatorAgentDescriptor(string Id, string Name, string Description);

    public sealed class PlannerDecision
    {
        public string? VisibleMessage { get; set; }
        public string? Summary { get; set; }
        public List<PlannerStepDecision> Steps { get; set; } = new();
    }

    public sealed class PlannerStepDecision
    {
        public string AgentId { get; set; } = string.Empty;
        public string? Goal { get; set; }
        public string? HandoffMessage { get; set; }
        public string? ContextSummary { get; set; }
    }

    public sealed class SpecialistResponse
    {
        public string? VisibleMessage { get; set; }
        public string? Summary { get; set; }
    }

    public static class FacilitatorAgentCatalog
    {
        public static readonly FacilitatorAgentDescriptor Planner = new(
            FacilitatorAgentIds.Planner,
            "Planner Agent",
            "Chooses the next specialist steps and emits visible handoffs.");

        public static readonly FacilitatorAgentDescriptor BoardAnalyst = new(
            FacilitatorAgentIds.BoardAnalyst,
            "Board Analyst",
            "Inspects the board state, recent activity, and modeling gaps.");

        public static readonly FacilitatorAgentDescriptor FacilitationCoach = new(
            FacilitatorAgentIds.FacilitationCoach,
            "Facilitation Coach",
            "Explains the current workshop phase and coaches participants on the next step.");

        public static readonly FacilitatorAgentDescriptor BoardEditor = new(
            FacilitatorAgentIds.BoardEditor,
            "Board Editor",
            "Applies note, connection, movement, and phase changes on the board.");

        public static FacilitatorAgentDescriptor Get(string agentId)
        {
            return agentId.Trim().ToLowerInvariant() switch
            {
                FacilitatorAgentIds.Planner => Planner,
                FacilitatorAgentIds.BoardAnalyst => BoardAnalyst,
                FacilitatorAgentIds.FacilitationCoach => FacilitationCoach,
                FacilitatorAgentIds.BoardEditor => BoardEditor,
                _ => throw new InvalidOperationException($"Unknown facilitator agent '{agentId}'.")
            };
        }

        public static bool IsKnown(string? agentId)
        {
            if (string.IsNullOrWhiteSpace(agentId))
            {
                return false;
            }

            return agentId.Trim().ToLowerInvariant() is
                FacilitatorAgentIds.Planner or
                FacilitatorAgentIds.BoardAnalyst or
                FacilitatorAgentIds.FacilitationCoach or
                FacilitatorAgentIds.BoardEditor;
        }
    }

    public static class AgentToolPolicy
    {
        public static IReadOnlyList<string> GetToolMethodNames(string agentId, bool allowDestructiveChanges)
        {
            return agentId.Trim().ToLowerInvariant() switch
            {
                FacilitatorAgentIds.Planner => ReadOnlyTools(),
                FacilitatorAgentIds.BoardAnalyst => ReadOnlyTools(),
                FacilitatorAgentIds.FacilitationCoach => ReadOnlyTools(),
                FacilitatorAgentIds.BoardEditor => BoardEditorTools(allowDestructiveChanges),
                _ => throw new InvalidOperationException($"Unknown facilitator agent '{agentId}'.")
            };

            static IReadOnlyList<string> ReadOnlyTools()
            {
                return
                [
                    nameof(BoardPlugin.GetBoardState),
                    nameof(BoardPlugin.GetRecentEvents)
                ];
            }

            static IReadOnlyList<string> BoardEditorTools(bool allowDeletes)
            {
                var toolNames = new List<string>
                {
                    nameof(BoardPlugin.GetBoardState),
                    nameof(BoardPlugin.GetRecentEvents),
                    nameof(BoardPlugin.SetDomain),
                    nameof(BoardPlugin.SetSessionScope),
                    nameof(BoardPlugin.SetPhase),
                    nameof(BoardPlugin.CompleteAutonomousSession),
                    nameof(BoardPlugin.CreateNote),
                    nameof(BoardPlugin.CreateConnection),
                    nameof(BoardPlugin.EditNoteText),
                    nameof(BoardPlugin.MoveNotes),
                    nameof(BoardPlugin.CreateNotes),
                    nameof(BoardPlugin.CreateConnections)
                };

                if (allowDeletes)
                {
                    toolNames.Add(nameof(BoardPlugin.DeleteNotes));
                }

                return toolNames;
            }
        }
    }
}