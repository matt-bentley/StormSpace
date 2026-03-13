using EventStormingBoard.Server.Entities;
using EventStormingBoard.Server.Events;
using EventStormingBoard.Server.Models;
using EventStormingBoard.Server.Repositories;
using System.Collections.Concurrent;

namespace EventStormingBoard.Server.Services
{
    public interface IBoardStateService
    {
        void ApplyBoardNameUpdated(BoardNameUpdatedEvent @event);
        void ApplyBoardContextUpdated(BoardContextUpdatedEvent @event);
        void ApplyNoteCreated(NoteCreatedEvent @event);
        void ApplyNotesMoved(NotesMovedEvent @event);
        void ApplyNoteResized(NoteResizedEvent @event);
        void ApplyNotesDeleted(NotesDeletedEvent @event);
        void ApplyNoteTextEdited(NoteTextEditedEvent @event);
        void ApplyConnectionCreated(ConnectionCreatedEvent @event);
        void ApplyPasted(PastedEvent @event);
    }

    public sealed class BoardStateService : IBoardStateService
    {
        private readonly IBoardsRepository _repository;
        private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _locks = new();

        public BoardStateService(IBoardsRepository repository)
        {
            _repository = repository;
        }

        public void ApplyBoardNameUpdated(BoardNameUpdatedEvent @event)
        {
            WithBoard(@event.BoardId, board =>
            {
                board.Name = @event.IsUndo ? @event.OldName : @event.NewName;
            });
        }

        public void ApplyBoardContextUpdated(BoardContextUpdatedEvent @event)
        {
            WithBoard(@event.BoardId, board =>
            {
                board.Domain = @event.IsUndo ? @event.OldDomain : @event.NewDomain;
                board.SessionScope = @event.IsUndo ? @event.OldSessionScope : @event.NewSessionScope;
                board.AgentInstructions = @event.IsUndo ? @event.OldAgentInstructions : @event.NewAgentInstructions;
                board.Phase = @event.IsUndo ? @event.OldPhase : @event.NewPhase;
                board.AutonomousEnabled = @event.IsUndo ? @event.OldAutonomousEnabled : @event.NewAutonomousEnabled;
            });
        }

        public void ApplyNoteCreated(NoteCreatedEvent @event)
        {
            WithBoard(@event.BoardId, board =>
            {
                if (@event.IsUndo)
                {
                    board.Notes.RemoveAll(n => n.Id == @event.Note.Id);
                }
                else
                {
                    board.Notes.Add(new Note
                    {
                        Id = @event.Note.Id,
                        Text = @event.Note.Text,
                        X = @event.Note.X,
                        Y = @event.Note.Y,
                        Width = @event.Note.Width,
                        Height = @event.Note.Height,
                        Color = @event.Note.Color,
                        Type = @event.Note.Type
                    });
                }
            });
        }

        public void ApplyNotesMoved(NotesMovedEvent @event)
        {
            WithBoard(@event.BoardId, board =>
            {
                var moves = @event.IsUndo ? @event.From : @event.To;
                foreach (var move in moves)
                {
                    var note = board.Notes.FirstOrDefault(n => n.Id == move.NoteId);
                    if (note != null)
                    {
                        note.X = move.Coordinates.X;
                        note.Y = move.Coordinates.Y;
                    }
                }
            });
        }

        public void ApplyNoteResized(NoteResizedEvent @event)
        {
            WithBoard(@event.BoardId, board =>
            {
                var note = board.Notes.FirstOrDefault(n => n.Id == @event.NoteId);
                if (note != null)
                {
                    var size = @event.IsUndo ? @event.From : @event.To;
                    note.X = size.X;
                    note.Y = size.Y;
                    note.Width = size.Width;
                    note.Height = size.Height;
                }
            });
        }

