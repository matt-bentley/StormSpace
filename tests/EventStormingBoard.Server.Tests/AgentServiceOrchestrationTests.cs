using EventStormingBoard.Server.Entities;
using EventStormingBoard.Server.Models;
using EventStormingBoard.Server.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace EventStormingBoard.Server.Tests;

public class AgentServiceOrchestrationTests
{
    [Fact]
    public async Task ChatAsync_BroadcastsPlannerHandoffsAndSpecialistRepliesInOrder()
    {
        var boardId = Guid.NewGuid();
        var repository = new InMemoryBoardsRepository();
        repository.Add(new Board
        {
            Id = boardId,
            Name = "Order board"
        });

        var executor = new FakeAgentExecutor();
        executor.Enqueue(FacilitatorAgentIds.Planner, """
            {
              "visibleMessage": "I’ll inspect the board first and then make the smallest useful update.",
              "summary": "Planner chose analyst then editor.",
              "steps": [
                {
                  "agentId": "board-analyst",
                  "goal": "Review the current order flow for gaps.",
                  "handoffMessage": "Board Analyst, review the flow for missing domain events or unclear sequencing.",
                  "contextSummary": "The board is early in the flow-discovery stage."
                },
                {
                  "agentId": "board-editor",
                  "goal": "Add one missing starter event if the flow is too sparse.",
                  "handoffMessage": "Board Editor, add one concrete starter event if the board needs it.",
                  "contextSummary": "Keep the edit intentionally small."
                }
              ]
            }
            """);
        executor.Enqueue(FacilitatorAgentIds.BoardAnalyst, """
            {
              "visibleMessage": "The board needs one clearer starter event before the rest of the flow can emerge.",
              "summary": "The flow is sparse and missing an initial domain event."
            }
            """);
        executor.Enqueue(FacilitatorAgentIds.BoardEditor, """
            {
              "visibleMessage": "I added a small starter event so the team has a concrete place to continue.",
              "summary": "Added one starter event for the order flow."
            }
            """, new AgentToolCallDto
            {
                Name = "CreateNotes",
                Arguments = "count: 1"
            });

        var broadcaster = new FakeAgentChatBroadcaster();
        var service = CreateAgentService(repository, executor, broadcaster);

        await service.ChatAsync(boardId, "Help us get started.", "Mabel");

        Assert.Collection(broadcaster.AgentMessages,
            message =>
            {
                Assert.Equal(FacilitatorAgentIds.Planner, message.AgentId);
                Assert.Equal(AgentMessageKinds.Plan, message.MessageKind);
            },
            message =>
            {
                Assert.Equal(FacilitatorAgentIds.Planner, message.AgentId);
                Assert.Equal(AgentMessageKinds.Handoff, message.MessageKind);
            },
            message =>
            {
                Assert.Equal(FacilitatorAgentIds.BoardAnalyst, message.AgentId);
                Assert.Equal(AgentMessageKinds.Response, message.MessageKind);
            },
            message =>
            {
                Assert.Equal(FacilitatorAgentIds.Planner, message.AgentId);
                Assert.Equal(AgentMessageKinds.Handoff, message.MessageKind);
            },
            message =>
            {
                Assert.Equal(FacilitatorAgentIds.BoardEditor, message.AgentId);
                Assert.Equal(AgentMessageKinds.Response, message.MessageKind);
                Assert.Single(message.ToolCalls!);
            });

        Assert.Equal(
            [FacilitatorAgentIds.Planner, FacilitatorAgentIds.BoardAnalyst, FacilitatorAgentIds.BoardEditor],
            executor.Requests.Select(request => request.Definition.Id).ToArray());

        var history = service.GetHistory(boardId);
        Assert.Equal(5, history.Count);
        Assert.Equal(FacilitatorAgentIds.BoardEditor, history[^1].AgentId);
    }

