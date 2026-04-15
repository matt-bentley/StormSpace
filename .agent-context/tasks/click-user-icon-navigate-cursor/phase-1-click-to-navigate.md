# Phase 1: Click-to-Navigate with Animated Pan

**Objective**: Add click handlers on user avatar circles in the header that smoothly animate the canvas to the clicked user's cursor position, with feedback when no cursor is available and no-op for the local user.
**Files to modify** (6 files):
- `src/eventstormingboard.client/src/app/_shared/services/boards-signalr.service.ts` — expose `connectionId` getter
- `src/eventstormingboard.client/src/app/board/board-canvas/board-canvas.service.ts` — add `navigateToCoordinate$` Subject and `highlightedCursorConnectionId` state
- `src/eventstormingboard.client/src/app/board/board-canvas/board-canvas.component.ts` — subscribe to navigate Subject, implement animated pan, cancel RAF on destroy
- `src/eventstormingboard.client/src/app/board/board.component.ts` — add `navigateToUserCursor()` method, inject `MatSnackBar`, add `localConnectionId` getter
- `src/eventstormingboard.client/src/app/board/board.component.html` — add `(click)` handler on user circles
- `src/eventstormingboard.client/src/app/board/board.component.scss` — style user circles as clickable

## Context

The minimap already implements instant pan by setting `originX`/`originY` directly. This phase adds the same coordinate math but with `requestAnimationFrame`-based easing. The `BoardsSignalRService` wraps a SignalR `HubConnection` whose `connectionId` property uniquely identifies the local client — we expose this to distinguish the local user from remote users in the `connectedUsers` list.

## Knowledge, Instructions & Skills

- `.github/instructions/angular.instructions.md` — Angular 21 conventions: `inject()`, signals, `takeUntilDestroyed()`, standalone component imports
- `.github/skills/frontend-design/SKILL.md` — Kinetic Ionization design tokens for cursor styling (if adjusting user circle hover states)
- `.agent-context/knowledge/architecture-principles.md` — Canvas service architecture, Subject-based communication

## Implementation Steps

### Step 1: Expose `connectionId` from `BoardsSignalRService`

Add a public getter that returns the SignalR `HubConnection.connectionId`. This is `null` before the connection is established and a unique string after.

**File**: `src/eventstormingboard.client/src/app/_shared/services/boards-signalr.service.ts`

Add after the existing `autonomousStatuses` field (around line 82):

```typescript
public get connectionId(): string | null | undefined {
  return this.hubConnection?.connectionId;
}
```

**Reference**: The `hubConnection` is already a private field on this service (line ~55). The `connectionId` property is part of the `@microsoft/signalr` `HubConnection` API.

### Step 2: Add navigation Subject and highlight state to `BoardCanvasService`

Add a Subject that `BoardCanvasComponent` will subscribe to for animated pan requests, plus state for the cursor highlight (used in Phase 2, but added now to keep the interface clean).

**File**: `src/eventstormingboard.client/src/app/board/board-canvas/board-canvas.service.ts`

Add after the existing `canvasImageDownloaded$` field:

```typescript
public navigateToCoordinate$ = new Subject<{ x: number; y: number; highlightConnectionId?: string }>();
public highlightedCursorConnectionId: string | null = null;
public highlightStartTime = 0;
```

### Step 3: Implement animated pan in `BoardCanvasComponent`

Subscribe to `navigateToCoordinate$` and animate `originX`/`originY` using `requestAnimationFrame` with ease-out-cubic easing over 400ms. Cancel any in-flight animation when a new one starts.

**File**: `src/eventstormingboard.client/src/app/board/board-canvas/board-canvas.component.ts`

Add private fields to track the active animation and component lifecycle:

```typescript
private panAnimationId: number | null = null;
private destroyed = false;
```

Add subscription in `ngOnInit()` (after existing subscriptions):

```typescript
this.canvasService.navigateToCoordinate$
  .pipe(takeUntilDestroyed(this.destroyRef))
  .subscribe(target => this.animatePanTo(target.x, target.y, target.highlightConnectionId));
```

Add destroy cleanup in `ngOnDestroy()` (or add `OnDestroy` implementation if not present):

```typescript
ngOnDestroy(): void {
  this.destroyed = true;
  if (this.panAnimationId !== null) {
    cancelAnimationFrame(this.panAnimationId);
    this.panAnimationId = null;
  }
}
```

Add the animated pan method:

```typescript
private animatePanTo(worldX: number, worldY: number, highlightConnectionId?: string): void {
  // Cancel any in-flight animation
  if (this.panAnimationId !== null) {
    cancelAnimationFrame(this.panAnimationId);
    this.panAnimationId = null;
  }

  const canvasEl = this.canvas().nativeElement;
  const targetOriginX = -worldX * this.canvasService.scale + canvasEl.width / 2;
  const targetOriginY = -worldY * this.canvasService.scale + canvasEl.height / 2;

  const startOriginX = this.canvasService.originX;
  const startOriginY = this.canvasService.originY;
  const duration = 400;
  const startTime = performance.now();

  const animate = (now: number) => {
    // Guard against firing after component destroy
    if (this.destroyed) {
      this.panAnimationId = null;
      return;
    }

    const elapsed = now - startTime;
    const t = Math.min(elapsed / duration, 1);
    const eased = t < 1 ? 1 - Math.pow(1 - t, 3) : 1; // easeOutCubic

    this.canvasService.originX = startOriginX + (targetOriginX - startOriginX) * eased;
    this.canvasService.originY = startOriginY + (targetOriginY - startOriginY) * eased;

    this.drawCanvasFrame();

    if (t < 1) {
      this.panAnimationId = requestAnimationFrame(animate);
    } else {
      this.panAnimationId = null;
      // FR-5: Set highlight AFTER animation completes so the full pulse effect
      // plays once the target cursor is centred on screen
      if (highlightConnectionId) {
        this.canvasService.highlightedCursorConnectionId = highlightConnectionId;
        this.canvasService.highlightStartTime = Date.now();
        this.drawCanvas(); // Kick off the highlight redraw loop
      }
    }
  };

  this.panAnimationId = requestAnimationFrame(animate);
}
```

**Reference**: The coordinate math (`-worldX * scale + canvasEl.width / 2`) is identical to `onMinimapClick()` at line ~104 of `board-canvas.component.ts`. The easing curve matches the Kinetic Ionization spec: `cubic-bezier(0.4, 0, 0.2, 1)` approximated as easeOutCubic.

**Note**: The animation callback calls `drawCanvasFrame()` directly (not `drawCanvas()`) to avoid double-scheduling through the `rafPending` indirection, which would cause a 1-frame rendering lag. The final frame after completion calls `drawCanvas()` to kick off the highlight redraw loop via Phase 2.

### Step 4: Add `navigateToUserCursor()` to `BoardComponent`

This method looks up the cursor position by `connectionId`, triggers animated pan or shows a snackbar.

**File**: `src/eventstormingboard.client/src/app/board/board.component.ts`

Add `MatSnackBar` import and injection:

```typescript
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
```

Add `MatSnackBarModule` to the component's `imports` array.

Inject the snackbar and SignalR service:

```typescript
private snackBar = inject(MatSnackBar);
```

Add a public getter for the local connection ID (used in template for local-user styling):

```typescript
get localConnectionId(): string | null | undefined {
  return this.boardsHub.connectionId;
}
```

Add the navigation method:

```typescript
public navigateToUserCursor(user: BoardUser): void {
  // FR-6: Skip local user. Also skip if connectionId isn't established yet
  // (prevents false navigation attempt for own avatar before SignalR connects)
  const localId = this.boardsHub.connectionId;
  if (!localId || user.connectionId === localId) {
    return;
  }

  const cursor = this.canvasService.remoteCursors.get(user.connectionId);
  if (!cursor) {
    // FR-4: No cursor position available.
    // Spec says "tooltip" but a click-triggered tooltip is poor UX (matTooltip is hover-only).
    // A snackbar provides clear, transient click feedback — deliberate spec deviation.
    this.snackBar.open('No cursor position available', '', {
      duration: 2000,
      horizontalPosition: 'center',
      verticalPosition: 'top'
    });
    return;
  }

  // FR-1 & FR-2: Animated pan to cursor position
  this.canvasService.navigateToCoordinate$.next({
    x: cursor.x,
    y: cursor.y,
    highlightConnectionId: cursor.connectionId
  });
}
```

### Step 5: Add click handler to user avatar template

**File**: `src/eventstormingboard.client/src/app/board/board.component.html`

Update the user-circle `<div>` to add a click handler. Use the `localConnectionId` getter (added in Step 4) for template access — `boardsHub` remains private:

```html
<div
  class="user-circle"
  [class.local-user]="user.connectionId === localConnectionId"
  [style.background-color]="user.getColour()"
  [matTooltip]="user.userName"
  (click)="navigateToUserCursor(user)">
  {{ user.userName.charAt(0).toUpperCase() }}
</div>
```

Then in template: `[class.local-user]="user.connectionId === localConnectionId"`

### Step 6: Style user circles as clickable

**File**: `src/eventstormingboard.client/src/app/board/board.component.scss`

Update the `.user-circle` styles:

```scss
.user-circle {
  // ... existing styles ...
  cursor: pointer;

  &.local-user {
    cursor: default;
  }
}
```

## Verification Criteria

- [ ] `cd src/eventstormingboard.client && npm run build` passes with no errors
- [ ] Clicking a remote user's avatar in the header smoothly pans the canvas (animated over ~400ms) to center on their cursor
- [ ] The zoom level does not change during navigation
- [ ] Clicking a user with no known cursor position shows "No cursor position available" snackbar
- [ ] Clicking the local user's own avatar does nothing (no pan, no snackbar)
- [ ] User circles appear clickable (pointer cursor) for remote users, default cursor for local user
- [ ] Rapid double-click on a user avatar doesn't cause visual glitches (animation cancellation works)
- [ ] User avatars remain clickable in both collapsed (≤5) and expanded (hover) states
