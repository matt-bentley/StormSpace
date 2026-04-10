using EventStormingBoard.Server.Events;
using EventStormingBoard.Server.Models;
using EventStormingBoard.Server.Services;

namespace EventStormingBoard.Server.Tests;

public class BoardEventPipelineTests
{
    [Fact]
    public void GivenBoardNameUpdatedEvent_WhenApplyingAndLogging_ThenAppliesStateAndAppendsLog()
    {
        // Arrange
        var state = new SpyBoardStateService();
        var log = new SpyBoardEventLog();
        var pipeline = new BoardEventPipeline(state, log);
        var boardId = Guid.NewGuid();
        var @event = new BoardNameUpdatedEvent { BoardId = boardId, OldName = "Old", NewName = "New" };

        // Act
        pipeline.ApplyAndLog(@event, "Alice");

        // Assert
        state.LastApplyCall.Should().Be(nameof(IBoardStateService.ApplyBoardNameUpdated));
        state.LastEvent.Should().BeSameAs(@event);
        log.LastBoardId.Should().Be(boardId);
        log.LastEvent.Should().BeSameAs(@event);
        log.LastUserName.Should().Be("Alice");
        log.AppendCount.Should().Be(1);
    }

    [Fact]
    public void GivenNoteCreatedEvent_WhenApplyingAndLogging_ThenAppliesStateAndAppendsLog()
    {
        // Arrange
        var state = new SpyBoardStateService();
        var log = new SpyBoardEventLog();
        var pipeline = new BoardEventPipeline(state, log);
        var @event = new NoteCreatedEvent
        {
            BoardId = Guid.NewGuid(),
            Note = new NoteDto { Id = Guid.NewGuid(), Text = "OrderPlaced", Type = NoteType.Event }
        };

        // Act
        pipeline.ApplyAndLog(@event, null);

        // Assert
        state.LastApplyCall.Should().Be(nameof(IBoardStateService.ApplyNoteCreated));
        state.LastEvent.Should().BeSameAs(@event);
        log.LastEvent.Should().BeSameAs(@event);
        log.LastUserName.Should().BeNull();
    }

    [Fact]
    public void GivenBoundedContextCreatedEvent_WhenApplyingAndLogging_ThenAppliesStateAndAppendsLog()
    {
        // Arrange
        var state = new SpyBoardStateService();
        var log = new SpyBoardEventLog();
        var pipeline = new BoardEventPipeline(state, log);
        var @event = new BoundedContextCreatedEvent
        {
            BoardId = Guid.NewGuid(),
            BoundedContext = new BoundedContextDto { Id = Guid.NewGuid(), Name = "Orders" }
        };

        // Act
        pipeline.ApplyAndLog(@event, "Alice");

        // Assert
        state.LastApplyCall.Should().Be(nameof(IBoardStateService.ApplyBoundedContextCreated));
        state.LastEvent.Should().BeSameAs(@event);
        log.AppendCount.Should().Be(1);
    }

    [Fact]
    public void GivenBoundedContextUpdatedEvent_WhenApplyingAndLogging_ThenAppliesStateAndAppendsLog()
    {
        // Arrange
        var state = new SpyBoardStateService();
        var log = new SpyBoardEventLog();
        var pipeline = new BoardEventPipeline(state, log);
        var @event = new BoundedContextUpdatedEvent
        {
            BoardId = Guid.NewGuid(),
            BoundedContextId = Guid.NewGuid(),
            OldName = "Orders",
            NewName = "Order Management"
        };

        // Act
        pipeline.ApplyAndLog(@event, "Bob");

        // Assert
        state.LastApplyCall.Should().Be(nameof(IBoardStateService.ApplyBoundedContextUpdated));
        state.LastEvent.Should().BeSameAs(@event);
        log.AppendCount.Should().Be(1);
    }

    [Fact]
    public void GivenBoundedContextDeletedEvent_WhenApplyingAndLogging_ThenAppliesStateAndAppendsLog()
    {
        // Arrange
        var state = new SpyBoardStateService();
        var log = new SpyBoardEventLog();
        var pipeline = new BoardEventPipeline(state, log);
        var @event = new BoundedContextDeletedEvent
        {
            BoardId = Guid.NewGuid(),
            BoundedContext = new BoundedContextDto { Id = Guid.NewGuid(), Name = "Payments" }
        };

        // Act
        pipeline.ApplyAndLog(@event, "Charlie");

        // Assert
        state.LastApplyCall.Should().Be(nameof(IBoardStateService.ApplyBoundedContextDeleted));
        state.LastEvent.Should().BeSameAs(@event);
        log.AppendCount.Should().Be(1);
    }

