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

        public void BeginCapture(AgentExecutionScope scope)
        {
            _pendingToolCalls[scope.ExecutionId] = new ConcurrentBag<AgentToolCallDto>();
        }

        public List<AgentToolCallDto> EndCapture(string executionId)
        {
            if (_pendingToolCalls.TryRemove(executionId, out var bag))
            {
                return bag.Reverse().ToList();
            }
            return new List<AgentToolCallDto>();
        }

        public async Task CaptureToolCallAsync(
            AgentExecutionScope scope,
            string functionName,
            IReadOnlyDictionary<string, string> arguments,
            CancellationToken cancellationToken)
        {
            var argsString = arguments.Count > 0
                ? string.Join(", ", arguments.Select(a => $"{a.Key}: {a.Value}"))
                : string.Empty;

            _pendingToolCalls.GetOrAdd(scope.ExecutionId, _ => new ConcurrentBag<AgentToolCallDto>())
                .Add(new AgentToolCallDto { Name = functionName, Arguments = argsString });

            await _hubContext.Clients.Group(scope.BoardId.ToString()).SendAsync("AgentToolCallStarted", new AgentToolCallStartedDto
            {
                BoardId = scope.BoardId.ToString(),
                ExecutionId = scope.ExecutionId,
                AgentId = scope.AgentId,
                AgentName = scope.AgentName,
                ToolName = functionName,
                Arguments = new Dictionary<string, string>(arguments, StringComparer.Ordinal)
            }, cancellationToken);
        }

        public async ValueTask<object?> OnFunctionInvocationAsync(
            AgentExecutionScope scope,
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

            await CaptureToolCallAsync(scope, functionName, arguments, cancellationToken).ConfigureAwait(false);

            return await next(context, cancellationToken).ConfigureAwait(false);
        }
    }
}
