# Plan: Optional Entra ID Authentication with JWT + PKCE

Add optional Entra ID (Azure AD) authentication to StormSpace using JWT Bearer validation on the backend and MSAL.js Auth Code Flow + PKCE on the frontend. Auth is disabled by default; enabled when the `EntraId` config section is populated with `TenantId`, `ClientId`, and `Scopes`. When enabled, all API endpoints and the SignalR hub require valid tokens, and the user's Entra ID display name replaces self-reported usernames.

## Prerequisites
- **TLS required when auth is enabled** — SignalR passes tokens via query string during WebSocket negotiation (documented Microsoft pattern). Tokens are visible in server logs and URLs without TLS. Production and dev must use HTTPS.
- **Entra ID app registration** — see App Registration Checklist below

## Current State
- Zero authentication — all endpoints and SignalR hub are public
- Users self-identify via `JoinBoard(boardId, userName)` 
- `app.UseAuthorization()` is called but no authentication scheme is registered
- Frontend `HttpClient` has no interceptors; SignalR connects without tokens
- Static files (`UseDefaultFiles`/`UseStaticFiles`) run before authorization middleware — JS bundles and assets are always unauthenticated. This is acceptable for a workshop tool; the SPA itself enforces login via MSAL redirect.

## Decisions
- Auth toggle: presence of `EntraId` section with non-empty `ClientId`, `TenantId`, and `Scopes` in appsettings — all three must be set or startup fails with a clear error
- When enabled: FallbackPolicy requires authenticated user on all endpoints, **except** `MapFallbackToFile("/index.html")` which is marked `.AllowAnonymous()` so deep-links/refreshes load the SPA shell (MSAL then handles redirect)
- When disabled: `AddAuthentication()` is still called with no schemes and no FallbackPolicy so `UseAuthentication()` is a safe no-op (avoids missing `IAuthenticationSchemeProvider` crash)
- Frontend auto-redirects to Entra ID login (no login button/page)
- Authenticated user's display name is the sole identity source — client-supplied `userName` is ignored when `IsAuthenticated == true`
- Config endpoint (`/api/auth/config`) is always `[AllowAnonymous]` — frontend fetches it before bootstrap to decide whether to initialize MSAL

## App Registration Checklist
- **Type**: Single-page application (SPA) with PKCE — single app registration
- **Redirect URIs**: `https://localhost:51710` (dev SPA proxy), production origin, Docker origin if applicable
- **API scope**: Expose an API scope (e.g. `api://{ClientId}/access_as_user`) for delegated access — this is the `Scopes` config value
- **Audience**: Backend validates against `api://{ClientId}` (set via `Microsoft.Identity.Web` `Audience` config)
- **Optional claims (access token)**: Add `name` and `preferred_username` as optional claims under **"Token configuration → Optional claims → Access token"** in the app registration. The backend reads claims from the access token (JWT bearer), not the ID token — configuring ID token claims alone is insufficient
- **MSAL Angular version**: Verify `@azure/msal-angular` compatibility with Angular 21 before installing. Pin to a known-good version if the latest release does not yet support Angular 21

---

**Phase 1: Backend Auth Infrastructure** *(no dependencies — start here)*

1. **NuGet Package** — Add `Microsoft.Identity.Web` to `src/EventStormingBoard.Server/EventStormingBoard.Server.csproj`. This provides `AddMicrosoftIdentityWebApi()` for JWT Bearer validation + Entra ID token validation
2. **Auth Config DTO** — Create `src/EventStormingBoard.Server/Models/AuthConfigDto.cs` with `{ Enabled, ClientId, TenantId, Instance, Scopes }` where `Scopes` is `string[]` (not `string` — MSAL expects an array). `Instance` defaults to `https://login.microsoftonline.com` but is configurable for sovereign clouds
3. **Auth Config Endpoint** — Create `src/EventStormingBoard.Server/Controllers/AuthController.cs` with `[ApiController] [Route("api/auth")] [AllowAnonymous]`. Single endpoint `GET /api/auth/config` returns `AuthConfigDto` by reading from `IConfiguration` to determine if auth is enabled
4. **Conditional Auth in Program.cs** — Read `EntraId` config section; check if `ClientId`, `TenantId`, and `Scopes` are all non-empty. **If any one is set but others are missing, throw on startup with a descriptive error** (prevents half-enabled broken auth). If all present: `builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddMicrosoftIdentityWebApi(config.GetSection("EntraId"))`, configure `JwtBearerEvents.OnMessageReceived` to extract `access_token` from query string when path starts with `/hub` (SignalR token pattern), set `FallbackPolicy` to `RequireAuthenticatedUser()`. **If absent**: still call `builder.Services.AddAuthentication()` with no schemes and no default policy so `UseAuthentication()` is a safe no-op (required because `AuthenticationMiddleware` resolves `IAuthenticationSchemeProvider` from DI — omitting `AddAuthentication()` causes a runtime crash). Add `app.UseAuthentication()` before `app.UseAuthorization()`. **Mark `MapFallbackToFile("/index.html")` with `.AllowAnonymous()`** so deep-link/refresh routes load the SPA shell before MSAL can redirect
5. **appsettings.json Template** — Add empty `EntraId` section with `Instance`, `TenantId`, `ClientId`, `Audience`, `Scopes` (as JSON array) as documentation template (empty values = disabled)

