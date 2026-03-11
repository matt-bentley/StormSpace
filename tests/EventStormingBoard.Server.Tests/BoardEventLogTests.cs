using EventStormingBoard.Server.Events;
using EventStormingBoard.Server.Models;
using EventStormingBoard.Server.Services;

namespace EventStormingBoard.Server.Tests;

public class BoardEventLogTests
{
    private readonly BoardEventLog _log = new();
    private readonly Guid _boardId = Guid.NewGuid();

    [Fact]
    public void GetRecent_EmptyBoard_ReturnsEmptyList()
    {
        var result = _log.GetRecent(Guid.NewGuid());
        Assert.Empty(result);
    }

    [Fact]
    public void Append_And_GetRecent_ReturnsSingleEntry()
    {
        var @event = new NoteCreatedEvent
        {
            BoardId = _boardId,
            Note = new NoteDto { Id = Guid.NewGuid(), Text = "OrderPlaced", Type = NoteType.Event }
        };

        _log.Append(_boardId, @event, "Alice");

        var entries = _log.GetRecent(_boardId);
        Assert.Single(entries);
        Assert.Equal("NoteCreatedEvent", entries[0].EventType);
        Assert.Contains("OrderPlaced", entries[0].Summary);
        Assert.Equal("Alice", entries[0].UserName);
    }

    [Fact]
    public void Append_NullUserName_IsStoredAsNull()
    {
        var @event = new BoardNameUpdatedEvent
        {
            BoardId = _boardId,
            NewName = "New",
            OldName = "Old"
        };

        _log.Append(_boardId, @event, null);

        var entries = _log.GetRecent(_boardId);
        Assert.Null(entries[0].UserName);
    }

    [Fact]
    public void GetRecent_RespectsCountParameter()
    {
        for (int i = 0; i < 10; i++)
        {
            _log.Append(_boardId, new BoardNameUpdatedEvent
            {
                BoardId = _boardId,
                NewName = $"Name {i}",
                OldName = $"Name {i - 1}"
            }, "User");
        }

        var entries = _log.GetRecent(_boardId, 3);
        Assert.Equal(3, entries.Count);
    }

    [Fact]
    public void GetRecent_ReturnsLatestEntries()
    {
        for (int i = 0; i < 5; i++)
        {
            _log.Append(_boardId, new BoardNameUpdatedEvent
            {
                BoardId = _boardId,
                NewName = $"Name {i}",
                OldName = "Prev"
            }, "User");
        }

        var entries = _log.GetRecent(_boardId, 2);
        Assert.Contains("Name 4", entries[1].Summary);
        Assert.Contains("Name 3", entries[0].Summary);
    }

    [Fact]
    public void Append_ExceedingMaxEntries_EvictsOldest()
    {
        for (int i = 0; i < 60; i++)
        {
            _log.Append(_boardId, new BoardNameUpdatedEvent
            {
                BoardId = _boardId,
                NewName = $"Name {i}",
                OldName = "Prev"
            }, "User");
        }

        var entries = _log.GetRecent(_boardId);
        Assert.Equal(50, entries.Count);
        // Oldest entry should be #10 (0-9 evicted)
        Assert.Contains("Name 10", entries[0].Summary);
        Assert.Contains("Name 59", entries[^1].Summary);
    }

    [Fact]
    public void GetRecent_SeparateBoards_AreIsolated()
    {
        var boardA = Guid.NewGuid();
        var boardB = Guid.NewGuid();

        _log.Append(boardA, new BoardNameUpdatedEvent { BoardId = boardA, NewName = "A", OldName = "" }, "User");
        _log.Append(boardB, new BoardNameUpdatedEvent { BoardId = boardB, NewName = "B", OldName = "" }, "User");

        Assert.Single(_log.GetRecent(boardA));
        Assert.Single(_log.GetRecent(boardB));
    }

