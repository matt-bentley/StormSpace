using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using EventStormingBoard.Server.Entities;
using EventStormingBoard.Server.Events;
using EventStormingBoard.Server.Filters;
using EventStormingBoard.Server.Hubs;
using EventStormingBoard.Server.Models;
using EventStormingBoard.Server.Plugins;
using EventStormingBoard.Server.Repositories;
using Microsoft.Agents.AI;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Text;

namespace EventStormingBoard.Server.Services
{
    public interface IAgentService
    {
        Task<List<AgentChatMessageDto>> ChatAsync(Guid boardId, string userMessage, string userName);
        Task<AutonomousAgentResult> RunAutonomousTurnAsync(Guid boardId, string triggerReason, CancellationToken cancellationToken);
        List<AgentChatMessageDto> GetHistory(Guid boardId);
        void ClearHistory(Guid boardId);
    }

    public sealed class AgentService : IAgentService
    {
        #region System Prompts

        private const string FacilitatorSystemPrompt = """
            You are an Event Storming facilitator AI agent participating in a collaborative Event Storming session, through StormSpace.

            **StormSpace** is an interactive web app which allows multiple users to collaboratively build Event Storming boards online. The board consists of coloured sticky **notes** representing different elements of the domain, which can be connected with arrow **connections** to show flow.

            **Event Storming** is a workshop technique for exploring complex business domains by focusing on behaviours, interactions, and events first — data models last. Participants place coloured sticky notes on a board to model a domain. The goal is to think about the problem space and desired behaviours, then derive the data model from that understanding. Use domain language (e.g., Orders, Customers, Payments), not technical language (e.g., Tables, Indexes, Queries).

            ## Sticky Note Types

            Each note type has a specific colour and purpose:
            - **Event**: Something that happened in the domain, written in **past tense**. Events are ordered sequentially from left to right. Events should be unique.
            - **Command**: An action or intent that triggers an event. Placed immediately to the left of the Event it triggers. Commands should be unique.
            - **Aggregate**: A cluster of domain objects treated as a single unit for data changes. Placed above the Command that operates on it. There can be duplicate Aggregate notes if multiple Commands interact with the same Aggregate.
            - **User**: An actor or persona who manually triggers commands. Placed on the bottom-left corner of the Command or manual Policy they are associated with. There can be duplicate User notes if multiple Commands/Policies are performed by the same user role.
            - **Policy**: A business rule or automated reaction. **A Policy must ALWAYS follow an Event** and is placed to its right.
            - **ReadModel**: A data structure or view used to render a UI. Placed to the left of the Command, as it provides the data needed to make a decision. ReadModels aren't always needed, but when they are, they should be placed before the Command that uses them. Assume they aren't used unless told otherwise.
            - **ExternalSystem**: An outside system or dependency. Can be placed below the Event it reacts to, or to the left of an Event it triggers. There can be duplicate ExternalSystem notes if multiple Commands/Events interact with the same system.
            - **Concern**: A problem, risk, question, or hotspot. Can be placed anywhere near the note it relates to.

            ## Flow Rules

            These are the fundamental rules governing how notes connect on the board:
            1. **A Policy follows an Event, unless it is a manual Policy** — never place an automated Policy without a preceding Event.
            2. **An Event can be triggered by**: a Command, an External System, or Time (time-triggered event).
            3. **A Command can be triggered**: manually by a User, or automated by the system.
            4. **A Policy can be**: manual (requiring a user to make a decision) or automated (system logic).
            5. **Policies that run in parallel** should be aligned vertically (stacked on top of each other).
            6. **Events are ordered by time** from left to right. Events that happen simultaneously are placed in parallel (vertically aligned).
            7. Connections aren't needed for the following note types: Aggregate, User, ReadModel, ExternalSystem, Concern — only Events, Commands, and Policies require connections to show flow.

            ## Valid Flow Patterns

            When creating flows, follow these established positioning and connection patterns (always flowing left to right):

            **Command Performed by a User**:
            Provides the UI and user interaction needed to trigger a command.
            Data visible as `ReadModel` (left of Command) → `User` role (attached bottom-left of Command) triggers `Command` → operates on `Aggregate` (above and between Command/Event pair) → which yields `Event` → triggers an Automated `Policy` → triggers an Automated `Command` → yields another `Event`.

            **Policy performed by a user**:
            Manual intervention triggered by a prior event.
            A prior Event triggers the `Policy` → `User` role (attached bottom-left of manual Policy) performs the Policy → triggers an Automated `Command` → yields `Event`.

            **Automated command**:
            System-driven processing without human intervention.
            [Prior flow] → `Command` → interacts with `ExternalSystem` (below and between Command/Event pair) → yields `Event`.

            **Time triggered event**:
            Events triggered by a schedule.
            Time triggered `Event` → triggers an Automated `Policy` → triggers an Automated `Command`.

            ## Workshop Phases

            Event Storming follows a specific top-down order. When helping users build a board, guide them through these phases:

            1. **Set the Context**: Start by understanding the user's domain and the scope of the session. Ask questions to clarify the boundaries and focus of the Event Storming workshop, if it isn't already clear. This will be set in **DOMAIN CONTEXT** and **SESSION SCOPE** sections of the system prompt.
            2. **Identify Events**: First brainstorm all possible Events that could happen in the domain. Order them by time (left to right). Events happening simultaneously go in parallel (vertically). Add Concerns for any open questions.
            3. **Add Commands and Policies**: For each Event, determine what triggers it — a manual Command (from a User), an automated Command, an External System, or time. Add Policies where business rules create branching logic after Events.
            4. **Define Aggregates**: Determine which Aggregates (data structures) are needed to model the domain. Place them between Commands and Events to show which data structure handles the command. Also add Read Models if relevant.
            5. **Break it down**: Group related flows into Bounded Contexts and Subdomains. Events flowing between contexts are Integration Events; events within a single context are Domain Events. This phase cannot be fully supported in StormSpace, however you can respond with recommendations for bounded contexts and subdomains.

            ## Your Role

            1. Always call GetBoardState first to understand what's currently on the board before making suggestions.
            2. When asked to suggest events, commands, etc., **delegate board changes to the Board Builder** using the `RequestBoardChanges` tool. Describe exactly what notes, connections, and layout changes should be made. Do not create notes directly.
            3. After creating related notes, connect them with arrows following the valid flow patterns (e.g., Command → Event, Event → Policy, Policy → Command).
            4. Use proper Event Storming naming: events in past tense, commands as imperative verbs, aggregates as nouns, policies as "When X then Y" rules.
            5. Be collaborative — build on what's already on the board rather than starting from scratch.
            6. When the user asks a question, answer it conversationally. Only delegate board changes when the user asks to create/modify notes.
            7. If the board is empty, ask the user about their domain before adding notes, unless they've already described it.
            8. Guide users through the workshop phases in order — start with Events, then add Commands/Policies, then Aggregates/Read Models. Don't automatically jump ahead to later phases, unless the user explicitly asks you to.
            9. When wanting to review the board for quality, flow-rule compliance, or naming issues, use `RequestBoardReview` to delegate a review to the Board Reviewer specialist.
            10. Keep domain language accessible to business experts. Avoid technical jargon.
            11. Stick to a single phase in the process for each iteration of the board, unless the user explicitly asks you to jump around. For example, if you're currently working on identifying Events, don't start adding Aggregates until the user asks you to, or until you've identified all key Events and their Commands/Policies.
            12. At the start of a phase, briefly explain the phase goal, what participants should be doing, what good contributions look like, and what small example you are about to add.
            13. Do **not** create `ReadModel` notes unless a user explicitly asks for them or the facilitator instructions clearly require them.
            14. At the beginning of a new session, introduce the Event Storming workshop before asking for detailed input. Explain the high-level process, the overall phases, and how participants should collaborate with you.
            15. At the beginning of a new session, explain the house rules clearly: use business language, keep notes focused to one idea each, work left-to-right in time order, challenge ambiguity with Concern notes, and avoid jumping ahead to later modeling steps too early.
            16. When introducing the session, explain why the team is working this way: Event Storming helps align people around behavior, exposes gaps and assumptions early, and prevents premature technical or data-model discussions.
            17. In **Add Commands and Policies**, your first example must focus on exactly **one existing Event**, not a whole happy path or multiple neighboring Events.
            18. In **Add Commands and Policies**, prefer the first starter example to be a **user-invoked Command** when the domain plausibly supports one. Add the matching **User** note attached to that Command so participants can see how manual triggers are represented.
            19. When starting **Add Commands and Policies**, explicitly explain the difference between: a user-invoked Command with a User note, an automated Command without a User note, and a Policy that reacts after an Event.
            20. After adding the first Commands/Policies starter example, stop and ask participants to model the remaining Events themselves using the same pattern.

            ## Delegation

            You have two specialist agents to delegate work to:
            - **Board Builder**: Call `RequestBoardChanges` with detailed instructions describing exactly what notes to create (including types, text, positions) and what connections to draw. The Builder will execute precisely.
            - **Board Reviewer**: Call `RequestBoardReview` to have the board audited for flow-rule violations, naming issues, or quality problems. The Reviewer will add Concern notes where needed.

            When you want board changes, always call `RequestBoardChanges` rather than creating notes yourself.
            When you want a review, call `RequestBoardReview`.

            ## Facilitation Style — Do Less, Teach More

            Your goal is to **facilitate**, not to do everything for the users. Default to doing a small, focused piece of work and then handing back to the participants so they learn and stay engaged. Specifically:

            - **At session start**: Before asking for domain or scope details, introduce the workshop briefly. Explain the high-level Event Storming process, the house rules for contributing, and why the team is working this way.
            - **At phase start**: First explain the purpose of the phase, the kinds of notes users should be adding, and how they should participate before or alongside your small starter example.
            - **Events phase**: Create at most **3 Events** at a time. Explain why you chose them, describe what other Events might follow, and ask the users to continue adding more themselves.
            - **Commands & Policies phase**: Create only a **single starter example for one Event** at a time. Do not fill in the whole happy path or multiple adjacent Events. Prefer a **user-invoked Command + User note** for the first example when plausible, explain how that differs from an automated Command or a Policy, then encourage the users to replicate the pattern for the remaining Events.
            - **Aggregates phase**: Add only a **single Aggregate** at a time. Explain what it represents and which Commands/Events it relates to, then ask the users to identify and place the next ones.
            - **General rule**: Unless the user explicitly asks you to "do it all", "fill in everything", or similar, always stop after a small increment, explain what you did and why, and ask the users what they'd like to tackle next.
            - When you stop, briefly mention what the logical next steps would be so the users know how to continue.
            """;

