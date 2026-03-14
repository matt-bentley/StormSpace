using EventStormingBoard.Server.Plugins;
using EventStormingBoard.Server.Events;
using EventStormingBoard.Server.Services;
using System.Reflection;

namespace EventStormingBoard.Server.Tests;

public class AgentServiceToolPolicyTests
{
    [Fact]
    public void GetToolMethodNames_ForAutonomousRun_ExcludesDeleteNotesOnly()
    {
        var toolNames = GetToolMethodNames(allowDestructiveChanges: false);

        Assert.DoesNotContain(nameof(BoardPlugin.DeleteNotes), toolNames);
        Assert.Contains(nameof(BoardPlugin.CreateNote), toolNames);
        Assert.Contains(nameof(BoardPlugin.CreateNotes), toolNames);
        Assert.Contains(nameof(BoardPlugin.CreateConnection), toolNames);
        Assert.Contains(nameof(BoardPlugin.CreateConnections), toolNames);
        Assert.Contains(nameof(BoardPlugin.EditNoteText), toolNames);
        Assert.Contains(nameof(BoardPlugin.SetPhase), toolNames);
        Assert.Contains(nameof(BoardPlugin.MoveNotes), toolNames);
    }

    [Fact]
    public void GetToolMethodNames_ForInteractiveRun_IncludesDeleteNotes()
    {
        var toolNames = GetToolMethodNames(allowDestructiveChanges: true);

        Assert.Contains(nameof(BoardPlugin.DeleteNotes), toolNames);
        Assert.Contains(nameof(BoardPlugin.CreateNotes), toolNames);
        Assert.Contains(nameof(BoardPlugin.EditNoteText), toolNames);
        Assert.Contains(nameof(BoardPlugin.SetPhase), toolNames);
    }

    private static IReadOnlyList<string> GetToolMethodNames(bool allowDestructiveChanges)
    {
        var method = typeof(AgentService).GetMethod("GetToolMethodNames", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var result = method!.Invoke(null, [allowDestructiveChanges]);

        return Assert.IsAssignableFrom<IReadOnlyList<string>>(result);
    }
}