    [Fact]
    public async Task RunAutonomousTurnAsync_UsesSameOrchestrationPathAndReturnsLastVisibleMessage()
    {
        var boardId = Guid.NewGuid();
        var repository = new InMemoryBoardsRepository();
        repository.Add(new Board
        {
            Id = boardId,
            Name = "Claims board",
            AutonomousEnabled = true,
            Phase = EventStormingPhase.IdentifyEvents
        });

        var executor = new FakeAgentExecutor();
        executor.Enqueue(FacilitatorAgentIds.Planner, """
            {
              "visibleMessage": "I’ll coach the group on the next event to add.",
              "summary": "Planner chose the coach.",
              "steps": [
                {
                  "agentId": "facilitation-coach",
                  "goal": "Explain how to add the next event in past tense.",
                  "handoffMessage": "Facilitation Coach, guide the team through one small next step.",
                  "contextSummary": "Keep it short and participant-facing."
                }
              ]
            }
            """);
        executor.Enqueue(FacilitatorAgentIds.FacilitationCoach, """
            {
              "visibleMessage": "Add one more event in past tense and place it to the right of the latest event if it happens later.",
              "summary": "Coached the team to add the next event in sequence."
            }
            """);

        var broadcaster = new FakeAgentChatBroadcaster();
        var service = CreateAgentService(repository, executor, broadcaster);

        var result = await service.RunAutonomousTurnAsync(boardId, "timer", CancellationToken.None);

        Assert.Equal(AutonomousAgentStatus.Acted, result.Status);
        Assert.Equal("timer", result.TriggerReason);
        Assert.Equal("Add one more event in past tense and place it to the right of the latest event if it happens later.", result.VisibleMessage);
        Assert.Equal(3, broadcaster.AgentMessages.Count);
        Assert.Equal(FacilitatorAgentIds.FacilitationCoach, broadcaster.AgentMessages[^1].AgentId);
    }

    private static AgentService CreateAgentService(
        InMemoryBoardsRepository repository,
        FakeAgentExecutor executor,
        FakeAgentChatBroadcaster broadcaster)
    {
        var services = new ServiceCollection();
        services.AddSingleton<EventStormingBoard.Server.Repositories.IBoardsRepository>(repository);
        var serviceProvider = services.BuildServiceProvider();

        return new AgentService(serviceProvider, executor, broadcaster);
    }

    private sealed class FakeAgentExecutor : IAgentExecutor
    {
        private readonly Dictionary<string, Queue<FakeAgentResponse>> _responses = new(StringComparer.OrdinalIgnoreCase);

        public List<AgentExecutionRequest> Requests { get; } = new();

        public void Enqueue(string agentId, string text, params AgentToolCallDto[] toolCalls)
        {
            if (!_responses.TryGetValue(agentId, out var queue))
            {
                queue = new Queue<FakeAgentResponse>();
                _responses[agentId] = queue;
            }

            queue.Enqueue(new FakeAgentResponse(text, toolCalls.ToList()));
        }

        public Task<AgentExecutionResult> ExecuteAsync(AgentExecutionRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            var queue = _responses[request.Definition.Id];
            var response = queue.Dequeue();

            return Task.FromResult(new AgentExecutionResult
            {
                Scope = request.Scope,
                Text = response.Text,
                ToolCalls = response.ToolCalls
            });
        }

        private sealed record FakeAgentResponse(string Text, List<AgentToolCallDto> ToolCalls);
    }

    private sealed class FakeAgentChatBroadcaster : IAgentChatBroadcaster
    {
        private readonly List<AgentChatMessageDto> _history = new();

        public List<AgentChatMessageDto> AgentMessages => _history.Where(message => message.Role == "assistant").ToList();

        public Task BroadcastUserMessageAsync(Guid boardId, string userName, string content, CancellationToken cancellationToken = default)
        {
            _history.Add(new AgentChatMessageDto
            {
                Role = "user",
                UserName = userName,
                Content = content,
                MessageKind = AgentMessageKinds.User,
                Timestamp = DateTime.UtcNow
            });

            return Task.CompletedTask;
        }

        public Task BroadcastAgentMessageAsync(Guid boardId, AgentChatMessageDto message, CancellationToken cancellationToken = default)
        {
            _history.Add(message);
            return Task.CompletedTask;
        }

        public List<AgentChatMessageDto> GetHistory(Guid boardId) => new(_history);

        public void ClearHistory(Guid boardId) => _history.Clear();
    }
}