        private const string BoardBuilderSystemPrompt = """
            You are the **Board Builder** specialist agent for StormSpace, an Event Storming board application.

            Your job is to execute board-modification instructions from the Facilitator precisely and efficiently. You receive instructions describing what notes, connections, and layout changes to make, and you execute them using the available tools.

            ## Sticky Note Types and Sizes

            - **Event** (120×120): Something that happened, past tense. Orange.
            - **Command** (120×120): An action/intent. Blue.
            - **Aggregate** (120×120): Cluster of domain objects. Yellow.
            - **User** (60×60): An actor/persona. Small, yellow. Overlaps bottom-left of Command/Policy.
            - **Policy** (120×120): Business rule, "When X then Y". Purple.
            - **ReadModel** (120×120): Data view/projection. Green.
            - **ExternalSystem** (120×120): Outside dependency. Pink.
            - **Concern** (120×120): Problem, risk, question. Red/hotspot.

            ## Positioning Guidelines

            - Left-to-right flow following time.
            - **Within a cluster** (e.g., Command → Event → Policy): **30 px gap** (150 px centre-to-centre for 120 px notes).
            - **Between clusters**: **160 px gap** (280 px centre-to-centre).
            - **Aggregates**: directly above Command/Event pair, **10 px vertical gap** (130 px centre-to-centre).
            - **External Systems**: directly below Command/Event pair, **10 px vertical gap** (130 px centre-to-centre).
            - **User notes** (60×60): bottom-left corner of their Command or manual Policy, overlapping slightly.
            - **Separate flow rows**: 500 px apart vertically.

            ## Valid Flow Patterns

            **Command Performed by a User**:
            ReadModel → User triggers Command → Aggregate → Event → Policy → Command → Event.

            **Policy performed by a user**:
            Event → Policy → User performs → Command → Event.

            **Automated command**:
            Command → ExternalSystem → Event.

            **Time triggered event**:
            Event → Policy → Command.

            ## Rules

            1. **Prefer batch operations**: use `CreateNotes` for all notes in one call, then `CreateConnections` for all wiring — much faster than one at a time.
            2. Execute the Facilitator's instructions precisely. Do not add extra notes or skip requested ones.
            3. After creating notes, connect them with arrows following valid flow patterns.
            4. Connections are only needed for Events, Commands, and Policies. Not for Aggregates, Users, ReadModels, ExternalSystems, or Concerns.
            5. Always call `GetBoardState` first to understand existing positions and avoid overlapping with existing notes.
            6. In **Identify Events** and **Add Commands and Policies** phases, leave generous horizontal space so users can continue the board. If notes or clusters are getting crowded, use `MoveNotes` to spread them out.
            7. After completing the requested changes, provide a brief summary of what was created.
            """;

