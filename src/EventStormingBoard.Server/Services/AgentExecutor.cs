using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using EventStormingBoard.Server.Filters;
using EventStormingBoard.Server.Models;
using EventStormingBoard.Server.Plugins;
using EventStormingBoard.Server.Repositories;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using System.ComponentModel;

namespace EventStormingBoard.Server.Services
{
    public sealed class AgentExecutionRequest
    {
        public required Guid BoardId { get; init; }
        public required FacilitatorAgentDescriptor Definition { get; init; }
        public required string Instructions { get; init; }
        public required IReadOnlyList<string> ToolNames { get; init; }
        public required IReadOnlyList<ChatMessage> Messages { get; init; }
        public required AgentExecutionScope Scope { get; init; }
    }

    public sealed class AgentExecutionResult
    {
        public required AgentExecutionScope Scope { get; init; }
        public string Text { get; init; } = string.Empty;
        public List<AgentToolCallDto> ToolCalls { get; init; } = new();
    }

    public interface IAgentExecutor
    {
        Task<AgentExecutionResult> ExecuteAsync(AgentExecutionRequest request, CancellationToken cancellationToken);
    }

    public sealed class AgentExecutor : IAgentExecutor
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly AzureOpenAIOptions _options;
        private readonly AgentToolCallFilter _toolCallFilter;
        private readonly AzureOpenAIClient _azureOpenAIClient;

        public AgentExecutor(
            IServiceProvider serviceProvider,
            IOptions<AzureOpenAIOptions> options,
            AgentToolCallFilter toolCallFilter)
        {
            _serviceProvider = serviceProvider;
            _options = options.Value;
            _toolCallFilter = toolCallFilter;
            _azureOpenAIClient = CreateAzureOpenAIClient(_options);
        }

        public async Task<AgentExecutionResult> ExecuteAsync(AgentExecutionRequest request, CancellationToken cancellationToken)
        {
            var agent = BuildAgent(request);

            _toolCallFilter.BeginCapture(request.Scope);
            var response = await agent.RunAsync(request.Messages.ToList(), cancellationToken: cancellationToken).ConfigureAwait(false);

            return new AgentExecutionResult
            {
                Scope = request.Scope,
                Text = response.Text ?? string.Empty,
                ToolCalls = _toolCallFilter.EndCapture(request.Scope.ExecutionId)
            };
        }

        private static AzureOpenAIClient CreateAzureOpenAIClient(AzureOpenAIOptions options)
        {
            if (!string.IsNullOrWhiteSpace(options.ApiKey))
            {
                return new AzureOpenAIClient(new Uri(options.Endpoint), new AzureKeyCredential(options.ApiKey));
            }

            return new AzureOpenAIClient(new Uri(options.Endpoint), new DefaultAzureCredential());
        }

        private AIAgent BuildAgent(AgentExecutionRequest request)
        {
            var boardPlugin = new BoardPlugin(
                _serviceProvider.GetRequiredService<IBoardsRepository>(),
                _serviceProvider.GetRequiredService<IBoardEventPipeline>(),
                _serviceProvider.GetRequiredService<IBoardEventLog>(),
                _serviceProvider.GetRequiredService<Microsoft.AspNetCore.SignalR.IHubContext<EventStormingBoard.Server.Hubs.BoardsHub>>(),
                request.BoardId);

            var baseAgent = _azureOpenAIClient
                .GetChatClient(_options.DeploymentName)
                .AsIChatClient()
                .AsAIAgent(new ChatClientAgentOptions
                {
                    Name = request.Definition.Name,
                    Description = request.Definition.Description,
                    ChatOptions = BuildChatOptions(request, boardPlugin)
                }, services: _serviceProvider);

            return baseAgent
                .AsBuilder()
                .Use((agent, context, next, ct) => _toolCallFilter.OnFunctionInvocationAsync(request.Scope, agent, context, next, ct))
                .Build();
        }

        private ChatOptions BuildChatOptions(AgentExecutionRequest request, BoardPlugin boardPlugin)
        {
            var options = new ChatOptions
            {
                Instructions = request.Instructions,
                Tools = CreateTools(boardPlugin, request.ToolNames),
                AllowMultipleToolCalls = true
            };

            if (IsReasoningModel())
            {
                options.Reasoning = new Microsoft.Extensions.AI.ReasoningOptions
                {
                    Effort = ParseReasoningEffort(_options.ReasoningEffort)
                };
            }

            return options;
        }

        private static IList<AITool> CreateTools(BoardPlugin boardPlugin, IReadOnlyList<string> toolNames)
        {
            return toolNames
                .Select(methodName => CreateTool(boardPlugin, methodName))
                .Cast<AITool>()
                .ToList();

            static AIFunction CreateTool(BoardPlugin plugin, string methodName)
            {
                var methodInfo = typeof(BoardPlugin).GetMethod(methodName)!;
                var description = methodInfo.GetCustomAttributes(typeof(DescriptionAttribute), inherit: false)
                    .OfType<DescriptionAttribute>()
                    .FirstOrDefault()
                    ?.Description;

                Delegate method = methodName switch
                {
                    nameof(BoardPlugin.GetBoardState) => plugin.GetBoardState,
                    nameof(BoardPlugin.GetRecentEvents) => plugin.GetRecentEvents,
                    nameof(BoardPlugin.SetDomain) => plugin.SetDomain,
                    nameof(BoardPlugin.SetSessionScope) => plugin.SetSessionScope,
                    nameof(BoardPlugin.SetPhase) => plugin.SetPhase,
                    nameof(BoardPlugin.CompleteAutonomousSession) => plugin.CompleteAutonomousSession,
                    nameof(BoardPlugin.CreateNote) => plugin.CreateNote,
                    nameof(BoardPlugin.CreateConnection) => plugin.CreateConnection,
                    nameof(BoardPlugin.EditNoteText) => plugin.EditNoteText,
                    nameof(BoardPlugin.MoveNotes) => plugin.MoveNotes,
                    nameof(BoardPlugin.CreateNotes) => plugin.CreateNotes,
                    nameof(BoardPlugin.CreateConnections) => plugin.CreateConnections,
                    nameof(BoardPlugin.DeleteNotes) => plugin.DeleteNotes,
                    _ => throw new InvalidOperationException($"Unsupported board tool method '{methodName}'.")
                };

                return AIFunctionFactory.Create(method, new AIFunctionFactoryOptions
                {
                    Name = methodName,
                    Description = description
                });
            }
        }

        private bool IsReasoningModel() => !_options.DeploymentName.Contains("gpt-4", StringComparison.OrdinalIgnoreCase);

        private static ReasoningEffort ParseReasoningEffort(string? effort)
        {
            return effort?.Trim().ToLowerInvariant() switch
            {
                "high" => ReasoningEffort.High,
                "xhigh" => ReasoningEffort.ExtraHigh,
                "medium" => ReasoningEffort.Medium,
                _ => ReasoningEffort.Low
            };
        }
    }
}