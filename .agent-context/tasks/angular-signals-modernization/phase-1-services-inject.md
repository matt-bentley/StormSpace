# Phase 1: Services — inject() Migration

**Objective**: Convert all 6 Angular services with dependencies from constructor injection to the `inject()` function pattern. (`ThemeService` has no injected dependencies and requires no changes.)

**Files to modify**:
- `src/eventstormingboard.client/src/app/_shared/services/auth.service.ts` — constructor(MsalService) → inject()
- `src/eventstormingboard.client/src/app/_shared/services/boards.service.ts` — constructor(HttpClient) → inject()
- `src/eventstormingboard.client/src/app/_shared/services/boards-signalr.service.ts` — constructor(AuthService) → inject()
- `src/eventstormingboard.client/src/app/_shared/services/user.service.ts` — constructor(AuthService) → inject()
- `src/eventstormingboard.client/src/app/_shared/services/icons.service.ts` — constructor(MatIconRegistry, DomSanitizer) → inject()
- `src/eventstormingboard.client/src/app/board/board-canvas/board-canvas.service.ts` — constructor(BoardsSignalRService) → inject()

## Context

Services are the foundation — components depend on them. Converting services first ensures the injection pattern is modernized before touching components. All services use `@Injectable({ providedIn: 'root' })` (except `BoardCanvasService` which is `@Injectable()` with component-level providers). `ThemeService` has no injected dependencies and is excluded from this phase.

## Knowledge, Instructions & Skills

- `.github/instructions/angular.instructions.md` — Defines the `inject()` convention for DI

## Implementation Steps

### Step 1: Convert AuthService

Replace constructor injection with `inject()`. The `MsalService | null` optional injection requires special handling — use `inject(MsalService, { optional: true })`.

**Current code:**
```typescript
constructor(private msalService: MsalService | null) {
    this._initialized$.next();
}
```

**Target code:**
```typescript
private msalService = inject(MsalService, { optional: true });

constructor() {
    this._initialized$.next();
}
```

Note: `inject(MsalService, { optional: true })` returns `MsalService | null`

Add `import { inject } from '@angular/core';` to the imports.

**Reference**: `src/eventstormingboard.client/src/app/_shared/services/auth.service.ts`

### Step 2: Convert BoardsService

Simple single-injection conversion.

**Current:**
```typescript
constructor(private http: HttpClient) {}
```

**Target:**
```typescript
private http = inject(HttpClient);
```

Remove the constructor entirely. Add `inject` to imports from `@angular/core`.

**Reference**: `src/eventstormingboard.client/src/app/_shared/services/boards.service.ts`

### Step 3: Convert BoardsSignalRService

The constructor has logic that calls `this.initConnection()`.

**Current:**
```typescript
constructor(private authService: AuthService) {
    this.connectionEstablished = this.initConnection();
}
```

**Target:**
```typescript
private authService = inject(AuthService);
private connectionEstablished = this.initConnection();
```

Remove the constructor. The `connectionEstablished` field initializer calls `initConnection()` which may reference `this.authService` — since field initializers execute in declaration order and `authService` is declared first, this is safe.

**Reference**: `src/eventstormingboard.client/src/app/_shared/services/boards-signalr.service.ts`

### Step 4: Convert UserService

The constructor has logic that checks auth and overrides the username.

**Current:**
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

**Target:**
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

Keep the constructor for the init logic. Add `inject` to imports.

**Reference**: `src/eventstormingboard.client/src/app/_shared/services/user.service.ts`

### Step 5: Convert IconsService

**Current:**
```typescript
constructor(private iconRegistry: MatIconRegistry, private sanitizer: DomSanitizer) {}
```

**Target:**
```typescript
private iconRegistry = inject(MatIconRegistry);
private sanitizer = inject(DomSanitizer);
```

Remove the constructor. Add `inject` to imports from `@angular/core`.

**Reference**: `src/eventstormingboard.client/src/app/_shared/services/icons.service.ts`

### Step 6: Convert BoardCanvasService

**Current:**
```typescript
constructor(
    private boardsHub: BoardsSignalRService
) {
}
```

**Target:**
```typescript
private boardsHub = inject(BoardsSignalRService);
```

Remove the empty constructor. Add `inject` to imports from `@angular/core`.

**Reference**: `src/eventstormingboard.client/src/app/board/board-canvas/board-canvas.service.ts`

## Verification Criteria

- [ ] `cd src/eventstormingboard.client && npm run build` passes with no errors
- [ ] All 6 services with dependencies use `inject()` for DI (no `private x: Type` in constructors)
- [ ] Constructors only remain where they contain initialization logic
- [ ] `inject` is imported from `@angular/core` in all modified files
- [ ] `ThemeService` is unchanged (no dependencies to inject)
