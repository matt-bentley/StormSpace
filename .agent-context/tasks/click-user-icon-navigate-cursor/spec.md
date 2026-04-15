# Specification: Click User Icon to Navigate to Their Cursor

## Overview

When multiple users are collaborating on an Event Storming board, it can be difficult to find where a specific person is working, especially on large boards. This feature allows a user to click on another user's avatar icon in the header bar to smoothly pan the board canvas to that user's current cursor position, making it easy to follow along with what a teammate is doing.

## Goals

- Enable quick navigation to any connected user's cursor location by clicking their avatar in the header
- Provide a smooth, animated pan transition so users don't lose spatial context
- Give clear visual feedback when the target user's cursor position is unknown
- Briefly highlight the target user's cursor after navigation so it's easy to spot

## User Stories

- As a collaborator, I want to click on a teammate's icon in the header so that my view pans to where they are working on the board
- As a collaborator, I want to see the target user's cursor highlighted briefly after navigating so that I can quickly identify their exact position
- As a collaborator, I want clear feedback when a user has no known cursor position so that I understand why nothing happened

## Functional Requirements

| ID | Requirement | Priority |
|----|------------|----------|
| FR-1 | Clicking a user's avatar circle in the header pans the board canvas so that the target user's last known cursor position is centred on screen | Must |
| FR-2 | The pan transition is smoothly animated (not an instant snap) | Must |
| FR-3 | The current zoom level remains unchanged when navigating to a user's cursor | Must |
| FR-4 | If the target user has no known cursor position (e.g. just joined, hasn't moved their mouse), a tooltip is shown indicating "No cursor position available" | Must |
| FR-5 | After the pan animation completes, the target user's cursor is visually highlighted with a brief pulse or emphasis animation so it stands out | Should |
| FR-6 | Clicking the local user's own avatar does nothing (the local user's cursor is always at their own position) | Must |
| FR-7 | The user avatar circles remain clickable whether the connected users list is collapsed (showing up to 5) or expanded (showing all) | Must |

## Non-Functional Requirements

- The pan animation should feel responsive — complete within 300–500ms
- The cursor highlight effect should be subtle and non-disruptive, lasting approximately 1–2 seconds
- No additional network requests should be required — cursor positions are already tracked locally in the `remoteCursors` Map

## Constraints & Assumptions

- Remote cursor positions are already stored on the frontend in `canvasService.remoteCursors` (a Map keyed by `connectionId`)
- Each `BoardUser` in the header has a `connectionId` that maps to the `remoteCursors` Map key
- The existing minimap click-to-pan logic (`onMinimapClick`) provides a proven pattern for centring the viewport on a target coordinate
- Cursor positions become stale after 15 seconds and are pruned — if a user is idle, their cursor may not be available
- The local user's own cursor position is not stored in `remoteCursors` (only remote cursors are tracked)

## Out of Scope

- Following/tracking a user in real-time (continuous viewport following)
- Displaying a user's cursor position in the header tooltip
- Any backend changes — this feature is entirely frontend
- Adding click-to-navigate for the "+N more users" overflow indicator

## Open Questions

_None — all questions resolved via GitHub issue #46._
