# Phase 1: Services DI Modernization

**Objective**: Convert all 7 Angular services from constructor injection to the `inject()` function pattern, aligning with the project's Angular conventions.

**Files to modify**:
- `src/eventstormingboard.client/src/app/_shared/services/auth.service.ts` — constructor `MsalService` → `inject()`
- `src/eventstormingboard.client/src/app/_shared/services/boards.service.ts` — constructor `HttpClient` → `inject()`
- `src/eventstormingboard.client/src/app/_shared/services/boards-signalr.service.ts` — constructor `AuthService` → `inject()`
- `src/eventstormingboard.client/src/app/_shared/services/theme.service.ts` — no constructor params, no changes needed (already clean)
- `src/eventstormingboard.client/src/app/_shared/services/user.service.ts` — constructor `AuthService` → `inject()`
- `src/eventstormingboard.client/src/app/_shared/services/icons.service.ts` — constructor `MatIconRegistry`, `DomSanitizer` → `inject()`
- `src/eventstormingboard.client/src/app/board/board-canvas/board-canvas.service.ts` — constructor `BoardsSignalRService` → `inject()`

## Context

Services are the foundation of the app. Converting them first ensures all downstream component changes in later phases build on modernized services. Services retain their RxJS Subject-based event streaming — only the DI pattern changes.

## Knowledge, Instructions & Skills

- `.github/instructions/angular.instructions.md` — Defines `inject()` as the required DI pattern for services

## Implementation Steps

### Step 1: Convert `AuthService`

Replace constructor injection with `inject()`. Note: `MsalService` can be null (optional auth), so use `inject(MsalService, { optional: true })`.

**Before:**
```typescript
import { Injectable } from '@angular/core';
import { MsalService } from '@azure/msal-angular';

constructor(private msalService: MsalService | null) {
  this._initialized$.next();
}
```

**After:**
```typescript
import { inject, Injectable } from '@angular/core';
import { MsalService } from '@azure/msal-angular';

private msalService = inject(MsalService, { optional: true });

constructor() {
  this._initialized$.next();
}
```

Note: The `MsalService | null` typing with optional inject produces `MsalService | null`, keeping the same type.

### Step 2: Convert `BoardsService`

**Before:**
```typescript
constructor(private http: HttpClient) {}
```

**After:**
```typescript
private http = inject(HttpClient);
```

Remove the empty constructor entirely.

### Step 3: Convert `BoardsSignalRService`

**Before:**
```typescript
constructor(private authService: AuthService) { ... }
```

**After:**
```typescript
private authService = inject(AuthService);

constructor() { ... }
```

Keep the constructor body if it has initialization logic — just remove the parameter.

### Step 4: Convert `UserService`

**Before:**
```typescript
constructor(private authService: AuthService) {
  if (this.authService.isAuthEnabled) {
    const authName = this.authService.getUserName();
    if (authName) {
      this.userName$.next(authName);
    }
  }
}
```

**After:**
```typescript
private authService = inject(AuthService);

constructor() {
  if (this.authService.isAuthEnabled) {
    const authName = this.authService.getUserName();
    if (authName) {
      this.userName$.next(authName);
    }
  }
}
```

### Step 5: Convert `IconsService`

**Before:**
```typescript
constructor(private iconRegistry: MatIconRegistry, private sanitizer: DomSanitizer) {}
```

**After:**
```typescript
private iconRegistry = inject(MatIconRegistry);
private sanitizer = inject(DomSanitizer);
```

Remove the empty constructor.

### Step 6: Convert `BoardCanvasService`

This is component-provided (`@Injectable()` without `providedIn`), but `inject()` works the same way.

**Before:**
```typescript
constructor(
  private boardsHub: BoardsSignalRService
) { }
```

**After:**
```typescript
private boardsHub = inject(BoardsSignalRService);
```

Remove the empty constructor.

### Step 7: Verify `ThemeService`

`ThemeService` has a parameterless constructor with initialization logic. No changes needed — it's already clean. Just verify it compiles.

## Verification Criteria

- [ ] `cd src/eventstormingboard.client && npm run build` passes with no errors
- [ ] All 7 service files use `inject()` for DI (or have no injectable dependencies)
- [ ] No `constructor(private someService: SomeType)` patterns remain in service files
- [ ] `import { inject } from '@angular/core'` added to each modified file
