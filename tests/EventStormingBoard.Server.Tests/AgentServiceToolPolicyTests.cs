using EventStormingBoard.Server.Plugins;
using EventStormingBoard.Server.Services;

namespace EventStormingBoard.Server.Tests;

public class AgentServiceToolPolicyTests
{
    [Fact]
    public void PlannerToolPolicy_IsReadOnly()
    {
        var toolNames = AgentToolPolicy.GetToolMethodNames(FacilitatorAgentIds.Planner, allowDestructiveChanges: false);

        Assert.Equal(2, toolNames.Count);
        Assert.Contains(nameof(BoardPlugin.GetBoardState), toolNames);
        Assert.Contains(nameof(BoardPlugin.GetRecentEvents), toolNames);
        Assert.DoesNotContain(nameof(BoardPlugin.CreateNote), toolNames);
        Assert.DoesNotContain(nameof(BoardPlugin.DeleteNotes), toolNames);
    }

    [Fact]
    public void CoachAndAnalystPolicies_AreReadOnly()
    {
        var analystTools = AgentToolPolicy.GetToolMethodNames(FacilitatorAgentIds.BoardAnalyst, allowDestructiveChanges: false);
        var coachTools = AgentToolPolicy.GetToolMethodNames(FacilitatorAgentIds.FacilitationCoach, allowDestructiveChanges: false);

        Assert.Equal(analystTools, coachTools);
        Assert.DoesNotContain(nameof(BoardPlugin.CreateNotes), analystTools);
        Assert.DoesNotContain(nameof(BoardPlugin.SetPhase), analystTools);
    }

    [Fact]
    public void BoardEditorPolicy_ForAutonomousRun_ExcludesDeleteNotesOnly()
    {
        var toolNames = AgentToolPolicy.GetToolMethodNames(FacilitatorAgentIds.BoardEditor, allowDestructiveChanges: false);

        Assert.DoesNotContain(nameof(BoardPlugin.DeleteNotes), toolNames);
        Assert.Contains(nameof(BoardPlugin.CreateNote), toolNames);
        Assert.Contains(nameof(BoardPlugin.CreateNotes), toolNames);
        Assert.Contains(nameof(BoardPlugin.CreateConnection), toolNames);
        Assert.Contains(nameof(BoardPlugin.CreateConnections), toolNames);
        Assert.Contains(nameof(BoardPlugin.EditNoteText), toolNames);
        Assert.Contains(nameof(BoardPlugin.SetPhase), toolNames);
        Assert.Contains(nameof(BoardPlugin.SetDomain), toolNames);
        Assert.Contains(nameof(BoardPlugin.SetSessionScope), toolNames);
        Assert.Contains(nameof(BoardPlugin.CompleteAutonomousSession), toolNames);
        Assert.Contains(nameof(BoardPlugin.MoveNotes), toolNames);
    }

    [Fact]
    public void BoardEditorPolicy_ForInteractiveRun_IncludesDeleteNotes()
    {
        var toolNames = AgentToolPolicy.GetToolMethodNames(FacilitatorAgentIds.BoardEditor, allowDestructiveChanges: true);

        Assert.Contains(nameof(BoardPlugin.DeleteNotes), toolNames);
        Assert.Contains(nameof(BoardPlugin.CreateNotes), toolNames);
        Assert.Contains(nameof(BoardPlugin.EditNoteText), toolNames);
        Assert.Contains(nameof(BoardPlugin.SetPhase), toolNames);
    }
}