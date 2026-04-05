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
        Assert.DoesNotContain(nameof(BoardPlugin.EditNoteText), tools);
        Assert.DoesNotContain(nameof(BoardPlugin.MoveNotes), tools);
        Assert.DoesNotContain(nameof(BoardPlugin.DeleteNotes), tools);
    }

    [Fact]
    public void Specialists_HaveFullBoardTools()
    {
        foreach (var name in new[] { "EventExplorer", "TriggerMapper", "DomainDesigner" })
        {
            var tools = GetToolsFor(name);
            Assert.Contains(nameof(BoardPlugin.GetBoardState), tools);
            Assert.Contains(nameof(BoardPlugin.GetRecentEvents), tools);
            Assert.Contains(nameof(BoardPlugin.CreateNote), tools);
            Assert.Contains(nameof(BoardPlugin.CreateNotes), tools);
            Assert.Contains(nameof(BoardPlugin.CreateConnection), tools);
            Assert.Contains(nameof(BoardPlugin.CreateConnections), tools);
            Assert.Contains(nameof(BoardPlugin.EditNoteText), tools);
            Assert.Contains(nameof(BoardPlugin.MoveNotes), tools);
            Assert.Contains(nameof(BoardPlugin.DeleteNotes), tools);
            Assert.Contains(nameof(DelegationPlugin.AskAgentQuestion), tools);
        }
    }

    [Fact]
    public void DomainExpert_HasNoTools()
    {
        var tools = GetToolsFor("DomainExpert");
        Assert.Empty(tools);
    }

    [Fact]
    public void Organiser_HasMoveAndConcernTools_Only()
    {
        var tools = GetToolsFor("Organiser");

        Assert.Contains(nameof(BoardPlugin.GetBoardState), tools);
        Assert.Contains(nameof(BoardPlugin.MoveNotes), tools);
        Assert.Contains(nameof(BoardPlugin.CreateNote), tools);
        Assert.Equal(3, tools.Count);

        Assert.DoesNotContain(nameof(BoardPlugin.CreateNotes), tools);
        Assert.DoesNotContain(nameof(BoardPlugin.CreateConnection), tools);
        Assert.DoesNotContain(nameof(BoardPlugin.CreateConnections), tools);
        Assert.DoesNotContain(nameof(BoardPlugin.EditNoteText), tools);
        Assert.DoesNotContain(nameof(BoardPlugin.DeleteNotes), tools);
    }
}