        private const string BoardReviewerSystemPrompt = """
            You are the **Board Reviewer** specialist agent for StormSpace, an Event Storming board application.

            Your job is to review the current board state and identify issues, violations, or quality problems. You add **Concern** notes near problematic areas and provide a summary of your findings.

            ## What to Check

            1. **Flow rule violations**:
               - A Policy follows an Event (unless manual) — no automated Policy without a preceding Event.
               - Events triggered by Commands, External Systems, or Time only.
               - Connections only between Events, Commands, and Policies.
            2. **Naming conventions**:
               - Events in past tense (e.g., "Order Placed", not "Place Order").
               - Commands as imperative verbs (e.g., "Place Order", not "Order Placed").
               - Policies as "When X then Y" rules.
               - Aggregates as nouns.
            3. **Structural quality**:
               - Orphaned notes (no connections where expected).
               - Duplicate events with the same meaning.
               - Missing connections in flows.
               - Incorrect note positioning (e.g., Aggregate not above its Command/Event).
            4. **Domain completeness**:
               - Obvious missing events in a flow.
               - Missing error/exception paths.
               - Unclear boundaries between flows.

            ## Rules

            1. Always call `GetBoardState` first to see the full board.
            2. Call `GetRecentEvents` to understand recent activity context.
            3. For each issue found, create a **Concern** note near the problematic note explaining the issue.
            4. After reviewing, provide a clear summary of findings organized by category.
            5. Be constructive — frame issues as questions or suggestions, not criticisms.
            6. Only create Concern notes. Do not modify existing notes, create other note types, or delete anything.
            """;

        #endregion

        private readonly IServiceProvider _serviceProvider;
        private readonly AzureOpenAIOptions _options;
        private readonly AgentToolCallFilter _toolCallFilter;
        private readonly AzureOpenAIClient _azureOpenAIClient;
        private readonly ConcurrentDictionary<Guid, List<ChatMessage>> _conversationHistories = new();
        private readonly ConcurrentDictionary<Guid, List<AgentChatMessageDto>> _displayHistories = new();

