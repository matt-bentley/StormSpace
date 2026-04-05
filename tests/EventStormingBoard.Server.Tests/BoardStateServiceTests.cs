using EventStormingBoard.Server.Entities;
using EventStormingBoard.Server.Events;
using EventStormingBoard.Server.Models;
using EventStormingBoard.Server.Services;

namespace EventStormingBoard.Server.Tests;

public class BoardStateServiceTests
{
    private readonly InMemoryBoardsRepository _repository;
    private readonly BoardStateService _service;
    private readonly Board _board;

    public BoardStateServiceTests()
    {
        _repository = new InMemoryBoardsRepository();
        _service = new BoardStateService(_repository);
        _board = new Board
        {
            Id = Guid.NewGuid(),
            Name = "Test Board"
        };
        _repository.Add(_board);
    }

    // ── BoardNameUpdated ────────────────────────────────────

    [Fact]
    public void ApplyBoardNameUpdated_UpdatesName()
    {
        var @event = new BoardNameUpdatedEvent
        {
            BoardId = _board.Id,
            NewName = "New Name",
            OldName = "Test Board"
        };

        _service.ApplyBoardNameUpdated(@event);

        Assert.Equal("New Name", _board.Name);
    }

    [Fact]
    public void ApplyBoardNameUpdated_Undo_RevertsToOldName()
    {
        _board.Name = "New Name";
        var @event = new BoardNameUpdatedEvent
        {
            BoardId = _board.Id,
            NewName = "New Name",
            OldName = "Test Board",
            IsUndo = true
        };

        _service.ApplyBoardNameUpdated(@event);

        Assert.Equal("Test Board", _board.Name);
    }

    // ── BoardContextUpdated (Phase) ─────────────────────────

    [Fact]
    public void ApplyBoardContextUpdated_SetsPhase()
    {
        var @event = new BoardContextUpdatedEvent
        {
            BoardId = _board.Id,
            OldDomain = null,
            NewDomain = null,
            OldSessionScope = null,
            NewSessionScope = null,
            OldPhase = null,
            NewPhase = EventStormingPhase.IdentifyEvents,
            OldAutonomousEnabled = false,
            NewAutonomousEnabled = true
        };

        _service.ApplyBoardContextUpdated(@event);

        Assert.Equal(EventStormingPhase.IdentifyEvents, _board.Phase);
        Assert.True(_board.AutonomousEnabled);
    }

    [Fact]
    public void ApplyBoardContextUpdated_Undo_RevertsPhase()
    {
        _board.Phase = EventStormingPhase.AddCommandsAndPolicies;
        _board.AutonomousEnabled = true;
        var @event = new BoardContextUpdatedEvent
        {
            BoardId = _board.Id,
            OldDomain = null,
            NewDomain = null,
            OldSessionScope = null,
            NewSessionScope = null,
            OldPhase = EventStormingPhase.IdentifyEvents,
            NewPhase = EventStormingPhase.AddCommandsAndPolicies,
            OldAutonomousEnabled = false,
            NewAutonomousEnabled = true,
            IsUndo = true
        };

        _service.ApplyBoardContextUpdated(@event);

        Assert.Equal(EventStormingPhase.IdentifyEvents, _board.Phase);
        Assert.False(_board.AutonomousEnabled);
    }

    // ── NoteCreated ─────────────────────────────────────────

    [Fact]
    public void ApplyNoteCreated_AddsNoteToBoard()
    {
        var noteId = Guid.NewGuid();
        var @event = new NoteCreatedEvent
        {
            BoardId = _board.Id,
            Note = new NoteDto
            {
                Id = noteId,
                Text = "OrderPlaced",
                X = 100, Y = 200,
                Width = 120, Height = 120,
                Color = "#fdb634",
                Type = NoteType.Event
            }
        };

        _service.ApplyNoteCreated(@event);

        Assert.Single(_board.Notes);
        var note = _board.Notes[0];
        Assert.Equal(noteId, note.Id);
        Assert.Equal("OrderPlaced", note.Text);
        Assert.Equal(100, note.X);
        Assert.Equal(200, note.Y);
        Assert.Equal(NoteType.Event, note.Type);
    }

