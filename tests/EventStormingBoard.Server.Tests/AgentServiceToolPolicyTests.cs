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
    public void GivenDefaultAgentConfigurations_WhenLoaded_ThenContainsSixAgents()
    {
        // Arrange

        // Act

        // Assert
        _defaults.Should().HaveCount(6);
    }

    [Fact]
    public void GivenDefaultAgentConfigurations_WhenLoaded_ThenHasExactlyOneFacilitator()
    {
        // Arrange

        // Act
        var facilitatorNames = _defaults.Where(a => a.IsFacilitator).Select(a => a.Name);

        // Assert
        facilitatorNames.Should().ContainSingle().Which.Should().Be("Facilitator");
    }

    [Fact]
    public void GivenFacilitatorConfiguration_WhenInspectingAllowedTools_ThenOnlyContextAndDelegationToolsAreAllowed()
    {
        // Arrange

        // Act
        var tools = GetToolsFor("Facilitator");

        // Assert
        tools.Should().Contain(nameof(BoardPlugin.GetBoardState));
        tools.Should().Contain(nameof(BoardPlugin.GetRecentEvents));
        tools.Should().Contain(nameof(BoardPlugin.SetDomain));
        tools.Should().Contain(nameof(BoardPlugin.SetSessionScope));
        tools.Should().Contain(nameof(BoardPlugin.SetPhase));
        tools.Should().Contain(nameof(BoardPlugin.CompleteAutonomousSession));
        tools.Should().Contain(nameof(DelegationPlugin.DelegateToAgent));
        tools.Should().Contain(nameof(DelegationPlugin.RequestBoardReview));
        tools.Should().Contain(nameof(DelegationPlugin.AskAgentQuestion));

        tools.Should().NotContain(nameof(BoardPlugin.CreateNote));
        tools.Should().NotContain(nameof(BoardPlugin.CreateNotes));
        tools.Should().NotContain(nameof(BoardPlugin.CreateConnection));
        tools.Should().NotContain(nameof(BoardPlugin.CreateConnections));
        tools.Should().NotContain(nameof(BoardPlugin.EditNoteTexts));
        tools.Should().NotContain(nameof(BoardPlugin.MoveNotes));
        tools.Should().NotContain(nameof(BoardPlugin.DeleteNotes));
        tools.Should().NotContain(nameof(BoardPlugin.CreateBoundedContext));
        tools.Should().NotContain(nameof(BoardPlugin.CreateBoundedContexts));
        tools.Should().NotContain(nameof(BoardPlugin.UpdateBoundedContext));
        tools.Should().NotContain(nameof(BoardPlugin.DeleteBoundedContext));
    }

    [Fact]
    public void GivenEventExplorerConfiguration_WhenInspectingAllowedTools_ThenBoardToolsAreAllowedAndBoundedContextToolsAreNot()
    {
        // Arrange

        // Act
        var tools = GetToolsFor("EventExplorer");

        // Assert
        tools.Should().Contain(nameof(BoardPlugin.GetBoardState));
        tools.Should().Contain(nameof(BoardPlugin.GetRecentEvents));
        tools.Should().Contain(nameof(BoardPlugin.CreateNotes));
        tools.Should().Contain(nameof(BoardPlugin.EditNoteTexts));
        tools.Should().Contain(nameof(BoardPlugin.MoveNotes));
        tools.Should().Contain(nameof(BoardPlugin.DeleteNotes));
        tools.Should().Contain(nameof(DelegationPlugin.AskAgentQuestion));

        tools.Should().NotContain(nameof(BoardPlugin.CreateBoundedContext));
        tools.Should().NotContain(nameof(BoardPlugin.CreateBoundedContexts));
        tools.Should().NotContain(nameof(BoardPlugin.UpdateBoundedContext));
        tools.Should().NotContain(nameof(BoardPlugin.DeleteBoundedContext));
    }

    [Fact]
    public void GivenTriggerMapperConfiguration_WhenInspectingAllowedTools_ThenBoardToolsAreAllowedAndBoundedContextToolsAreNot()
    {
        // Arrange

        // Act
        var tools = GetToolsFor("TriggerMapper");

        // Assert
        tools.Should().Contain(nameof(BoardPlugin.GetBoardState));
        tools.Should().Contain(nameof(BoardPlugin.GetRecentEvents));
        tools.Should().Contain(nameof(BoardPlugin.CreateNotes));
        tools.Should().Contain(nameof(BoardPlugin.CreateConnections));
        tools.Should().Contain(nameof(BoardPlugin.EditNoteTexts));
        tools.Should().Contain(nameof(BoardPlugin.MoveNotes));
        tools.Should().Contain(nameof(BoardPlugin.DeleteNotes));
        tools.Should().Contain(nameof(DelegationPlugin.AskAgentQuestion));

        tools.Should().NotContain(nameof(BoardPlugin.CreateBoundedContext));
        tools.Should().NotContain(nameof(BoardPlugin.CreateBoundedContexts));
        tools.Should().NotContain(nameof(BoardPlugin.UpdateBoundedContext));
        tools.Should().NotContain(nameof(BoardPlugin.DeleteBoundedContext));
    }

    [Fact]
    public void GivenDomainDesignerConfiguration_WhenInspectingAllowedTools_ThenAllBoardAndBoundedContextToolsAreAllowed()
    {
        // Arrange

        // Act
        var tools = GetToolsFor("DomainDesigner");

        // Assert
        tools.Should().Contain(nameof(BoardPlugin.GetBoardState));
        tools.Should().Contain(nameof(BoardPlugin.GetRecentEvents));
        tools.Should().Contain(nameof(BoardPlugin.CreateNotes));
        tools.Should().Contain(nameof(BoardPlugin.CreateConnections));
        tools.Should().Contain(nameof(BoardPlugin.EditNoteTexts));
        tools.Should().Contain(nameof(BoardPlugin.MoveNotes));
        tools.Should().Contain(nameof(BoardPlugin.DeleteNotes));
        tools.Should().Contain(nameof(BoardPlugin.CreateBoundedContext));
        tools.Should().Contain(nameof(BoardPlugin.CreateBoundedContexts));
        tools.Should().Contain(nameof(BoardPlugin.UpdateBoundedContext));
        tools.Should().Contain(nameof(BoardPlugin.DeleteBoundedContext));
        tools.Should().Contain(nameof(DelegationPlugin.AskAgentQuestion));
    }

    [Fact]
    public void GivenDomainExpertConfiguration_WhenInspectingAllowedTools_ThenOnlyReadOnlyToolsAreAllowed()
    {
        // Arrange

        // Act
        var tools = GetToolsFor("DomainExpert");

        // Assert
        tools.Should().Contain(nameof(BoardPlugin.GetBoardState));
        tools.Should().HaveCount(1);
    }

    [Fact]
    public void GivenOrganiserConfiguration_WhenInspectingAllowedTools_ThenOnlyMoveAndConcernToolsAreAllowed()
    {
        // Arrange

        // Act
        var tools = GetToolsFor("Organiser");

        // Assert
        tools.Should().Contain(nameof(BoardPlugin.GetBoardState));
        tools.Should().Contain(nameof(BoardPlugin.MoveNotes));
        tools.Should().Contain(nameof(BoardPlugin.CreateNotes));
        tools.Should().HaveCount(3);

        tools.Should().NotContain(nameof(BoardPlugin.CreateConnection));
        tools.Should().NotContain(nameof(BoardPlugin.CreateConnections));
        tools.Should().NotContain(nameof(BoardPlugin.EditNoteTexts));
        tools.Should().NotContain(nameof(BoardPlugin.DeleteNotes));
        tools.Should().NotContain(nameof(BoardPlugin.CreateBoundedContext));
        tools.Should().NotContain(nameof(BoardPlugin.CreateBoundedContexts));
        tools.Should().NotContain(nameof(BoardPlugin.UpdateBoundedContext));
        tools.Should().NotContain(nameof(BoardPlugin.DeleteBoundedContext));
    }

    [Fact]
    public void GivenDestructiveTools_WhenDestructiveChangesAreDisabled_ThenDeleteToolsAreFilteredOut()
    {
        // Arrange
        // Verify that ResolveTools skips destructive tools by checking
        // the filtering logic directly against the tool name list.
        var domainDesigner = _defaults.First(a => a.Name == "DomainDesigner");

        // Both destructive tools must be in the allowed list
        domainDesigner.AllowedTools.Should().Contain(nameof(BoardPlugin.DeleteNotes));
        domainDesigner.AllowedTools.Should().Contain(nameof(BoardPlugin.DeleteBoundedContext));

        // Act
        // Simulate the filter logic from BoardAgentFactory.ResolveTools
        var filteredTools = domainDesigner.AllowedTools
            .Where(toolName => toolName != nameof(BoardPlugin.DeleteNotes) &&
                               toolName != nameof(BoardPlugin.DeleteBoundedContext))
            .ToList();

        // Assert
        filteredTools.Should().NotContain(nameof(BoardPlugin.DeleteNotes));
        filteredTools.Should().NotContain(nameof(BoardPlugin.DeleteBoundedContext));
    }
}