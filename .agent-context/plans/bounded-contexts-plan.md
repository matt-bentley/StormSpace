I want a way to identify bounded contexts in the final phase of event storming sessions. Usually this is done by using a frame around the relevant notes with a title aoove the top left corner of the frame. There should be UI tools to do this and also AI plugins/prompts so that agents can define bounded contexts.

## Plan: Bounded Context Frames

Add a new `BoundedContext` entity (separate from Notes) that renders as a titled dashed frame on the canvas. Users draw rectangles via a toolbar tool; AI agents (DomainDesigner) get dedicated tools to create/update/delete bounded contexts during the BreakItDown phase. Purely visual — no stored containment relationship with notes.

**Phase 1: Backend Entity & Event Infrastructure** *(no dependencies — start here)*

1. **Entity & DTOs** — Create `BoundedContext.cs` entity (`Id`, `Name`, `X`, `Y`, `Width`, `Height`, `Color`), `BoundedContextDto.cs`, `CreateBoundedContextInput.cs`. Add `List<BoundedContext> BoundedContexts` to `Board.cs` and `BoardDto.cs`
2. **Events** — Create `BoundedContextCreatedEvent` (stores `BoundedContextDto` snapshot + `IsUndo`, following `NoteCreatedEvent` pattern — undo removes it), `BoundedContextDeletedEvent` (stores `BoundedContextDto` snapshot + `IsUndo` — undo re-adds it, following `NotesDeletedEvent` pattern), `BoundedContextUpdatedEvent` (stores old/new values for changed fields, following `NoteTextEditedEvent` pattern)
3. **State Service & Pipeline** *(depends on 1–2)* — Add `ApplyBoundedContextCreated/Updated/Deleted` to `IBoardStateService` + `BoardStateService` using `WithBoard()` pattern. Add 3 case branches to `BoardEventPipeline.Apply()`. Add summary strings to `BoardEventLog.BuildSummary()`
3b. **Backend Tests** *(depends on 3)* — Update spy/stub `IBoardStateService` in test doubles to implement the 3 new `Apply*` methods. Add `BoardEventPipelineTests` for each new event type (apply + undo round-trip). Add `BoardStateServiceTests` for each `Apply*` method. Add `BoardEventLogTests` summary assertions for each new event type
4. **Controller Mapping** *(depends on 1)* — Update `BoardsController.GetById()` and `Post()` to map/initialise bounded contexts

**Phase 2: SignalR Real-Time Sync** *(depends on Phase 1)*

5. **Hub Methods** — Add `BroadcastBoundedContextCreated/Updated/Deleted` to `BoardsHub.cs`, following the exact `BroadcastNoteCreated` pattern (apply & log → record activity → broadcast to others)

**Phase 3: AI Agent Plugins & Prompts** *(depends on Phase 1, parallel with Phases 2 & 4)*

