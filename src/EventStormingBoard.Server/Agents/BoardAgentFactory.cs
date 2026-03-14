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

        public AIAgent CreateFacilitator(
            Board? board,
            BoardPlugin boardPlugin,
            Func<SpecialistAgent, string, Task<string>> delegationHandler,
            Guid boardId)
        {
            var delegationPlugin = new DelegationPlugin(delegationHandler);
            var tools = new List<AITool>
            {
                CreateTool(boardPlugin, nameof(BoardPlugin.GetBoardState)),
                CreateTool(boardPlugin, nameof(BoardPlugin.GetRecentEvents)),
                CreateTool(boardPlugin, nameof(BoardPlugin.SetDomain)),
                CreateTool(boardPlugin, nameof(BoardPlugin.SetSessionScope)),
                CreateTool(boardPlugin, nameof(BoardPlugin.SetPhase)),
                CreateTool(boardPlugin, nameof(BoardPlugin.CompleteAutonomousSession)),
                CreateTool(delegationPlugin, nameof(DelegationPlugin.RequestSpecialistProposal))
            };

            return BuildAgent(AgentPrompts.BuildLeadFacilitatorPrompt(board), tools, boardId);
        }

        public AIAgent CreateSpecialist(
            AgentRole role,
            Board? board,
            BoardPlugin boardPlugin,
            Guid boardId)
        {
            var prompt = role switch
            {
                AgentRole.EventExplorer => AgentPrompts.BuildEventExplorerPrompt(board),
                AgentRole.TriggerMapper => AgentPrompts.BuildTriggerMapperPrompt(board),
                AgentRole.DomainDesigner => AgentPrompts.BuildDomainDesignerPrompt(board),
                _ => throw new ArgumentException($"Not a specialist role: {role}", nameof(role))
            };

            var tools = CreateReadOnlyTools(boardPlugin);
            return BuildAgent(prompt, tools, boardId);
        }

        public AIAgent CreateChallenger(Board? board, BoardPlugin boardPlugin, Guid boardId)
        {
            var tools = CreateReadOnlyTools(boardPlugin);
            return BuildAgent(AgentPrompts.BuildChallengerPrompt(board), tools, boardId);
        }

        public AIAgent CreateWallScribe(
            Board? board,
            BoardPlugin boardPlugin,
            bool allowDestructiveChanges,
            Guid boardId)
        {
            var tools = CreateWallScribeTools(boardPlugin, allowDestructiveChanges);
            return BuildAgent(AgentPrompts.BuildWallScribePrompt(board), tools, boardId);
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

        #region Tool Lists

        public static IReadOnlyList<string> GetFacilitatorToolNames()
        {
            return new[]
            {
                nameof(BoardPlugin.GetBoardState),
                nameof(BoardPlugin.GetRecentEvents),
                nameof(BoardPlugin.SetDomain),
                nameof(BoardPlugin.SetSessionScope),
                nameof(BoardPlugin.SetPhase),
                nameof(BoardPlugin.CompleteAutonomousSession),
                nameof(DelegationPlugin.RequestSpecialistProposal)
            };
        }

        public static IReadOnlyList<string> GetReadOnlyToolNames()
        {
            return new[]
            {
                nameof(BoardPlugin.GetBoardState),
                nameof(BoardPlugin.GetRecentEvents)
            };
        }

        public static IReadOnlyList<string> GetWallScribeToolNames(bool allowDestructiveChanges)
        {
            var names = new List<string>
            {
                nameof(BoardPlugin.GetBoardState),
                nameof(BoardPlugin.CreateNote),
                nameof(BoardPlugin.CreateNotes),
                nameof(BoardPlugin.CreateConnection),
                nameof(BoardPlugin.CreateConnections),
                nameof(BoardPlugin.EditNoteText),
                nameof(BoardPlugin.MoveNotes)
            };

            if (allowDestructiveChanges)
            {
                names.Add(nameof(BoardPlugin.DeleteNotes));
            }

            return names;
        }

        private static IList<AITool> CreateReadOnlyTools(BoardPlugin boardPlugin)
        {
            return new List<AITool>
            {
                CreateTool(boardPlugin, nameof(BoardPlugin.GetBoardState)),
                CreateTool(boardPlugin, nameof(BoardPlugin.GetRecentEvents))
            };
        }

        private static IList<AITool> CreateWallScribeTools(BoardPlugin boardPlugin, bool allowDestructiveChanges)
        {
            var tools = new List<AITool>
            {
                CreateTool(boardPlugin, nameof(BoardPlugin.GetBoardState)),
                CreateTool(boardPlugin, nameof(BoardPlugin.CreateNote)),
                CreateTool(boardPlugin, nameof(BoardPlugin.CreateNotes)),
                CreateTool(boardPlugin, nameof(BoardPlugin.CreateConnection)),
                CreateTool(boardPlugin, nameof(BoardPlugin.CreateConnections)),
                CreateTool(boardPlugin, nameof(BoardPlugin.EditNoteText)),
                CreateTool(boardPlugin, nameof(BoardPlugin.MoveNotes))
            };

            if (allowDestructiveChanges)
            {
                tools.Add(CreateTool(boardPlugin, nameof(BoardPlugin.DeleteNotes)));
            }

            return tools;
        }

        #endregion

        #region Helpers

        private static AIFunction CreateTool(object plugin, string methodName)
        {
            var methodInfo = plugin.GetType().GetMethod(methodName)
                ?? throw new InvalidOperationException($"Method '{methodName}' not found on {plugin.GetType().Name}.");

            var description = methodInfo.GetCustomAttributes(typeof(DescriptionAttribute), inherit: false)
                .OfType<DescriptionAttribute>()
                .FirstOrDefault()
                ?.Description;

            return AIFunctionFactory.Create(methodInfo, plugin, new AIFunctionFactoryOptions
            {
                Name = methodName,
                Description = description
            });
        }

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
