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
    public void GivenBoardNameUpdatedEvent_WhenApplying_ThenBoardNameIsUpdated()
    {
        // Arrange
        var @event = new BoardNameUpdatedEvent
        {
            BoardId = _board.Id,
            NewName = "New Name",
            OldName = "Test Board"
        };

        // Act
        _service.ApplyBoardNameUpdated(@event);

        // Assert
        _board.Name.Should().Be("New Name");
    }

    [Fact]
    public void GivenUndoBoardNameUpdatedEvent_WhenApplying_ThenBoardNameRevertsToOldName()
    {
        // Arrange
        _board.Name = "New Name";
        var @event = new BoardNameUpdatedEvent
        {
            BoardId = _board.Id,
            NewName = "New Name",
            OldName = "Test Board",
            IsUndo = true
        };

        // Act
        _service.ApplyBoardNameUpdated(@event);

        // Assert
        _board.Name.Should().Be("Test Board");
    }

    // ── BoardContextUpdated (Phase) ─────────────────────────

    [Fact]
    public void GivenBoardContextUpdatedEvent_WhenApplying_ThenPhaseAndAutonomousFlagAreSet()
    {
        // Arrange
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

        // Act
        _service.ApplyBoardContextUpdated(@event);

        // Assert
        _board.Phase.Should().Be(EventStormingPhase.IdentifyEvents);
        _board.AutonomousEnabled.Should().BeTrue();
    }

    [Fact]
    public void GivenUndoBoardContextUpdatedEvent_WhenApplying_ThenPhaseAndAutonomousFlagRevert()
    {
        // Arrange
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

        // Act
        _service.ApplyBoardContextUpdated(@event);

        // Assert
        _board.Phase.Should().Be(EventStormingPhase.IdentifyEvents);
        _board.AutonomousEnabled.Should().BeFalse();
    }

    // ── NoteCreated ─────────────────────────────────────────

    [Fact]
    public void GivenNoteCreatedEvent_WhenApplying_ThenNoteIsAddedToBoard()
    {
        // Arrange
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

        // Act
        _service.ApplyNoteCreated(@event);

        // Assert
        _board.Notes.Should().ContainSingle();
        var note = _board.Notes[0];
        note.Id.Should().Be(noteId);
        note.Text.Should().Be("OrderPlaced");
        note.X.Should().Be(100);
        note.Y.Should().Be(200);
        note.Type.Should().Be(NoteType.Event);
    }

    [Fact]
    public void GivenUndoNoteCreatedEvent_WhenApplying_ThenNoteIsRemoved()
    {
        // Arrange
        var noteId = Guid.NewGuid();
        _board.Notes.Add(new Note { Id = noteId, Text = "OrderPlaced" });

        var @event = new NoteCreatedEvent
        {
            BoardId = _board.Id,
            IsUndo = true,
            Note = new NoteDto { Id = noteId, Text = "OrderPlaced" }
        };

        // Act
        _service.ApplyNoteCreated(@event);

        // Assert
        _board.Notes.Should().BeEmpty();
    }

    // ── NotesMoved ──────────────────────────────────────────

    [Fact]
    public void GivenNotesMovedEvent_WhenApplying_ThenCoordinatesAreUpdated()
    {
        // Arrange
        var noteId = Guid.NewGuid();
        _board.Notes.Add(new Note { Id = noteId, X = 10, Y = 20 });

        var @event = new NotesMovedEvent
        {
            BoardId = _board.Id,
            From = [new NoteMoveDto { NoteId = noteId, Coordinates = new CoordinatesDto { X = 10, Y = 20 } }],
            To = [new NoteMoveDto { NoteId = noteId, Coordinates = new CoordinatesDto { X = 300, Y = 400 } }]
        };

        // Act
        _service.ApplyNotesMoved(@event);

        // Assert
        _board.Notes[0].X.Should().Be(300);
        _board.Notes[0].Y.Should().Be(400);
    }

    [Fact]
    public void GivenUndoNotesMovedEvent_WhenApplying_ThenCoordinatesRevertToFromValues()
    {
        // Arrange
        var noteId = Guid.NewGuid();
        _board.Notes.Add(new Note { Id = noteId, X = 300, Y = 400 });

        var @event = new NotesMovedEvent
        {
            BoardId = _board.Id,
            IsUndo = true,
            From = [new NoteMoveDto { NoteId = noteId, Coordinates = new CoordinatesDto { X = 10, Y = 20 } }],
            To = [new NoteMoveDto { NoteId = noteId, Coordinates = new CoordinatesDto { X = 300, Y = 400 } }]
        };

        // Act
        _service.ApplyNotesMoved(@event);

        // Assert
        _board.Notes[0].X.Should().Be(10);
        _board.Notes[0].Y.Should().Be(20);
    }

    [Fact]
    public void GivenNotesMovedEventWithMultipleNotes_WhenApplying_ThenEachNoteIsMoved()
    {
        // Arrange
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

        // Act
        _service.ApplyNotesMoved(@event);

        // Assert
        _board.Notes.First(n => n.Id == id1).X.Should().Be(50);
        _board.Notes.First(n => n.Id == id2).X.Should().Be(70);
    }

    // ── NoteResized ─────────────────────────────────────────

    [Fact]
    public void GivenNoteResizedEvent_WhenApplying_ThenNoteDimensionsAreUpdated()
    {
        // Arrange
        var noteId = Guid.NewGuid();
        _board.Notes.Add(new Note { Id = noteId, X = 10, Y = 20, Width = 120, Height = 120 });

        var @event = new NoteResizedEvent
        {
            BoardId = _board.Id,
            NoteId = noteId,
            From = new NoteSizeDto { X = 10, Y = 20, Width = 120, Height = 120 },
            To = new NoteSizeDto { X = 10, Y = 20, Width = 200, Height = 300 }
        };

        // Act
        _service.ApplyNoteResized(@event);

        // Assert
        var note = _board.Notes[0];
        note.Width.Should().Be(200);
        note.Height.Should().Be(300);
    }

    [Fact]
    public void GivenUndoNoteResizedEvent_WhenApplying_ThenNoteDimensionsRevertToFromValues()
    {
        // Arrange
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

        // Act
        _service.ApplyNoteResized(@event);

        // Assert
        var note = _board.Notes[0];
        note.Width.Should().Be(120);
        note.Height.Should().Be(120);
    }

    // ── NotesDeleted ────────────────────────────────────────

    [Fact]
    public void GivenNotesDeletedEvent_WhenApplying_ThenNotesAndConnectionsAreRemoved()
    {
        // Arrange
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

        // Act
        _service.ApplyNotesDeleted(@event);

        // Assert
        _board.Notes.Should().BeEmpty();
        _board.Connections.Should().BeEmpty();
    }

    [Fact]
    public void GivenUndoNotesDeletedEvent_WhenApplying_ThenNotesAndConnectionsAreRestored()
    {
        // Arrange
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();

        var @event = new NotesDeletedEvent
        {
            BoardId = _board.Id,
            IsUndo = true,
            Notes = [new NoteDto { Id = id1, Text = "A", Color = "#fff", Type = NoteType.Event }, new NoteDto { Id = id2, Text = "B", Color = "#fff", Type = NoteType.Command }],
            Connections = [new ConnectionDto { FromNoteId = id1, ToNoteId = id2 }]
        };

        // Act
        _service.ApplyNotesDeleted(@event);

        // Assert
        _board.Notes.Count.Should().Be(2);
        _board.Connections.Should().ContainSingle();
    }

    [Fact]
    public void GivenUndoNotesDeletedEventWithExistingNotes_WhenApplying_ThenExistingNotesAreNotDuplicated()
    {
        // Arrange
        var id1 = Guid.NewGuid();
        _board.Notes.Add(new Note { Id = id1, Text = "A" });

        var @event = new NotesDeletedEvent
        {
            BoardId = _board.Id,
            IsUndo = true,
            Notes = [new NoteDto { Id = id1, Text = "A" }],
            Connections = []
        };

        // Act
        _service.ApplyNotesDeleted(@event);

        // Assert
        _board.Notes.Should().ContainSingle();
    }

    // ── NoteTextEdited ──────────────────────────────────────

    [Fact]
    public void GivenNoteTextEditedEvent_WhenApplying_ThenNoteTextIsUpdated()
    {
        // Arrange
        var noteId = Guid.NewGuid();
        _board.Notes.Add(new Note { Id = noteId, Text = "Old" });

        var @event = new NoteTextEditedEvent
        {
            BoardId = _board.Id,
            NoteId = noteId,
            FromText = "Old",
            ToText = "New"
        };

        // Act
        _service.ApplyNoteTextEdited(@event);

        // Assert
        _board.Notes[0].Text.Should().Be("New");
    }

    [Fact]
    public void GivenUndoNoteTextEditedEvent_WhenApplying_ThenNoteTextReverts()
    {
        // Arrange
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

        // Act
        _service.ApplyNoteTextEdited(@event);

        // Assert
        _board.Notes[0].Text.Should().Be("Old");
    }

    // ── ConnectionCreated ───────────────────────────────────

    [Fact]
    public void GivenConnectionCreatedEvent_WhenApplying_ThenConnectionIsAdded()
    {
        // Arrange
        var from = Guid.NewGuid();
        var to = Guid.NewGuid();

        var @event = new ConnectionCreatedEvent
        {
            BoardId = _board.Id,
            Connection = new ConnectionDto { FromNoteId = from, ToNoteId = to }
        };

        // Act
        _service.ApplyConnectionCreated(@event);

        // Assert
        _board.Connections.Should().ContainSingle();
        _board.Connections[0].FromNoteId.Should().Be(from);
        _board.Connections[0].ToNoteId.Should().Be(to);
    }

    [Fact]
    public void GivenUndoConnectionCreatedEvent_WhenApplying_ThenConnectionIsRemoved()
    {
        // Arrange
        var from = Guid.NewGuid();
        var to = Guid.NewGuid();
        _board.Connections.Add(new Connection { FromNoteId = from, ToNoteId = to });

        var @event = new ConnectionCreatedEvent
        {
            BoardId = _board.Id,
            IsUndo = true,
            Connection = new ConnectionDto { FromNoteId = from, ToNoteId = to }
        };

        // Act
        _service.ApplyConnectionCreated(@event);

        // Assert
        _board.Connections.Should().BeEmpty();
    }

    // ── Pasted ──────────────────────────────────────────────

    [Fact]
    public void GivenPastedEvent_WhenApplying_ThenNotesAndConnectionsAreAdded()
    {
        // Arrange
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

        // Act
        _service.ApplyPasted(@event);

        // Assert
        _board.Notes.Count.Should().Be(2);
        _board.Connections.Should().ContainSingle();
    }

    [Fact]
    public void GivenUndoPastedEvent_WhenApplying_ThenPastedNotesAndConnectionsAreRemoved()
    {
        // Arrange
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

        // Act
        _service.ApplyPasted(@event);

        // Assert
        _board.Notes.Should().BeEmpty();
        _board.Connections.Should().BeEmpty();
    }

    // ── BoundedContextCreated ───────────────────────────────

    [Fact]
    public void GivenBoundedContextCreatedEvent_WhenApplying_ThenBoundedContextIsAdded()
    {
        // Arrange
        var bcId = Guid.NewGuid();
        var @event = new BoundedContextCreatedEvent
        {
            BoardId = _board.Id,
            BoundedContext = new BoundedContextDto
            {
                Id = bcId,
                Name = "Orders",
                X = 100, Y = 200,
                Width = 800, Height = 600,
                Color = "#00bcd4"
            }
        };

        // Act
        _service.ApplyBoundedContextCreated(@event);

        // Assert
        _board.BoundedContexts.Should().ContainSingle();
        var bc = _board.BoundedContexts[0];
        bc.Id.Should().Be(bcId);
        bc.Name.Should().Be("Orders");
        bc.X.Should().Be(100);
        bc.Y.Should().Be(200);
        bc.Width.Should().Be(800);
        bc.Height.Should().Be(600);
    }

    [Fact]
    public void GivenUndoBoundedContextCreatedEvent_WhenApplying_ThenBoundedContextIsRemoved()
    {
        // Arrange
        var bcId = Guid.NewGuid();
        _board.BoundedContexts.Add(new BoundedContext { Id = bcId, Name = "Orders" });

        var @event = new BoundedContextCreatedEvent
        {
            BoardId = _board.Id,
            IsUndo = true,
            BoundedContext = new BoundedContextDto { Id = bcId, Name = "Orders" }
        };

        // Act
        _service.ApplyBoundedContextCreated(@event);

        // Assert
        _board.BoundedContexts.Should().BeEmpty();
    }

    // ── BoundedContextUpdated ───────────────────────────────

    [Fact]
    public void GivenBoundedContextUpdatedEventWithNameChange_WhenApplying_ThenBoundedContextNameIsUpdated()
    {
        // Arrange
        var bcId = Guid.NewGuid();
        _board.BoundedContexts.Add(new BoundedContext { Id = bcId, Name = "Orders", X = 100, Y = 200, Width = 800, Height = 600 });

        var @event = new BoundedContextUpdatedEvent
        {
            BoardId = _board.Id,
            BoundedContextId = bcId,
            OldName = "Orders",
            NewName = "Order Management"
        };

        // Act
        _service.ApplyBoundedContextUpdated(@event);

        // Assert
        _board.BoundedContexts[0].Name.Should().Be("Order Management");
    }

    [Fact]
    public void GivenBoundedContextUpdatedEventWithGeometryChange_WhenApplying_ThenBoundedContextGeometryIsUpdated()
    {
        // Arrange
        var bcId = Guid.NewGuid();
        _board.BoundedContexts.Add(new BoundedContext { Id = bcId, Name = "Orders", X = 100, Y = 200, Width = 800, Height = 600 });

        var @event = new BoundedContextUpdatedEvent
        {
            BoardId = _board.Id,
            BoundedContextId = bcId,
            OldX = 100, NewX = 300,
            OldY = 200, NewY = 400,
            OldWidth = 800, NewWidth = 1000,
            OldHeight = 600, NewHeight = 700
        };

        // Act
        _service.ApplyBoundedContextUpdated(@event);

        // Assert
        var bc = _board.BoundedContexts[0];
        bc.X.Should().Be(300);
        bc.Y.Should().Be(400);
        bc.Width.Should().Be(1000);
        bc.Height.Should().Be(700);
    }

    [Fact]
    public void GivenUndoBoundedContextUpdatedEvent_WhenApplying_ThenBoundedContextValuesRevert()
    {
        // Arrange
        var bcId = Guid.NewGuid();
        _board.BoundedContexts.Add(new BoundedContext { Id = bcId, Name = "Order Management", X = 300, Y = 400, Width = 1000, Height = 700 });

        var @event = new BoundedContextUpdatedEvent
        {
            BoardId = _board.Id,
            BoundedContextId = bcId,
            IsUndo = true,
            OldName = "Orders",
            NewName = "Order Management",
            OldX = 100, NewX = 300,
            OldY = 200, NewY = 400,
            OldWidth = 800, NewWidth = 1000,
            OldHeight = 600, NewHeight = 700
        };

        // Act
        _service.ApplyBoundedContextUpdated(@event);

        // Assert
        var bc = _board.BoundedContexts[0];
        bc.Name.Should().Be("Orders");
        bc.X.Should().Be(100);
        bc.Y.Should().Be(200);
        bc.Width.Should().Be(800);
        bc.Height.Should().Be(600);
    }

    // ── BoundedContextDeleted ───────────────────────────────

    [Fact]
    public void GivenBoundedContextDeletedEvent_WhenApplying_ThenBoundedContextIsRemoved()
    {
        // Arrange
        var bcId = Guid.NewGuid();
        _board.BoundedContexts.Add(new BoundedContext { Id = bcId, Name = "Orders", X = 100, Y = 200, Width = 800, Height = 600 });

        var @event = new BoundedContextDeletedEvent
        {
            BoardId = _board.Id,
            BoundedContext = new BoundedContextDto { Id = bcId, Name = "Orders", X = 100, Y = 200, Width = 800, Height = 600 }
        };

        // Act
        _service.ApplyBoundedContextDeleted(@event);

        // Assert
        _board.BoundedContexts.Should().BeEmpty();
    }

    [Fact]
    public void GivenUndoBoundedContextDeletedEvent_WhenApplying_ThenBoundedContextIsRestored()
    {
        // Arrange
        var bcId = Guid.NewGuid();

        var @event = new BoundedContextDeletedEvent
        {
            BoardId = _board.Id,
            IsUndo = true,
            BoundedContext = new BoundedContextDto { Id = bcId, Name = "Orders", X = 100, Y = 200, Width = 800, Height = 600, Color = "#00bcd4" }
        };

        // Act
        _service.ApplyBoundedContextDeleted(@event);

        // Assert
        _board.BoundedContexts.Should().ContainSingle();
        var bc = _board.BoundedContexts[0];
        bc.Id.Should().Be(bcId);
        bc.Name.Should().Be("Orders");
    }

    [Fact]
    public void GivenUndoBoundedContextDeletedEventWithExistingContext_WhenApplying_ThenExistingContextIsNotDuplicated()
    {
        // Arrange
        var bcId = Guid.NewGuid();
        _board.BoundedContexts.Add(new BoundedContext { Id = bcId, Name = "Orders" });

        var @event = new BoundedContextDeletedEvent
        {
            BoardId = _board.Id,
            IsUndo = true,
            BoundedContext = new BoundedContextDto { Id = bcId, Name = "Orders" }
        };

        // Act
        _service.ApplyBoundedContextDeleted(@event);

        // Assert
        _board.BoundedContexts.Should().ContainSingle();
    }

    // ── Edge cases ──────────────────────────────────────────

    [Fact]
    public void GivenEventForNonExistentBoard_WhenApplying_ThenDoesNotThrow()
    {
        // Arrange
        var @event = new NoteCreatedEvent
        {
            BoardId = Guid.NewGuid(),
            Note = new NoteDto { Id = Guid.NewGuid(), Text = "X" }
        };
        Action act = () => _service.ApplyNoteCreated(@event);

        // Act

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void GivenNotesMovedEventForNonExistentNote_WhenApplying_ThenDoesNotThrow()
    {
        // Arrange
        var @event = new NotesMovedEvent
        {
            BoardId = _board.Id,
            From = [new NoteMoveDto { NoteId = Guid.NewGuid(), Coordinates = new CoordinatesDto { X = 0, Y = 0 } }],
            To = [new NoteMoveDto { NoteId = Guid.NewGuid(), Coordinates = new CoordinatesDto { X = 10, Y = 10 } }]
        };
        Action act = () => _service.ApplyNotesMoved(@event);

        // Act

        // Assert
        act.Should().NotThrow();
    }
}
