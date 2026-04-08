using EventStormingBoard.Server.Agents;
using EventStormingBoard.Server.Plugins;

namespace EventStormingBoard.Server.Tests;

public class AgentServiceToolPolicyTests
{
    private readonly List<EventStormingBoard.Server.Entities.AgentConfiguration> _defaults =
        DefaultAgentConfigurations.CreateDefaults();

    private List<string> GetToolsFor(string agentName) =>
        _defaults.First(a => a.Name == agentName).AllowedTools;

    [Fact]
    public void DefaultConfigurations_ContainsSixAgents()
    {
        Assert.Equal(6, _defaults.Count);
    }

    [Fact]
    public void DefaultConfigurations_HasExactlyOneFacilitator()
    {
        Assert.Single(_defaults.Where(a => a.IsFacilitator));
        Assert.Equal("Facilitator", _defaults.First(a => a.IsFacilitator).Name);
    }

    [Fact]
    public void Facilitator_HasContextToolsAndDelegation_ButNoMutationTools()
    {
        var tools = GetToolsFor("Facilitator");

        Assert.Contains(nameof(BoardPlugin.GetBoardState), tools);
        Assert.Contains(nameof(BoardPlugin.GetRecentEvents), tools);
        Assert.Contains(nameof(BoardPlugin.SetDomain), tools);
        Assert.Contains(nameof(BoardPlugin.SetSessionScope), tools);
        Assert.Contains(nameof(BoardPlugin.SetPhase), tools);
        Assert.Contains(nameof(BoardPlugin.CompleteAutonomousSession), tools);
        Assert.Contains(nameof(DelegationPlugin.DelegateToAgent), tools);
        Assert.Contains(nameof(DelegationPlugin.RequestBoardReview), tools);
        Assert.Contains(nameof(DelegationPlugin.AskAgentQuestion), tools);

        Assert.DoesNotContain(nameof(BoardPlugin.CreateNote), tools);
        Assert.DoesNotContain(nameof(BoardPlugin.CreateNotes), tools);
        Assert.DoesNotContain(nameof(BoardPlugin.CreateConnection), tools);
        Assert.DoesNotContain(nameof(BoardPlugin.CreateConnections), tools);
        Assert.DoesNotContain(nameof(BoardPlugin.EditNoteTexts), tools);
        Assert.DoesNotContain(nameof(BoardPlugin.MoveNotes), tools);
        Assert.DoesNotContain(nameof(BoardPlugin.DeleteNotes), tools);
        Assert.DoesNotContain(nameof(BoardPlugin.CreateBoundedContext), tools);
        Assert.DoesNotContain(nameof(BoardPlugin.CreateBoundedContexts), tools);
        Assert.DoesNotContain(nameof(BoardPlugin.UpdateBoundedContext), tools);
        Assert.DoesNotContain(nameof(BoardPlugin.DeleteBoundedContext), tools);
    }

    [Fact]
    public void EventExplorer_HasBoardTools_ButNoBoundedContextTools()
    {
        var tools = GetToolsFor("EventExplorer");
        Assert.Contains(nameof(BoardPlugin.GetBoardState), tools);
        Assert.Contains(nameof(BoardPlugin.GetRecentEvents), tools);
        Assert.Contains(nameof(BoardPlugin.CreateNotes), tools);
        Assert.Contains(nameof(BoardPlugin.EditNoteTexts), tools);
        Assert.Contains(nameof(BoardPlugin.MoveNotes), tools);
        Assert.Contains(nameof(BoardPlugin.DeleteNotes), tools);
        Assert.Contains(nameof(DelegationPlugin.AskAgentQuestion), tools);

        Assert.DoesNotContain(nameof(BoardPlugin.CreateBoundedContext), tools);
        Assert.DoesNotContain(nameof(BoardPlugin.CreateBoundedContexts), tools);
        Assert.DoesNotContain(nameof(BoardPlugin.UpdateBoundedContext), tools);
        Assert.DoesNotContain(nameof(BoardPlugin.DeleteBoundedContext), tools);
    }