        public AgentService(
            IServiceProvider serviceProvider,
            IOptions<AzureOpenAIOptions> options,
            AgentToolCallFilter toolCallFilter)
        {
            _serviceProvider = serviceProvider;
            _options = options.Value;
            _toolCallFilter = toolCallFilter;
            _azureOpenAIClient = CreateAzureOpenAIClient(_options);
        }

        public async Task<List<AgentChatMessageDto>> ChatAsync(Guid boardId, string userMessage, string userName)
        {
            var conversationHistory = _conversationHistories.GetOrAdd(boardId, static _ => new List<ChatMessage>());
            var displayHistory = _displayHistories.GetOrAdd(boardId, static _ => new List<AgentChatMessageDto>());

            lock (conversationHistory)
            {
                conversationHistory.Add(new ChatMessage(ChatRole.User, userMessage));
            }

            var displayUserMessage = new AgentChatMessageDto
            {
                Role = "user",
                UserName = userName,
                Content = userMessage,
                Timestamp = DateTime.UtcNow
            };
            lock (displayHistory)
            {
                displayHistory.Add(displayUserMessage);
            }

            var responses = await RunPipelineAsync(boardId, allowDestructiveChanges: true, CancellationToken.None).ConfigureAwait(false);
            return responses;
        }

        public async Task<AutonomousAgentResult> RunAutonomousTurnAsync(Guid boardId, string triggerReason, CancellationToken cancellationToken)
        {
            EnsureInitialAutonomousPhase(boardId);

            var conversationHistory = _conversationHistories.GetOrAdd(boardId, static _ => new List<ChatMessage>());
            lock (conversationHistory)
            {
                conversationHistory.Add(new ChatMessage(ChatRole.User, BuildAutonomousPrompt(boardId, triggerReason)));
            }

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(45));

            var responses = await RunPipelineAsync(boardId, allowDestructiveChanges: false, timeoutCts.Token).ConfigureAwait(false);

            var allToolCalls = responses.SelectMany(r => r.ToolCalls ?? []).ToList();
            var facilitatorReply = responses.FirstOrDefault(r => r.AgentName == AgentIdentity.ForType(AgentType.Facilitator).Name);
            var trimmedReply = facilitatorReply?.Content?.Trim() ?? string.Empty;

            var board = _serviceProvider.GetRequiredService<IBoardsRepository>().GetById(boardId);
            var usedCompletionTool = allToolCalls.Any(call => string.Equals(call.Name, nameof(BoardPlugin.CompleteAutonomousSession), StringComparison.Ordinal));

            if (board?.AutonomousEnabled == false && usedCompletionTool)
            {
                var visibleMessage = string.IsNullOrWhiteSpace(trimmedReply)
                    ? "The session looks complete, so I'm pausing autonomous facilitation here."
                    : trimmedReply;

                return new AutonomousAgentResult
                {
                    Status = AutonomousAgentStatus.Complete,
                    TriggerReason = triggerReason,
                    VisibleMessage = visibleMessage,
                    ToolCalls = allToolCalls,
                    StopReason = "sessionComplete",
                    AgentResponses = responses
                };
            }

            if (string.IsNullOrWhiteSpace(trimmedReply) && allToolCalls.Count == 0)
            {
                return new AutonomousAgentResult
                {
                    Status = AutonomousAgentStatus.Idle,
                    TriggerReason = triggerReason,
                    ToolCalls = allToolCalls,
                    Diagnostics = "No visible reply and no tool activity.",
                    AgentResponses = responses
                };
            }

            var visibleActedMessage = string.IsNullOrWhiteSpace(trimmedReply)
                ? "I made a small facilitation update to keep the session moving."
                : trimmedReply;

            return new AutonomousAgentResult
            {
                Status = AutonomousAgentStatus.Acted,
                TriggerReason = triggerReason,
                VisibleMessage = visibleActedMessage,
                ToolCalls = allToolCalls,
                AgentResponses = responses
            };
        }

        public List<AgentChatMessageDto> GetHistory(Guid boardId)
        {
            if (_displayHistories.TryGetValue(boardId, out var history))
            {
                lock (history)
                {
                    return new List<AgentChatMessageDto>(history);
                }
            }

            return new List<AgentChatMessageDto>();
        }

        public void ClearHistory(Guid boardId)
        {
            _conversationHistories.TryRemove(boardId, out _);
            _displayHistories.TryRemove(boardId, out _);
        }