**Phase 2: Backend User Identity** *(depends on Phase 1)*

6. **Authenticated Username in BoardsHub** — Add a static helper `GetAuthenticatedUserName(ClaimsPrincipal? user)` (accepts `ClaimsPrincipal` directly for testability — avoids coupling to `HubCallerContext`). Claim fallback chain: `name` → `preferred_username`. If neither claim is present on an authenticated user, throw `HubException` with a descriptive message (do not fall back to `oid` — a raw GUID is unusable as a display name; missing human-readable claims indicates misconfigured app registration). In `JoinBoard()`: when `Context.User?.Identity?.IsAuthenticated == true`, call `GetAuthenticatedUserName(Context.User)` and **replace the `userName` variable before passing to `_boardPresenceService.JoinBoard()` and `UserJoinedBoardEvent`** — this ensures the auth-derived name propagates through the presence store to all subsequent events. Only use the client-supplied `userName` parameter when `IsAuthenticated` is false (auth disabled)
7. **Enforce identity on all board-scoped hub methods** — The `GetUserName()` helper and `BroadcastCursorPositionUpdated` currently use client-supplied `@event.UserName` when presence lookup returns null. When auth is enabled: (a) override any client-supplied `UserName` in events with the claim-derived name, (b) add a presence-based gate to **all board-scoped hub methods** (not just mutations) — this includes `SendAgentMessage`, `GetAgentHistory`, `ClearAgentHistory`, `BroadcastCursorPositionUpdated`, and all `Broadcast*` methods. If the caller's `ConnectionId` is not in the presence store for the target `boardId`, reject the call with `HubException`. This prevents rejected-but-connected callers from invoking any board method and prevents cursor label spoofing
8. **Re-establish presence on reconnect** — SignalR's `withAutomaticReconnect()` creates a new transport connection — the server fires `OnDisconnectedAsync` → `OnConnectedAsync`, not `OnReconnectedAsync` (which requires stateful reconnect). Therefore, reconnect handling must be **client-side**: add a `hubConnection.onreconnected(() => joinBoard(boardId, userName))` handler in `BoardsSignalRService` that re-invokes `JoinBoard` with the auth-derived name after reconnect. This re-establishes presence so the step 7 gate doesn't reject subsequent calls. The client must **await** JoinBoard completion before issuing any other board-scoped hub calls after reconnect

**Phase 3: Frontend Auth Infrastructure** *(parallel with Phase 2)*

