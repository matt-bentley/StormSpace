# Phase 2: Wire Phase Stepper Click Handlers

**Objective**: Add click handlers to the phase stepper steps that open the confirmation modal and execute the phase change via the existing command/SignalR pipeline.
**Files to modify**:
- `src/eventstormingboard.client/src/app/board/board.component.ts` — Add `onPhaseClick()` method, import `ConfirmModalComponent`
- `src/eventstormingboard.client/src/app/board/board.component.html` — Add `(click)` binding to stepper steps

## Context

The phase stepper is built inline in the board component template (not a separate component). Steps already have `cursor: pointer` styling. This phase adds a click handler that:
1. Ignores clicks on the already-active phase (FR-5)
2. Opens the confirmation modal with the target phase name (FR-1, FR-2)
3. On confirm, creates and executes an `UpdateBoardContextCommand` that changes only the phase field (FR-3)
4. On cancel, does nothing (FR-4)

The command goes through `BoardCanvasService.executeCommand()` which handles undo/redo stacking and SignalR broadcast (FR-7, FR-8). Users can jump to any phase (FR-6).

## Knowledge, Instructions & Skills

- `.github/instructions/angular.instructions.md` — Event binding patterns, `inject()`, `takeUntilDestroyed()`
- `.agent-context/knowledge/domain-model.md` — Frontend Command pattern, `UpdateBoardContextCommand`
- `.agent-context/knowledge/architecture-principles.md` — RxJS Subjects + Command Pattern, event pipeline

## Implementation Steps

### Step 1: Add click handler and keyboard accessibility to the template

Add `(click)`, keyboard handlers, and ARIA attributes to each `.step` div. The steps become interactive, so they need `tabindex`, `role`, and keyboard support to remain accessible.

**Current code** (`board.component.html` lines 170-173):
```html
<div class="step" [class.active]="isPhaseActive(phase.value)">
  <div class="step-circle">{{ i + 1 }}</div>
  <span class="step-label">{{ phase.label }}</span>
</div>
```

**Updated code:**
```html
<div class="step"
     [class.active]="isPhaseActive(phase.value)"
     (click)="onPhaseClick(phase)"
     (keydown.enter)="onPhaseClick(phase)"
     (keydown.space)="onPhaseClick(phase); $event.preventDefault()"
     [attr.tabindex]="isPhaseActive(phase.value) ? -1 : 0"
     [attr.aria-current]="isPhaseActive(phase.value) ? 'step' : null"
     role="tab">
  <div class="step-circle">{{ i + 1 }}</div>
  <span class="step-label">{{ phase.label }}</span>
</div>
```

Also update the parent container's role from `role="progressbar"` to `role="tablist"` since the steps are now interactive:
```html
<div class="stepper-content" role="tablist" aria-label="Event Storming Phase">
```

**Accessibility notes:**
- `tabindex="0"` on inactive steps makes them keyboard-focusable; `tabindex="-1"` on the active step removes it from tab order (clicking does nothing anyway per FR-5)
- `keydown.enter` and `keydown.space` mirror the click behavior for keyboard users
- `aria-current="step"` identifies the active phase for screen readers
- `role="tab"` / `role="tablist"` is the appropriate ARIA pattern for step navigation

**Reference**: The template already uses `(click)` bindings extensively for toolbar buttons in the same file.

### Step 2: Import ConfirmModalComponent in board.component.ts

Add the import statement at the top of the file alongside other modal imports.

**Current imports** (board.component.ts lines 21-22):
```typescript
import { BoardContextModalComponent, BoardContextData } from './board-context-modal/board-context-modal.component';
import { AgentConfigModalComponent, AgentConfigModalData } from './agent-config-modal/agent-config-modal.component';
```

**Add after:**
```typescript
import { ConfirmModalComponent, ConfirmModalData } from '../_shared/components/confirm-modal/confirm-modal.component';
```

### Step 3: Add the onPhaseClick method to BoardComponent

Add the method to the board component class. It should:
1. Early-return if the clicked phase is already active
2. Open the `ConfirmModalComponent` with the target phase label
3. On confirm, create an `UpdateBoardContextCommand` with only the phase changed (all other context fields pass through unchanged)

Also add an import for `EventStormingPhase` at the top of the file (alongside the existing `EVENT_STORMING_PHASES` import on line 24):
```typescript
import { EVENT_STORMING_PHASES, EventStormingPhase } from '../_shared/models/board.model';
```

**Code example:**
```typescript
public onPhaseClick(phase: { value: EventStormingPhase; label: string }): void {
  if (this.isPhaseActive(phase.value)) {
    return;
  }

  const dialogRef = this.dialog.open(ConfirmModalComponent, {
    width: '400px',
    maxWidth: '95vw',
    data: {
      title: 'Change Phase',
      message: `Are you sure you want to change the workshop phase to "${phase.label}"?`,
    } as ConfirmModalData
  });

  dialogRef.afterClosed().subscribe((confirmed: boolean) => {
    if (confirmed) {
      const state = this.canvasService.boardState;
      const command = new UpdateBoardContextCommand(
        state.domain,
        state.domain,
        state.sessionScope,
        state.sessionScope,
        phase.value,
        state.phase,
        state.autonomousEnabled,
        state.autonomousEnabled
      );
      this.canvasService.executeCommand(command);
    }
  });
}
```

**Key details**:
- The parameter type uses `EventStormingPhase` (not `string`) to preserve type safety — `EVENT_STORMING_PHASES` is typed as `{ value: EventStormingPhase; label: string }[]`.
- For the `UpdateBoardContextCommand`, the "old" and "new" values for `domain`, `sessionScope`, and `autonomousEnabled` are set to the **same current value** so undo only reverts the phase. Only `newPhase` / `oldPhase` differ.
- Unlike `openBoardContext()` which uses `result.phase || undefined` coercion, here `phase.value` is always a truthy `EventStormingPhase` string from the phases array, so no `|| undefined` fallback is needed.
- The `afterClosed()` observable auto-completes after one emission, so no `takeUntilDestroyed()` is needed (same pattern as all existing dialog subscriptions in this component).

**Reference**: `src/eventstormingboard.client/src/app/board/board.component.ts` line 468 — `openBoardContext()` method shows the exact pattern for opening a dialog, subscribing to `afterClosed()`, and executing an `UpdateBoardContextCommand`.

## Verification Criteria

- [ ] `cd src/eventstormingboard.client && npx ng build` passes with no errors
- [ ] Clicking an inactive phase step opens the confirmation modal with the phase name
- [ ] Clicking the active phase step does nothing (no modal)
- [ ] Confirming the modal updates the phase stepper to show the new active phase
- [ ] Cancelling the modal leaves the phase unchanged
- [ ] The phase change is broadcast to other connected users via SignalR
- [ ] The phase change can be undone with Ctrl+Z
- [ ] Jumping to any phase is allowed (no sequential restriction)
- [ ] Steps are keyboard-focusable (Tab key) and activatable (Enter/Space)
- [ ] Active step has `aria-current="step"`, stepper container has `role="tablist"`