    [Fact]
    public void GivenUnsupportedEvent_WhenApplyingAndLogging_ThenThrowsAndDoesNotAppend()
    {
        // Arrange
        var state = new SpyBoardStateService();
        var log = new SpyBoardEventLog();
        var pipeline = new BoardEventPipeline(state, log);
        var @event = new UnknownBoardEvent { BoardId = Guid.NewGuid() };
        Action act = () => pipeline.ApplyAndLog(@event, "User");

        // Act
        var ex = act.Should().Throw<InvalidOperationException>().Which;

        // Assert
        ex.Message.Should().Contain("Unsupported board event type");
        state.LastApplyCall.Should().BeNull();
        log.AppendCount.Should().Be(0);
    }

    private sealed class SpyBoardStateService : IBoardStateService
    {
        public string? LastApplyCall { get; private set; }
        public BoardEvent? LastEvent { get; private set; }

        public void ApplyBoardNameUpdated(BoardNameUpdatedEvent @event)
        {
            LastApplyCall = nameof(ApplyBoardNameUpdated);
            LastEvent = @event;
        }

        public void ApplyBoardContextUpdated(BoardContextUpdatedEvent @event)
        {
            LastApplyCall = nameof(ApplyBoardContextUpdated);
            LastEvent = @event;
        }

        public void ApplyNoteCreated(NoteCreatedEvent @event)
        {
            LastApplyCall = nameof(ApplyNoteCreated);
            LastEvent = @event;
        }

        public void ApplyNotesMoved(NotesMovedEvent @event)
        {
            LastApplyCall = nameof(ApplyNotesMoved);
            LastEvent = @event;
        }

        public void ApplyNoteResized(NoteResizedEvent @event)
        {
            LastApplyCall = nameof(ApplyNoteResized);
            LastEvent = @event;
        }

        public void ApplyNotesDeleted(NotesDeletedEvent @event)
        {
            LastApplyCall = nameof(ApplyNotesDeleted);
            LastEvent = @event;
        }

        public void ApplyNoteTextEdited(NoteTextEditedEvent @event)
        {
            LastApplyCall = nameof(ApplyNoteTextEdited);
            LastEvent = @event;
        }

        public void ApplyConnectionCreated(ConnectionCreatedEvent @event)
        {
            LastApplyCall = nameof(ApplyConnectionCreated);
            LastEvent = @event;
        }

        public void ApplyPasted(PastedEvent @event)
        {
            LastApplyCall = nameof(ApplyPasted);
            LastEvent = @event;
        }

        public void ApplyBoundedContextCreated(BoundedContextCreatedEvent @event)
        {
            LastApplyCall = nameof(ApplyBoundedContextCreated);
            LastEvent = @event;
        }

        public void ApplyBoundedContextUpdated(BoundedContextUpdatedEvent @event)
        {
            LastApplyCall = nameof(ApplyBoundedContextUpdated);
            LastEvent = @event;
        }

        public void ApplyBoundedContextDeleted(BoundedContextDeletedEvent @event)
        {
            LastApplyCall = nameof(ApplyBoundedContextDeleted);
            LastEvent = @event;
        }
    }

    private sealed class SpyBoardEventLog : IBoardEventLog
    {
        public Guid LastBoardId { get; private set; }
        public BoardEvent? LastEvent { get; private set; }
        public string? LastUserName { get; private set; }
        public int AppendCount { get; private set; }

        public void Append(Guid boardId, BoardEvent @event, string? userName)
        {
            LastBoardId = boardId;
            LastEvent = @event;
            LastUserName = userName;
            AppendCount++;
        }

        public List<BoardEventEntry> GetRecent(Guid boardId, int count = 50)
        {
            return [];
        }

        public void ClearLog(Guid boardId) { }
    }

    private sealed class UnknownBoardEvent : BoardEvent
    {
    }
}
