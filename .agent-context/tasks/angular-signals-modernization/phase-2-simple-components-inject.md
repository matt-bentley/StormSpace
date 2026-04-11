# Phase 2: Simple Components — inject() Migration

**Objective**: Convert all 7 simple modal/leaf components from constructor injection (including `@Inject()` decorator) to the `inject()` function.

**Files to modify**:
- `src/eventstormingboard.client/src/app/board/board-canvas/bc-name-modal/bc-name-modal.component.ts` — @Inject(MAT_DIALOG_DATA) + MatDialogRef
- `src/eventstormingboard.client/src/app/board/board-canvas/note-text-modal/note-text-modal.component.ts` — @Inject(MAT_DIALOG_DATA) + MatDialogRef
- `src/eventstormingboard.client/src/app/board/keyboard-shortcuts-modal/keyboard-shortcuts-modal.component.ts` — MatDialogRef
- `src/eventstormingboard.client/src/app/board/board-context-modal/board-context-modal.component.ts` — @Inject(MAT_DIALOG_DATA) + MatDialogRef
- `src/eventstormingboard.client/src/app/splash/select-board-modal/select-board-modal.component.ts` — @Inject(MAT_DIALOG_DATA) + MatDialogRef + BoardsService
- `src/eventstormingboard.client/src/app/splash/create-board-modal/create-board-modal.component.ts` — @Inject(MAT_DIALOG_DATA) + MatDialogRef + BoardsService
- `src/eventstormingboard.client/src/app/board/agent-config-modal/agent-config-modal.component.ts` — @Inject(MAT_DIALOG_DATA) + MatDialogRef + BoardsService

## Context

These are leaf components (modals and simple views) with no child components that need signal I/O. They only require converting `constructor(... @Inject(MAT_DIALOG_DATA) ...)` to `inject()`. Template bindings to `dialogRef` and `data` continue to work since they become regular class properties.

**Critical**: Several of these components use `public dialogRef` and `public data` which are referenced directly in templates (e.g., `(click)="dialogRef.close()"`, `[(ngModel)]="data.name"`). The `inject()` conversion must keep these as `public` (or at minimum non-private) fields.

## Knowledge, Instructions & Skills

- `.github/instructions/angular.instructions.md` — `inject()` convention: `inject(MAT_DIALOG_DATA)`, `inject(ActivatedRoute)`

## Implementation Steps

### Step 1: Convert BcNameModalComponent

**Current:**
```typescript
import { Component, Inject } from '@angular/core';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
// ...

export class BcNameModalComponent {
  constructor(
    public dialogRef: MatDialogRef<BcNameModalComponent>,
    @Inject(MAT_DIALOG_DATA) public data: { name: string }
  ) {}
```

**Target:**
```typescript
import { Component, inject } from '@angular/core';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
// ...

export class BcNameModalComponent {
  public dialogRef = inject<MatDialogRef<BcNameModalComponent>>(MatDialogRef);
  public data = inject<{ name: string }>(MAT_DIALOG_DATA);
```

Remove `Inject` from the `@angular/core` import, add `inject`. Remove the constructor entirely.

### Step 2: Convert NoteTextModalComponent

Identical pattern to BcNameModalComponent.

**Target:**
```typescript
import { Component, inject } from '@angular/core';
// ...
export class NoteTextModalComponent {
  public dialogRef = inject<MatDialogRef<NoteTextModalComponent>>(MatDialogRef);
  public data = inject<{ text: string }>(MAT_DIALOG_DATA);
```

### Step 3: Convert KeyboardShortcutsModalComponent

This one only has `MatDialogRef`, no `MAT_DIALOG_DATA`.

**Current:**
```typescript
constructor(public dialogRef: MatDialogRef<KeyboardShortcutsModalComponent>) {}
```

**Target:**
```typescript
public dialogRef = inject<MatDialogRef<KeyboardShortcutsModalComponent>>(MatDialogRef);
```

Remove `Component` from needing changes, add `inject` to `@angular/core` import. Remove constructor.

