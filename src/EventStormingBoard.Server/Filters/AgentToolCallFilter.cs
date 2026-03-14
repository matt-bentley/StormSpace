using EventStormingBoard.Server.Hubs;
using EventStormingBoard.Server.Models;
using Microsoft.Agents.AI;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.AI;
using System.Collections.Concurrent;

namespace EventStormingBoard.Server.Filters
{
    public sealed class AgentToolCallFilter
    {
        private readonly IHubContext<BoardsHub> _hubContext;
        private readonly ConcurrentDictionary<string, ConcurrentBag<AgentToolCallDto>> _pendingToolCalls = new();

        public AgentToolCallFilter(IHubContext<BoardsHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public void BeginCapture(string captureKey)
        {
            _pendingToolCalls[captureKey] = new ConcurrentBag<AgentToolCallDto>();
        }

        public List<AgentToolCallDto> EndCapture(string captureKey)
        {
            if (_pendingToolCalls.TryRemove(captureKey, out var bag))
            {
                return bag.Reverse().ToList();
            }
            return new List<AgentToolCallDto>();
        }

        public async ValueTask<object?> OnFunctionInvocationAsync(
            Guid boardId,
            AgentType agentType,
            AIAgent agent,
            FunctionInvocationContext context,
            Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next,
            CancellationToken cancellationToken)
        {
            var functionName = context.Function.Name;
            var identity = AgentIdentity.ForType(agentType);

            var arguments = new Dictionary<string, string>();
            if (context.CallContent.Arguments is not null)
            {
                foreach (var arg in context.CallContent.Arguments)
                {
                    arguments[arg.Key] = arg.Value?.ToString() ?? string.Empty;
                }
            }

            var argsString = arguments.Count > 0
                ? string.Join(", ", arguments.Select(a => $"{a.Key}: {a.Value}"))
                : string.Empty;

            var captureKey = $"{boardId}:{agentType}";
            _pendingToolCalls.GetOrAdd(captureKey, _ => new ConcurrentBag<AgentToolCallDto>())
                .Add(new AgentToolCallDto { Name = functionName, Arguments = argsString });

            var boardKey = boardId.ToString();
            await _hubContext.Clients.Group(boardKey).SendAsync("AgentToolCallStarted", new
            {
                BoardId = boardKey,
                AgentName = identity.Name,
                ToolName = functionName,
                Arguments = arguments
            }, cancellationToken);

            return await next(context, cancellationToken).ConfigureAwait(false);
        }
    }
}
