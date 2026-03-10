# StormSpace - Full Application Review

> Review Date: March 10, 2026  
> Scope: Full-stack code review, architecture analysis, browser testing, bug identification, and feature recommendations

---

## Table of Contents
1. [Architecture Overview](#architecture-overview)
2. [Implementation Tracking (Items 1-17)](#implementation-tracking-items-1-17)
3. [Bug Fixes (Critical)](#bug-fixes-critical)
4. [Bug Fixes (Moderate)](#bug-fixes-moderate)
5. [Code Quality / Cleanup](#code-quality--cleanup)
6. [Security Concerns](#security-concerns)
7. [Architecture Improvements](#architecture-improvements)
8. [Recommended New Features](#recommended-new-features)

---

## Architecture Overview

**Server:** ASP.NET (.NET 10) with SignalR for real-time collaboration, REST API for board CRUD, in-memory cache for persistence.  
**Client:** Angular 21 SPA with canvas-based board rendering, Material Design UI, SignalR client for real-time events, and a Command pattern for undo/redo.

The architecture is well-designed for a real-time collaboration tool. The Command pattern is a strong choice for undo/redo support. SignalR provides effective real-time sync between users.

---

## Implementation Tracking (Items 1-17)

Status key: Implemented = completed in codebase.

1. Implemented - Fixed delete logic to collect connections to delete (selected/attached), not those to keep.
2. Implemented - Reworked `BoardsHub` board-user store to use thread-safe nested `ConcurrentDictionary` and atomic operations.
3. Implemented - Moved Swagger middleware behind `app.Environment.IsDevelopment()`.
4. Implemented - Removed trailing comma from `appsettings.Development.json`.
5. Implemented - Scoped `BoardCanvasService` to `BoardComponent` (`providers`) and removed root singleton registration.
6. Implemented - Added `await` to SignalR `joinBoard` and `leaveBoard` invocations.
7. Implemented - Added server-side board-name validation for create/update and trims whitespace.
8. Implemented - Simplified repository persistence to one cache key (`boards`) so list and board lookup stay in sync.
9. Implemented - Replaced `var` with `const` in delete handler.
10. Implemented - Corrected move-command emission to compare initial vs final drag positions instead of checking `(0,0)`.
11. Implemented - Renamed event file from `NoteMovedEvent.cs` to `NotesMovedEvent.cs` to match class naming.
12. Implemented - Removed duplicate panning fallback logic from `handleDragging`.
13. Implemented - Changed `NoteSizeDto.X` and `NoteSizeDto.Y` from `int` to `double` for coordinate consistency.
14. Implemented - Added `DELETE /api/boards/{id}` endpoint and repository delete support.
15. Implemented - Replaced string note type with strongly typed `NoteType` enum in server entity/DTO; configured camelCase enum JSON serialization.
16. Implemented - Removed redundant splash board prefetch and simplified select button enabling.
17. Implemented - Ensured select mode is activated on board initialization so toolbar state is visually correct.

Post-implementation browser test fix:
- Fixed SignalR enum JSON serialization for hub payloads by configuring `AddSignalR().AddJsonProtocol(...)` with `JsonStringEnumConverter(JsonNamingPolicy.CamelCase)` in `Program.cs`.

---

## Bug Fixes (Critical)

### 1. BUG: Delete logic captures wrong connections — should capture connections TO delete, not connections to KEEP
**File:** `eventstormingboard.client/src/app/board/board-canvas/board-canvas.component.ts` (line ~317)

The delete handler collects connections that are **NOT** selected and **NOT** connected to the selected notes. This means the `DeleteNotesCommand` receives connections that should be **kept**, not the ones that should be **deleted**. Looking at `DeleteNotesCommand.execute()`, it removes the passed connections from state — meaning it incorrectly removes connections that are unrelated to the deletion.

**Current (buggy):**
```typescript
var connections = JSON.parse(JSON.stringify(this.canvasService.boardState.connections.filter(c =>
    !c.selected &&
    !selectedNoteIds.includes(c.fromNoteId) &&
    !selectedNoteIds.includes(c.toNoteId)
))) as Connection[];
```

**Fix — should collect connections that ARE selected or ARE connected to deleted notes:**
```typescript
var connections = JSON.parse(JSON.stringify(this.canvasService.boardState.connections.filter(c =>
    c.selected ||
    selectedNoteIds.includes(c.fromNoteId) ||
    selectedNoteIds.includes(c.toNoteId)
))) as Connection[];
```

### 2. BUG: Thread-safety issue in BoardsHub with `HashSet<BoardUserDto>`
**File:** `EventStormingBoard.Server/Hubs/BoardsHub.cs`

`BoardUsers` is a `ConcurrentDictionary<Guid, HashSet<BoardUserDto>>`, but `HashSet<BoardUserDto>` is not thread-safe. Multiple concurrent SignalR calls can cause data corruption. The `ContainsKey` + add/modify pattern is also a race condition (TOCTOU).

**Fix:** Use `ConcurrentDictionary` with `AddOrUpdate`, and wrap `HashSet` operations with locks, or replace with `ConcurrentBag`/`ConcurrentDictionary` of users.

### 3. BUG: Swagger exposed in all environments (including production)
**File:** `EventStormingBoard.Server/Program.cs`

```csharp
app.UseSwagger();
app.UseSwaggerUI();
```
These are outside any environment check, meaning Swagger (with full API documentation) is exposed in production.

**Fix:**
```csharp
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
```

### 4. BUG: `appsettings.Development.json` has trailing comma (invalid JSON)
**File:** `EventStormingBoard.Server/appsettings.Development.json`

```json
"Microsoft.AspNetCore": "Warning",  // <-- trailing comma
```
While .NET's JSON parser tolerates this, it's technically invalid JSON and can cause issues with stricter tools.

---

## Bug Fixes (Moderate)

### 5. BUG: `BoardCanvasService` is `providedIn: 'root'` (singleton) but holds per-board state
**File:** `eventstormingboard.client/src/app/board/board-canvas/board-canvas.service.ts`

The `BoardCanvasService` is a root singleton but stores board-specific state (`boardState`, `id`, undo/redo stacks, etc.). If a user navigates away and back to a different board, stale state from the previous board persists (including undo/redo history).

**Fix:** Either provide it at component level (`providers: [BoardCanvasService]` in `BoardComponent`) or explicitly reset all state in `ngOnInit`/`ngOnDestroy` of the board component.

### 6. BUG: `joinBoard` calls `invoke` without awaiting properly
**File:** `eventstormingboard.client/src/app/_shared/services/boards-signalr.service.ts` (line ~77)

```typescript
public async joinBoard(boardId: string, userName: string): Promise<void> {
    await this.connectionEstablished;
    this.hubConnection.invoke('JoinBoard', boardId, userName)
      .catch(err => console.error('Error joining board group:', err));
}
```
The `invoke` result is not awaited, so errors are only caught by `.catch()` and the promise resolves before the operation completes. Same issue exists for `leaveBoard`.

**Fix:** `await this.hubConnection.invoke(...)` (already has `.catch()` for error handling).

### 7. BUG: No validation on `BoardCreateDto.Name` — server accepts empty/whitespace board names
**File:** `EventStormingBoard.Server/Controllers/BoardsController.cs`

The `Post` action directly creates a board without validating `boardCreate.Name`. While the client dialog requires a name, the API can be called directly.

**Fix:** Add validation:
```csharp
if (string.IsNullOrWhiteSpace(boardCreate.Name))
    return BadRequest("Board name is required.");
```

### 8. BUG: `BoardsRepository.GetAll()` and individual boards can go out of sync
**File:** `EventStormingBoard.Server/Repositories/BoardsRepository.cs`

The "boards" list key and individual board cache entries have independent sliding expirations. An individual board could expire while still being in the "boards" list (or vice versa), leading to ghost entries or missing boards.

### 9. BUG: `var` used instead of `const`/`let` in TypeScript
**File:** `eventstormingboard.client/src/app/board/board-canvas/board-canvas.component.ts` (line ~317-320)

```typescript
var notes = JSON.parse(...)
var connections = JSON.parse(...)
```
`var` should not be used in TypeScript — use `const` instead.

### 10. BUG: `MoveNotesCommand` initial-position check is incorrect
**File:** `eventstormingboard.client/src/app/board/board-canvas/board-canvas.component.ts` (line ~218)

```typescript
if (0 !== to.x || 0 !== to.y) {
```
This condition checks if the final position is not at `(0,0)`, rather than checking if the note actually moved. A note moved to `(0,0)` would not generate a command, and a no-op move from anywhere else would.

**Fix:**
```typescript
const from = this.initialDragPositions.get(this.draggingNote.id);
if (from && (from.x !== to.x || from.y !== to.y)) {
```

---

## Code Quality / Cleanup

### 11. `NoteMovedEvent.cs` filename doesn't match class name
**File:** `EventStormingBoard.Server/Events/NoteMovedEvent.cs` contains class `NotesMovedEvent` (plural). The filename should be `NotesMovedEvent.cs` for consistency.

### 12. Duplicated panning logic in `handleDragging`
**File:** `eventstormingboard.client/src/app/board/board-canvas/board-canvas.component.ts`

The `handleDragging` method contains a fallback panning block that duplicates `handlePanning`. Since `handlePanning` is called first in `onMouseMove`, this dead code should be removed.

### 13. `CoordinatesDto` on the server has `X` and `Y` but `NoteSizeDto` also has `X` and `Y`
**File:** `EventStormingBoard.Server/Models/NoteSizeDto.cs`

`NoteSizeDto.X` and `NoteSizeDto.Y` are `int`, while `CoordinatesDto.X` and `CoordinatesDto.Y` are `double`. The types are inconsistent — coordinates should use `double` everywhere for consistency with how the client uses them.

### 14. No `HttpDelete` endpoint for boards
**File:** `EventStormingBoard.Server/Controllers/BoardsController.cs`

There's no API endpoint to delete a board. Boards can only be created and updated.

### 15. Comment in Note entity and NoteDto says `// event, command, aggregate...` — should use an enum
Both the server `Note.Type` and `NoteDto.Type` are `string?` with a comment listing valid values. A proper enum would provide type safety.

### 16. Unused `BoardsService.boards` fetch in `SplashComponent`
**File:** `eventstormingboard.client/src/app/splash/splash.component.ts`

The `ngOnInit` fetches `this.boardsService.get()` and stores it in `this.boards`, but `this.boards` is only used to conditionally enable the "Select Existing Board" button. The same data is fetched again inside `SelectBoardModalComponent`. Consider sharing the data or simplifying.

### 17. `isSelectMode` starts as `true` in `BoardCanvasService` but doesn't visually highlight the select button on load
The select button in the toolbar doesn't have the `.active` class on initial load because the board component calls `reset()` which sets `isSelectMode = false`.

---

## Security Concerns

### 18. No input sanitization on board names or note text
Board names and note text can contain arbitrary strings, including HTML/script content. While the canvas-based rendering means XSS via DOM injection is unlikely for notes, the board name is rendered via `<input [(ngModel)]>` which is safe, but exported JSON files could carry payloads that get re-imported.

### 19. No authentication or authorization
Any user can access, modify, or delete any board. The `userName` is stored client-side in `localStorage` and sent to the server — it's trivially spoofable. For a public-facing deployment, this is a significant concern.

### 20. No rate limiting on API or SignalR hub
There's no protection against abuse — a malicious client could flood the server with board creation requests or SignalR messages.

### 21. `MemoryCache` with no size limit
**File:** `EventStormingBoard.Server/Repositories/BoardsRepository.cs`

The cache has no size limit configured. A malicious actor could exhaust server memory by creating many boards with large amounts of notes.

---

## Architecture Improvements

### 22. Replace in-memory cache with persistent storage
The entire data store is `MemoryCache` with a 1-hour sliding expiration. All data is lost on server restart. For production use, this needs a database (e.g., SQLite for simplicity, PostgreSQL for scale, or MongoDB for document storage).

### 23. SignalR hub methods should validate and persist state
Currently, the SignalR hub only broadcasts events — it does not validate them or update server-side state. The server relies on the 2-second polling `PUT` from the board component to persist. If a client disconnects before the save, changes are lost.

**Recommendation:** Have hub methods validate events and update the repository, making the server the source of truth.

### 24. HTTPS not configured for production
**File:** `EventStormingBoard.Server/Properties/launchSettings.json`

Only an HTTP profile exists. The Dockerfile also only exposes port 8080 (HTTP). For production with collaborative features (usernames, board data), HTTPS should be enforced.

### 25. Board saving relies on client-side polling
**File:** `eventstormingboard.client/src/app/board/board.component.ts` (line ~235)

The board auto-saves via `interval(2000)`. This means:
- Changes are lost if the browser closes within the 2-second window
- Every client writes the full board state every 2 seconds, which doesn't scale
- Concurrent saves from multiple clients can cause last-write-wins conflicts

**Recommendation:** Save on disconnect (`ngOnDestroy`), debounce after changes, or move persistence to the server via SignalR events.

---

## Recommended New Features

### 26. Board deletion
Allow users to delete boards they no longer need, both in the UI and via API.

### 27. Connection deletion
Currently there's no way to delete individual connections from the board. Users should be able to select and delete connections.

### 28. Multi-line note text / Markdown support
Note text editing is currently single-line (`<input>`). A `<textarea>` or Markdown-enabled editor would allow richer content for event descriptions.

### 29. Board sharing via URL / invite link
Currently users must know a board ID or select from a list. A shareable URL or invite link would improve collaboration workflow.

### 30. Note color customization
Colors are fixed per note type. Users may want to customize colors (e.g., different shades of orange for different event categories).

### 31. Keyboard shortcuts guide / help overlay
The app has keyboard shortcuts (Ctrl+Z, Ctrl+C/V, Delete) but no discoverability. A help dialog or keyboard shortcut overlay would improve UX.

### 32. Touch/mobile support
The canvas uses `mousedown/mouseup/mousemove` events exclusively. Adding `touchstart/touchmove/touchend` handlers would enable mobile/tablet usage.

### 33. Board versioning / history
With the Command pattern already in place, it would be natural to persist the event history and allow users to step through board revisions.

### 34. Snap-to-grid for note alignment
Notes can be placed at any arbitrary position. An optional snap-to-grid feature would help users create well-organized layouts.

### 35. Export to other formats
Beyond JSON and PNG export, consider SVG export, Mermaid diagram syntax, or BPMN-compatible formats for integration with other tools.

### 36. Connection labels
Connections (arrows) don't have labels. Adding text labels to connections would help describe relationships between elements.

### 37. Swimlanes / grouping zones
Event Storming often uses horizontal swimlanes to organize events by time or bounded context. Adding colored zones or lanes would be valuable.

### 38. Collaborative cursor / presence indicators
While connected users are shown as circles, there's no indication of where other users are working on the board. Showing cursor positions would enhance collaboration.

### 39. Board templates
Pre-built templates for common Event Storming patterns (Big Picture, Process Modeling, Design Level) would help new users get started.

### 40. Search / filter notes
On large boards, the ability to search for notes by text or filter by type would be very useful.

---

## Summary

| Category | Count |
|---|---|
| Critical Bugs | 4 |
| Moderate Bugs | 6 |
| Code Quality Issues | 7 |
| Security Concerns | 4 |
| Architecture Improvements | 4 |
| Recommended Features | 15 |

The application has a solid foundation with good architectural choices (Command pattern, SignalR real-time, canvas rendering). The critical bugs around the delete logic, thread safety, and Swagger exposure should be addressed first. The moderate bugs around singleton service state and missing validation should follow. For production readiness, persistent storage and authentication are essential.