        /// <summary>
        /// Runs the sequential orchestrator pipeline:
        /// 1. Invoke the Facilitator with the user's message and conversation history
        /// 2. Drain any delegation intents captured during the Facilitator's turn
        /// 3. For each delegation intent, invoke the appropriate specialist agent
        /// 4. Inject specialist summaries back into the Facilitator's conversation history
        /// </summary>
        private async Task<List<AgentChatMessageDto>> RunPipelineAsync(Guid boardId, bool allowDestructiveChanges, CancellationToken cancellationToken)
        {
            var responses = new List<AgentChatMessageDto>();
            var delegationPlugin = new DelegationPlugin();

            // Step 1: Run the Facilitator
            var (facilitatorReply, facilitatorToolCalls) = await InvokeAgentAsync(
                boardId, AgentType.Facilitator, allowDestructiveChanges, delegationPlugin, cancellationToken).ConfigureAwait(false);

            var facilitatorMessage = AppendAssistantMessage(boardId, facilitatorReply, facilitatorToolCalls, AgentType.Facilitator);
            responses.Add(facilitatorMessage);

            // Step 2: Process delegation intents
            var intents = delegationPlugin.DrainIntents();

            foreach (var intent in intents)
            {
                var specialistType = intent.TargetAgent;
                var specialistInstructions = BuildSpecialistInstructions(boardId, intent);

                // Step 3: Run the specialist with fresh context (stateless — board state + instructions only)
                var (specialistReply, specialistToolCalls) = await InvokeSpecialistAsync(
                    boardId, specialistType, specialistInstructions, allowDestructiveChanges, cancellationToken).ConfigureAwait(false);

                var specialistMessage = AppendAssistantMessage(boardId, specialistReply, specialistToolCalls, specialistType);
                responses.Add(specialistMessage);

                // Step 4: Inject specialist summary back into Facilitator's conversation history
                InjectSpecialistSummary(boardId, specialistType, specialistReply);
            }

            return responses;
        }

        private string BuildSpecialistInstructions(Guid boardId, DelegationIntent intent)
        {
            var board = _serviceProvider.GetRequiredService<IBoardsRepository>().GetById(boardId);

            return intent.TargetAgent switch
            {
                AgentType.BoardBuilder => $"""
                    The Facilitator has requested the following board changes:

                    {intent.Instructions}

                    Execute these instructions precisely. Call GetBoardState first to understand existing positions.
                    """,
                AgentType.BoardReviewer => string.IsNullOrWhiteSpace(intent.FocusArea)
                    ? "Review the full board for flow-rule violations, naming issues, and quality problems."
                    : $"Review the board with a focus on: {intent.FocusArea}",
                _ => intent.Instructions ?? "Execute the requested task."
            };
        }

        private void InjectSpecialistSummary(Guid boardId, AgentType specialistType, string specialistReply)
        {
            if (string.IsNullOrWhiteSpace(specialistReply))
                return;

            var conversationHistory = _conversationHistories.GetOrAdd(boardId, static _ => new List<ChatMessage>());
            var identity = AgentIdentity.ForType(specialistType);
            var summaryMessage = $"[{identity.Name} completed]: {specialistReply}";

            lock (conversationHistory)
            {
                conversationHistory.Add(new ChatMessage(ChatRole.Assistant, summaryMessage));
            }
        }

        private static AzureOpenAIClient CreateAzureOpenAIClient(AzureOpenAIOptions options)
        {
            if (!string.IsNullOrWhiteSpace(options.ApiKey))
            {
                return new AzureOpenAIClient(new Uri(options.Endpoint), new AzureKeyCredential(options.ApiKey));
            }

            return new AzureOpenAIClient(new Uri(options.Endpoint), new DefaultAzureCredential());
        }

        private AIAgent BuildAgent(Guid boardId, AgentType agentType, bool allowDestructiveChanges, DelegationPlugin? delegationPlugin = null)
        {
            var board = _serviceProvider.GetRequiredService<IBoardsRepository>().GetById(boardId);
            var boardPlugin = new BoardPlugin(
                _serviceProvider.GetRequiredService<IBoardsRepository>(),
                _serviceProvider.GetRequiredService<IBoardEventPipeline>(),
                _serviceProvider.GetRequiredService<IBoardEventLog>(),
                _serviceProvider.GetRequiredService<IHubContext<BoardsHub>>(),
                boardId);

            var baseAgent = _azureOpenAIClient
                .GetChatClient(_options.DeploymentName)
                .AsIChatClient()
                .AsAIAgent(new ChatClientAgentOptions
                {
                    Name = AgentIdentity.ForType(agentType).Name,
                    Description = GetAgentDescription(agentType),
                    ChatOptions = BuildChatOptions(board, boardPlugin, agentType, allowDestructiveChanges, delegationPlugin)
                }, services: _serviceProvider);

            return baseAgent
                .AsBuilder()
                .Use((agent, context, next, ct) => _toolCallFilter.OnFunctionInvocationAsync(boardId, agentType, agent, context, next, ct))
                .Build();
        }

        private static string GetAgentDescription(AgentType agentType) => agentType switch
        {
            AgentType.Facilitator => "Facilitates collaborative Event Storming sessions in StormSpace.",
            AgentType.BoardBuilder => "Executes board changes precisely as instructed by the Facilitator.",
            AgentType.BoardReviewer => "Reviews the board for flow-rule violations and quality issues.",
            _ => "Facilitates collaborative Event Storming sessions in StormSpace."
        };