9. **Install MSAL Packages** — `npm install @azure/msal-browser @azure/msal-angular` in `src/eventstormingboard.client/`. Verify Angular 21 compatibility first — pin to a known-good version if needed (see App Registration Checklist)
10. **Auth Config Model** — Create `src/eventstormingboard.client/src/app/_shared/models/auth-config.model.ts` with interface `AuthConfig { enabled: boolean; clientId?: string; tenantId?: string; instance?: string; scopes?: string[] }` — `scopes` is `string[]` to match MSAL's expected format, `instance` for sovereign cloud support
11. **Auth Service** — Create `src/eventstormingboard.client/src/app/_shared/services/auth.service.ts`. Wraps MSAL: `getAccessToken(): Promise<string | null>` returns token when auth enabled and acquisition succeeds, `null` when auth disabled, and **throws** when auth enabled but token acquisition fails (distinguishes "no auth" from "auth broken" — callers handle accordingly). `getUserName(): string | null` returns display name from MSAL account, `isAuthenticated(): boolean`. Exposes an `initialized$` observable that resolves after MSAL has processed any redirect response
12. **Conditional MSAL Bootstrap** — Modify `src/eventstormingboard.client/src/main.ts`: before `bootstrapApplication()`, `fetch('/api/auth/config')` **with a 5-second timeout and retry (1 attempt)**. If fetch fails after retry **and auth was previously known to be enabled** (e.g. from a cached flag), show a blocking error page. If no prior auth state is known, bootstrap with auth disabled and log warning. If `enabled`, configure MSAL providers with: `MsalModule.forRoot()` standalone providers using `PublicClientApplication` config from the fetched config, **`protectedResourceMap`** mapping `/api/*` to the configured scopes (note: `/hub` **negotiation** is also covered by this mapping, but the WebSocket token is handled separately by `accessTokenFactory` in step 13), `MsalInterceptor` for HTTP calls, `MsalRedirectComponent` (or `handleRedirectObservable()` in `AppComponent.ngOnInit`) for processing auth code responses, and `MsalGuard` on **all routes** (not just root — use `canActivateChild` on a parent shell route or guard each route individually so deep-links like `/boards/{id}` are protected before the component loads and triggers hub join). If disabled, bootstrap with current providers unchanged. Update `src/eventstormingboard.client/src/app/app.config.ts` to accept auth config parameter and conditionally include `withInterceptorsFromDi()` and MSAL interceptor
13. **SignalR Token Passing** — Modify `src/eventstormingboard.client/src/app/_shared/services/boards-signalr.service.ts`: inject `AuthService`, **defer `startConnection()` until `AuthService.initialized$` resolves** (prevents racing MSAL redirect processing on first login). Update `HubConnectionBuilder().withUrl('/hub', { accessTokenFactory: () => this.authService.getAccessToken() })`. When auth disabled, `getAccessToken()` returns `null` (SignalR ignores falsy values — use a wrapper to return `undefined` or empty string to satisfy the `string | Promise<string>` type signature). **When auth enabled and token acquisition fails, catch the error and either trigger interactive re-auth or surface a user-visible notification** — do not silently return empty string (prevents infinite 401→reconnect loop). **Handle `JoinBoard` rejection**: if the hub throws `HubException` (e.g. missing claims), treat it as a fatal error in auth mode — show a blocking error message or redirect to an error page rather than leaving the user on a partially loaded board with no collaboration. **Reconnect handler**: add `hubConnection.onreconnected(() => joinBoard(...))` that re-invokes `JoinBoard` with the auth-derived name (see step 8). **Await JoinBoard** completion before issuing any other board-scoped hub calls (both on initial load and after reconnect) to avoid race conditions with the presence gate
14. **Auth-Derived Username** — When auth is enabled, pre-populate the user's display name from MSAL account info as a **read-only** value, removing the manual name input entirely. When auth is disabled, show the manual name entry as today. **Centralize current-user identity** by adding `getCurrentUserName(): string` to `AuthService` that returns MSAL display name (auth enabled) or delegates to existing manual flow (auth disabled). Update all identity consumption points: `UserService` (localStorage), splash component name input, `AppComponent` top-nav name editor, `BoardComponent`, `BoardCanvasComponent` cursor labels, and `AiChatPanelComponent` — all read from the centralized method. When auth enabled, name fields are read-only

---

## Files

### Backend — Modify
- `src/EventStormingBoard.Server/EventStormingBoard.Server.csproj` — add `Microsoft.Identity.Web` package
- `src/EventStormingBoard.Server/Program.cs` — conditional auth registration, `UseAuthentication()`, SignalR token events, `.AllowAnonymous()` on `MapFallbackToFile`, startup validation for partial config
- `src/EventStormingBoard.Server/Hubs/BoardsHub.cs` — `GetAuthenticatedUserName()` static helper (accepts `ClaimsPrincipal`), identity enforcement on `JoinBoard()` and all board-scoped hub methods (mutations, agent methods, cursor), presence-based gate on all board-scoped methods
- `src/EventStormingBoard.Server/appsettings.json` — add empty `EntraId` section with `Instance`, `TenantId`, `ClientId`, `Audience`, `Scopes` (JSON array)

