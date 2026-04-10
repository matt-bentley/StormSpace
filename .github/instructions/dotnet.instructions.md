---
applyTo: "src/EventStormingBoard.Server/**,tests/EventStormingBoard.Server.Tests/**"
---

# .NET Code Conventions

Follow Microsoft recommended .NET conventions unless explicitly overridden below.

## Language & Project Settings

- Target: .NET 10 with C# 14
- Nullable reference types enabled globally — annotate all reference types explicitly
- Implicit usings enabled — do not add redundant `using` directives for `System`, `System.Collections.Generic`, `System.Linq`, `System.Threading.Tasks`, etc.
- Use file-scoped namespaces (`namespace X;`)

## Naming

| Symbol | Convention | Example |
|--------|-----------|---------|
| Interfaces | `I{Name}` prefix | `IBoardStateService` |
| Services | `{Name}Service` implementing `I{Name}Service` | `BoardStateService : IBoardStateService` |
| Events | `{Entity}{Action}Event` inheriting `BoardEvent` | `NoteCreatedEvent` |
| DTOs | `{Entity}Dto` sealed class | `NoteDto`, `BoardDto` |
| Input models | `{Action}{Entity}Input` or `{Entity}CreateDto` | `CreateNoteInput` |
| Enums | PascalCase values | `NoteType.Event`, `EventStormingPhase.IdentifyEvents` |
| Private fields | `_camelCase` with underscore prefix | `_boardEventPipeline` |
| Constants | `PascalCase` | `BoardsCacheKey` |

## Type Design

- Seal all service implementations, controllers, hubs, DTOs, and event classes (`public sealed class`)
- Leave entity classes unsealed only when they serve as extensible domain models
- Prefer `sealed class` over `record` for DTOs and events
- Use `required` keyword for mandatory properties instead of constructors
- Initialise collections with target-typed `new()`: `public List<NoteDto> Notes { get; set; } = new();`
- Use auto-properties with `{ get; set; }` — no backing fields unless needed for logic
- Mark nullable reference-type properties with `?` explicitly

## Dependency Injection

- All services are registered as **Singletons** (in-memory state model)
- Use constructor injection — no service locator or `[FromServices]`
- Register as `builder.Services.AddSingleton<IInterface, Implementation>()`
- Background services use `AddHostedService<T>()`
- Options use `AddOptions<T>().BindConfiguration("Section").ValidateDataAnnotations().ValidateOnStart()`

## Async & Concurrency

- All I/O methods must be async (`async Task` / `async Task<T>`)
- Use `ConcurrentDictionary<Guid, T>` for thread-safe per-board collections
- Use `SemaphoreSlim` for board-level locking (one semaphore per board)
- Never use `async void` except for event handlers
- Do not call `.Result` or `.Wait()` on tasks — always `await`

## Controllers

- Annotate with `[ApiController]` and `[Route("api/{resource}")]`
- Use `[HttpGet]`, `[HttpPost]`, `[HttpPut]`, `[HttpDelete]` verb attributes
- Return `IActionResult` with explicit helpers: `Ok()`, `NotFound()`, `BadRequest()`, `CreatedAtAction()`
- Document responses with `[ProducesResponseType(typeof(T), StatusCodes.Status200OK)]`
- Validate inputs and return `BadRequest()` with a descriptive message for invalid requests
- Return `NotFound()` for missing resources — do not throw exceptions for expected cases

## SignalR Hubs

- Hub broadcast methods follow `Broadcast{EventName}` naming
- All hub methods are `async Task`
- Use group-based messaging: `Clients.Group()`, `Clients.OthersInGroup()`, `Clients.Caller`
- Validate board membership before operations with guard methods
- All state mutations go through `IBoardEventPipeline.ApplyAndLog()` — never mutate state directly

## Events & State Pipeline

- All board mutations must flow through `IBoardEventPipeline.ApplyAndLog(event, userName)`
- Events inherit from abstract `BoardEvent` base class
- Events carry minimal data — only what changed, plus before/after values for undo support
- State service methods are named `Apply{EventName}` matching the event class

## JSON Serialization

- Enums serialize as camelCase strings via `JsonStringEnumConverter(JsonNamingPolicy.CamelCase)`
- Use `[JsonConverter(typeof(JsonStringEnumConverter))]` on enum definitions
- No custom JSON converters unless strictly necessary
- Use `[Description("...")]` attributes on input model properties used by AI agents

## Error Handling

- Controller errors use `IActionResult` status codes, not exceptions
- Hub errors throw `HubException` with a descriptive message for client-facing errors
- Validate at system boundaries (controllers, hubs) — internal services trust their callers
- Do not add catch blocks for exceptions that cannot be meaningfully handled
