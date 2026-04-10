using EventStormingBoard.Server.Events;
using EventStormingBoard.Server.Models;
using EventStormingBoard.Server.Services;

namespace EventStormingBoard.Server.Tests;

public class BoardEventLogTests
{
    private readonly BoardEventLog _log = new();
    private readonly Guid _boardId = Guid.NewGuid();

    [Fact]
    public void GivenEmptyBoard_WhenGettingRecentEvents_ThenReturnsEmptyList()
    {
        // Arrange

        // Act
        var result = _log.GetRecent(Guid.NewGuid());

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GivenSingleAppendedEvent_WhenGettingRecentEvents_ThenReturnsSingleEntry()
    {
        // Arrange
        var @event = new NoteCreatedEvent
        {
            BoardId = _boardId,
            Note = new NoteDto { Id = Guid.NewGuid(), Text = "OrderPlaced", Type = NoteType.Event }
        };

        // Act
        _log.Append(_boardId, @event, "Alice");

        var entries = _log.GetRecent(_boardId);

        // Assert
        entries.Should().ContainSingle();
        entries[0].EventType.Should().Be("NoteCreatedEvent");
        entries[0].Summary.Should().Contain("OrderPlaced");
        entries[0].UserName.Should().Be("Alice");
    }

    [Fact]
    public void GivenNullUserName_WhenAppendingEvent_ThenUserNameIsStoredAsNull()
    {
        // Arrange
        var @event = new BoardNameUpdatedEvent
        {
            BoardId = _boardId,
            NewName = "New",
            OldName = "Old"
        };

        // Act
        _log.Append(_boardId, @event, null);

        var entries = _log.GetRecent(_boardId);

        // Assert
        entries[0].UserName.Should().BeNull();
    }

    [Fact]
    public void GivenMoreEventsThanRequested_WhenGettingRecentEventsWithCount_ThenReturnsRequestedCount()
    {
        // Arrange
        for (int i = 0; i < 10; i++)
        {
            _log.Append(_boardId, new BoardNameUpdatedEvent
            {
                BoardId = _boardId,
                NewName = $"Name {i}",
                OldName = $"Name {i - 1}"
            }, "User");
        }

        // Act
        var entries = _log.GetRecent(_boardId, 3);

        // Assert
        entries.Count.Should().Be(3);
    }

    [Fact]
    public void GivenMultipleEvents_WhenGettingRecentEventsWithCount_ThenReturnsLatestEntries()
    {
        // Arrange
        for (int i = 0; i < 5; i++)
        {
            _log.Append(_boardId, new BoardNameUpdatedEvent
            {
                BoardId = _boardId,
                NewName = $"Name {i}",
                OldName = "Prev"
            }, "User");
        }

        // Act
        var entries = _log.GetRecent(_boardId, 2);

        // Assert
        entries[1].Summary.Should().Contain("Name 4");
        entries[0].Summary.Should().Contain("Name 3");
    }

    [Fact]
    public void GivenMoreThanMaxEntries_WhenAppendingEvents_ThenOldestEntriesAreEvicted()
    {
        // Arrange
        for (int i = 0; i < 60; i++)
        {
            _log.Append(_boardId, new BoardNameUpdatedEvent
            {
                BoardId = _boardId,
                NewName = $"Name {i}",
                OldName = "Prev"
            }, "User");
        }

        // Act
        var entries = _log.GetRecent(_boardId);

        // Assert
        entries.Count.Should().Be(50);
        // Oldest entry should be #10 (0-9 evicted)
        entries[0].Summary.Should().Contain("Name 10");
        entries[^1].Summary.Should().Contain("Name 59");
    }

    [Fact]
    public void GivenEventsOnDifferentBoards_WhenGettingRecentEvents_ThenBoardsAreIsolated()
    {
        // Arrange
        var boardA = Guid.NewGuid();
        var boardB = Guid.NewGuid();

        _log.Append(boardA, new BoardNameUpdatedEvent { BoardId = boardA, NewName = "A", OldName = "" }, "User");
        _log.Append(boardB, new BoardNameUpdatedEvent { BoardId = boardB, NewName = "B", OldName = "" }, "User");

        // Act
        var boardAEntries = _log.GetRecent(boardA);
        var boardBEntries = _log.GetRecent(boardB);

        // Assert
        boardAEntries.Should().ContainSingle();
        boardBEntries.Should().ContainSingle();
    }

    [Fact]
    public void GivenAppendedEvent_WhenReadingRecentEvents_ThenTimestampIsWithinExpectedRange()
    {
        // Arrange
        var before = DateTimeOffset.UtcNow;
        _log.Append(_boardId, new BoardNameUpdatedEvent
        {
            BoardId = _boardId,
            NewName = "X",
            OldName = "Y"
        }, "User");
        var after = DateTimeOffset.UtcNow;

        // Act
        var entries = _log.GetRecent(_boardId);

        // Assert
        entries[0].Timestamp.Should().BeOnOrAfter(before);
        entries[0].Timestamp.Should().BeOnOrBefore(after);
    }

    // ── Summary format tests ────────────────────────────────

    [Fact]
    public void GivenNoteCreatedEvent_WhenAppending_ThenSummaryDescribesCreatedNote()
    {
        // Arrange

        // Act
        _log.Append(_boardId, new NoteCreatedEvent
        {
            BoardId = _boardId,
            Note = new NoteDto { Id = Guid.NewGuid(), Text = "OrderPlaced", Type = NoteType.Event }
        }, null);

        var entry = _log.GetRecent(_boardId)[0];

        // Assert
        entry.Summary.Should().Be("Created Event note \"OrderPlaced\"");
    }

    [Fact]
    public void GivenNotesMovedEvent_WhenAppending_ThenSummaryDescribesMovedNotes()
    {
        // Arrange

        // Act
        _log.Append(_boardId, new NotesMovedEvent
        {
            BoardId = _boardId,
            To = [
                new NoteMoveDto { NoteId = Guid.NewGuid(), Coordinates = new CoordinatesDto() },
                new NoteMoveDto { NoteId = Guid.NewGuid(), Coordinates = new CoordinatesDto() }
            ]
        }, null);

        var entry = _log.GetRecent(_boardId)[0];

        // Assert
        entry.Summary.Should().Be("Moved 2 note(s)");
    }

    [Fact]
    public void GivenNotesDeletedEvent_WhenAppending_ThenSummaryDescribesDeletedNotesAndConnections()
    {
        // Arrange

        // Act
        _log.Append(_boardId, new NotesDeletedEvent
        {
            BoardId = _boardId,
            Notes = [new NoteDto { Id = Guid.NewGuid() }],
            Connections = [new ConnectionDto { FromNoteId = Guid.NewGuid(), ToNoteId = Guid.NewGuid() }]
        }, null);

        var entry = _log.GetRecent(_boardId)[0];

        // Assert
        entry.Summary.Should().Be("Deleted 1 note(s) and 1 connection(s)");
    }

    [Fact]
    public void GivenNoteTextEditedEvent_WhenAppending_ThenSummaryDescribesEditedText()
    {
        // Arrange

        // Act
        _log.Append(_boardId, new NoteTextEditedEvent
        {
            BoardId = _boardId,
            NoteId = Guid.NewGuid(),
            ToText = "Updated",
            FromText = "Old"
        }, null);

        var entry = _log.GetRecent(_boardId)[0];

        // Assert
        entry.Summary.Should().Be("Edited note text to \"Updated\"");
    }

    [Fact]
    public void GivenConnectionCreatedEvent_WhenAppending_ThenSummaryContainsConnectionEndpoints()
    {
        // Arrange
        var from = Guid.NewGuid();
        var to = Guid.NewGuid();

        // Act
        _log.Append(_boardId, new ConnectionCreatedEvent
        {
            BoardId = _boardId,
            Connection = new ConnectionDto { FromNoteId = from, ToNoteId = to }
        }, null);

        var entry = _log.GetRecent(_boardId)[0];

        // Assert
        entry.Summary.Should().Contain(from.ToString());
        entry.Summary.Should().Contain(to.ToString());
    }

    [Fact]
    public void GivenPastedEvent_WhenAppending_ThenSummaryDescribesPastedNotesAndConnections()
    {
        // Arrange

        // Act
        _log.Append(_boardId, new PastedEvent
        {
            BoardId = _boardId,
            Notes = [new NoteDto { Id = Guid.NewGuid() }, new NoteDto { Id = Guid.NewGuid() }],
            Connections = []
        }, null);

        var entry = _log.GetRecent(_boardId)[0];

        // Assert
        entry.Summary.Should().Be("Pasted 2 note(s) and 0 connection(s)");
    }

    [Fact]
    public void GivenBoardNameUpdatedEvent_WhenAppending_ThenSummaryDescribesBoardRename()
    {
        // Arrange

        // Act
        _log.Append(_boardId, new BoardNameUpdatedEvent
        {
            BoardId = _boardId,
            NewName = "My Board",
            OldName = "Old Board"
        }, null);

        var entry = _log.GetRecent(_boardId)[0];

        // Assert
        entry.Summary.Should().Be("Renamed board to \"My Board\"");
    }

    [Fact]
    public void GivenBoundedContextCreatedEvent_WhenAppending_ThenSummaryDescribesCreatedBoundedContext()
    {
        // Arrange

        // Act
        _log.Append(_boardId, new BoundedContextCreatedEvent
        {
            BoardId = _boardId,
            BoundedContext = new BoundedContextDto { Id = Guid.NewGuid(), Name = "Orders" }
        }, null);

        var entry = _log.GetRecent(_boardId)[0];

        // Assert
        entry.Summary.Should().Be("Created bounded context \"Orders\"");
    }

    [Fact]
    public void GivenBoundedContextUpdatedEvent_WhenAppending_ThenSummaryContainsBoundedContextId()
    {
        // Arrange
        var bcId = Guid.NewGuid();

        // Act
        _log.Append(_boardId, new BoundedContextUpdatedEvent
        {
            BoardId = _boardId,
            BoundedContextId = bcId,
            OldName = "Orders",
            NewName = "Order Management"
        }, null);

        var entry = _log.GetRecent(_boardId)[0];

        // Assert
        entry.Summary.Should().Contain(bcId.ToString());
    }

    [Fact]
    public void GivenBoundedContextDeletedEvent_WhenAppending_ThenSummaryDescribesDeletedBoundedContext()
    {
        // Arrange

        // Act
        _log.Append(_boardId, new BoundedContextDeletedEvent
        {
            BoardId = _boardId,
            BoundedContext = new BoundedContextDto { Id = Guid.NewGuid(), Name = "Payments" }
        }, null);

        var entry = _log.GetRecent(_boardId)[0];

        // Assert
        entry.Summary.Should().Be("Deleted bounded context \"Payments\"");
    }
}