    [Fact]
    public void ApplyNoteCreated_Undo_RemovesNote()
    {
        var noteId = Guid.NewGuid();
        _board.Notes.Add(new Note { Id = noteId, Text = "OrderPlaced" });

        var @event = new NoteCreatedEvent
        {
            BoardId = _board.Id,
            IsUndo = true,
            Note = new NoteDto { Id = noteId, Text = "OrderPlaced" }
        };

        _service.ApplyNoteCreated(@event);

        Assert.Empty(_board.Notes);
    }

    // ── NotesMoved ──────────────────────────────────────────

    [Fact]
    public void ApplyNotesMoved_UpdatesCoordinates()
    {
        var noteId = Guid.NewGuid();
        _board.Notes.Add(new Note { Id = noteId, X = 10, Y = 20 });

        var @event = new NotesMovedEvent
        {
            BoardId = _board.Id,
            From = [new NoteMoveDto { NoteId = noteId, Coordinates = new CoordinatesDto { X = 10, Y = 20 } }],
            To = [new NoteMoveDto { NoteId = noteId, Coordinates = new CoordinatesDto { X = 300, Y = 400 } }]
        };

        _service.ApplyNotesMoved(@event);

        Assert.Equal(300, _board.Notes[0].X);
        Assert.Equal(400, _board.Notes[0].Y);
    }

    [Fact]
    public void ApplyNotesMoved_Undo_RevertsToFromCoordinates()
    {
        var noteId = Guid.NewGuid();
        _board.Notes.Add(new Note { Id = noteId, X = 300, Y = 400 });

        var @event = new NotesMovedEvent
        {
            BoardId = _board.Id,
            IsUndo = true,
            From = [new NoteMoveDto { NoteId = noteId, Coordinates = new CoordinatesDto { X = 10, Y = 20 } }],
            To = [new NoteMoveDto { NoteId = noteId, Coordinates = new CoordinatesDto { X = 300, Y = 400 } }]
        };

        _service.ApplyNotesMoved(@event);

        Assert.Equal(10, _board.Notes[0].X);
        Assert.Equal(20, _board.Notes[0].Y);
    }

    [Fact]
    public void ApplyNotesMoved_MultipleNotes()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        _board.Notes.Add(new Note { Id = id1, X = 0, Y = 0 });
        _board.Notes.Add(new Note { Id = id2, X = 0, Y = 0 });

        var @event = new NotesMovedEvent
        {
            BoardId = _board.Id,
            From = [
                new NoteMoveDto { NoteId = id1, Coordinates = new CoordinatesDto { X = 0, Y = 0 } },
                new NoteMoveDto { NoteId = id2, Coordinates = new CoordinatesDto { X = 0, Y = 0 } }
            ],
            To = [
                new NoteMoveDto { NoteId = id1, Coordinates = new CoordinatesDto { X = 50, Y = 60 } },
                new NoteMoveDto { NoteId = id2, Coordinates = new CoordinatesDto { X = 70, Y = 80 } }
            ]
        };

        _service.ApplyNotesMoved(@event);

