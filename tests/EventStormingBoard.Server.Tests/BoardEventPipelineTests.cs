using EventStormingBoard.Server.Events;
using EventStormingBoard.Server.Models;
using EventStormingBoard.Server.Services;

namespace EventStormingBoard.Server.Tests;

public class BoardEventPipelineTests
{
    [Fact]
    public void ApplyAndLog_BoardNameUpdated_AppliesStateAndAppendsLog()
    {
        var state = new SpyBoardStateService();
        var log = new SpyBoardEventLog();
        var pipeline = new BoardEventPipeline(state, log);
        var boardId = Guid.NewGuid();
        var @event = new BoardNameUpdatedEvent { BoardId = boardId, OldName = "Old", NewName = "New" };

        pipeline.ApplyAndLog(@event, "Alice");

        Assert.Equal(nameof(IBoardStateService.ApplyBoardNameUpdated), state.LastApplyCall);
        Assert.Same(@event, state.LastEvent);
        Assert.Equal(boardId, log.LastBoardId);
        Assert.Same(@event, log.LastEvent);
        Assert.Equal("Alice", log.LastUserName);
        Assert.Equal(1, log.AppendCount);
    }

    [Fact]
    public void ApplyAndLog_NoteCreated_AppliesStateAndAppendsLog()
    {
        var state = new SpyBoardStateService();
        var log = new SpyBoardEventLog();
        var pipeline = new BoardEventPipeline(state, log);
        var @event = new NoteCreatedEvent
        {
            BoardId = Guid.NewGuid(),
            Note = new NoteDto { Id = Guid.NewGuid(), Text = "OrderPlaced", Type = NoteType.Event }
        };

        pipeline.ApplyAndLog(@event, null);

        Assert.Equal(nameof(IBoardStateService.ApplyNoteCreated), state.LastApplyCall);
        Assert.Same(@event, state.LastEvent);
        Assert.Same(@event, log.LastEvent);
        Assert.Null(log.LastUserName);
    }

    [Fact]
    public void ApplyAndLog_BoundedContextCreated_AppliesStateAndAppendsLog()
    {
        var state = new SpyBoardStateService();
        var log = new SpyBoardEventLog();
        var pipeline = new BoardEventPipeline(state, log);
        var @event = new BoundedContextCreatedEvent
        {
            BoardId = Guid.NewGuid(),
            BoundedContext = new BoundedContextDto { Id = Guid.NewGuid(), Name = "Orders" }
        };

        pipeline.ApplyAndLog(@event, "Alice");

        Assert.Equal(nameof(IBoardStateService.ApplyBoundedContextCreated), state.LastApplyCall);
        Assert.Same(@event, state.LastEvent);
        Assert.Equal(1, log.AppendCount);
    }

    [Fact]
    public void ApplyAndLog_BoundedContextUpdated_AppliesStateAndAppendsLog()
    {
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

        pipeline.ApplyAndLog(@event, "Bob");

        Assert.Equal(nameof(IBoardStateService.ApplyBoundedContextUpdated), state.LastApplyCall);
        Assert.Same(@event, state.LastEvent);
        Assert.Equal(1, log.AppendCount);
    }

    [Fact]
    public void ApplyAndLog_BoundedContextDeleted_AppliesStateAndAppendsLog()
    {
        var state = new SpyBoardStateService();
        var log = new SpyBoardEventLog();
        var pipeline = new BoardEventPipeline(state, log);
        var @event = new BoundedContextDeletedEvent
        {
            BoardId = Guid.NewGuid(),
            BoundedContext = new BoundedContextDto { Id = Guid.NewGuid(), Name = "Payments" }
        };

        pipeline.ApplyAndLog(@event, "Charlie");

        Assert.Equal(nameof(IBoardStateService.ApplyBoundedContextDeleted), state.LastApplyCall);
        Assert.Same(@event, state.LastEvent);
        Assert.Equal(1, log.AppendCount);
    }

    [Fact]
    public void ApplyAndLog_UnsupportedEvent_ThrowsAndDoesNotAppend()
    {
        var state = new SpyBoardStateService();
        var log = new SpyBoardEventLog();
        var pipeline = new BoardEventPipeline(state, log);
        var @event = new UnknownBoardEvent { BoardId = Guid.NewGuid() };

        var ex = Assert.Throws<InvalidOperationException>(() => pipeline.ApplyAndLog(@event, "User"));

        Assert.Contains("Unsupported board event type", ex.Message);
        Assert.Null(state.LastApplyCall);
        Assert.Equal(0, log.AppendCount);
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
