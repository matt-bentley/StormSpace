# Phase 5: Large Components — inject() + viewChild() + takeUntilDestroyed()

**Objective**: Convert `BoardCanvasComponent` (1,976 lines) and `BoardComponent` (995 lines) — the two largest components — from constructor injection, `@ViewChild` decorators, and `destroy$` patterns to modern Angular equivalents.

**Files to modify**:
- `src/eventstormingboard.client/src/app/board/board-canvas/board-canvas.component.ts` — inject(), viewChild(), takeUntilDestroyed()
- `src/eventstormingboard.client/src/app/board/board.component.ts` — inject(), takeUntilDestroyed()

## Context

These are the largest files in the frontend. `BoardCanvasComponent` has 2 `@ViewChild` (static), constructor injection (5 deps), and `destroy$` with 3 subscriptions. `BoardComponent` has constructor injection (6 deps) and `destroy$` with 16+ subscriptions.

**Strategy**: Apply only the mechanical inject/viewChild/takeUntilDestroyed transformations. Do NOT attempt to convert local state variables to signals or restructure subscriptions — these files are too large for invasive refactoring in a single phase.

**Key risk**: `BoardCanvasComponent` uses `@ViewChild('canvas', { static: true })` for canvas elements. Signal `viewChild.required()` has no `static` option — it always behaves like `{ static: false }`, resolving after the view initializes. Since the existing code accesses canvas elements in `ngAfterViewInit → generateCanvas()`, the timing is compatible. The `viewChild.required()` signal will be set by the time `ngAfterViewInit` runs. Verify canvas init still works after migration.

## Knowledge, Instructions & Skills

- `.github/instructions/angular.instructions.md` — `inject()`, `viewChild()`, `takeUntilDestroyed()` conventions

## Implementation Steps

### Step 1: Convert BoardCanvasComponent

#### 1a. Update imports

**Remove** from `@angular/core`: `ViewChild`
**Add** to `@angular/core`: `inject`, `viewChild`, `DestroyRef`
**Add**: `import { takeUntilDestroyed } from '@angular/core/rxjs-interop';`
**Remove** from `rxjs`: `Subject`, `takeUntil`

Keep `AfterViewInit`, `Component`, `ElementRef`, `HostListener`, `OnDestroy`, `OnInit`. Actually, remove `OnDestroy` since we're removing `destroy$` and `ngOnDestroy`.

Wait — `OnDestroy` may be removed IF the only thing in `ngOnDestroy` is `destroy$.next()` + `destroy$.complete()`. Let's check:

```typescript
public ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
}
```

Yes, that's all. So remove `OnDestroy` from `implements` and the `ngOnDestroy()` method.

#### 1b. Convert @ViewChild to viewChild()

**Current:**
```typescript
@ViewChild('canvas', { static: true })
public canvas!: ElementRef<HTMLCanvasElement>;

@ViewChild('minimap', { static: true })
public minimap!: ElementRef<HTMLCanvasElement>;
```

**Target:**
```typescript
public canvas = viewChild.required<ElementRef<HTMLCanvasElement>>('canvas');
public minimap = viewChild.required<ElementRef<HTMLCanvasElement>>('minimap');
```

Using `viewChild.required()` because these elements are always present (static template elements). The signal resolves after view init (no `static` option exists for signal queries). The signal returns `ElementRef<HTMLCanvasElement>` directly (not `undefined`). Only access from `ngAfterViewInit` onward.

#### 1c. Update all canvas/minimap references

Every reference to `this.canvas.nativeElement` must become `this.canvas().nativeElement`, and similarly for `this.minimap.nativeElement` → `this.minimap().nativeElement`.

These are used extensively throughout the 1,976-line file. Key locations include:
- `onMinimapClick()`: `this.minimap.nativeElement.getBoundingClientRect()`
- `generateCanvas()`: `this.ctx = this.canvas.nativeElement.getContext('2d')!`
- `onResize()`: `this.canvas.nativeElement.width = window.innerWidth`
- `drawCanvas()`: numerous `this.canvas.nativeElement` references
- `getMousePos()`: `this.canvas.nativeElement.getBoundingClientRect()`
- All mouse event handlers accessing canvas dimensions
- Minimap rendering methods

**Search pattern**: Find all occurrences of `this.canvas.nativeElement` and `this.minimap.nativeElement` in the file and add `()` after `canvas`/`minimap`.

Also search for any bare `this.canvas` or `this.minimap` references that don't chain `.nativeElement` (e.g., passed as arguments).

#### 1d. Convert constructor injection to inject()

**Current:**
```typescript
constructor(
    private dialog: MatDialog,
    private canvasService: BoardCanvasService,
    private boardsHub: BoardsSignalRService,
    private themeService: ThemeService,
    private userService: UserService
) {
}
```

**Target:**
```typescript
private dialog = inject(MatDialog);
private canvasService = inject(BoardCanvasService);
private boardsHub = inject(BoardsSignalRService);
private themeService = inject(ThemeService);
private userService = inject(UserService);
private destroyRef = inject(DestroyRef);
```

Remove the empty constructor.

#### 1e. Replace destroy$ + takeUntil with takeUntilDestroyed

