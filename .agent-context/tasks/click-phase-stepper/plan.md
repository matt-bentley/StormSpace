# Plan: Clickable Phase Stepper

**Objective**: Allow users to change the workshop phase by clicking directly on a step in the phase stepper, with a reusable confirmation modal.
**Phases**: 2
**Estimated complexity**: Low

## Context

The phase stepper at the bottom-left of the board canvas currently displays the workshop phase as a read-only visual indicator. Users must open the "Board Context for AI" modal to change the phase. This task makes each step clickable with a confirmation prompt, reusing the existing `UpdateBoardContextCommand` and SignalR broadcast flow. No backend changes are needed.

## Phase Summary

| Phase | Name | Description | Files Modified | Verification |
|-------|------|-------------|----------------|--------------|
| 1 | Confirmation Modal | Create a reusable confirmation modal component following Kinetic Ionization design system | New: `_shared/components/confirm-modal/` (3 files) | `npm run build` passes, component files exist with correct exports |
| 2 | Phase Stepper Click | Wire click handlers on phase stepper steps to open the confirmation modal and execute the phase change | `board.component.ts`, `board.component.html` | `npm run build` passes, clicking a step opens modal, confirming changes phase via SignalR |

## Dependencies

- No new packages required — uses existing `MatDialogModule`, `MatButtonModule`
- No backend changes — reuses existing `BoardContextUpdatedEvent` and `BroadcastBoardContextUpdated` hub method

## Risks

- **Undo/redo coherence**: The `UpdateBoardContextCommand` updates all context fields (domain, sessionScope, phase, autonomousEnabled). When changing only phase, the other fields must pass through unchanged (current values as both old and new) to avoid unintended side effects on undo. The existing `openBoardContext()` method in `board.component.ts` shows the correct pattern.
- **Race conditions**: Multiple users clicking phase changes simultaneously is handled by the existing per-board `SemaphoreSlim` locking on the backend. No additional concern.
- **Autonomous mode**: The plan does not gate stepper clicks on `autonomousEnabled`. This matches existing behavior (the Board Context modal doesn't guard either) and the spec does not require it. If autonomous-mode gating is desired later, it can be added as a follow-up.
