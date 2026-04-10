---
description: "Use when writing, modifying, or reviewing .NET unit tests. Covers xUnit conventions, Given/When/Then naming, Arrange/Act/Assert structure, Moq mocking, and AwesomeAssertions."
applyTo: "tests/**"
---

# .NET Testing Conventions

xUnit test project with Moq for mocking and AwesomeAssertions for fluent assertions.

## Test Method Naming

Use **Given/When/Then** BDD-style naming:

```
Given{Precondition}_When{Action}_Then{ExpectedOutcome}
```

```csharp
[Fact]
public void GivenBoardWithNotes_WhenDeletingNote_ThenNoteIsRemoved()

[Fact]
public async Task GivenExpiredToken_WhenAuthenticating_ThenReturnsUnauthorized()
```

- Start with `Given` — the precondition or initial state
- `When` — the action under test
- `Then` — the expected outcome
- Use PascalCase throughout, no underscores within segments

## Test Body Structure

Always use explicit **Arrange / Act / Assert** comments:

```csharp
[Fact]
public void GivenNewBoard_WhenUpdatingName_ThenNameIsChanged()
{
    // Arrange
    var repository = new InMemoryBoardsRepository();
    var service = new BoardStateService(repository);
    var board = new Board { Id = Guid.NewGuid(), Name = "Old" };
    repository.Add(board);

    // Act
    service.ApplyBoardNameUpdated(board.Id, "New");

    // Assert
    board.Name.Should().Be("New");
}
```

- One blank line between each section
- `// Arrange` — set up dependencies, test data, and the system under test
- `// Act` — execute exactly one action
- `// Assert` — verify the outcome

## Mocking with Moq

Use **Moq** for mocking interfaces. Prefer `Mock<T>` with `.Setup()` and `.Verify()`:

```csharp
[Fact]
public void GivenValidEvent_WhenApplyingAndLogging_ThenStateAndLogAreUpdated()
{
    // Arrange
    var mockStateService = new Mock<IBoardStateService>();
    var mockEventLog = new Mock<IBoardEventLog>();
    var pipeline = new BoardEventPipeline(mockStateService.Object, mockEventLog.Object);
    var boardEvent = new BoardNameUpdatedEvent { BoardId = Guid.NewGuid(), Name = "Test" };

    // Act
    pipeline.ApplyAndLog(boardEvent, "Alice");

    // Assert
    mockStateService.Verify(s => s.ApplyBoardNameUpdated(boardEvent.BoardId, "Test"), Times.Once);
    mockEventLog.Verify(l => l.Append(boardEvent.BoardId, boardEvent, "Alice"), Times.Once);
}
```

Guidelines:
- Use `Mock<T>` for interfaces — never mock concrete classes
- Use `It.IsAny<T>()` for flexible argument matching when the exact value doesn't matter
- Use `It.Is<T>(predicate)` when you need to assert on argument values
- Prefer `.Verify()` over `.Setup(...).Callback()` for interaction assertions
- Use `.Setup(...).Returns(...)` / `.ReturnsAsync(...)` for stubs
- Use `MockBehavior.Strict` sparingly — default `MockBehavior.Loose` is usually sufficient
- Existing hand-rolled spies/stubs (e.g., `SpyBoardStateService`, `InMemoryBoardsRepository`) remain valid — do not replace when they provide clearer test intent

## Assertions with AwesomeAssertions

Use fluent assertion chains (`Should()`):

```csharp
// Equality
result.Name.Should().Be("Expected");

// Collections
notes.Should().HaveCount(3);
notes.Should().ContainSingle(n => n.Type == NoteType.Event);
notes.Should().BeEmpty();

// Nullability
board.Should().NotBeNull();
result.Should().BeNull();

// Exceptions
var act = () => service.GetBoard(Guid.Empty);
act.Should().Throw<ArgumentException>().WithMessage("*invalid*");

// Async exceptions
var act = async () => await service.ProcessAsync(input);
await act.Should().ThrowAsync<InvalidOperationException>();

// Type checking
result.Should().BeOfType<OkObjectResult>();
```

- Always chain from `.Should()` — never use xUnit `Assert.*` methods
- Use `.Because("reason")` for non-obvious assertions

## Test Class Setup

Use **constructor-based initialization** — no DI container, no `IClassFixture`:

```csharp
public sealed class BoardStateServiceTests
{
    private readonly InMemoryBoardsRepository _repository;
    private readonly BoardStateService _service;
    private readonly Board _board;

    public BoardStateServiceTests()
    {
        _repository = new InMemoryBoardsRepository();
        _service = new BoardStateService(_repository);
        _board = new Board { Id = Guid.NewGuid(), Name = "Test Board" };
        _repository.Add(_board);
    }
}
```

- Construct the system under test and its dependencies directly in the constructor
- Use `readonly` fields for shared test state
- Each test gets a fresh instance — xUnit creates a new class per test

## General Rules

- One `[Fact]` or `[Theory]` per test method — test one behaviour per method
- Use `[Theory]` with `[InlineData]` for parameterised tests, not loops
- Use `async Task` return type for async tests — never `async void`
- Keep tests independent — no shared mutable static state
- Seal test double classes (`sealed class SpyService : IService`)
- Namespace: `EventStormingBoard.Server.Tests` (file-scoped)
- Global usings for `AwesomeAssertions` and `Xunit` are configured in the project file — do not add redundant `using` directives for these
