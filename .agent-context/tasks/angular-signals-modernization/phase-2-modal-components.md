# Phase 2: Modal Components

**Objective**: Convert all 7 modal/dialog components from constructor injection and `@Inject()` decorators to `inject()`.

**Files to modify**:
- `src/eventstormingboard.client/src/app/splash/create-board-modal/create-board-modal.component.ts` — constructor + `@Inject(MAT_DIALOG_DATA)` → `inject()`
- `src/eventstormingboard.client/src/app/splash/select-board-modal/select-board-modal.component.ts` — constructor + `@Inject(MAT_DIALOG_DATA)` → `inject()`
- `src/eventstormingboard.client/src/app/board/keyboard-shortcuts-modal/keyboard-shortcuts-modal.component.ts` — constructor `MatDialogRef` → `inject()`
- `src/eventstormingboard.client/src/app/board/board-context-modal/board-context-modal.component.ts` — constructor + `@Inject(MAT_DIALOG_DATA)` → `inject()`
- `src/eventstormingboard.client/src/app/board/agent-config-modal/agent-config-modal.component.ts` — constructor + `@Inject(MAT_DIALOG_DATA)` → `inject()`
- `src/eventstormingboard.client/src/app/board/board-canvas/note-text-modal/note-text-modal.component.ts` — constructor + `@Inject(MAT_DIALOG_DATA)` → `inject()`
- `src/eventstormingboard.client/src/app/board/board-canvas/bc-name-modal/bc-name-modal.component.ts` — constructor + `@Inject(MAT_DIALOG_DATA)` → `inject()`

## Context

These are the simplest components in the codebase — small dialog modals with no inputs, outputs, subscriptions, or destroy patterns. The only modernization needed is converting constructor injection and `@Inject(MAT_DIALOG_DATA)` to `inject()`.

Note: `data` properties injected via `MAT_DIALOG_DATA` are used with `[(ngModel)]` bindings in templates (e.g., `[(ngModel)]="data.name"`). Since `inject()` returns the same object reference, all two-way bindings continue to work without template changes.

## Knowledge, Instructions & Skills

- `.github/instructions/angular.instructions.md` — Defines `inject()` as the required DI pattern, including `inject(MAT_DIALOG_DATA)` for dialog data

## Implementation Steps

### Step 1: Convert `CreateBoardModalComponent`

**Before:**
```typescript
import { Component, Inject } from '@angular/core';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';

export class CreateBoardModalComponent {
  constructor(
    public dialogRef: MatDialogRef<CreateBoardModalComponent>,
    private boardsService: BoardsService,
    @Inject(MAT_DIALOG_DATA) public data: {
      name: string;
      domain?: string;
      sessionScope?: string;
    }
  ) { }
```

**After:**
```typescript
import { Component, inject } from '@angular/core';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';

export class CreateBoardModalComponent {
  readonly dialogRef = inject(MatDialogRef<CreateBoardModalComponent>);
  private boardsService = inject(BoardsService);
  readonly data = inject<{ name: string; domain?: string; sessionScope?: string }>(MAT_DIALOG_DATA);
```

**Key pattern**: `@Inject(MAT_DIALOG_DATA) public data: T` becomes `readonly data = inject<T>(MAT_DIALOG_DATA)`. Use `readonly` for `dialogRef` and `data` since they are referenced in templates.

### Step 2: Convert `SelectBoardModalComponent`

Same pattern. Replace constructor injection and `@Inject(MAT_DIALOG_DATA)`.

**After:**
```typescript
readonly dialogRef = inject(MatDialogRef<SelectBoardModalComponent>);
private boardsService = inject(BoardsService);
readonly data = inject<{ id: string }>(MAT_DIALOG_DATA);
```

### Step 3: Convert `KeyboardShortcutsModalComponent`

Simplest component — only has `MatDialogRef`.

**Before:**
```typescript
constructor(public dialogRef: MatDialogRef<KeyboardShortcutsModalComponent>) {}
```

**After:**
```typescript
readonly dialogRef = inject(MatDialogRef<KeyboardShortcutsModalComponent>);
```

### Step 4: Convert `BoardContextModalComponent`

**After:**
```typescript
readonly dialogRef = inject(MatDialogRef<BoardContextModalComponent>);
readonly data = inject<BoardContextData>(MAT_DIALOG_DATA);
```

### Step 5: Convert `AgentConfigModalComponent`

Has additional `BoardsService` dependency, plus `OnInit`.

**Before:**
```typescript
constructor(
  public dialogRef: MatDialogRef<AgentConfigModalComponent>,
  @Inject(MAT_DIALOG_DATA) public data: AgentConfigModalData,
  private boardsService: BoardsService
) { }
```

**After:**
```typescript
readonly dialogRef = inject(MatDialogRef<AgentConfigModalComponent>);
readonly data = inject<AgentConfigModalData>(MAT_DIALOG_DATA);
private boardsService = inject(BoardsService);
```

### Step 6: Convert `NoteTextModalComponent`

**After:**
```typescript
readonly dialogRef = inject(MatDialogRef<NoteTextModalComponent>);
readonly data = inject<{ text: string }>(MAT_DIALOG_DATA);
```

### Step 7: Convert `BcNameModalComponent`

**After:**
```typescript
readonly dialogRef = inject(MatDialogRef<BcNameModalComponent>);
readonly data = inject<{ name: string }>(MAT_DIALOG_DATA);
```

### Import Changes

For all files:
- Remove `Inject` from `@angular/core` import
- Add `inject` to `@angular/core` import (lowercase function)
- Keep `MAT_DIALOG_DATA` import from `@angular/material/dialog`

## Verification Criteria

- [ ] `cd src/eventstormingboard.client && npm run build` passes with no errors
- [ ] No `@Inject()` decorators remain in any modal component
- [ ] No constructor parameters remain in any modal component
- [ ] All `dialogRef` and `data` properties accessible in templates (use `readonly` visibility)
- [ ] Dialog two-way bindings (`[(ngModel)]="data.name"` etc.) still function correctly
