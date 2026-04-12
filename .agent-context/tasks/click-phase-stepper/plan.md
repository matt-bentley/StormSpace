# Plan: Clickable Phase Stepper

**Objective**: Allow users to change the board's active workshop phase by clicking on a step in the phase stepper, broadcasting the change to all connected clients via SignalR.
**Phases**: 1
**Estimated complexity**: Low

## Context

The phase stepper in `board.component.html` (lines 165–181) currently renders five workshop phases as a read-only progress indicator. Users must open the board context settings dialog to change phases. This task adds click-to-navigate behaviour directly on each step, using the existing `UpdateBoardContextCommand` and SignalR broadcast pipeline — no backend changes needed.

## Phase Summary

| Phase | Name | Description | Files Modified | Verification |
|-------|------|-------------|----------------|--------------|
| 1 | Clickable Phase Steps | Add click handler, keyboard a11y, and `onPhaseClick()` method to the phase stepper | `board.component.html`, `board.component.ts`, `board.component.scss` | `npm run build` passes; manual click test |

## Dependencies

None. All backend infrastructure (`BroadcastBoardContextUpdated`, `BoardContextUpdatedEvent`, `BoardEventPipeline`, `BoardStateService`) already exists and is tested.

## Risks

| Risk | Mitigation |
|------|------------|
| Clicking sends redundant events for the already-active phase | Guard in `onPhaseClick()` — no-op when clicking the current phase (FR-5) |
| ARIA role mismatch — current `role="progressbar"` doesn't semantically fit clickable steps | Change parent to `role="group"` and use `aria-current="step"` for active state (not `aria-pressed`, which implies toggle semantics) |
| Space key scrolls page when step is focused | `$event.preventDefault()` in the `(keydown.space)` handler suppresses browser default scroll |

## Spec Open-Question Resolutions

The spec raised three open questions. These are resolved as follows for this implementation:
- **Unrestricted navigation**: Users can jump to any phase freely. Restricting navigation (e.g., only forward) is out of scope per the spec.
- **All participants can click**: No role-based gating. Any board participant can change the phase. Role restrictions are out of scope.
- **Autonomous mode not gated**: Clicking a phase step is allowed even during autonomous AI sessions. Conflict avoidance is out of scope.
