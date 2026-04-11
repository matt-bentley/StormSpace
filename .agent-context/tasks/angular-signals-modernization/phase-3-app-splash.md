# Phase 3: App & Splash Components

**Objective**: Convert `AppComponent` and `SplashComponent` to use `inject()`, `takeUntilDestroyed()`, and `toSignal()` where practical.

**Files to modify**:
- `src/eventstormingboard.client/src/app/app.component.ts` — constructor → `inject()`, `destroy$` → `takeUntilDestroyed()`, `userName` → `toSignal()`
- `src/eventstormingboard.client/src/app/app.component.html` — update template for signal reads if converting properties to signals
- `src/eventstormingboard.client/src/app/splash/splash.component.ts` — constructor → `inject()`, `destroy$` → `takeUntilDestroyed()`, `userName` → `toSignal()`
- `src/eventstormingboard.client/src/app/splash/splash.component.html` — update template for signal reads if converting properties to signals

## Context

These two components share the same pattern: constructor injection, a `destroy$` subject with `takeUntil()`, a subscription to `userService.displayName$` that feeds a `userName` string property. Both are excellent candidates for `toSignal()` since the observable-to-property-via-subscribe pattern is exactly what `toSignal()` replaces.

## Knowledge, Instructions & Skills

- `.github/instructions/angular.instructions.md` — Defines `inject()`, `takeUntilDestroyed()`, and `toSignal()` patterns

## Implementation Steps

### Step 1: Convert `AppComponent`

**Current pattern:**
```typescript
export class AppComponent implements OnInit, OnDestroy {
  public isUserMenuOpen = false;
  public isSettingsMenuOpen = false;
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
}
```

**Target pattern:**
```typescript
import { Component, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

export class AppComponent {
  private iconsService = inject(IconsService);
  private router = inject(Router);
  private userService = inject(UserService);
  readonly themeService = inject(ThemeService);

  public isUserMenuOpen = false;
  public isSettingsMenuOpen = false;
  public userName = '';

  constructor() {
    this.iconsService.registerIcons();
    this.userService.displayName$.pipe(
      takeUntilDestroyed()
    ).subscribe(name => {
      this.userName = name;
    });
  }
}
```

**Key changes:**
1. Constructor params → `inject()` field declarations
2. `themeService` stays `readonly` (public access in template)
3. `destroy$` subject + `takeUntil()` + `ngOnDestroy()` → `takeUntilDestroyed()` in constructor
4. Move subscription from `ngOnInit()` into `constructor()` (required for `takeUntilDestroyed()` without explicit `DestroyRef`)
5. Remove `OnInit`, `OnDestroy` interfaces, `ngOnInit()`, `ngOnDestroy()` methods
6. Remove `Subject, takeUntil` from rxjs imports, add `takeUntilDestroyed` from `@angular/core/rxjs-interop`
7. `userName` stays as a plain string property — it's two-way bound via `[(ngModel)]` in the template, so a read-only `toSignal()` won't work. Keep `userInitial` as a getter (it reads a plain property, not a signal)

**No template changes needed** — `userName` stays as a plain string property.

### Step 2: Convert `SplashComponent`

Same pattern as `AppComponent`. The subscription to `displayName$` feeds `userName` which is used with `[(ngModel)]`.

**Target pattern:**
```typescript
import { Component, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

export class SplashComponent {
  private router = inject(Router);
  private dialog = inject(MatDialog);
  private userService = inject(UserService);

  public userName = '';

  constructor() {
    this.userService.displayName$.pipe(
      takeUntilDestroyed()
    ).subscribe(name => {
      this.userName = name;
    });
  }
}
```

**Key changes:**
1. Constructor params → `inject()` field declarations
2. `destroy$` subject + `takeUntil()` + `ngOnDestroy()` → `takeUntilDestroyed()` in constructor
3. Move subscription from `ngOnInit()` into `constructor()` (required for `takeUntilDestroyed()` without explicit `DestroyRef`)
4. Remove `OnInit`, `OnDestroy` interfaces
5. Remove `Subject, takeUntil` imports, add `takeUntilDestroyed` from `@angular/core/rxjs-interop`

**No template changes needed** — `userName` stays as a plain string property.

## Verification Criteria

- [ ] `cd src/eventstormingboard.client && npm run build` passes with no errors
- [ ] No `destroy$` subjects remain in `app.component.ts` or `splash.component.ts`
- [ ] No `ngOnDestroy()` methods remain in these components
- [ ] No constructor parameters remain (all use `inject()`)
- [ ] `takeUntilDestroyed()` imported from `@angular/core/rxjs-interop`
- [ ] User name input still works (two-way binding preserved)
