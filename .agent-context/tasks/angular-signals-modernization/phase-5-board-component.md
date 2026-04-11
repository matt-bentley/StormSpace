# Phase 5: Board Component

**Objective**: Convert the main board orchestrator component (1243 lines) to use `inject()` and `takeUntilDestroyed()`, eliminating the `destroy$` pattern across 20 SignalR subscriptions.

**Files to modify**:
- `src/eventstormingboard.client/src/app/board/board.component.ts` — constructor → `inject()`, `destroy$` → `takeUntilDestroyed()` for 23 subscriptions
- `src/eventstormingboard.client/src/app/board/board.component.html` — minimal changes (no `@Input`/`@Output` conversions, but verify template still works)

## Context

The board component is the main orchestrator for the board editor view. It has no `@Input()` or `@Output()` decorators (it's a routed component), but it has the heaviest subscription load in the app: 23 separate SignalR event subscriptions, all using `takeUntil(this.destroy$)`. This phase is purely about DI modernization and cleanup pattern replacement.

The component also has a large amount of imperative logic (import/export, agent config management, clipboard operations). None of that changes — only the DI and subscription cleanup patterns.

## Knowledge, Instructions & Skills

- `.github/instructions/angular.instructions.md` — Defines `inject()` and `takeUntilDestroyed()` patterns

## Implementation Steps

### Step 1: Convert constructor injection to `inject()`

**Before:**
```typescript
constructor(
  private boardsHub: BoardsSignalRService,
  public canvasService: BoardCanvasService,
  private activatedRoute: ActivatedRoute,
  private boardsService: BoardsService,
  private dialog: MatDialog,
  private userService: UserService
) {
  this.canvasService.boardState = {
    name: '',
    autonomousEnabled: false,
    connections: [],
    notes: [],
    boundedContexts: []
  };
  this.id = this.activatedRoute.snapshot.paramMap.get('id') || '';
  this.userName = this.userService.displayName || 'Anonymous';
  this.previousName = this.canvasService.boardState.name;
}
```

**After:**
```typescript
private boardsHub = inject(BoardsSignalRService);
readonly canvasService = inject(BoardCanvasService);
private activatedRoute = inject(ActivatedRoute);
private boardsService = inject(BoardsService);
private dialog = inject(MatDialog);
private userService = inject(UserService);

constructor() {
  this.canvasService.boardState = {
    name: '',
    autonomousEnabled: false,
    connections: [],
    notes: [],
    boundedContexts: []
  };
  this.id = this.activatedRoute.snapshot.paramMap.get('id') || '';
  this.userName = this.userService.displayName || 'Anonymous';
  this.previousName = this.canvasService.boardState.name;
}
```

Note: `canvasService` must be `readonly` (not `private`) because it's accessed in the template (e.g., `canvasService.boardState.name`, `canvasService.id`).

### Step 2: Replace `destroy$` with `takeUntilDestroyed()`

**Before (in `ngOnInit`):**
```typescript
private destroy$ = new Subject<void>();

ngOnInit(): void {
  this.boardsHub.joinBoard(this.id, this.userName);
  this.canvasService.id = this.id;

  this.boardsService.getById(this.id).subscribe(board => { ... });

  this.boardsHub.connectedUsers$.pipe(takeUntil(this.destroy$)).subscribe(users => { ... });
  this.boardsHub.userJoinedBoard$.pipe(takeUntil(this.destroy$)).subscribe(event => { ... });
  // ... 18 more subscriptions
}

ngOnDestroy(): void {
  this.boardsHub.leaveBoard(this.id);
  this.destroy$.next();
  this.destroy$.complete();
}
```

**After:**
```typescript
// No destroy$ field needed

ngOnInit(): void {
  this.boardsHub.joinBoard(this.id, this.userName);
  this.canvasService.id = this.id;

  this.boardsService.getById(this.id).subscribe(board => { ... });

  this.boardsHub.connectedUsers$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(users => { ... });
  this.boardsHub.userJoinedBoard$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(event => { ... });
  // ... 18 more subscriptions with takeUntilDestroyed(this.destroyRef)
}

ngOnDestroy(): void {
  this.boardsHub.leaveBoard(this.id);
  // No destroy$ cleanup needed
}
```

**Important**: Since subscriptions are in `ngOnInit()` (not constructor), `takeUntilDestroyed()` without arguments won't work — it must be called in injection context (constructor). Use `inject(DestroyRef)` and pass it explicitly:

```typescript
private destroyRef = inject(DestroyRef);

ngOnInit(): void {
  // Use takeUntilDestroyed(this.destroyRef) since we're outside constructor
  this.boardsHub.connectedUsers$.pipe(
    takeUntilDestroyed(this.destroyRef)
  ).subscribe(users => { ... });
}
```

Alternatively, move all subscriptions to the constructor. However, since there's also `boardsService.getById()` and `boardsHub.joinBoard()` calls that logically belong in init, keeping `ngOnInit()` with explicit `DestroyRef` is cleaner.

**Keep `ngOnDestroy()`** — it still needs `this.boardsHub.leaveBoard(this.id)` and `this.canvasService.remoteCursors.clear()` for cleanup. Just remove the `destroy$.next()` and `destroy$.complete()` lines.

### Step 3: Replace `destroy$.subscribe()` in `startPruningCursors()`

The method `startPruningCursors()` uses `this.destroy$.subscribe(() => clearInterval(intervalId))` to clean up the cursor-pruning interval. Since `destroy$` is being removed, replace this with `DestroyRef.onDestroy()`:

**Before:**
```typescript
private startPruningCursors(): void {
  const intervalId = setInterval(() => this.pruneStaleRemoteCursors(), 2000);
  this.destroy$.subscribe(() => clearInterval(intervalId));
}
```

**After:**
```typescript
private startPruningCursors(): void {
  const intervalId = setInterval(() => this.pruneStaleRemoteCursors(), 2000);
  this.destroyRef.onDestroy(() => clearInterval(intervalId));
}
```

### Step 4: Update imports

```typescript
// Remove
import { Subject, takeUntil } from 'rxjs';

// Add
import { inject, DestroyRef } from '@angular/core';  // add to existing @angular/core import
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
```

Keep `OnDestroy` in the interface list (still needed for `leaveBoard` cleanup).

### Step 5: Bulk replace `takeUntil(this.destroy$)` → `takeUntilDestroyed(this.destroyRef)`

This is a mechanical find-and-replace across the file:
- `takeUntil(this.destroy$)` → `takeUntilDestroyed(this.destroyRef)` (20 occurrences)
- `this.destroy$.subscribe(...)` in `startPruningCursors()` → `this.destroyRef.onDestroy(...)` (Step 3 above)
- Remove the `private destroy$ = new Subject<void>();` field
- Remove `this.destroy$.next();` and `this.destroy$.complete();` from `ngOnDestroy()`
- Keep `this.boardsHub.leaveBoard(this.id)` and `this.canvasService.remoteCursors.clear()` in `ngOnDestroy()`

### Step 6: Verify template still works

The board component template references `canvasService` publicly. After converting to `inject()`, ensure `canvasService` is `readonly` (publicly accessible). Scan the template for any references that might break:
- `canvasService.boardState.name` — works (canvasService is readonly, boardState is mutable)
- `canvasService.id` — works
- All other template references are to component's own properties (no change)

## Verification Criteria

- [ ] `cd src/eventstormingboard.client && npm run build` passes with no errors
- [ ] No `destroy$` subject in `board.component.ts`
- [ ] No `takeUntil` import from `rxjs` (replaced with `takeUntilDestroyed`)
- [ ] No constructor parameters in `BoardComponent`
- [ ] All 20 subscriptions use `takeUntilDestroyed(this.destroyRef)`
- [ ] `startPruningCursors()` uses `this.destroyRef.onDestroy()` instead of `this.destroy$.subscribe()`
- [ ] `ngOnDestroy()` still calls `boardsHub.leaveBoard()` and `canvasService.remoteCursors.clear()`
- [ ] `canvasService` accessible in template (readonly, not private)