        public void ApplyNotesDeleted(NotesDeletedEvent @event)
        {
            WithBoard(@event.BoardId, board =>
            {
                if (@event.IsUndo)
                {
                    foreach (var noteDto in @event.Notes)
                    {
                        if (!board.Notes.Any(n => n.Id == noteDto.Id))
                        {
                            board.Notes.Add(new Note
                            {
                                Id = noteDto.Id,
                                Text = noteDto.Text,
                                X = noteDto.X,
                                Y = noteDto.Y,
                                Width = noteDto.Width,
                                Height = noteDto.Height,
                                Color = noteDto.Color,
                                Type = noteDto.Type
                            });
                        }
                    }
                    foreach (var connDto in @event.Connections)
                    {
                        if (!board.Connections.Any(c => c.FromNoteId == connDto.FromNoteId && c.ToNoteId == connDto.ToNoteId))
                        {
                            board.Connections.Add(new Connection
                            {
                                FromNoteId = connDto.FromNoteId,
                                ToNoteId = connDto.ToNoteId
                            });
                        }
                    }
                }
                else
                {
                    var noteIds = @event.Notes.Select(n => n.Id).ToHashSet();
                    board.Notes.RemoveAll(n => noteIds.Contains(n.Id));
                    foreach (var connDto in @event.Connections)
                    {
                        board.Connections.RemoveAll(c => c.FromNoteId == connDto.FromNoteId && c.ToNoteId == connDto.ToNoteId);
                    }
                }
            });
        }

        public void ApplyNoteTextEdited(NoteTextEditedEvent @event)
        {
            WithBoard(@event.BoardId, board =>
            {
                var note = board.Notes.FirstOrDefault(n => n.Id == @event.NoteId);
                if (note != null)
                {
                    note.Text = @event.IsUndo ? @event.FromText : @event.ToText;
                }
            });
        }

        public void ApplyConnectionCreated(ConnectionCreatedEvent @event)
        {
            WithBoard(@event.BoardId, board =>
            {
                if (@event.IsUndo)
                {
                    board.Connections.RemoveAll(c =>
                        c.FromNoteId == @event.Connection.FromNoteId &&
                        c.ToNoteId == @event.Connection.ToNoteId);
                }
                else
                {
                    board.Connections.Add(new Connection
                    {
                        FromNoteId = @event.Connection.FromNoteId,
                        ToNoteId = @event.Connection.ToNoteId
                    });
                }
            });
        }

        public void ApplyPasted(PastedEvent @event)
        {
            WithBoard(@event.BoardId, board =>
            {
                if (@event.IsUndo)
                {
                    var noteIds = @event.Notes.Select(n => n.Id).ToHashSet();
                    board.Notes.RemoveAll(n => noteIds.Contains(n.Id));
                    foreach (var connDto in @event.Connections)
                    {
                        board.Connections.RemoveAll(c =>
                            c.FromNoteId == connDto.FromNoteId && c.ToNoteId == connDto.ToNoteId);
                    }
                }
                else
                {
                    foreach (var noteDto in @event.Notes)
                    {
                        board.Notes.Add(new Note
                        {
                            Id = noteDto.Id,
                            Text = noteDto.Text,
                            X = noteDto.X,
                            Y = noteDto.Y,
                            Width = noteDto.Width,
                            Height = noteDto.Height,
                            Color = noteDto.Color,
                            Type = noteDto.Type
                        });
                    }
                    foreach (var connDto in @event.Connections)
                    {
                        board.Connections.Add(new Connection
                        {
                            FromNoteId = connDto.FromNoteId,
                            ToNoteId = connDto.ToNoteId
                        });
                    }
                }
            });
        }

        private void WithBoard(Guid boardId, Action<Board> action)
        {
            var semaphore = _locks.GetOrAdd(boardId, _ => new SemaphoreSlim(1, 1));
            semaphore.Wait();
            try
            {
                var board = _repository.GetById(boardId);
                if (board != null)
                {
                    action(board);
                    _repository.Update(boardId, board);
                }
            }
            finally
            {
                semaphore.Release();
            }
        }
    }
}