6. **Plugin Tools** — Add to `BoardPlugin.cs`: `CreateBoundedContext(name, x, y, width, height)`, `CreateBoundedContexts(List<CreateBoundedContextInput>)`, `UpdateBoundedContext(id, name?, x?, y?, width?, height?)`, `DeleteBoundedContext(id)` — each follows existing tool patterns. **Update `GetBoardState()`** to include bounded contexts in its text summary (list each frame's ID, name, position, and dimensions so agents can see, reference, update, and avoid duplicating existing frames)
6b. **Destructive-Action Safety** — Update `BoardAgentFactory.cs` destructive-tool filter to suppress `DeleteBoundedContext` alongside `DeleteNotes` when `allowDestructiveChanges` is false, so autonomous agents cannot delete user-created frames
7. **Agent Configuration** — Update `DefaultAgentConfigurations.cs`: add 4 bounded context tools to DomainDesigner's `AllowedTools`; update DomainDesigner system prompt with BreakItDown-phase instructions to use frames for grouping clusters with sizing/naming guidance. **Include a prompt-level guardrail**: instruct DomainDesigner to only create/modify bounded contexts during the BreakItDown phase (tool access is agent-wide, not phase-scoped, so this is enforced via prompt, not code)
8. **Tool Policy Tests** *(depends on 6–7)* — Restructure `AgentServiceToolPolicyTests.cs`: split the existing `Specialists_HaveFullBoardTools` test so DomainDesigner has its own assertion block. Add `Assert.Contains` for all 4 BC tools on DomainDesigner. Add explicit `Assert.DoesNotContain` for BC tools on EventExplorer, TriggerMapper, and Organiser to prove exclusivity. Add a test verifying `DeleteBoundedContext` is suppressed when `allowDestructiveChanges` is false

**Phase 4: Frontend Model & State** *(parallel with Phases 2 & 3)*

9. **Model & DTOs** — Create `bounded-context.model.ts` (`id`, `name`, `x`, `y`, `width`, `height`, `color?`, `selected?`). Add `boundedContexts: BoundedContext[]` to `BoardState` in `board-state.model.ts`. Add `boundedContexts` to the `BoardDto` interface in `board.model.ts` (HTTP response DTO). Add 3 event interfaces (`BoundedContextCreatedEvent`, `BoundedContextUpdatedEvent`, `BoundedContextDeletedEvent`) to `board-events.model.ts`
10. **SignalR Subjects** *(depends on 9)* — Add 3 event Subjects, hub listener registrations, and broadcast methods to `BoardsSignalRService`
11. **Commands & Dispatch** *(depends on 9)* — Add `CreateBoundedContextCommand`, `UpdateBoundedContextCommand`, `DeleteBoundedContextCommand`, `MoveBoundedContextCommand`, `ResizeBoundedContextCommand` to `board.commands.ts`. Update the command-to-SignalR dispatch switch in `board-canvas.service.ts` to handle all 5 new command types. Specify that `MoveBoundedContextCommand` and `ResizeBoundedContextCommand` both serialize to `BoundedContextUpdated` on the wire (single server event, multiple client command types)

**Phase 5: Frontend Canvas Rendering** *(depends on Phase 4)*

12. **Drawing** — In `drawCanvasFrame()`, draw bounded contexts BEFORE notes (renders behind). Style: 2px dashed border (`setLineDash([8, 4])`), semi-transparent fill (~8% opacity), title in Space Grotesk ALL CAPS above top-left corner. Selected state gets cyan glow
13. **Minimap** — Draw bounded context outlines on minimap. **Update `getCanvasBoundsAndScale()`** to include bounded context positions/dimensions in the extents calculation (currently notes-only), so frames extending beyond notes are not clipped

**Phase 6: Frontend Canvas Interaction** *(depends on Phases 4 & 5)*

14. **Draw-to-Create Mode** — New `isDrawingBoundedContext` flag on `BoardCanvasService`. mouseDown records start, mouseMove draws preview, mouseUp creates the command. Prompt for name after drawing (dialog)
15. **Selection, Drag, Resize** — Click detection for bounded contexts (checked AFTER notes since frames are behind). Resize corners, drag handling, Delete key. Double-click on title to edit name
16. **Toolbar Button** — Add "CONTEXT" button to toolbar with `crop_free` Material icon. Click activates draw mode, styled with design-system accent
17. **Board Component Wiring** *(depends on 10, 14)* — Subscribe to SignalR events, apply as server-invoked commands. Update initial board load mapping in `board.component.ts` to hydrate `boardState.boundedContexts` from the `BoardDto` response

**Relevant files**

- [Board.cs](src/EventStormingBoard.Server/Entities/Board.cs) — add `BoundedContexts` collection
- [BoardDto.cs](src/EventStormingBoard.Server/Models/BoardDto.cs) — add `BoundedContexts` list
- [BoardEventPipeline.cs](src/EventStormingBoard.Server/Services/BoardEventPipeline.cs) — add 3 case branches to `Apply()` switch
- [BoardStateService.cs](src/EventStormingBoard.Server/Services/BoardStateService.cs) — add 3 `Apply*` methods using `WithBoard()` pattern
- [BoardEventLog.cs](src/EventStormingBoard.Server/Services/BoardEventLog.cs) — add summaries to `BuildSummary()` switch
- [BoardsController.cs](src/EventStormingBoard.Server/Controllers/BoardsController.cs) — update DTO mapping in `GetById()` and `Post()`
- [BoardsHub.cs](src/EventStormingBoard.Server/Hubs/BoardsHub.cs) — add 3 broadcast methods
- [BoardPlugin.cs](src/EventStormingBoard.Server/Agents/Plugins/BoardPlugin.cs) — add 4 tool methods
- [DefaultAgentConfigurations.cs](src/EventStormingBoard.Server/Agents/DefaultAgentConfigurations.cs) — update DomainDesigner tools + prompt
- [board-state.model.ts](src/eventstormingboard.client/src/app/_shared/models/board-state.model.ts) — add `boundedContexts` array
- [board.model.ts](src/eventstormingboard.client/src/app/_shared/models/board.model.ts) — add `boundedContexts` to `BoardDto` interface
- [board-events.model.ts](src/eventstormingboard.client/src/app/_shared/models/board-events.model.ts) — add 3 BC event interfaces
- [boards-signalr.service.ts](src/eventstormingboard.client/src/app/_shared/services/boards-signalr.service.ts) — add 3 subjects + broadcast methods
- [board.commands.ts](src/eventstormingboard.client/src/app/board/board.commands.ts) — add 5 command classes
- [board-canvas.component.ts](src/eventstormingboard.client/src/app/board/board-canvas/board-canvas.component.ts) — rendering + all interaction logic
- [board-canvas.service.ts](src/eventstormingboard.client/src/app/board/board-canvas/board-canvas.service.ts) — `isDrawingBoundedContext` flag + command-to-SignalR dispatch for 5 BC commands
- [board.component.ts](src/eventstormingboard.client/src/app/board/board.component.ts) — subscriptions + toolbar handler + initial load BC hydration
- [board.component.html](src/eventstormingboard.client/src/app/board/board.component.html) — toolbar button
- [BoardAgentFactory.cs](src/EventStormingBoard.Server/Agents/BoardAgentFactory.cs) — extend destructive-tool filter for `DeleteBoundedContext`
- [AgentServiceToolPolicyTests.cs](tests/EventStormingBoard.Server.Tests/AgentServiceToolPolicyTests.cs) — tool policy tests (restructured for exclusivity)
- [BoardEventPipelineTests.cs](tests/EventStormingBoard.Server.Tests/BoardEventPipelineTests.cs) — pipeline tests for 3 new event types
- [BoardStateServiceTests.cs](tests/EventStormingBoard.Server.Tests/BoardStateServiceTests.cs) — state service tests for 3 new Apply methods
- [BoardEventLogTests.cs](tests/EventStormingBoard.Server.Tests/BoardEventLogTests.cs) — summary assertion tests for 3 new events
- Test spy/stub classes — update to implement new `IBoardStateService` methods
- 7 new files: entity, DTO, input model, 3 events, 1 frontend model

**Verification**
1. `dotnet build src/EventStormingBoard.Server/EventStormingBoard.Server.csproj` — compiles
2. `dotnet test tests/EventStormingBoard.Server.Tests/` — all tests pass
3. `cd src/eventstormingboard.client && npm run build` — frontend compiles
4. Manual: draw a bounded context frame, verify it renders behind notes with dashed border + title
5. Manual: move/resize frame, verify undo/redo works
6. Manual: second browser tab — verify SignalR real-time sync
7. Manual: autonomous BreakItDown phase — verify DomainDesigner agent creates frames via tools
8. Manual: delete + undo — verify restoration

**Decisions**
- New entity (not NoteType) for clean domain modeling and independent z-ordering
- Purely visual containment — no stored note-ID membership
- Draw-to-create UX — toolbar activates drawing mode, drag creates frame
- DomainDesigner is the only agent with bounded context tools
- Name prompted via dialog after drawing the rectangle

**Scope exclusions**
- No drag-to-move-contained-notes behavior (frames are independent)
- No color picker for bounded contexts
- No `copilot-instructions.md` updates (can be done after feature lands)

**Known limitations**
- JSON export/import does not include bounded contexts — exporting and re-importing a board will lose all frames. The import path clears notes/connections and repopulates via Paste, but does not clear or restore bounded contexts. A user-facing warning should be shown on export if bounded contexts exist. Full JSON round-trip support is deferred to a follow-up
- Tool access for DomainDesigner is agent-wide, not phase-scoped — bounded context tools are available in DefineAggregates as well as BreakItDown. Enforcement is via prompt-level guardrail only. If code-level phase gating is needed later, `BoardAgentFactory` would need to accept the current phase and filter tools accordingly