    [Fact]
    public void Append_Timestamp_IsReasonable()
    {
        var before = DateTimeOffset.UtcNow;
        _log.Append(_boardId, new BoardNameUpdatedEvent
        {
            BoardId = _boardId,
            NewName = "X",
            OldName = "Y"
        }, "User");
        var after = DateTimeOffset.UtcNow;

        var entries = _log.GetRecent(_boardId);
        Assert.InRange(entries[0].Timestamp, before, after);
    }

    // ── Summary format tests ────────────────────────────────

    [Fact]
    public void Summary_NoteCreated()
    {
        _log.Append(_boardId, new NoteCreatedEvent
        {
            BoardId = _boardId,
            Note = new NoteDto { Id = Guid.NewGuid(), Text = "OrderPlaced", Type = NoteType.Event }
        }, null);

        var entry = _log.GetRecent(_boardId)[0];
        Assert.Equal("Created Event note \"OrderPlaced\"", entry.Summary);
    }

    [Fact]
    public void Summary_NotesMoved()
    {
        _log.Append(_boardId, new NotesMovedEvent
        {
            BoardId = _boardId,
            To = [
                new NoteMoveDto { NoteId = Guid.NewGuid(), Coordinates = new CoordinatesDto() },
                new NoteMoveDto { NoteId = Guid.NewGuid(), Coordinates = new CoordinatesDto() }
            ]
        }, null);

        var entry = _log.GetRecent(_boardId)[0];
        Assert.Equal("Moved 2 note(s)", entry.Summary);
    }

    [Fact]
    public void Summary_NotesDeleted()
    {
        _log.Append(_boardId, new NotesDeletedEvent
        {
            BoardId = _boardId,
            Notes = [new NoteDto { Id = Guid.NewGuid() }],
            Connections = [new ConnectionDto { FromNoteId = Guid.NewGuid(), ToNoteId = Guid.NewGuid() }]
        }, null);

        var entry = _log.GetRecent(_boardId)[0];
        Assert.Equal("Deleted 1 note(s) and 1 connection(s)", entry.Summary);
    }

    [Fact]
    public void Summary_NoteTextEdited()
    {
        _log.Append(_boardId, new NoteTextEditedEvent
        {
            BoardId = _boardId,
            NoteId = Guid.NewGuid(),
            ToText = "Updated",
            FromText = "Old"
        }, null);

        var entry = _log.GetRecent(_boardId)[0];
        Assert.Equal("Edited note text to \"Updated\"", entry.Summary);
    }

    [Fact]
    public void Summary_ConnectionCreated()
    {
        var from = Guid.NewGuid();
        var to = Guid.NewGuid();
        _log.Append(_boardId, new ConnectionCreatedEvent
        {
            BoardId = _boardId,
            Connection = new ConnectionDto { FromNoteId = from, ToNoteId = to }
        }, null);

        var entry = _log.GetRecent(_boardId)[0];
        Assert.Contains(from.ToString(), entry.Summary);
        Assert.Contains(to.ToString(), entry.Summary);
    }

    [Fact]
    public void Summary_Pasted()
    {
        _log.Append(_boardId, new PastedEvent
        {
            BoardId = _boardId,
            Notes = [new NoteDto { Id = Guid.NewGuid() }, new NoteDto { Id = Guid.NewGuid() }],
            Connections = []
        }, null);

        var entry = _log.GetRecent(_boardId)[0];
        Assert.Equal("Pasted 2 note(s) and 0 connection(s)", entry.Summary);
    }

    [Fact]
    public void Summary_BoardNameUpdated()
    {
        _log.Append(_boardId, new BoardNameUpdatedEvent
        {
            BoardId = _boardId,
            NewName = "My Board",
            OldName = "Old Board"
        }, null);

        var entry = _log.GetRecent(_boardId)[0];
        Assert.Equal("Renamed board to \"My Board\"", entry.Summary);
    }
}
