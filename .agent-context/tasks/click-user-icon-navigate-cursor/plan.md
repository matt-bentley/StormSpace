# Plan: Click User Icon to Navigate to Their Cursor

**Objective**: Enable clicking a user's avatar in the header to smoothly pan the board canvas to that user's cursor position, with visual feedback for missing cursors and a highlight effect on arrival.
**Phases**: 2
**Estimated complexity**: Low

## Context

StormSpace's board header displays connected users as avatar circles. Remote cursor positions are already tracked in `BoardCanvasService.remoteCursors` (a `Map<string, RemoteCursorState>` keyed by `connectionId`). The minimap already implements click-to-pan by setting `originX`/`originY` on the canvas service. This feature extends those existing patterns with animated panning, local-user detection, and a post-navigation cursor highlight.

## Approach

Single clear approach — extend existing patterns:
1. Expose `hubConnection.connectionId` from `BoardsSignalRService` to identify the local user
2. Add a `navigateToCoordinate$` Subject on `BoardCanvasService` (follows existing `canvasUpdated$` / `canvasImageDownloaded$` Subject pattern)
3. `BoardCanvasComponent` subscribes and runs `requestAnimationFrame`-based animated pan (mirrors `onMinimapClick` coordinate math)
4. `BoardComponent` adds `navigateToUserCursor(user)` handler — looks up cursor, triggers navigation or shows snackbar
5. `drawRemoteCursors()` adds pulse ring rendering for the highlighted cursor

No alternative approaches considered — this is a direct extension of proven canvas patterns.

## Phase Summary

| Phase | Name | Description | Files Modified | Verification |
|-------|------|-------------|----------------|--------------|
| 1 | Click-to-Navigate with Animated Pan | Add click handler on user avatars, animated pan to cursor position, MatSnackBar feedback for unknown cursors, local user skip | 6 files | `npm run build` passes; clicking remote user avatar pans canvas smoothly; clicking local user does nothing; clicking user with no cursor shows snackbar |
| 2 | Cursor Highlight Pulse Effect | Draw animated pulse ring around target cursor after navigation completes | 2 files | `npm run build` passes; after pan animation, a cyan pulse ring expands and fades around the target cursor over ~1.5s |

## Dependencies

- `@angular/material` v21 is already installed — `MatSnackBarModule` available but not yet imported
- No new packages needed
- No backend changes needed

## Risks

| Risk | Mitigation |
|------|------------|
| `hubConnection.connectionId` may be `null` before connection establishes | Check for `null` before comparing — if null, treat as remote user (allows navigation attempt) |
| Cursor pruned (stale >15s) before user clicks avatar | Show "No cursor position available" snackbar — this is expected behavior per spec |
| Rapid repeated clicks could queue multiple animations | Cancel any in-flight animation before starting a new one (use animation ID tracking) |
| `requestAnimationFrame` loop for highlight could conflict with existing `drawCanvas` RAF batching | The highlight triggers `drawCanvas()` which already batches via `rafPending` — no conflict. Elapsed-time guard prevents unbounded loop if target cursor is pruned |
| Component destroyed during pan animation | `destroyed` guard in RAF callback + `cancelAnimationFrame` in `ngOnDestroy()` prevents post-destroy errors |
| Local `connectionId` null before SignalR connects | Skip click handling entirely when `connectionId` is null — prevents false navigation for own avatar |
