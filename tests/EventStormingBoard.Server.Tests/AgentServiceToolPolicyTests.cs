using EventStormingBoard.Server.Models;
using EventStormingBoard.Server.Plugins;
using EventStormingBoard.Server.Services;

namespace EventStormingBoard.Server.Tests;

public class AgentServiceToolPolicyTests
{
    // --- Facilitator tool policy ---

    [Fact]
    public void Facilitator_HasBoardStateAndContextTools()
    {
        var toolNames = AgentService.GetToolMethodNames(AgentType.Facilitator, allowDestructiveChanges: true);

        Assert.Contains(nameof(BoardPlugin.GetBoardState), toolNames);
        Assert.Contains(nameof(BoardPlugin.GetRecentEvents), toolNames);
        Assert.Contains(nameof(BoardPlugin.SetDomain), toolNames);
        Assert.Contains(nameof(BoardPlugin.SetSessionScope), toolNames);
        Assert.Contains(nameof(BoardPlugin.SetPhase), toolNames);
        Assert.Contains(nameof(BoardPlugin.CompleteAutonomousSession), toolNames);
    }

    [Fact]
    public void Facilitator_DoesNotHaveBoardMutationTools()
    {
        var toolNames = AgentService.GetToolMethodNames(AgentType.Facilitator, allowDestructiveChanges: true);

        Assert.DoesNotContain(nameof(BoardPlugin.CreateNote), toolNames);
        Assert.DoesNotContain(nameof(BoardPlugin.CreateNotes), toolNames);
        Assert.DoesNotContain(nameof(BoardPlugin.CreateConnection), toolNames);
        Assert.DoesNotContain(nameof(BoardPlugin.CreateConnections), toolNames);
        Assert.DoesNotContain(nameof(BoardPlugin.EditNoteText), toolNames);
        Assert.DoesNotContain(nameof(BoardPlugin.MoveNotes), toolNames);
        Assert.DoesNotContain(nameof(BoardPlugin.DeleteNotes), toolNames);
    }

    // --- Board Builder tool policy ---

    [Fact]
    public void BoardBuilder_HasBoardMutationTools()
    {
        var toolNames = AgentService.GetToolMethodNames(AgentType.BoardBuilder, allowDestructiveChanges: true);

        Assert.Contains(nameof(BoardPlugin.GetBoardState), toolNames);
        Assert.Contains(nameof(BoardPlugin.CreateNote), toolNames);
        Assert.Contains(nameof(BoardPlugin.CreateNotes), toolNames);
        Assert.Contains(nameof(BoardPlugin.CreateConnection), toolNames);
        Assert.Contains(nameof(BoardPlugin.CreateConnections), toolNames);
        Assert.Contains(nameof(BoardPlugin.EditNoteText), toolNames);
        Assert.Contains(nameof(BoardPlugin.MoveNotes), toolNames);
    }

    [Fact]
    public void BoardBuilder_Interactive_IncludesDeleteNotes()
    {
        var toolNames = AgentService.GetToolMethodNames(AgentType.BoardBuilder, allowDestructiveChanges: true);

        Assert.Contains(nameof(BoardPlugin.DeleteNotes), toolNames);
    }

    [Fact]
    public void BoardBuilder_Autonomous_ExcludesDeleteNotes()
    {
        var toolNames = AgentService.GetToolMethodNames(AgentType.BoardBuilder, allowDestructiveChanges: false);

        Assert.DoesNotContain(nameof(BoardPlugin.DeleteNotes), toolNames);
    }

    [Fact]
    public void BoardBuilder_DoesNotHaveContextOrDelegationTools()
    {
        var toolNames = AgentService.GetToolMethodNames(AgentType.BoardBuilder, allowDestructiveChanges: true);

        Assert.DoesNotContain(nameof(BoardPlugin.SetDomain), toolNames);
        Assert.DoesNotContain(nameof(BoardPlugin.SetSessionScope), toolNames);
        Assert.DoesNotContain(nameof(BoardPlugin.SetPhase), toolNames);
        Assert.DoesNotContain(nameof(BoardPlugin.CompleteAutonomousSession), toolNames);
        Assert.DoesNotContain(nameof(BoardPlugin.GetRecentEvents), toolNames);
    }

    // --- Board Reviewer tool policy ---

    [Fact]
    public void BoardReviewer_HasReadAndConcernTools()
    {
        var toolNames = AgentService.GetToolMethodNames(AgentType.BoardReviewer, allowDestructiveChanges: true);

        Assert.Contains(nameof(BoardPlugin.GetBoardState), toolNames);
        Assert.Contains(nameof(BoardPlugin.GetRecentEvents), toolNames);
        Assert.Contains(nameof(BoardPlugin.CreateNote), toolNames);
        Assert.Contains(nameof(BoardPlugin.CreateNotes), toolNames);
    }

    [Fact]
    public void BoardReviewer_DoesNotHaveMutationOrContextTools()
    {
        var toolNames = AgentService.GetToolMethodNames(AgentType.BoardReviewer, allowDestructiveChanges: true);

        Assert.DoesNotContain(nameof(BoardPlugin.CreateConnection), toolNames);
        Assert.DoesNotContain(nameof(BoardPlugin.CreateConnections), toolNames);
        Assert.DoesNotContain(nameof(BoardPlugin.EditNoteText), toolNames);
        Assert.DoesNotContain(nameof(BoardPlugin.MoveNotes), toolNames);
        Assert.DoesNotContain(nameof(BoardPlugin.DeleteNotes), toolNames);
        Assert.DoesNotContain(nameof(BoardPlugin.SetDomain), toolNames);
        Assert.DoesNotContain(nameof(BoardPlugin.SetPhase), toolNames);
    }
}