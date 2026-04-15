# Phase 2: Cursor Highlight Pulse Effect

**Objective**: After the pan animation completes, draw an animated pulse ring around the target user's cursor so it stands out visually. The effect fades over ~1.5 seconds.
**Files to modify**:
- `src/eventstormingboard.client/src/app/board/board-canvas/board-canvas.component.ts` — modify `drawRemoteCursors()` to render pulse ring, add continuous redraw during highlight

## Context

Phase 1 sets `highlightedCursorConnectionId` and `highlightStartTime` on `BoardCanvasService` **after the pan animation completes** (in the RAF completion branch). This phase reads that state during the canvas draw loop to render a pulse effect. The existing `drawCanvas()` method uses `requestAnimationFrame` batching via `rafPending` — the highlight triggers repeated redraws until the effect completes.

The pulse uses the design system's `primaryContainer` color (`#00f0ff` in dark mode, `#00838f` in light mode) — the same cyan used for selection indicators and grid lines, matching the Kinetic Ionization palette.

## Knowledge, Instructions & Skills

- `.github/instructions/angular.instructions.md` — Component patterns
- `.github/skills/frontend-design/SKILL.md` — Kinetic Ionization animation specs: sharp easing, 300ms in / 200ms out conventions; primary/primaryContainer color tokens
- `.agent-context/knowledge/architecture-principles.md` — Canvas rendering pipeline

## Implementation Steps

### Step 1: Modify `drawRemoteCursors()` to render highlight pulse

**File**: `src/eventstormingboard.client/src/app/board/board-canvas/board-canvas.component.ts`

At the end of the per-cursor drawing loop in `drawRemoteCursors()`, after `this.ctx.restore()`, add a highlight ring check:

```typescript
// After the existing ctx.restore() for each cursor:
if (cursor.connectionId === this.canvasService.highlightedCursorConnectionId) {
  const elapsed = Date.now() - this.canvasService.highlightStartTime;
  const highlightDuration = 1500; // 1.5 seconds

  if (elapsed < highlightDuration) {
    const progress = elapsed / highlightDuration;
    const radius = 20 + progress * 30; // Expand from 20px to 50px
    const opacity = (1 - progress) * 0.6; // Fade from 0.6 to 0

    this.ctx.save();
    this.ctx.beginPath();
    this.ctx.arc(cursor.x, cursor.y, radius, 0, Math.PI * 2);
    this.ctx.strokeStyle = `rgba(0, 240, 255, ${opacity})`;
    this.ctx.lineWidth = 2;
    this.ctx.stroke();

    // Second ring (offset for depth)
    const radius2 = 10 + progress * 20;
    const opacity2 = (1 - progress) * 0.3;
    this.ctx.beginPath();
    this.ctx.arc(cursor.x, cursor.y, radius2, 0, Math.PI * 2);
    this.ctx.strokeStyle = `rgba(0, 240, 255, ${opacity2})`;
    this.ctx.lineWidth = 1.5;
    this.ctx.stroke();

    this.ctx.restore();
  } else {
    // Clear highlight after duration
    this.canvasService.highlightedCursorConnectionId = null;
  }
}
```

**Note on color**: Using `rgba(0, 240, 255, ...)` directly rather than the theme token because the canvas API needs rgba strings with dynamic opacity. `#00f0ff` is the dark-mode `primaryContainer` token value. For proper theme support, a helper could extract the RGB, but the cyan pulse is intentionally vivid in both themes.

### Step 2: Add continuous redraw during highlight animation

The highlight needs to redraw every frame during the 1.5s animation. Add a check in `drawCanvasFrame()` to schedule another frame if a highlight is active, with an elapsed-time safety guard to prevent permanent RAF churn if the target cursor is pruned from the `remoteCursors` map before the highlight drawing branch clears the state.

**File**: `src/eventstormingboard.client/src/app/board/board-canvas/board-canvas.component.ts`

At the end of `drawCanvasFrame()` (after `this.drawMinimap()`), add:

```typescript
// Continue redrawing while cursor highlight is active
if (this.canvasService.highlightedCursorConnectionId) {
  const highlightElapsed = Date.now() - this.canvasService.highlightStartTime;
  if (highlightElapsed < 1600) { // slightly over 1500ms duration to ensure final clear frame renders
    this.drawCanvas();
  } else {
    // Safety expiry: clear highlight if the per-cursor drawing branch didn't clear it
    // (e.g. cursor was pruned from remoteCursors while highlight was active)
    this.canvasService.highlightedCursorConnectionId = null;
  }
}
```

This leverages the existing `rafPending` batching in `drawCanvas()` — it won't cause double-renders, it just ensures the next frame is scheduled. The elapsed-time guard prevents an unbounded redraw loop if the highlighted cursor disappears from the `remoteCursors` map (stale timer prune or user disconnect).

## Verification Criteria

- [ ] `cd src/eventstormingboard.client && npm run build` passes with no errors
- [ ] After navigating to a user's cursor, a cyan pulse ring expands outward from the cursor position
- [ ] The pulse ring fades from visible to transparent over approximately 1.5 seconds
- [ ] After the pulse completes, no extra redraws occur (check that `highlightedCursorConnectionId` is cleared to `null`)
- [ ] The pulse effect does not affect the cursor's normal appearance (pointer + label remain unchanged)
- [ ] Multiple sequential navigations correctly show the pulse on each new target (previous highlight is replaced)
- [ ] If the target cursor is pruned (user disconnects) during the highlight, the redraw loop terminates cleanly within ~1.6s
