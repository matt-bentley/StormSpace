using EventStormingBoard.Server.Hubs;
using EventStormingBoard.Server.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.SemanticKernel;
using System.Collections.Concurrent;

namespace EventStormingBoard.Server.Filters
{
    public sealed class AgentToolCallFilter : IAutoFunctionInvocationFilter
    {
        private readonly IHubContext<BoardsHub> _hubContext;
        private readonly ConcurrentDictionary<string, ConcurrentBag<AgentToolCallDto>> _pendingToolCalls = new();

        public AgentToolCallFilter(IHubContext<BoardsHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public void BeginCapture(string boardId)
        {
            _pendingToolCalls[boardId] = new ConcurrentBag<AgentToolCallDto>();
        }

        public List<AgentToolCallDto> EndCapture(string boardId)
        {
            if (_pendingToolCalls.TryRemove(boardId, out var bag))
            {
                return bag.Reverse().ToList();
            }
            return new List<AgentToolCallDto>();
        }

        public async Task OnAutoFunctionInvocationAsync(AutoFunctionInvocationContext context, Func<AutoFunctionInvocationContext, Task> next)
        {
            var boardId = context.Kernel.Data.TryGetValue("boardId", out var id) ? id?.ToString() : null;
            var functionName = context.Function.Name;

            var arguments = new Dictionary<string, string>();
            if (context.Arguments is not null)
            {
                foreach (var arg in context.Arguments)
                {
                    if (arg.Key != "boardId")
                    {
                        arguments[arg.Key] = arg.Value?.ToString() ?? "";
                    }
                }
            }

            var argsString = arguments.Count > 0
                ? string.Join(", ", arguments.Select(a => $"{a.Key}: {a.Value}"))
                : "";

            if (boardId is not null)
            {
                _pendingToolCalls.GetOrAdd(boardId, _ => new ConcurrentBag<AgentToolCallDto>())
                    .Add(new AgentToolCallDto { Name = functionName, Arguments = argsString });

                await _hubContext.Clients.Group(boardId).SendAsync("AgentToolCallStarted", new
                {
                    BoardId = boardId,
                    ToolName = functionName,
                    Arguments = arguments
                });
            }

            await next(context);
        }
    }
}
