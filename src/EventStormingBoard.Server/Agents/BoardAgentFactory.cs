using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using EventStormingBoard.Server.Entities;
using EventStormingBoard.Server.Filters;
using EventStormingBoard.Server.Models;
using EventStormingBoard.Server.Plugins;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.ComponentModel;

namespace EventStormingBoard.Server.Agents
{
    public sealed class BoardAgentFactory
    {
        private readonly AzureOpenAIClient _azureOpenAIClient;
        private readonly AzureOpenAIOptions _options;
        private readonly AgentToolCallFilter _toolCallFilter;
        private readonly IServiceProvider _serviceProvider;

        public BoardAgentFactory(
            AzureOpenAIClient azureOpenAIClient,
            AzureOpenAIOptions options,
            AgentToolCallFilter toolCallFilter,
            IServiceProvider serviceProvider)
        {
            _azureOpenAIClient = azureOpenAIClient;
            _options = options;
            _toolCallFilter = toolCallFilter;
            _serviceProvider = serviceProvider;
        }

        public AIAgent CreateAgent(
            AgentConfiguration config,
            Board? board,
            BoardPlugin boardPlugin,
            DelegationPlugin? delegationPlugin,
            Guid boardId,
            bool allowDestructiveChanges = true,
            List<AgentConfiguration>? allAgentConfigurations = null)
        {
            var instructions = config.SystemPrompt + AgentPrompts.BuildRuntimeContext(board, allAgentConfigurations, callingAgent: config);
            var tools = ResolveTools(config.AllowedTools, boardPlugin, delegationPlugin, allowDestructiveChanges);
            return BuildAgent(instructions, tools, boardId);
        }

        private AIAgent BuildAgent(string instructions, IList<AITool> tools, Guid boardId)
        {
            var chatOptions = new ChatOptions
            {
                Instructions = instructions,
                Tools = tools,
                AllowMultipleToolCalls = true
            };

            if (IsReasoningModel())
            {
                chatOptions.Reasoning = new ReasoningOptions
                {
                    Effort = ParseReasoningEffort(_options.ReasoningEffort)
                };
            }

            var baseAgent = _azureOpenAIClient
                .GetChatClient(_options.DeploymentName)
                .AsIChatClient()
                .AsAIAgent(new ChatClientAgentOptions
                {
                    ChatOptions = chatOptions
                }, services: _serviceProvider);

            return baseAgent
                .AsBuilder()
                .Use((agent, context, next, ct) =>
                    _toolCallFilter.OnFunctionInvocationAsync(boardId, agent, context, next, ct))
                .Build();
        }

        #region Tool Resolution

        public IList<AITool> ResolveTools(
            IEnumerable<string> allowedToolNames,
            BoardPlugin boardPlugin,
            DelegationPlugin? delegationPlugin,
            bool allowDestructiveChanges)
        {
            var tools = new List<AITool>();
            foreach (var toolName in allowedToolNames)
            {
                if (!allowDestructiveChanges && toolName == nameof(BoardPlugin.DeleteNotes))
                    continue;

                var tool = TryCreateTool(toolName, boardPlugin, delegationPlugin);
                if (tool != null)
                    tools.Add(tool);
            }
            return tools;
        }

        private static AIFunction? TryCreateTool(string toolName, BoardPlugin boardPlugin, DelegationPlugin? delegationPlugin)
        {
            // Try BoardPlugin first
            var method = typeof(BoardPlugin).GetMethod(toolName);
            if (method != null)
                return CreateToolFromMethod(method, boardPlugin);

            // Try DelegationPlugin
            if (delegationPlugin != null)
            {
                method = typeof(DelegationPlugin).GetMethod(toolName);
                if (method != null)
                    return CreateToolFromMethod(method, delegationPlugin);
            }

            return null;
        }

        private static AIFunction CreateToolFromMethod(System.Reflection.MethodInfo method, object target)
        {
            var description = method.GetCustomAttributes(typeof(DescriptionAttribute), inherit: false)
                .OfType<DescriptionAttribute>()
                .FirstOrDefault()
                ?.Description;

            return AIFunctionFactory.Create(method, target, new AIFunctionFactoryOptions
            {
                Name = method.Name,
                Description = description
            });
        }

        #endregion

        #region Tool Definitions Registry

        public static List<ToolDefinitionDto> GetAllToolDefinitions()
        {
            var definitions = new List<ToolDefinitionDto>();

            foreach (var method in typeof(BoardPlugin).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly))
            {
                var desc = method.GetCustomAttributes(typeof(DescriptionAttribute), false)
                    .OfType<DescriptionAttribute>()
                    .FirstOrDefault()?.Description;
                definitions.Add(new ToolDefinitionDto { Name = method.Name, Description = desc });
            }

            foreach (var method in typeof(DelegationPlugin).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly))
            {
                var desc = method.GetCustomAttributes(typeof(DescriptionAttribute), false)
                    .OfType<DescriptionAttribute>()
                    .FirstOrDefault()?.Description;
                definitions.Add(new ToolDefinitionDto { Name = method.Name, Description = desc });
            }

            return definitions;
        }

        #endregion

        #region Helpers

        private bool IsReasoningModel() =>
            !_options.DeploymentName.Contains("gpt-4", StringComparison.OrdinalIgnoreCase);

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

        public static AzureOpenAIClient CreateAzureOpenAIClient(AzureOpenAIOptions options)
        {
            if (!string.IsNullOrWhiteSpace(options.ApiKey))
            {
                return new AzureOpenAIClient(new Uri(options.Endpoint), new AzureKeyCredential(options.ApiKey));
            }

            return new AzureOpenAIClient(new Uri(options.Endpoint), new DefaultAzureCredential());
        }

        #endregion
    }
}