### Backend — Create
- `src/EventStormingBoard.Server/Controllers/AuthController.cs` — `[AllowAnonymous]` config endpoint
- `src/EventStormingBoard.Server/Models/AuthConfigDto.cs` — DTO for auth config response (with `Scopes` as `string[]`, `Instance` field)

### Frontend — Modify
- `src/eventstormingboard.client/package.json` — add MSAL dependencies (version-pinned)
- `src/eventstormingboard.client/src/main.ts` — pre-fetch auth config (with timeout/retry/fail-closed when auth known-enabled), conditional MSAL bootstrap with full provider config
- `src/eventstormingboard.client/src/app/app.config.ts` — accept auth config, conditional providers including `protectedResourceMap` and `MsalInterceptor`
- `src/eventstormingboard.client/src/app/app.routes.ts` — `MsalGuard` on all routes (or `canActivateChild` on parent shell) when auth enabled
- `src/eventstormingboard.client/src/app/_shared/services/boards-signalr.service.ts` — defer connection until MSAL initialized, pass access token to SignalR, handle token-failure errors
- All identity consumption points: `AppComponent`, `BoardComponent`, `BoardCanvasComponent`, `AiChatPanelComponent`, splash component — read from centralized `AuthService.getCurrentUserName()`, read-only when auth enabled

### Frontend — Create
- `src/eventstormingboard.client/src/app/_shared/services/auth.service.ts` — MSAL wrapper with `initialized$` observable, error-throwing token acquisition, centralized `getCurrentUserName()`
- `src/eventstormingboard.client/src/app/_shared/models/auth-config.model.ts` — config interface with `scopes: string[]` and `instance`

## Verification

1. `dotnet build src/EventStormingBoard.Server/EventStormingBoard.Server.csproj` — backend compiles
2. `cd src/eventstormingboard.client && npm run build` — frontend compiles
3. `dotnet test tests/EventStormingBoard.Server.Tests/` — existing tests pass (auth disabled in tests)
4. **New unit tests** (hand-rolled test doubles per project convention):
   - `AuthControllerTests` — returns `enabled: false` when no config, returns correct values when configured, `Scopes` serializes as array
   - `GetAuthenticatedUserName` tests (static method accepting `ClaimsPrincipal` — no Hub.Context coupling): extracts `name` claim, falls back to `preferred_username`, throws `HubException` when no human-readable claims on authenticated user, returns null when unauthenticated
   - Config validation tests — extract the startup validation logic into a testable static method: partial config (e.g. `ClientId` set but `Scopes` missing) throws descriptive error, all-empty is valid (disabled), all-present is valid (enabled)
5. **Manual verification** (integration/startup paths — no `WebApplicationFactory` in test project):
   - Start app without `EntraId` config → app works exactly as before (no login redirect, all endpoints open)
   - Start app with `EntraId` config populated → auto-redirect to Entra ID, tokens attached to API calls and SignalR, authenticated username appears on board
   - Deep-link to `/boards/{id}` with auth enabled → `MsalGuard` triggers redirect before `BoardComponent` loads, returns to original route after login
   - Reconnect scenario: kill SignalR connection, verify auto-reconnect re-establishes presence with auth-derived name
6. Verify `GET /api/auth/config` returns `{ enabled: false }` when unconfigured and correct values when configured
7. Verify hub mutation methods reject calls from non-member connections when auth is enabled

## Excluded from Scope
- Role-based authorization (all authenticated users have equal access)
- Per-board access control / multi-tenancy
- Token refresh error handling UI (MSAL handles refresh silently; SignalR reconnect surfaces error to user)
- Logout button (can be added later)
- Backend-to-backend auth for AI agent calls (Azure SDK uses `DefaultAzureCredential` separately)
- Frontend unit tests (none exist currently per project guidelines)
- Static asset protection (JS bundles/images served unauthenticated — acceptable for workshop tool)

## Deployment Notes
- **Docker**: Pass Entra config via environment variables using ASP.NET's `__` separator convention: `EntraId__ClientId`, `EntraId__TenantId`, `EntraId__Scopes__0` (array element), or mount a custom `appsettings.json`
- **Dev**: Angular SPA proxy runs on `https://localhost:51710` — register this as a redirect URI in the Entra app registration
