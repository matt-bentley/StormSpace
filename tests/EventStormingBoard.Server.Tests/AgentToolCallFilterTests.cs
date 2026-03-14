using EventStormingBoard.Server.Filters;
using EventStormingBoard.Server.Hubs;
using EventStormingBoard.Server.Models;
using Microsoft.AspNetCore.SignalR;

namespace EventStormingBoard.Server.Tests;

public class AgentToolCallFilterTests
{
    [Fact]
    public async Task CaptureToolCallAsync_KeepsExecutionsIsolatedAndBroadcastsMetadata()
    {
        var hubContext = new RecordingHubContext();
        var filter = new AgentToolCallFilter(hubContext);
        var boardId = Guid.NewGuid();
        var plannerExecution = new AgentExecutionScope(boardId, "exec-planner", FacilitatorAgentIds.Planner, "Planner Agent");
        var editorExecution = new AgentExecutionScope(boardId, "exec-editor", FacilitatorAgentIds.BoardEditor, "Board Editor");

        filter.BeginCapture(plannerExecution);
        filter.BeginCapture(editorExecution);

        await filter.CaptureToolCallAsync(plannerExecution, "GetBoardState", new Dictionary<string, string>
        {
            ["count"] = "20"
        }, CancellationToken.None);
        await filter.CaptureToolCallAsync(editorExecution, "CreateNotes", new Dictionary<string, string>
        {
            ["notes"] = "1"
        }, CancellationToken.None);

        var plannerCalls = filter.EndCapture(plannerExecution.ExecutionId);
        var editorCalls = filter.EndCapture(editorExecution.ExecutionId);

        Assert.Single(plannerCalls);
        Assert.Equal("GetBoardState", plannerCalls[0].Name);
        Assert.Single(editorCalls);
        Assert.Equal("CreateNotes", editorCalls[0].Name);

        var firstInvocation = Assert.Single(hubContext.Proxy.Invocations.Take(1));
        Assert.Equal("AgentToolCallStarted", firstInvocation.Method);
        var firstPayload = Assert.IsType<AgentToolCallStartedDto>(Assert.Single(firstInvocation.Args));
        Assert.Equal("exec-planner", firstPayload.ExecutionId);
        Assert.Equal(FacilitatorAgentIds.Planner, firstPayload.AgentId);
    }

    private sealed class RecordingHubContext : IHubContext<BoardsHub>
    {
        public RecordingHubContext()
        {
            Clients = new RecordingHubClients(Proxy);
            Groups = new NoOpGroupManager();
        }

        public RecordingClientProxy Proxy { get; } = new();

        public IHubClients Clients { get; }

        public IGroupManager Groups { get; }
    }

    private sealed class RecordingHubClients : IHubClients
    {
        private readonly IClientProxy _proxy;

        public RecordingHubClients(IClientProxy proxy)
        {
            _proxy = proxy;
        }

        public IClientProxy All => _proxy;

        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => _proxy;

        public IClientProxy Client(string connectionId) => _proxy;

        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => _proxy;

        public IClientProxy Group(string groupName) => _proxy;

        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => _proxy;

        public IClientProxy Groups(IReadOnlyList<string> groupNames) => _proxy;

        public IClientProxy User(string userId) => _proxy;

        public IClientProxy Users(IReadOnlyList<string> userIds) => _proxy;
    }

    private sealed class RecordingClientProxy : IClientProxy
    {
        public List<(string Method, object?[] Args)> Invocations { get; } = new();

        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            Invocations.Add((method, args));
            return Task.CompletedTask;
        }
    }

    private sealed class NoOpGroupManager : IGroupManager
    {
        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}