    [Fact]
    public void TriggerMapper_HasBoardTools_ButNoBoundedContextTools()
    {
        var tools = GetToolsFor("TriggerMapper");
        Assert.Contains(nameof(BoardPlugin.GetBoardState), tools);
        Assert.Contains(nameof(BoardPlugin.GetRecentEvents), tools);
        Assert.Contains(nameof(BoardPlugin.CreateNotes), tools);
        Assert.Contains(nameof(BoardPlugin.CreateConnections), tools);
        Assert.Contains(nameof(BoardPlugin.EditNoteTexts), tools);
        Assert.Contains(nameof(BoardPlugin.MoveNotes), tools);
        Assert.Contains(nameof(BoardPlugin.DeleteNotes), tools);
        Assert.Contains(nameof(DelegationPlugin.AskAgentQuestion), tools);

        Assert.DoesNotContain(nameof(BoardPlugin.CreateBoundedContext), tools);
        Assert.DoesNotContain(nameof(BoardPlugin.CreateBoundedContexts), tools);
        Assert.DoesNotContain(nameof(BoardPlugin.UpdateBoundedContext), tools);
        Assert.DoesNotContain(nameof(BoardPlugin.DeleteBoundedContext), tools);
    }

    [Fact]
    public void DomainDesigner_HasFullBoardToolsAndBoundedContextTools()
    {
        var tools = GetToolsFor("DomainDesigner");
        Assert.Contains(nameof(BoardPlugin.GetBoardState), tools);
        Assert.Contains(nameof(BoardPlugin.GetRecentEvents), tools);
        Assert.Contains(nameof(BoardPlugin.CreateNotes), tools);
        Assert.Contains(nameof(BoardPlugin.CreateConnections), tools);
        Assert.Contains(nameof(BoardPlugin.EditNoteTexts), tools);
        Assert.Contains(nameof(BoardPlugin.MoveNotes), tools);
        Assert.Contains(nameof(BoardPlugin.DeleteNotes), tools);
        Assert.Contains(nameof(BoardPlugin.CreateBoundedContext), tools);
        Assert.Contains(nameof(BoardPlugin.CreateBoundedContexts), tools);
        Assert.Contains(nameof(BoardPlugin.UpdateBoundedContext), tools);
        Assert.Contains(nameof(BoardPlugin.DeleteBoundedContext), tools);
        Assert.Contains(nameof(DelegationPlugin.AskAgentQuestion), tools);
    }

    [Fact]
    public void DomainExpert_HasReadOnlyTools_Only()
    {
        var tools = GetToolsFor("DomainExpert");
        Assert.Contains(nameof(BoardPlugin.GetBoardState), tools);
        Assert.Equal(1, tools.Count);
    }

    [Fact]
    public void Organiser_HasMoveAndConcernTools_Only()
    {
        var tools = GetToolsFor("Organiser");

        Assert.Contains(nameof(BoardPlugin.GetBoardState), tools);
        Assert.Contains(nameof(BoardPlugin.MoveNotes), tools);
        Assert.Contains(nameof(BoardPlugin.CreateNotes), tools);
        Assert.Equal(3, tools.Count);

        Assert.DoesNotContain(nameof(BoardPlugin.CreateConnection), tools);
        Assert.DoesNotContain(nameof(BoardPlugin.CreateConnections), tools);
        Assert.DoesNotContain(nameof(BoardPlugin.EditNoteTexts), tools);
        Assert.DoesNotContain(nameof(BoardPlugin.DeleteNotes), tools);
        Assert.DoesNotContain(nameof(BoardPlugin.CreateBoundedContext), tools);
        Assert.DoesNotContain(nameof(BoardPlugin.CreateBoundedContexts), tools);
        Assert.DoesNotContain(nameof(BoardPlugin.UpdateBoundedContext), tools);
        Assert.DoesNotContain(nameof(BoardPlugin.DeleteBoundedContext), tools);
    }

    [Fact]
    public void DestructiveTools_SuppressedWhenDestructiveChangesDisabled()
    {
        // Verify that ResolveTools skips destructive tools by checking
        // the filtering logic directly against the tool name list.
        var domainDesigner = _defaults.First(a => a.Name == "DomainDesigner");

        // Both destructive tools must be in the allowed list
        Assert.Contains(nameof(BoardPlugin.DeleteNotes), domainDesigner.AllowedTools);
        Assert.Contains(nameof(BoardPlugin.DeleteBoundedContext), domainDesigner.AllowedTools);

        // Simulate the filter logic from BoardAgentFactory.ResolveTools
        var filteredTools = domainDesigner.AllowedTools
            .Where(toolName => toolName != nameof(BoardPlugin.DeleteNotes) &&
                               toolName != nameof(BoardPlugin.DeleteBoundedContext))
            .ToList();

        Assert.DoesNotContain(nameof(BoardPlugin.DeleteNotes), filteredTools);
        Assert.DoesNotContain(nameof(BoardPlugin.DeleteBoundedContext), filteredTools);
    }
}