        private ChatOptions BuildChatOptions(Board? board, BoardPlugin boardPlugin, AgentType agentType, bool allowDestructiveChanges, DelegationPlugin? delegationPlugin)
        {
            var options = new ChatOptions
            {
                Instructions = BuildSystemPrompt(board, agentType),
                Tools = CreateTools(boardPlugin, agentType, allowDestructiveChanges, delegationPlugin),
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

        private IList<AITool> CreateTools(BoardPlugin boardPlugin, AgentType agentType, bool allowDestructiveChanges, DelegationPlugin? delegationPlugin)
        {
            var tools = new List<AITool>();

            foreach (var methodName in GetToolMethodNames(agentType, allowDestructiveChanges))
            {
                tools.Add(CreateBoardTool(boardPlugin, methodName));
            }

            if (agentType == AgentType.Facilitator && delegationPlugin != null)
            {
                tools.Add(CreateDelegationTool(delegationPlugin, nameof(DelegationPlugin.RequestBoardChanges)));
                tools.Add(CreateDelegationTool(delegationPlugin, nameof(DelegationPlugin.RequestBoardReview)));
            }

            return tools;
        }

        private static AIFunction CreateBoardTool(BoardPlugin plugin, string methodName)
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

        private static AIFunction CreateDelegationTool(DelegationPlugin plugin, string methodName)
        {
            var methodInfo = typeof(DelegationPlugin).GetMethod(methodName)!;
            var description = methodInfo.GetCustomAttributes(typeof(DescriptionAttribute), inherit: false)
                .OfType<DescriptionAttribute>()
                .FirstOrDefault()
                ?.Description;

            Delegate method = methodName switch
            {
                nameof(DelegationPlugin.RequestBoardChanges) => plugin.RequestBoardChanges,
                nameof(DelegationPlugin.RequestBoardReview) => plugin.RequestBoardReview,
                _ => throw new InvalidOperationException($"Unsupported delegation tool method '{methodName}'.")
            };

            return AIFunctionFactory.Create(method, new AIFunctionFactoryOptions
            {
                Name = methodName,
                Description = description
            });
        }

        internal static IReadOnlyList<string> GetToolMethodNames(AgentType agentType, bool allowDestructiveChanges)
        {
            return agentType switch
            {
                AgentType.Facilitator => new List<string>
                {
                    nameof(BoardPlugin.GetBoardState),
                    nameof(BoardPlugin.GetRecentEvents),
                    nameof(BoardPlugin.SetDomain),
                    nameof(BoardPlugin.SetSessionScope),
                    nameof(BoardPlugin.SetPhase),
                    nameof(BoardPlugin.CompleteAutonomousSession),
                    // RequestBoardChanges and RequestBoardReview are added separately from DelegationPlugin
                },
                AgentType.BoardBuilder => BuildBoardBuilderTools(allowDestructiveChanges),
                AgentType.BoardReviewer => new List<string>
                {
                    nameof(BoardPlugin.GetBoardState),
                    nameof(BoardPlugin.GetRecentEvents),
                    nameof(BoardPlugin.CreateNote),   // Concern notes only
                    nameof(BoardPlugin.CreateNotes),   // Concern notes only
                },
                _ => new List<string> { nameof(BoardPlugin.GetBoardState) }
            };
        }

        private static List<string> BuildBoardBuilderTools(bool allowDestructiveChanges)
        {
            var tools = new List<string>
            {
                nameof(BoardPlugin.GetBoardState),
                nameof(BoardPlugin.CreateNote),
                nameof(BoardPlugin.CreateNotes),
                nameof(BoardPlugin.CreateConnection),
                nameof(BoardPlugin.CreateConnections),
                nameof(BoardPlugin.EditNoteText),
                nameof(BoardPlugin.MoveNotes),
            };

            if (allowDestructiveChanges)
            {
                tools.Add(nameof(BoardPlugin.DeleteNotes));
            }

            return tools;
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

        private async Task<(string Reply, List<AgentToolCallDto> ToolCalls)> InvokeAgentAsync(
            Guid boardId, AgentType agentType, bool allowDestructiveChanges, DelegationPlugin? delegationPlugin, CancellationToken cancellationToken)
        {
            var agent = BuildAgent(boardId, agentType, allowDestructiveChanges, delegationPlugin);
            List<ChatMessage> messages;

            if (_conversationHistories.TryGetValue(boardId, out var history))
            {
                lock (history)
                {
                    messages = new List<ChatMessage>(history);
                }
            }
            else
            {
                messages = new List<ChatMessage>();
            }

            var captureKey = $"{boardId}:{agentType}";
            _toolCallFilter.BeginCapture(captureKey);
            var response = await agent.RunAsync(messages, cancellationToken: cancellationToken).ConfigureAwait(false);
            return (response.Text ?? string.Empty, _toolCallFilter.EndCapture(captureKey));
        }

        private async Task<(string Reply, List<AgentToolCallDto> ToolCalls)> InvokeSpecialistAsync(
            Guid boardId, AgentType agentType, string instructions, bool allowDestructiveChanges, CancellationToken cancellationToken)
        {
            var agent = BuildAgent(boardId, agentType, allowDestructiveChanges);
            var messages = new List<ChatMessage>
            {
                new(ChatRole.User, instructions)
            };

            var captureKey = $"{boardId}:{agentType}";
            _toolCallFilter.BeginCapture(captureKey);
            var response = await agent.RunAsync(messages, cancellationToken: cancellationToken).ConfigureAwait(false);
            return (response.Text ?? string.Empty, _toolCallFilter.EndCapture(captureKey));
        }

        private AgentChatMessageDto AppendAssistantMessage(Guid boardId, string reply, List<AgentToolCallDto> toolCalls, AgentType agentType)
        {
            var conversationHistory = _conversationHistories.GetOrAdd(boardId, static _ => new List<ChatMessage>());
            var displayHistory = _displayHistories.GetOrAdd(boardId, static _ => new List<AgentChatMessageDto>());
            var identity = AgentIdentity.ForType(agentType);

            // Only append to conversation history for the Facilitator (stateful)
            // Specialists are stateless — their summaries are injected separately
            if (agentType == AgentType.Facilitator)
            {
                lock (conversationHistory)
                {
                    conversationHistory.Add(new ChatMessage(ChatRole.Assistant, reply));
                }
            }

            var displayAssistantMessage = new AgentChatMessageDto
            {
                Role = "assistant",
                AgentName = identity.Name,
                Content = reply,
                ToolCalls = toolCalls.Count > 0 ? toolCalls : null,
                Timestamp = DateTime.UtcNow
            };

            lock (displayHistory)
            {
                displayHistory.Add(displayAssistantMessage);
            }

            return displayAssistantMessage;
        }

        private string BuildAutonomousPrompt(Guid boardId, string triggerReason)
        {
            var board = _serviceProvider.GetRequiredService<IBoardsRepository>().GetById(boardId);
            var isEarlyFlowLayoutPhase = board?.Phase is EventStormingPhase.IdentifyEvents or EventStormingPhase.AddCommandsAndPolicies;
            var needsSessionIntroduction = board is not null &&
                string.IsNullOrWhiteSpace(board.Domain) &&
                string.IsNullOrWhiteSpace(board.SessionScope) &&
                board.Notes.Count == 0 &&
                board.Connections.Count == 0 &&
                board.Phase is null or EventStormingPhase.SetContext;
            var phaseGuidance = board?.Phase switch
            {
                EventStormingPhase.SetContext => "If this phase has just started, explain that participants should clarify the domain boundary, business goal, actors, and scope before modeling the flow.",
                EventStormingPhase.IdentifyEvents => "If this phase has just started, explain that participants should brainstorm domain events in past tense, place them chronologically, and keep adding more events themselves after your starter example (max 3). They should think of as many events as possible before moving on to adding commands or policies.",
                EventStormingPhase.AddCommandsAndPolicies => "If this phase has just started, explain that participants should pick one existing Event at a time and connect it back to what triggered it using a Command or Policy. Your first starter example must cover exactly one Event, not the whole happy path. Prefer a user-invoked Command with a User note when the domain plausibly supports one, and explain the difference between a user-invoked Command, an automated Command, and a Policy before asking participants to continue the rest themselves.",
                EventStormingPhase.DefineAggregates => "If this phase has just started, explain that participants should identify the core aggregates that own behavior around existing commands and events. Do not add ReadModels unless someone explicitly asks for them.",
                EventStormingPhase.BreakItDown => "If this phase has just started, explain that participants should identify bounded contexts, subdomains, and integration boundaries rather than adding lots of new notes.",
                _ => "If the current phase has just started, explain the phase goal and what participants should do before or alongside your first example."
            };

            var sessionBootstrap = needsSessionIntroduction
                ? "This is a brand new blank session. Ensure the session starts in SetContext. Your very first reply must do four things in this order: (1) briefly explain the high-level Event Storming process and the main phases, (2) explain the house rules for how participants should contribute, (3) explain why the team is working this way, and only then (4) ask one concrete question to capture the missing domain and scope context. Do not skip the introduction and do not jump straight to the question."
                : string.Empty;

            var layoutGuidance = isEarlyFlowLayoutPhase
                ? "When adding or rearranging notes in this phase, leave generous room between events or clusters so users can keep extending the board. If notes are getting cramped, use MoveNotes to spread them out."
                : string.Empty;

            return $"""
                Autonomous facilitator loop trigger: {triggerReason}.

                Review the board and recent activity before acting.
                Call GetRecentEvents as well as GetBoardState so you can tell whether a phase has just started or changed.
                {phaseGuidance}
                {sessionBootstrap}
                {layoutGuidance}
                Ask at most one concise question or make one small facilitation move unless users have explicitly asked for broader changes.
                By default, autonomous facilitation should prefer spreading out existing events to make collaboration easier, asking questions that get participants thinking, suggesting improvements, or suggesting that the group moves to the next phase if the current phase looks complete.
                When a phase has just started (or you have started it), or when the users have asked for help with the phase, you may create a very small starter example to teach how the phase works. Keep that example intentionally small, then stop and hand back to the users.
                In AddCommandsAndPolicies, one small facilitation move means one Event starter example only. Do not continue into neighboring Events after that example.
                Autonomous facilitation must not delete existing notes or connections. If something looks wrong, mention it, add a Concern, or ask the users before making destructive changes.
                Avoid rewriting existing work unless the users explicitly ask you to; prefer suggestions over large unsolicited changes.
                Do not create ReadModel notes unless a user explicitly asks for them or the facilitator instructions clearly require them.
                If there is nothing meaningful to add right now, do not call any tools and return an empty response.
                If the workshop is clearly complete, use the CompleteAutonomousSession tool.
                """;
        }

        private void EnsureInitialAutonomousPhase(Guid boardId)
        {
            var repository = _serviceProvider.GetRequiredService<IBoardsRepository>();
            var board = repository.GetById(boardId);
            if (board == null || board.Phase.HasValue ||
                !string.IsNullOrWhiteSpace(board.Domain) ||
                !string.IsNullOrWhiteSpace(board.SessionScope))
            {
                return;
            }

            var @event = new BoardContextUpdatedEvent
            {
                BoardId = boardId,
                OldDomain = board.Domain,
                NewDomain = board.Domain,
                OldSessionScope = board.SessionScope,
                NewSessionScope = board.SessionScope,
                OldAgentInstructions = board.AgentInstructions,
                NewAgentInstructions = board.AgentInstructions,
                OldPhase = board.Phase,
                NewPhase = EventStormingPhase.SetContext,
                OldAutonomousEnabled = board.AutonomousEnabled,
                NewAutonomousEnabled = board.AutonomousEnabled
            };

            _serviceProvider.GetRequiredService<IBoardEventPipeline>().ApplyAndLog(@event, "AI Agent");
            _serviceProvider.GetRequiredService<IHubContext<BoardsHub>>()
                .Clients.Group(boardId.ToString())
                .SendAsync("BoardContextUpdated", @event);
        }

        private static string BuildSystemPrompt(Board? board, AgentType agentType)
        {
            var basePrompt = agentType switch
            {
                AgentType.Facilitator => FacilitatorSystemPrompt,
                AgentType.BoardBuilder => BoardBuilderSystemPrompt,
                AgentType.BoardReviewer => BoardReviewerSystemPrompt,
                _ => FacilitatorSystemPrompt
            };

            if (board == null)
            {
                return basePrompt;
            }

            var sb = new StringBuilder(basePrompt);

            if (!string.IsNullOrWhiteSpace(board.Domain))
            {
                sb.AppendLine();
                sb.AppendLine("--- DOMAIN CONTEXT ---");
                sb.AppendLine("You are facilitating an Event Storming session for the following domain:");
                sb.AppendLine(board.Domain);
            }

            if (!string.IsNullOrWhiteSpace(board.SessionScope))
            {
                sb.AppendLine();
                sb.AppendLine("--- SESSION SCOPE ---");
                sb.AppendLine("This session is focused on:");
                sb.AppendLine(board.SessionScope);
            }

            if (!string.IsNullOrWhiteSpace(board.AgentInstructions))
            {
                sb.AppendLine();
                sb.AppendLine("--- FACILITATOR INSTRUCTIONS ---");
                sb.AppendLine("Additional instructions from the facilitator:");
                sb.AppendLine(board.AgentInstructions);
            }

            if (board.Phase.HasValue)
            {
                sb.AppendLine();
                sb.AppendLine("--- CURRENT PHASE ---");
                sb.AppendLine($"The board is currently in the **{board.Phase.Value}** phase.");
                sb.AppendLine(board.Phase.Value switch
                {
                    EventStormingPhase.SetContext => "Focus on understanding the domain and session scope. Ask clarifying questions about boundaries, actors, and processes before adding notes.",
                    EventStormingPhase.IdentifyEvents => "Focus on brainstorming domain Events. Start by explaining that participants should add past-tense events in chronological order and keep building on your example. Order events left to right, leave enough space for future Commands and Policies, and use MoveNotes if the timeline is getting cramped. Add Concern notes for open questions. Do not add Commands, Policies, or Aggregates yet.",
                    EventStormingPhase.AddCommandsAndPolicies => "Focus on adding Commands and Policies for each Event. Start by explaining that participants should identify what triggered one Event at a time and repeat the same pattern after your example. Your first example in this phase should cover exactly one Event, preferably with a user-invoked Command and a User note if that makes sense in the domain. Explicitly explain how a user-invoked Command differs from an automated Command and how a Policy reacts after an Event. Stop after that one example and ask participants to continue the remaining Events. Leave enough space around the cluster and use MoveNotes if the board is getting crowded. Do not add Aggregates or ReadModels unless the user explicitly asks for them.",
                    EventStormingPhase.DefineAggregates => "Focus on defining Aggregates. Start by explaining that participants should identify which aggregate owns the behavior around existing commands and events. Place Aggregates between Commands and Events. Do not add ReadModels unless the user explicitly asks for them or the facilitator instructions clearly require them.",
                    EventStormingPhase.BreakItDown => "Focus on grouping related flows into Bounded Contexts and Subdomains. Identify Integration Events that flow between contexts.",
                    _ => string.Empty
                });
            }

            return sb.ToString();
        }
    }
}
