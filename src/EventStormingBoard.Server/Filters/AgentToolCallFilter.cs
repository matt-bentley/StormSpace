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
        private readonly ConcurrentDictionary<string, ConcurrentQueue<AgentToolCallDto>> _pendingToolCalls = new();

        public AgentToolCallFilter(IHubContext<BoardsHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public void BeginCapture(string boardId)
        {
            _pendingToolCalls[boardId] = new ConcurrentQueue<AgentToolCallDto>();
        }

        public List<AgentToolCallDto> EndCapture(string boardId)
        {
            if (_pendingToolCalls.TryRemove(boardId, out var queue))
            {
                return queue.ToList();
            }
            return new List<AgentToolCallDto>();
        }

        public int PeekCaptureCount(string boardId)
        {
            if (_pendingToolCalls.TryGetValue(boardId, out var queue))
            {
                return queue.Count;
            }
            return 0;
        }

        public List<AgentToolCallDto> PeekCaptureSlice(string boardId, int fromIndex)
        {
            if (_pendingToolCalls.TryGetValue(boardId, out var queue))
            {
                var all = queue.ToList();
                if (fromIndex < all.Count)
                {
                    return all.GetRange(fromIndex, all.Count - fromIndex);
                }
            }
            return new List<AgentToolCallDto>();
        }

        public async ValueTask<object?> OnFunctionInvocationAsync(
            Guid boardId,
            AIAgent agent,
            FunctionInvocationContext context,
            Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next,
            CancellationToken cancellationToken)
        {
            var functionName = context.Function.Name;

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

            var boardKey = boardId.ToString();
            _pendingToolCalls.GetOrAdd(boardKey, _ => new ConcurrentQueue<AgentToolCallDto>())
                .Enqueue(new AgentToolCallDto { Name = functionName, Arguments = argsString });

            await _hubContext.Clients.Group(boardKey).SendAsync("AgentToolCallStarted", new
            {
                BoardId = boardKey,
                ToolName = functionName,
                Arguments = arguments
            }, cancellationToken);

            return await next(context, cancellationToken).ConfigureAwait(false);
        }
    }
}
