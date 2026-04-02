using EventStormingBoard.Server.Agents;
using EventStormingBoard.Server.Plugins;

namespace EventStormingBoard.Server.Tests;

public class AgentServiceToolPolicyTests
{
    [Fact]
    public void LeadFacilitator_HasContextToolsAndDelegation_ButNoMutationTools()
    {
        var tools = BoardAgentFactory.GetFacilitatorToolNames();

        Assert.Contains(nameof(BoardPlugin.GetBoardState), tools);
        Assert.Contains(nameof(BoardPlugin.GetRecentEvents), tools);
        Assert.Contains(nameof(BoardPlugin.SetDomain), tools);
        Assert.Contains(nameof(BoardPlugin.SetSessionScope), tools);
        Assert.Contains(nameof(BoardPlugin.SetPhase), tools);
        Assert.Contains(nameof(BoardPlugin.CompleteAutonomousSession), tools);
        Assert.Contains(nameof(DelegationPlugin.RequestSpecialistProposal), tools);
        Assert.Contains(nameof(DelegationPlugin.RequestBoardReview), tools);
        Assert.Contains(nameof(DelegationPlugin.RequestBoardOrganisation), tools);

        Assert.DoesNotContain(nameof(BoardPlugin.CreateNote), tools);
        Assert.DoesNotContain(nameof(BoardPlugin.CreateNotes), tools);
        Assert.DoesNotContain(nameof(BoardPlugin.CreateConnection), tools);
        Assert.DoesNotContain(nameof(BoardPlugin.CreateConnections), tools);
        Assert.DoesNotContain(nameof(BoardPlugin.EditNoteText), tools);
        Assert.DoesNotContain(nameof(BoardPlugin.MoveNotes), tools);
        Assert.DoesNotContain(nameof(BoardPlugin.DeleteNotes), tools);
    }

    [Fact]
    public void Specialists_HaveReadOnlyTools()
    {
        var tools = BoardAgentFactory.GetReadOnlyToolNames();

        Assert.Contains(nameof(BoardPlugin.GetBoardState), tools);
        Assert.Contains(nameof(BoardPlugin.GetRecentEvents), tools);
        Assert.Equal(2, tools.Count);
    }

    [Fact]
    public void WallScribe_HasMutationTools_ExcludesDeleteInAutonomous()
    {
        var tools = BoardAgentFactory.GetWallScribeToolNames(allowDestructiveChanges: false);

        Assert.Contains(nameof(BoardPlugin.GetBoardState), tools);
        Assert.Contains(nameof(BoardPlugin.CreateNote), tools);
        Assert.Contains(nameof(BoardPlugin.CreateNotes), tools);
        Assert.Contains(nameof(BoardPlugin.CreateConnection), tools);
        Assert.Contains(nameof(BoardPlugin.CreateConnections), tools);
        Assert.Contains(nameof(BoardPlugin.EditNoteText), tools);
        Assert.Contains(nameof(BoardPlugin.MoveNotes), tools);
        Assert.DoesNotContain(nameof(BoardPlugin.DeleteNotes), tools);
    }

    [Fact]
    public void WallScribe_IncludesDelete_InInteractiveMode()
    {
        var tools = BoardAgentFactory.GetWallScribeToolNames(allowDestructiveChanges: true);

        Assert.Contains(nameof(BoardPlugin.DeleteNotes), tools);
        Assert.Contains(nameof(BoardPlugin.CreateNotes), tools);
        Assert.Contains(nameof(BoardPlugin.EditNoteText), tools);
        Assert.Contains(nameof(BoardPlugin.MoveNotes), tools);
    }

    [Fact]
    public void Organiser_HasMoveAndConcernTools_Only()
    {
        var tools = BoardAgentFactory.GetOrganiserToolNames();

        Assert.Contains(nameof(BoardPlugin.GetBoardState), tools);
        Assert.Contains(nameof(BoardPlugin.MoveNotes), tools);
        Assert.Contains(nameof(BoardPlugin.CreateNote), tools);
        Assert.Equal(3, tools.Count);

        // Must NOT have destructive or creative tools beyond Concern notes
        Assert.DoesNotContain(nameof(BoardPlugin.CreateNotes), tools);
        Assert.DoesNotContain(nameof(BoardPlugin.CreateConnection), tools);
        Assert.DoesNotContain(nameof(BoardPlugin.CreateConnections), tools);
        Assert.DoesNotContain(nameof(BoardPlugin.EditNoteText), tools);
        Assert.DoesNotContain(nameof(BoardPlugin.DeleteNotes), tools);
    }
}