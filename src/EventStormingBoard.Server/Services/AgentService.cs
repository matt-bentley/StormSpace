using EventStormingBoard.Server.Entities;
using EventStormingBoard.Server.Events;
using EventStormingBoard.Server.Hubs;
using EventStormingBoard.Server.Models;
using EventStormingBoard.Server.Repositories;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.AI;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace EventStormingBoard.Server.Services
{
    public interface IAgentService
    {
        Task ChatAsync(Guid boardId, string userMessage, string userName);
        Task<AutonomousAgentResult> RunAutonomousTurnAsync(Guid boardId, string triggerReason, CancellationToken cancellationToken);
        List<AgentChatMessageDto> GetHistory(Guid boardId);
        void ClearHistory(Guid boardId);
    }

    public sealed class AgentService : IAgentService
    {
        private const string BaseSystemPrompt = """
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

            ## Positioning Guidelines

            - Always place notes in a logical left-to-right flow (following time).
            - **Within a cluster** (e.g., Command → Event → Policy), use a **30 px gap** between notes (150 px centre-to-centre for 120 px notes). Keep all notes in a cluster on the same vertical level.
            - **Between clusters** (e.g., after the last note of one flow group and before the first note of the next), leave a **160 px gap** (280 px centre-to-centre) so that distinct flow groups are visually separated.
            - **Aggregates** are placed directly above their Command/Event pair with only a **10 px vertical gap** (130 px centre-to-centre).
            - **External Systems** are placed directly below their Command/Event pair with only a **10 px vertical gap** (130 px centre-to-centre).
            - **User notes** (60×60) are attached to the bottom-left corner of their Command or manual Policy, overlapping slightly.
            - Start entirely **separate flow rows 500 px** apart vertically. Entirely separate flows should still be arranged horizontally in-line with other flows based on possible timings, but with a large vertical gap to show they are different threads of flow.

            ### Note Sizes

            - Most notes (Event, Command, Aggregate, Policy, ReadModel, ExternalSystem, Concern) are **120×120 px**.
            - User notes are **60×60 px** (smaller, as they attach to the corner of another note).

            ## Workshop Phases

            Event Storming follows a specific top-down order. When helping users build a board, guide them through these phases:

            1. **Set the Context**: Start by understanding the user's domain and the scope of the session. Ask questions to clarify the boundaries and focus of the Event Storming workshop, if it isn't already clear. This will be set in **DOMAIN CONTEXT** and **SESSION SCOPE** sections of the system prompt.
            2. **Identify Events**: First brainstorm all possible Events that could happen in the domain. Order them by time (left to right). Events happening simultaneously go in parallel (vertically). Add Concerns for any open questions.
            3. **Add Commands and Policies**: For each Event, determine what triggers it — a manual Command (from a User), an automated Command, an External System, or time. Add Policies where business rules create branching logic after Events.
            4. **Define Aggregates**: Determine which Aggregates (data structures) are needed to model the domain. Place them between Commands and Events to show which data structure handles the command. Also add Read Models if relevant.
            5. **Break it down**: Group related flows into Bounded Contexts and Subdomains. Events flowing between contexts are Integration Events; events within a single context are Domain Events. This phase cannot be fully supported in StormSpace, however you can respond with recommendations for bounded contexts and subdomains.

            ## Your Role

            1. Always call GetBoardState first to understand what's currently on the board before making suggestions.
            2. When asked to suggest events, commands, etc., create them as notes on the board using the available tools.
            3. **Prefer batch operations**: use `CreateNotes` to create all notes in a single call, then `CreateConnections` to wire them up — this is much faster than creating them one at a time.
            4. After creating related notes, connect them with arrows following the valid flow patterns (e.g., Command → Event, Event → Policy, Policy → Command).
            5. Use proper Event Storming naming: events in past tense, commands as imperative verbs, aggregates as nouns, policies as "When X then Y" rules.
            6. Be collaborative — build on what's already on the board rather than starting from scratch.
            7. When the user asks a question, answer it conversationally. Only create/modify notes when asked to do so.
            8. If the board is empty, ask the user about their domain before adding notes, unless they've already described it.
            9. Guide users through the workshop phases in order — start with Events, then add Commands/Policies, then Aggregates/Read Models. Don't automatically jump ahead to later phases, unless the user explicitly asks you to.
            10. When adding notes to the board, proactively add **Concern** notes near relevant areas if you spot important gaps, open questions, rule/standards breaches, ambiguities, or potential issues in the flow.
            11. Keep domain language accessible to business experts. Avoid technical jargon.
            12. Stick to a single phase in the process for each iteration of the board, unless the user explicitly asks you to jump around. For example, if you're currently working on identifying Events, don't start adding Aggregates until the user asks you to, or until you've identified all key Events and their Commands/Policies.
            13. At the start of a phase, briefly explain the phase goal, what participants should be doing, what good contributions look like, and what small example you are about to add.
            14. In **Identify Events** and **Add Commands and Policies**, leave generous horizontal space so users can continue the board. If notes or clusters are getting crowded, proactively use `MoveNotes` to spread them out before or after adding more notes.
            15. Do **not** create `ReadModel` notes unless a user explicitly asks for them or the facilitator instructions clearly require them.
            16. At the beginning of a new session, introduce the Event Storming workshop before asking for detailed input. Explain the high-level process, the overall phases, and how participants should collaborate with you.
            17. At the beginning of a new session, explain the house rules clearly: use business language, keep notes focused to one idea each, work left-to-right in time order, challenge ambiguity with Concern notes, and avoid jumping ahead to later modeling steps too early.
            18. When introducing the session, explain why the team is working this way: Event Storming helps align people around behavior, exposes gaps and assumptions early, and prevents premature technical or data-model discussions.
            19. In **Add Commands and Policies**, your first example must focus on exactly **one existing Event**, not a whole happy path or multiple neighboring Events.
            20. In **Add Commands and Policies**, prefer the first starter example to be a **user-invoked Command** when the domain plausibly supports one. Add the matching **User** note attached to that Command so participants can see how manual triggers are represented.
            21. When starting **Add Commands and Policies**, explicitly explain the difference between: a user-invoked Command with a User note, an automated Command without a User note, and a Policy that reacts after an Event.
            22. After adding the first Commands/Policies starter example, stop and ask participants to model the remaining Events themselves using the same pattern.

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

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly IServiceProvider _serviceProvider;
        private readonly IAgentExecutor _agentExecutor;
        private readonly IAgentChatBroadcaster _chatBroadcaster;
        private readonly ConcurrentDictionary<Guid, List<ChatMessage>> _conversationHistories = new();

        public AgentService(
            IServiceProvider serviceProvider,
            IAgentExecutor agentExecutor,
            IAgentChatBroadcaster chatBroadcaster)
        {
            _serviceProvider = serviceProvider;
            _agentExecutor = agentExecutor;
            _chatBroadcaster = chatBroadcaster;
        }

        public async Task ChatAsync(Guid boardId, string userMessage, string userName)
        {
            var conversationHistory = _conversationHistories.GetOrAdd(boardId, static _ => new List<ChatMessage>());

            lock (conversationHistory)
            {
                conversationHistory.Add(new ChatMessage(ChatRole.User, userMessage));
            }

            await RunOrchestratedTurnAsync(new OrchestrationTurnRequest(
                boardId,
                AllowDestructiveChanges: true,
                IsAutonomous: false,
                TriggerReason: null), CancellationToken.None).ConfigureAwait(false);
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

            var turnResult = await RunOrchestratedTurnAsync(new OrchestrationTurnRequest(
                boardId,
                AllowDestructiveChanges: false,
                IsAutonomous: true,
                TriggerReason: triggerReason), timeoutCts.Token).ConfigureAwait(false);
            var board = _serviceProvider.GetRequiredService<IBoardsRepository>().GetById(boardId);
            var trimmedReply = turnResult.LastVisibleMessage?.Trim() ?? string.Empty;
            var usedCompletionTool = turnResult.ToolCalls.Any(call => string.Equals(call.Name, nameof(Plugins.BoardPlugin.CompleteAutonomousSession), StringComparison.Ordinal));

            if (board?.AutonomousEnabled == false && usedCompletionTool)
            {
                var visibleMessage = string.IsNullOrWhiteSpace(trimmedReply)
                    ? "The session looks complete, so I’m pausing autonomous facilitation here."
                    : trimmedReply;

                return new AutonomousAgentResult
                {
                    Status = AutonomousAgentStatus.Complete,
                    TriggerReason = triggerReason,
                    VisibleMessage = visibleMessage,
                    ToolCalls = turnResult.ToolCalls,
                    StopReason = "sessionComplete"
                };
            }

            if (string.IsNullOrWhiteSpace(trimmedReply) && turnResult.ToolCalls.Count == 0)
            {
                return new AutonomousAgentResult
                {
                    Status = AutonomousAgentStatus.Idle,
                    TriggerReason = triggerReason,
                    ToolCalls = turnResult.ToolCalls,
                    Diagnostics = "No visible reply and no tool activity."
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
                ToolCalls = turnResult.ToolCalls
            };
        }

        public List<AgentChatMessageDto> GetHistory(Guid boardId) => _chatBroadcaster.GetHistory(boardId);

        public void ClearHistory(Guid boardId)
        {
            _conversationHistories.TryRemove(boardId, out _);
            _chatBroadcaster.ClearHistory(boardId);
        }

        private sealed record OrchestrationTurnRequest(Guid BoardId, bool AllowDestructiveChanges, bool IsAutonomous, string? TriggerReason);

        private sealed class OrchestratedTurnResult
        {
            public string? LastVisibleMessage { get; set; }
            public List<AgentToolCallDto> ToolCalls { get; } = new();
            public List<string> ConversationSummaries { get; } = new();
        }

        private async Task<OrchestratedTurnResult> RunOrchestratedTurnAsync(OrchestrationTurnRequest request, CancellationToken cancellationToken)
        {
            var result = new OrchestratedTurnResult();
            var board = _serviceProvider.GetRequiredService<IBoardsRepository>().GetById(request.BoardId);
            var planner = FacilitatorAgentCatalog.Planner;
            var plannerExecution = await ExecuteAgentAsync(
                request.BoardId,
                planner,
                BuildPlannerInstructions(board, request),
                AgentToolPolicy.GetToolMethodNames(planner.Id, allowDestructiveChanges: false),
                cancellationToken).ConfigureAwait(false);

            result.ToolCalls.AddRange(plannerExecution.ToolCalls);

            var plannerDecision = NormalizePlannerDecision(ParsePlannerDecision(plannerExecution.Text));
            if (!string.IsNullOrWhiteSpace(plannerDecision.VisibleMessage) || plannerExecution.ToolCalls.Count > 0)
            {
                var plannerMessage = CreateAgentMessage(
                    plannerExecution.Scope,
                    plannerDecision.Steps.Count > 0 ? AgentMessageKinds.Plan : AgentMessageKinds.Response,
                    EnsureVisibleMessage(planner.Id, plannerDecision.VisibleMessage, plannerExecution.ToolCalls),
                    plannerExecution.ToolCalls);

                await _chatBroadcaster.BroadcastAgentMessageAsync(request.BoardId, plannerMessage, cancellationToken).ConfigureAwait(false);
                result.LastVisibleMessage = plannerMessage.Content;
            }

            if (!string.IsNullOrWhiteSpace(plannerDecision.Summary))
            {
                result.ConversationSummaries.Add($"{planner.Name}: {plannerDecision.Summary.Trim()}");
            }

            foreach (var step in NormalizeSteps(plannerDecision.Steps))
            {
                if (!string.IsNullOrWhiteSpace(step.HandoffMessage))
                {
                    await _chatBroadcaster.BroadcastAgentMessageAsync(request.BoardId, CreateAgentMessage(
                        plannerExecution.Scope,
                        AgentMessageKinds.Handoff,
                        step.HandoffMessage.Trim(),
                        toolCalls: null), cancellationToken).ConfigureAwait(false);
                }

                var specialist = FacilitatorAgentCatalog.Get(step.AgentId);
                var specialistExecution = await ExecuteAgentAsync(
                    request.BoardId,
                    specialist,
                    BuildSpecialistInstructions(board, request, specialist, step, result.ConversationSummaries),
                    AgentToolPolicy.GetToolMethodNames(specialist.Id, request.AllowDestructiveChanges),
                    cancellationToken).ConfigureAwait(false);

                result.ToolCalls.AddRange(specialistExecution.ToolCalls);

                var specialistResponse = NormalizeSpecialistResponse(ParseSpecialistResponse(specialistExecution.Text));
                if (!string.IsNullOrWhiteSpace(specialistResponse.VisibleMessage) || specialistExecution.ToolCalls.Count > 0)
                {
                    var specialistMessage = CreateAgentMessage(
                        specialistExecution.Scope,
                        AgentMessageKinds.Response,
                        EnsureVisibleMessage(specialist.Id, specialistResponse.VisibleMessage, specialistExecution.ToolCalls),
                        specialistExecution.ToolCalls);

                    await _chatBroadcaster.BroadcastAgentMessageAsync(request.BoardId, specialistMessage, cancellationToken).ConfigureAwait(false);
                    result.LastVisibleMessage = specialistMessage.Content;
                }

                var summary = string.IsNullOrWhiteSpace(specialistResponse.Summary)
                    ? EnsureVisibleMessage(specialist.Id, specialistResponse.VisibleMessage, specialistExecution.ToolCalls)
                    : specialistResponse.Summary.Trim();

                result.ConversationSummaries.Add($"{specialist.Name}: {summary}");
            }

            AppendAssistantSummaryToConversation(request.BoardId, result.ConversationSummaries);
            return result;
        }

        private async Task<AgentExecutionResult> ExecuteAgentAsync(
            Guid boardId,
            FacilitatorAgentDescriptor definition,
            string instructions,
            IReadOnlyList<string> toolNames,
            CancellationToken cancellationToken)
        {
            return await _agentExecutor.ExecuteAsync(new AgentExecutionRequest
            {
                BoardId = boardId,
                Definition = definition,
                Instructions = instructions,
                ToolNames = toolNames,
                Messages = GetConversationSnapshot(boardId),
                Scope = CreateExecutionScope(boardId, definition)
            }, cancellationToken).ConfigureAwait(false);
        }

        private List<ChatMessage> GetConversationSnapshot(Guid boardId)
        {
            if (_conversationHistories.TryGetValue(boardId, out var history))
            {
                lock (history)
                {
                    return new List<ChatMessage>(history);
                }
            }

            return new List<ChatMessage>();
        }

        private void AppendAssistantSummaryToConversation(Guid boardId, IReadOnlyList<string> summaries)
        {
            if (summaries.Count == 0)
            {
                return;
            }

            var history = _conversationHistories.GetOrAdd(boardId, static _ => new List<ChatMessage>());
            var summaryText = string.Join(Environment.NewLine, summaries);

            lock (history)
            {
                history.Add(new ChatMessage(ChatRole.Assistant, summaryText));
            }
        }

        private static AgentExecutionScope CreateExecutionScope(Guid boardId, FacilitatorAgentDescriptor definition)
        {
            return new AgentExecutionScope(boardId, Guid.NewGuid().ToString("N"), definition.Id, definition.Name);
        }

        private static AgentChatMessageDto CreateAgentMessage(
            AgentExecutionScope scope,
            string messageKind,
            string content,
            List<AgentToolCallDto>? toolCalls)
        {
            return new AgentChatMessageDto
            {
                Role = "assistant",
                AgentId = scope.AgentId,
                AgentName = scope.AgentName,
                MessageKind = messageKind,
                ExecutionId = scope.ExecutionId,
                Content = content,
                ToolCalls = toolCalls is { Count: > 0 } ? toolCalls : null,
                Timestamp = DateTime.UtcNow
            };
        }

        private string BuildPlannerInstructions(Board? board, OrchestrationTurnRequest request)
        {
            var sb = new StringBuilder(BuildSystemPrompt(board));
            sb.AppendLine();
            sb.AppendLine("--- PLANNER AGENT ROLE ---");
            sb.AppendLine("You are PlannerAgent. Inspect the board, decide the next specialist steps for this turn, and emit visible handoffs.");
            sb.AppendLine("You are read-only. Do not edit the board directly. Use tools only to inspect the board state and recent events.");
            sb.AppendLine("Choose zero or more specialists, in order, from these exact agent IDs:");
            sb.AppendLine($"- {FacilitatorAgentIds.BoardAnalyst}: inspect board structure, gaps, and modeling issues.");
            sb.AppendLine($"- {FacilitatorAgentIds.FacilitationCoach}: explain the current phase and coach participants on what to do next.");
            sb.AppendLine($"- {FacilitatorAgentIds.BoardEditor}: make note, connection, movement, domain, scope, phase, or autonomous-completion changes.");
            sb.AppendLine("Only choose BoardEditor when the turn requires an actual board mutation or explicit board-context update.");
            sb.AppendLine("Prefer short sequences. Most turns should use zero, one, or two specialists.");
            sb.AppendLine("If you can answer directly without a specialist, return no steps.");

            if (request.IsAutonomous)
            {
                sb.AppendLine();
                sb.AppendLine("--- AUTONOMOUS TURN ---");
                sb.AppendLine($"This is an autonomous facilitation turn triggered by: {request.TriggerReason ?? "timer"}.");
                sb.AppendLine("Autonomous turns must stay small, safe, and incremental.");
                sb.AppendLine("Do not plan destructive cleanup. Prefer one small facilitation move, one concise question, or one targeted board update.");
            }

            sb.AppendLine();
            sb.AppendLine("Return JSON only. Do not wrap it in markdown fences.");
            sb.AppendLine("Schema:");
            sb.AppendLine("{");
            sb.AppendLine("  \"visibleMessage\": \"string\",");
            sb.AppendLine("  \"summary\": \"short summary for future context\",");
            sb.AppendLine("  \"steps\": [");
            sb.AppendLine("    {");
            sb.AppendLine("      \"agentId\": \"board-analyst|facilitation-coach|board-editor\",");
            sb.AppendLine("      \"goal\": \"specific instruction for the specialist\",");
            sb.AppendLine("      \"handoffMessage\": \"visible handoff message shown in chat\",");
            sb.AppendLine("      \"contextSummary\": \"compact context summary for the specialist\"");
            sb.AppendLine("    }");
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private string BuildSpecialistInstructions(
            Board? board,
            OrchestrationTurnRequest request,
            FacilitatorAgentDescriptor specialist,
            PlannerStepDecision step,
            IReadOnlyList<string> priorSummaries)
        {
            var sb = new StringBuilder(BuildSystemPrompt(board));
            sb.AppendLine();
            sb.AppendLine($"--- {specialist.Name.ToUpperInvariant()} ROLE ---");

            switch (specialist.Id)
            {
                case FacilitatorAgentIds.BoardAnalyst:
                    sb.AppendLine("You are BoardAnalystAgent. Inspect the board and recent events, identify gaps or risks, and stay read-only.");
                    sb.AppendLine("Do not propose unrelated changes. Focus on the planner goal.");
                    break;
                case FacilitatorAgentIds.FacilitationCoach:
                    sb.AppendLine("You are FacilitationCoachAgent. Explain the current workshop phase, what good contributions look like, and the next step participants should take.");
                    sb.AppendLine("Stay participant-facing and concise.");
                    break;
                case FacilitatorAgentIds.BoardEditor:
                    sb.AppendLine("You are BoardEditorAgent. Make only the targeted board changes needed for this step, then explain what changed and why.");
                    sb.AppendLine(request.AllowDestructiveChanges
                        ? "Interactive mode allows destructive changes when clearly requested."
                        : "Destructive tools are unavailable in this run. Do not delete notes.");
                    break;
            }

            sb.AppendLine();
            sb.AppendLine("Planner goal for this step:");
            sb.AppendLine(string.IsNullOrWhiteSpace(step.Goal) ? "Follow the planner's latest context and help advance the board safely." : step.Goal.Trim());

            if (!string.IsNullOrWhiteSpace(step.ContextSummary))
            {
                sb.AppendLine();
                sb.AppendLine("Planner context summary:");
                sb.AppendLine(step.ContextSummary.Trim());
            }

            if (priorSummaries.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Compact summaries from earlier steps in this turn:");
                foreach (var summary in priorSummaries)
                {
                    sb.AppendLine($"- {summary}");
                }
            }

            if (request.IsAutonomous)
            {
                sb.AppendLine();
                sb.AppendLine("Autonomous run constraints: stay incremental, avoid destructive cleanup, and prefer one small move over a broad rewrite.");
            }

            sb.AppendLine();
            sb.AppendLine("Return JSON only. Do not wrap it in markdown fences.");
            sb.AppendLine("Schema:");
            sb.AppendLine("{");
            sb.AppendLine("  \"visibleMessage\": \"assistant message shown in the chat\",");
            sb.AppendLine("  \"summary\": \"short summary for future agent context\"");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static PlannerDecision ParsePlannerDecision(string responseText)
        {
            if (TryDeserialize(responseText, out PlannerDecision? decision) && decision is not null)
            {
                decision.Steps ??= new List<PlannerStepDecision>();
                return decision;
            }

            var trimmed = responseText.Trim();
            return new PlannerDecision
            {
                VisibleMessage = trimmed,
                Summary = trimmed,
                Steps = new List<PlannerStepDecision>()
            };
        }

        private static SpecialistResponse ParseSpecialistResponse(string responseText)
        {
            if (TryDeserialize(responseText, out SpecialistResponse? response) && response is not null)
            {
                return response;
            }

            var trimmed = responseText.Trim();
            return new SpecialistResponse
            {
                VisibleMessage = trimmed,
                Summary = trimmed
            };
        }

        private static bool TryDeserialize<T>(string responseText, out T? value)
        {
            try
            {
                value = JsonSerializer.Deserialize<T>(StripCodeFences(responseText), JsonOptions);
                return value is not null;
            }
            catch (JsonException)
            {
                value = default;
                return false;
            }
        }

        private static string StripCodeFences(string value)
        {
            var trimmed = value.Trim();
            if (!trimmed.StartsWith("```", StringComparison.Ordinal) || !trimmed.EndsWith("```", StringComparison.Ordinal))
            {
                return trimmed;
            }

            var lines = trimmed.Split('\n').ToList();
            if (lines.Count >= 2)
            {
                lines.RemoveAt(0);
                lines.RemoveAt(lines.Count - 1);
            }

            return string.Join('\n', lines).Trim();
        }

        private static PlannerDecision NormalizePlannerDecision(PlannerDecision decision)
        {
            decision.VisibleMessage = NormalizeText(decision.VisibleMessage);
            decision.Summary = NormalizeText(decision.Summary) ?? decision.VisibleMessage;
            decision.Steps ??= new List<PlannerStepDecision>();
            return decision;
        }

        private static SpecialistResponse NormalizeSpecialistResponse(SpecialistResponse response)
        {
            response.VisibleMessage = NormalizeText(response.VisibleMessage);
            response.Summary = NormalizeText(response.Summary) ?? response.VisibleMessage;
            return response;
        }

        private static List<PlannerStepDecision> NormalizeSteps(IEnumerable<PlannerStepDecision>? steps)
        {
            return (steps ?? Enumerable.Empty<PlannerStepDecision>())
                .Where(step => FacilitatorAgentCatalog.IsKnown(step.AgentId) && !string.Equals(step.AgentId, FacilitatorAgentIds.Planner, StringComparison.OrdinalIgnoreCase))
                .Take(4)
                .Select(step => new PlannerStepDecision
                {
                    AgentId = step.AgentId.Trim().ToLowerInvariant(),
                    Goal = NormalizeText(step.Goal),
                    HandoffMessage = NormalizeText(step.HandoffMessage),
                    ContextSummary = NormalizeText(step.ContextSummary)
                })
                .ToList();
        }

        private static string EnsureVisibleMessage(string agentId, string? message, IReadOnlyCollection<AgentToolCallDto> toolCalls)
        {
            var normalized = NormalizeText(message);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                return normalized;
            }

            if (toolCalls.Count == 0)
            {
                return agentId switch
                {
                    FacilitatorAgentIds.Planner => "I reviewed the board and mapped the next step.",
                    FacilitatorAgentIds.BoardAnalyst => "I reviewed the board and noted the current structure.",
                    FacilitatorAgentIds.FacilitationCoach => "I reviewed the current phase and the next facilitation step.",
                    FacilitatorAgentIds.BoardEditor => "I checked the board and there was nothing to change.",
                    _ => "I completed this step."
                };
            }

            return agentId switch
            {
                FacilitatorAgentIds.BoardEditor => "I made the requested board updates.",
                FacilitatorAgentIds.BoardAnalyst => "I inspected the board and captured the main findings.",
                FacilitatorAgentIds.FacilitationCoach => "I reviewed the phase and prepared the next facilitation guidance.",
                _ => "I completed this step."
            };
        }

        private static string? NormalizeText(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
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

        private static string BuildSystemPrompt(Board? board)
        {
            if (board == null)
            {
                return BaseSystemPrompt;
            }

            var sb = new StringBuilder(BaseSystemPrompt);

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