        Assert.Equal(50, _board.Notes.First(n => n.Id == id1).X);
        Assert.Equal(70, _board.Notes.First(n => n.Id == id2).X);
    }

    // ── NoteResized ─────────────────────────────────────────

    [Fact]
    public void ApplyNoteResized_UpdatesDimensions()
    {
        var noteId = Guid.NewGuid();
        _board.Notes.Add(new Note { Id = noteId, X = 10, Y = 20, Width = 120, Height = 120 });

        var @event = new NoteResizedEvent
        {
            BoardId = _board.Id,
            NoteId = noteId,
            From = new NoteSizeDto { X = 10, Y = 20, Width = 120, Height = 120 },
            To = new NoteSizeDto { X = 10, Y = 20, Width = 200, Height = 300 }
        };

        _service.ApplyNoteResized(@event);

        var note = _board.Notes[0];
        Assert.Equal(200, note.Width);
        Assert.Equal(300, note.Height);
    }

    [Fact]
    public void ApplyNoteResized_Undo_RevertsToFromSize()
    {
        var noteId = Guid.NewGuid();
        _board.Notes.Add(new Note { Id = noteId, X = 10, Y = 20, Width = 200, Height = 300 });

        var @event = new NoteResizedEvent
        {
            BoardId = _board.Id,
            NoteId = noteId,
            IsUndo = true,
            From = new NoteSizeDto { X = 10, Y = 20, Width = 120, Height = 120 },
            To = new NoteSizeDto { X = 10, Y = 20, Width = 200, Height = 300 }
        };

        _service.ApplyNoteResized(@event);

        var note = _board.Notes[0];
        Assert.Equal(120, note.Width);
        Assert.Equal(120, note.Height);
    }

    // ── NotesDeleted ────────────────────────────────────────

    [Fact]
    public void ApplyNotesDeleted_RemovesNotesAndConnections()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        _board.Notes.Add(new Note { Id = id1, Text = "A" });
        _board.Notes.Add(new Note { Id = id2, Text = "B" });
        _board.Connections.Add(new Connection { FromNoteId = id1, ToNoteId = id2 });

        var @event = new NotesDeletedEvent
        {
            BoardId = _board.Id,
            Notes = [new NoteDto { Id = id1, Text = "A" }, new NoteDto { Id = id2, Text = "B" }],
            Connections = [new ConnectionDto { FromNoteId = id1, ToNoteId = id2 }]
        };

        _service.ApplyNotesDeleted(@event);

        Assert.Empty(_board.Notes);
        Assert.Empty(_board.Connections);
    }

    [Fact]
    public void ApplyNotesDeleted_Undo_RestoresNotesAndConnections()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();

        var @event = new NotesDeletedEvent
        {
            BoardId = _board.Id,
            IsUndo = true,
            Notes = [new NoteDto { Id = id1, Text = "A", Color = "#fff", Type = NoteType.Event }, new NoteDto { Id = id2, Text = "B", Color = "#fff", Type = NoteType.Command }],
            Connections = [new ConnectionDto { FromNoteId = id1, ToNoteId = id2 }]
        };

        _service.ApplyNotesDeleted(@event);

        Assert.Equal(2, _board.Notes.Count);
        Assert.Single(_board.Connections);
    }

    [Fact]
    public void ApplyNotesDeleted_Undo_DoesNotDuplicateExistingNotes()
    {
        var id1 = Guid.NewGuid();
        _board.Notes.Add(new Note { Id = id1, Text = "A" });

        var @event = new NotesDeletedEvent
        {
            BoardId = _board.Id,
            IsUndo = true,
            Notes = [new NoteDto { Id = id1, Text = "A" }],
            Connections = []
        };

        _service.ApplyNotesDeleted(@event);

        Assert.Single(_board.Notes);
    }

    // ── NoteTextEdited ──────────────────────────────────────

    [Fact]
    public void ApplyNoteTextEdited_UpdatesText()
    {
        var noteId = Guid.NewGuid();
        _board.Notes.Add(new Note { Id = noteId, Text = "Old" });

        var @event = new NoteTextEditedEvent
        {
            BoardId = _board.Id,
            NoteId = noteId,
            FromText = "Old",
            ToText = "New"
        };

        _service.ApplyNoteTextEdited(@event);

        Assert.Equal("New", _board.Notes[0].Text);
    }

    [Fact]
    public void ApplyNoteTextEdited_Undo_RevertsText()
    {
        var noteId = Guid.NewGuid();
        _board.Notes.Add(new Note { Id = noteId, Text = "New" });

        var @event = new NoteTextEditedEvent
        {
            BoardId = _board.Id,
            NoteId = noteId,
            IsUndo = true,
            FromText = "Old",
            ToText = "New"
        };

        _service.ApplyNoteTextEdited(@event);

        Assert.Equal("Old", _board.Notes[0].Text);
    }

    // ── ConnectionCreated ───────────────────────────────────

    [Fact]
    public void ApplyConnectionCreated_AddsConnection()
    {
        var from = Guid.NewGuid();
        var to = Guid.NewGuid();

        var @event = new ConnectionCreatedEvent
        {
            BoardId = _board.Id,
            Connection = new ConnectionDto { FromNoteId = from, ToNoteId = to }
        };

        _service.ApplyConnectionCreated(@event);

        Assert.Single(_board.Connections);
        Assert.Equal(from, _board.Connections[0].FromNoteId);
        Assert.Equal(to, _board.Connections[0].ToNoteId);
    }

    [Fact]
    public void ApplyConnectionCreated_Undo_RemovesConnection()
    {
        var from = Guid.NewGuid();
        var to = Guid.NewGuid();
        _board.Connections.Add(new Connection { FromNoteId = from, ToNoteId = to });

        var @event = new ConnectionCreatedEvent
        {
            BoardId = _board.Id,
            IsUndo = true,
            Connection = new ConnectionDto { FromNoteId = from, ToNoteId = to }
        };

        _service.ApplyConnectionCreated(@event);

        Assert.Empty(_board.Connections);
    }

    // ── Pasted ──────────────────────────────────────────────

    [Fact]
    public void ApplyPasted_AddsNotesAndConnections()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();

        var @event = new PastedEvent
        {
            BoardId = _board.Id,
            Notes = [
                new NoteDto { Id = id1, Text = "A", X = 10, Y = 20, Width = 120, Height = 120, Color = "#fdb634", Type = NoteType.Event },
                new NoteDto { Id = id2, Text = "B", X = 30, Y = 40, Width = 120, Height = 120, Color = "#61c4fd", Type = NoteType.Command }
            ],
            Connections = [new ConnectionDto { FromNoteId = id1, ToNoteId = id2 }]
        };

        _service.ApplyPasted(@event);

        Assert.Equal(2, _board.Notes.Count);
        Assert.Single(_board.Connections);
    }

    [Fact]
    public void ApplyPasted_Undo_RemovesPastedNotesAndConnections()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        _board.Notes.Add(new Note { Id = id1, Text = "A" });
        _board.Notes.Add(new Note { Id = id2, Text = "B" });
        _board.Connections.Add(new Connection { FromNoteId = id1, ToNoteId = id2 });

        var @event = new PastedEvent
        {
            BoardId = _board.Id,
            IsUndo = true,
            Notes = [new NoteDto { Id = id1, Text = "A" }, new NoteDto { Id = id2, Text = "B" }],
            Connections = [new ConnectionDto { FromNoteId = id1, ToNoteId = id2 }]
        };

        _service.ApplyPasted(@event);

        Assert.Empty(_board.Notes);
        Assert.Empty(_board.Connections);
    }

    // ── Edge cases ──────────────────────────────────────────

    [Fact]
    public void ApplyEvent_NonExistentBoard_DoesNotThrow()
    {
        var @event = new NoteCreatedEvent
        {
            BoardId = Guid.NewGuid(),
            Note = new NoteDto { Id = Guid.NewGuid(), Text = "X" }
        };

        var exception = Record.Exception(() => _service.ApplyNoteCreated(@event));
        Assert.Null(exception);
    }

    [Fact]
    public void ApplyNotesMoved_NonExistentNote_DoesNotThrow()
    {
        var @event = new NotesMovedEvent
        {
            BoardId = _board.Id,
            From = [new NoteMoveDto { NoteId = Guid.NewGuid(), Coordinates = new CoordinatesDto { X = 0, Y = 0 } }],
            To = [new NoteMoveDto { NoteId = Guid.NewGuid(), Coordinates = new CoordinatesDto { X = 10, Y = 10 } }]
        };

        var exception = Record.Exception(() => _service.ApplyNotesMoved(@event));
        Assert.Null(exception);
    }
}
