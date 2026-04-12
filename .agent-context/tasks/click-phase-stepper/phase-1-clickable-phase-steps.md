# Phase 1: Clickable Phase Steps

**Objective**: Make each step in the phase stepper clickable, triggering a phase change via the existing `UpdateBoardContextCommand` → SignalR pipeline.
**Files to modify**:
- `src/eventstormingboard.client/src/app/board/board.component.html` — add `(click)` and `(keydown)` handlers, `tabindex`, ARIA attributes on each step
- `src/eventstormingboard.client/src/app/board/board.component.ts` — add `onPhaseClick()` method
- `src/eventstormingboard.client/src/app/board/board.component.scss` — add focus-visible styles for keyboard navigation

## Context

The phase stepper template iterates `EVENT_STORMING_PHASES` and renders `.step` divs with an `.active` class. The existing `openBoardContext()` method already demonstrates the exact pattern for creating an `UpdateBoardContextCommand` and executing it via `canvasService.executeCommand()`. This phase replicates that pattern with a targeted method that only changes the phase field, preserving all other board context values.

## Knowledge, Instructions & Skills

- `.github/instructions/angular.instructions.md` — standalone component conventions, signal patterns, `takeUntilDestroyed()`
- `.github/skills/frontend-design/SKILL.md` — Kinetic Ionization design system for focus/hover styles (zero radius, primary color tokens)

## Implementation Steps

### Step 1: Add `onPhaseClick()` method to `board.component.ts`

Add a new public method after the existing `isPhaseActive()` method (around line 102). The method should:
1. Check if the clicked phase is already active — return early if so (FR-5)
2. Create an `UpdateBoardContextCommand` preserving all current board context values, only changing the phase
3. Execute the command via `canvasService.executeCommand()`

**Code example:**
```typescript
public onPhaseClick(phase: string): void {
  if (this.isPhaseActive(phase)) {
    return;
  }
  const command = new UpdateBoardContextCommand(
    this.canvasService.boardState.domain,
    this.canvasService.boardState.domain,
    this.canvasService.boardState.sessionScope,
    this.canvasService.boardState.sessionScope,
    phase,
    this.canvasService.boardState.phase,
    this.canvasService.boardState.autonomousEnabled,
    this.canvasService.boardState.autonomousEnabled
  );
  this.canvasService.executeCommand(command);
}
```

**Reference**: `src/eventstormingboard.client/src/app/board/board.component.ts` lines 468–495 — the `openBoardContext()` method uses the same `UpdateBoardContextCommand` constructor and `canvasService.executeCommand()` call pattern. The key difference: `onPhaseClick()` preserves domain, sessionScope, and autonomousEnabled unchanged (old === new), only changing the phase.

**Reference**: `src/eventstormingboard.client/src/app/board/board.commands.ts` — `UpdateBoardContextCommand` constructor signature:
```typescript
constructor(
  private newDomain: string | undefined,
  private oldDomain: string | undefined,
  private newSessionScope: string | undefined,
  private oldSessionScope: string | undefined,
  private newPhase: string | undefined,
  private oldPhase: string | undefined,
  private newAutonomousEnabled: boolean,
  private oldAutonomousEnabled: boolean
)
```

Note: `EventStormingPhase` type does not need to be imported — the existing `isPhaseActive()` method already takes `phase: string` and the command constructor uses `string | undefined`.

### Step 2: Add click and keyboard handlers to the template

Update the `.step` div in `board.component.html` (line 170) to add:
- `(click)="onPhaseClick(phase.value)"` — triggers phase change on click
- `(keydown.enter)="onPhaseClick(phase.value)"` — keyboard activation via Enter
- `(keydown.space)="$event.preventDefault(); onPhaseClick(phase.value)"` — keyboard activation via Space (must call `$event.preventDefault()` to suppress the browser's default page scroll on Space)
- `tabindex="0"` — makes steps focusable
- `role="button"` — communicates clickability to screen readers
- `[attr.aria-current]="isPhaseActive(phase.value) ? 'step' : null"` — conveys which step is active (prefer `aria-current="step"` over `aria-pressed` because the steps are mutually exclusive, not toggleable)

Also change the parent `<div class="stepper-content">` from `role="progressbar"` to `role="group"`. The ARIA `progressbar` role does not allow interactive child roles like `button`. `role="group"` is semantically correct for a set of related interactive elements.

**Current template** (lines 167–177):
```html
<div class="stepper-content" role="progressbar" aria-label="Event Storming Phase">
  @for (phase of phases; track phase.value; let i = $index) {
    <div class="step" [class.active]="isPhaseActive(phase.value)">
      <div class="step-circle">{{ i + 1 }}</div>
      <span class="step-label">{{ phase.label }}</span>
    </div>
    @if (i < phases.length - 1) {
      <div class="step-connector" [class.active]="isPhaseActive(phase.value)"></div>
    }
```

**Updated template**:
```html
<div class="stepper-content" role="group" aria-label="Event Storming Phase">
  @for (phase of phases; track phase.value; let i = $index) {
    <div class="step" [class.active]="isPhaseActive(phase.value)"
         (click)="onPhaseClick(phase.value)"
         (keydown.enter)="onPhaseClick(phase.value)"
         (keydown.space)="$event.preventDefault(); onPhaseClick(phase.value)"
         tabindex="0"
         role="button"
         [attr.aria-current]="isPhaseActive(phase.value) ? 'step' : null">
      <div class="step-circle">{{ i + 1 }}</div>
      <span class="step-label">{{ phase.label }}</span>
    </div>
    @if (i < phases.length - 1) {
      <div class="step-connector" [class.active]="isPhaseActive(phase.value)"></div>
    }
```

### Step 3: Add focus-visible styles to SCSS

Add a `:focus-visible` style to the `.step` rule in `board.component.scss` (within the `.phase-stepper-container` block). This provides a visible focus indicator for keyboard navigation without showing it on mouse click.

**Code example** — add inside the existing `.step { ... }` block:
```scss
&:focus-visible {
  outline: 1px solid var(--sys-primary);
  outline-offset: 2px;
}
```

**Reference**: The `.step` already has `cursor: pointer` and `&:hover` / `&.active` styles in `board.component.scss`. The focus-visible rule should sit alongside these.

## Verification Criteria

- [ ] `cd src/eventstormingboard.client && npx ng build` passes with no errors
- [ ] Clicking a non-active phase step in the UI changes the active phase (`.active` class moves)
- [ ] Clicking the already-active phase does nothing — verify by checking the browser DevTools Network/Console tab for absence of a duplicate `BoardContextUpdated` SignalR message
- [ ] Phase change broadcasts to other connected clients — verify by opening two browser tabs on the same board and clicking a phase in one tab; the other tab should update
- [ ] Undo (Ctrl+Z) after clicking a phase step reverts to the previous phase, confirming FR-4 command stack integration
- [ ] Steps are keyboard-focusable (Tab) and activatable (Enter/Space). Pressing Space does **not** scroll the page
- [ ] No backend changes — `dotnet build src/EventStormingBoard.Server/EventStormingBoard.Server.csproj` still passes
- [ ] `dotnet test tests/EventStormingBoard.Server.Tests/` still passes (no regressions)