### Step 4: Convert BoardContextModalComponent

**Current:**
```typescript
import { Component, Inject } from '@angular/core';
// ...
export class BoardContextModalComponent {
  public phases = EVENT_STORMING_PHASES;

  constructor(
    public dialogRef: MatDialogRef<BoardContextModalComponent>,
    @Inject(MAT_DIALOG_DATA) public data: BoardContextData
  ) { }
```

**Target:**
```typescript
import { Component, inject } from '@angular/core';
// ...
export class BoardContextModalComponent {
  public phases = EVENT_STORMING_PHASES;
  public dialogRef = inject<MatDialogRef<BoardContextModalComponent>>(MatDialogRef);
  public data = inject<BoardContextData>(MAT_DIALOG_DATA);
```

### Step 5: Convert SelectBoardModalComponent

Has `@Inject(MAT_DIALOG_DATA)` plus `BoardsService` injection. Also implements `OnInit`.

**Current:**
```typescript
import { Component, Inject, OnInit } from '@angular/core';
// ...
export class SelectBoardModalComponent implements OnInit {
  boards: BoardSummaryDto[] = [];
  selectedBoardId: string | null = null;

  constructor(
    public dialogRef: MatDialogRef<SelectBoardModalComponent>,
    private boardsService: BoardsService,
    @Inject(MAT_DIALOG_DATA) public data: { id: string }
  ) { }
```

**Target:**
```typescript
import { Component, inject, OnInit } from '@angular/core';
// ...
export class SelectBoardModalComponent implements OnInit {
  public dialogRef = inject<MatDialogRef<SelectBoardModalComponent>>(MatDialogRef);
  private boardsService = inject(BoardsService);
  public data = inject<{ id: string }>(MAT_DIALOG_DATA);

  boards: BoardSummaryDto[] = [];
  selectedBoardId: string | null = null;
```

Remove `Inject` from imports, add `inject`. Remove constructor.

### Step 6: Convert CreateBoardModalComponent

**Current:**
```typescript
import { Component, Inject } from '@angular/core';
// ...
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

**Target:**
```typescript
import { Component, inject } from '@angular/core';
// ...
export class CreateBoardModalComponent {
  public dialogRef = inject<MatDialogRef<CreateBoardModalComponent>>(MatDialogRef);
  private boardsService = inject(BoardsService);
  public data = inject<{ name: string; domain?: string; sessionScope?: string }>(MAT_DIALOG_DATA);
```

### Step 7: Convert AgentConfigModalComponent

Has `@Inject(MAT_DIALOG_DATA)` plus `MatDialogRef` plus `BoardsService`. Implements `OnInit`.

**Current:**
```typescript
import { Component, Inject, OnInit } from '@angular/core';
// ...
export class AgentConfigModalComponent implements OnInit {
  // ... fields ...
  constructor(
    public dialogRef: MatDialogRef<AgentConfigModalComponent>,
    @Inject(MAT_DIALOG_DATA) public data: AgentConfigModalData,
    private boardsService: BoardsService
  ) {}
```

**Target:**
```typescript
import { Component, inject, OnInit } from '@angular/core';
// ...
export class AgentConfigModalComponent implements OnInit {
  public dialogRef = inject<MatDialogRef<AgentConfigModalComponent>>(MatDialogRef);
  public data = inject<AgentConfigModalData>(MAT_DIALOG_DATA);
  private boardsService = inject(BoardsService);
  // ... fields ...
```

## Verification Criteria

- [ ] `cd src/eventstormingboard.client && npm run build` passes with no errors
- [ ] No `@Inject()` decorator remains in any modal component
- [ ] No constructor injection in any of the 7 files
- [ ] `public dialogRef` and `public data` remain accessible for template bindings
- [ ] `inject` imported from `@angular/core` in all modified files
- [ ] `MatDialogRef` typed via `inject<MatDialogRef<X>>(MatDialogRef)` pattern (type parameter on `inject`, not instantiation expression)
