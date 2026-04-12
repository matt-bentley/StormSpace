# Phase 3: Root & Splash — inject() + takeUntilDestroyed()

**Objective**: Convert `AppComponent` and `SplashComponent` from legacy patterns to modern Angular: `inject()` for DI and `takeUntilDestroyed()` for cleanup. (`toSignal()` was considered but is not feasible here — `userName` is two-way bound via `[(ngModel)]` and requires a mutable property.)

**Files to modify**:
- `src/eventstormingboard.client/src/app/app.component.ts` — inject(), takeUntilDestroyed()
- `src/eventstormingboard.client/src/app/splash/splash.component.ts` — inject(), takeUntilDestroyed()

## Context

Both components follow the same pattern: constructor injection, `destroy$` Subject + `takeUntil()`, and a simple subscription that maps an observable to a local property. The `userName` property is bound via `[(ngModel)]` for editing, so it must remain a plain mutable property — a readonly `toSignal()` cannot be used with two-way binding. The migration applies `inject()` + `takeUntilDestroyed()` only. No template changes are needed.

## Knowledge, Instructions & Skills

- `.github/instructions/angular.instructions.md` — `inject()`, `takeUntilDestroyed()` conventions

## Implementation Steps

### Step 1: Convert AppComponent

**Current pattern:**
```typescript
import { Component, OnDestroy, OnInit } from '@angular/core';
// ...
import { Subject, takeUntil } from 'rxjs';

export class AppComponent implements OnInit, OnDestroy {
  public userName: string = '';
  private destroy$ = new Subject<void>();

  constructor(private iconsService: IconsService,
    private router: Router,
    private userService: UserService,
    public themeService: ThemeService
  ) {
    this.iconsService.registerIcons();
  }

  public ngOnInit(): void {
    this.userService.displayName$.pipe(takeUntil(this.destroy$)).subscribe(name => {
      this.userName = name;
    });
  }

  public ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
```

**Target:**
```typescript
import { Component, DestroyRef, inject, OnInit } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
// ...

export class AppComponent implements OnInit {
  private iconsService = inject(IconsService);
  private router = inject(Router);
  private userService = inject(UserService);
  public themeService = inject(ThemeService);
  private destroyRef = inject(DestroyRef);

  public userName: string = '';

  constructor() {
    this.iconsService.registerIcons();
  }

  public ngOnInit(): void {
    this.userService.displayName$.pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(name => {
      this.userName = name;
    });
  }
```

Changes:
1. Constructor injection → `inject()` field initializers
2. Remove `OnDestroy`, `destroy$`, `ngOnDestroy()`
3. Add `DestroyRef` injection via `inject(DestroyRef)`
4. Replace `takeUntil(this.destroy$)` with `takeUntilDestroyed(this.destroyRef)`
5. Keep constructor for `iconsService.registerIcons()` side effect
6. No template changes needed — `userName` stays a plain mutable property for `[(ngModel)]`

**Reference**: `src/eventstormingboard.client/src/app/app.component.ts` (73 lines)

### Step 2: Convert SplashComponent

**Current pattern:**
```typescript
import { Component, OnDestroy, OnInit } from '@angular/core';
// ...
import { Subject, takeUntil } from 'rxjs';

export class SplashComponent implements OnInit, OnDestroy {
  public userName: string = '';
  private destroy$ = new Subject<void>();

  constructor(
    private router: Router,
    private dialog: MatDialog,
    private userService: UserService) { }

  public ngOnInit(): void {
    this.userService.displayName$.pipe(takeUntil(this.destroy$)).subscribe(name => {
      this.userName = name;
    });
  }

  public ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
```

**Target:**
```typescript
import { Component, DestroyRef, inject, OnInit } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
// ...

export class SplashComponent implements OnInit {
  private router = inject(Router);
  private dialog = inject(MatDialog);
  private userService = inject(UserService);
  private destroyRef = inject(DestroyRef);

  public userName: string = '';

  public ngOnInit(): void {
    this.userService.displayName$.pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(name => {
      this.userName = name;
    });
  }
```

Changes:
1. Constructor injection → `inject()`
2. Remove `OnDestroy`, `destroy$`, `ngOnDestroy()`
3. Add `DestroyRef` + `takeUntilDestroyed()`
4. Remove `Subject`, `takeUntil` from rxjs imports
5. Remove constructor entirely

Same rationale as AppComponent — `userName` is used with `[(ngModel)]` for editing, so keep as plain property. No template changes needed.

**Reference**: `src/eventstormingboard.client/src/app/splash/splash.component.ts` (73 lines)

## Verification Criteria

- [ ] `cd src/eventstormingboard.client && npm run build` passes with no errors
- [ ] No `destroy$` Subject in AppComponent or SplashComponent
- [ ] No `ngOnDestroy()` in AppComponent or SplashComponent
- [ ] `takeUntilDestroyed` imported from `@angular/core/rxjs-interop`
- [ ] `DestroyRef` injected via `inject(DestroyRef)` in both components
- [ ] No constructor injection in either component
- [ ] Templates render correctly (userName binding unchanged)