**Current (in ngOnInit):**
```typescript
this.canvasService.canvasUpdated$
    .pipe(takeUntil(this.destroy$))
    .subscribe(() => { this.drawCanvas(); });

this.canvasService.canvasImageDownloaded$
    .pipe(takeUntil(this.destroy$))
    .subscribe(() => { this.exportBoardAsImage(); });

this.themeService.theme$
    .pipe(takeUntil(this.destroy$))
    .subscribe(() => { if (this.ctx) { this.drawCanvas(); } });
```

**Target:**
```typescript
this.canvasService.canvasUpdated$
    .pipe(takeUntilDestroyed(this.destroyRef))
    .subscribe(() => { this.drawCanvas(); });

this.canvasService.canvasImageDownloaded$
    .pipe(takeUntilDestroyed(this.destroyRef))
    .subscribe(() => { this.exportBoardAsImage(); });

this.themeService.theme$
    .pipe(takeUntilDestroyed(this.destroyRef))
    .subscribe(() => { if (this.ctx) { this.drawCanvas(); } });
```

Remove `private destroy$ = new Subject<void>();` field and `ngOnDestroy()` method.

#### 1f. Update localUserName getter

**Current:**
```typescript
private get localUserName(): string { return this.userService.displayName || 'Anonymous'; }
```

This doesn't need changes — `userService` is now a field initializer but the getter still works.

### Step 2: Convert BoardComponent

#### 2a. Update imports

**Remove** from `@angular/core`: `OnDestroy` (only if ngOnDestroy has no other logic besides destroy$)
**Add** to `@angular/core`: `inject`, `DestroyRef`
**Add**: `import { takeUntilDestroyed } from '@angular/core/rxjs-interop';`
**Remove** from `rxjs`: `Subject`, `takeUntil`

Check `ngOnDestroy`:
```typescript
public ngOnDestroy(): void {
    this.boardsHub.leaveBoard(this.id);
    this.canvasService.remoteCursors.clear();
    this.destroy$.next();
    this.destroy$.complete();
}
```

**Important**: `ngOnDestroy` has cleanup logic beyond `destroy$` — `leaveBoard()` and `remoteCursors.clear()`. So we must keep `OnDestroy` and `ngOnDestroy()`, but remove the destroy$ lines:

```typescript
public ngOnDestroy(): void {
    this.boardsHub.leaveBoard(this.id);
    this.canvasService.remoteCursors.clear();
}
```

#### 2b. Convert constructor injection to inject()

**Current:**
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

**Target:**
```typescript
private boardsHub = inject(BoardsSignalRService);
public canvasService = inject(BoardCanvasService);
private activatedRoute = inject(ActivatedRoute);
private boardsService = inject(BoardsService);
private dialog = inject(MatDialog);
private userService = inject(UserService);
private destroyRef = inject(DestroyRef);

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

Keep constructor for the initialization logic that references injected services.

#### 2c. Replace destroy$ + takeUntil with takeUntilDestroyed

In `subscribeToEvents()`, replace all 16+ instances of `.pipe(takeUntil(this.destroy$))` with `.pipe(takeUntilDestroyed(this.destroyRef))`.

Also in `startPruningCursors()`:
**Current:**
```typescript
private startPruningCursors(): void {
    const intervalId = setInterval(() => this.pruneStaleRemoteCursors(), 2000);
    this.destroy$.subscribe(() => clearInterval(intervalId));
}
```

**Target:**
```typescript
private startPruningCursors(): void {
    const intervalId = setInterval(() => this.pruneStaleRemoteCursors(), 2000);
    this.destroyRef.onDestroy(() => clearInterval(intervalId));
}
```

This uses `DestroyRef.onDestroy()` callback instead of subscribing to the destroy$ subject.

Remove `private destroy$ = new Subject<void>();` field.

Remove the `destroy$` lines from `ngOnDestroy()`:
```typescript
public ngOnDestroy(): void {
    this.boardsHub.leaveBoard(this.id);
    this.canvasService.remoteCursors.clear();
}
```

Also remove `Subject`, `takeUntil` from the `rxjs` import statement.

#### 2d. Handle ngOnInit board data subscription

The `ngOnInit` has an unguarded `.subscribe()` that doesn't use `takeUntil`:
```typescript
this.boardsService.getById(this.id)
    .subscribe(board => { ... });
```

This is an HTTP one-shot that completes after emitting — it doesn't need unsubscription. Leave it as-is.

#### 2e. Handle dialog afterClosed subscriptions

Several methods subscribe to `dialogRef.afterClosed()`:
```typescript
dialogRef.afterClosed().subscribe((result) => { ... });
```

These are also one-shot observables (complete after dialog closes). They don't need `takeUntilDestroyed`. Leave as-is.

## Verification Criteria

- [ ] `cd src/eventstormingboard.client && npm run build` passes with no errors
- [ ] No `@ViewChild` decorators in BoardCanvasComponent
- [ ] No `destroy$` Subject in either component
- [ ] No `takeUntil(this.destroy$)` in either component
- [ ] All 16+ subscription pipes in BoardComponent use `takeUntilDestroyed(this.destroyRef)`
- [ ] All 3 subscription pipes in BoardCanvasComponent use `takeUntilDestroyed(this.destroyRef)`
- [ ] `this.canvas().nativeElement` and `this.minimap().nativeElement` used throughout BoardCanvasComponent
- [ ] Canvas renders correctly (manual visual check recommended)
- [ ] `DestroyRef.onDestroy()` used for interval cleanup in BoardComponent
- [ ] `inject()` used for all DI in both components
