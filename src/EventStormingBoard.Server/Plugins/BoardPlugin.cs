using EventStormingBoard.Server.Entities;
using EventStormingBoard.Server.Events;
using EventStormingBoard.Server.Hubs;
using EventStormingBoard.Server.Models;
using EventStormingBoard.Server.Repositories;
using EventStormingBoard.Server.Services;
using Microsoft.AspNetCore.SignalR;
using System.ComponentModel;
using System.Text;

namespace EventStormingBoard.Server.Plugins
{
    public sealed class BoardPlugin
    {
        private readonly IBoardsRepository _repository;
        private readonly IBoardEventPipeline _boardEventPipeline;
        private readonly IBoardEventLog _boardEventLog;
        private readonly IHubContext<BoardsHub> _hubContext;
        private readonly Guid _boardId;

        public BoardPlugin(
            IBoardsRepository repository,
            IBoardEventPipeline boardEventPipeline,
            IBoardEventLog boardEventLog,
            IHubContext<BoardsHub> hubContext,
            Guid boardId)
        {
            _repository = repository;
            _boardEventPipeline = boardEventPipeline;
            _boardEventLog = boardEventLog;
            _hubContext = hubContext;
            _boardId = boardId;
        }

        [Description("Gets the current board state including all notes and connections. Use this to understand what is currently on the board.")]
        public string GetBoardState()
        {
            var board = _repository.GetById(_boardId);
            if (board == null)
                return "Board not found.";

            var sb = new StringBuilder();
            sb.AppendLine($"Board: \"{board.Name}\"");
            if (board.Phase.HasValue)
                sb.AppendLine($"Phase: {board.Phase.Value}");
            if (!string.IsNullOrWhiteSpace(board.Domain))
                sb.AppendLine($"Domain: {board.Domain}");
            if (!string.IsNullOrWhiteSpace(board.SessionScope))
                sb.AppendLine($"Session Scope: {board.SessionScope}");
            sb.AppendLine($"Notes ({board.Notes.Count}):");
            foreach (var note in board.Notes)
            {
                sb.AppendLine($"  - [{note.Type}] \"{note.Text}\" (id: {note.Id}, position: {note.X:F0},{note.Y:F0})");
            }
            sb.AppendLine($"Connections ({board.Connections.Count}):");
            foreach (var conn in board.Connections)
            {
                var fromNote = board.Notes.FirstOrDefault(n => n.Id == conn.FromNoteId);
                var toNote = board.Notes.FirstOrDefault(n => n.Id == conn.ToNoteId);
                sb.AppendLine($"  - \"{fromNote?.Text}\" → \"{toNote?.Text}\"");
            }
            return sb.ToString();
        }

        [Description("Gets recent board events showing what actions have been performed on the board recently.")]
        public string GetRecentEvents([Description("Number of recent events to return, default 20")] int count = 20)
        {
            var entries = _boardEventLog.GetRecent(_boardId, count);
            if (entries.Count == 0)
                return "No recent events.";

            var sb = new StringBuilder();
            foreach (var entry in entries)
            {
                sb.AppendLine($"  [{entry.Timestamp:HH:mm:ss}] {entry.UserName ?? "System"}: {entry.Summary}");
            }
            return sb.ToString();
        }

        [Description("Sets the board Domain context used to guide the Event Storming facilitator. Pass an empty value to clear it.")]
        public string SetDomain(
            [Description("The domain context for this board")] string? domain)
        {
            var board = _repository.GetById(_boardId);
            if (board == null) return "Board not found.";

            var normalizedDomain = NormalizeOptional(domain);
            if (string.Equals(board.Domain, normalizedDomain, StringComparison.Ordinal))
                return "Domain is already set to that value.";

            var @event = new BoardContextUpdatedEvent
            {
                BoardId = _boardId,
                OldDomain = board.Domain,
                NewDomain = normalizedDomain,
                OldSessionScope = board.SessionScope,
                NewSessionScope = board.SessionScope,
                OldAgentInstructions = board.AgentInstructions,
                NewAgentInstructions = board.AgentInstructions,
                OldPhase = board.Phase,
                NewPhase = board.Phase,
                OldAutonomousEnabled = board.AutonomousEnabled,
                NewAutonomousEnabled = board.AutonomousEnabled
            };

            _boardEventPipeline.ApplyAndLog(@event, "AI Agent");
            _hubContext.Clients.Group(_boardId.ToString()).SendAsync("BoardContextUpdated", @event);

            return string.IsNullOrWhiteSpace(normalizedDomain)
                ? "Cleared board domain context."
                : $"Set board domain context to \"{normalizedDomain}\".";
        }

        [Description("Sets the board Session Scope context used to constrain the Event Storming session. Pass an empty value to clear it.")]
        public string SetSessionScope(
            [Description("The session scope for this board")] string? sessionScope)
        {
            var board = _repository.GetById(_boardId);
            if (board == null) return "Board not found.";

            var normalizedSessionScope = NormalizeOptional(sessionScope);
            if (string.Equals(board.SessionScope, normalizedSessionScope, StringComparison.Ordinal))
                return "Session scope is already set to that value.";

            var @event = new BoardContextUpdatedEvent
            {
                BoardId = _boardId,
                OldDomain = board.Domain,
                NewDomain = board.Domain,
                OldSessionScope = board.SessionScope,
                NewSessionScope = normalizedSessionScope,
                OldAgentInstructions = board.AgentInstructions,
                NewAgentInstructions = board.AgentInstructions,
                OldPhase = board.Phase,
                NewPhase = board.Phase,
                OldAutonomousEnabled = board.AutonomousEnabled,
                NewAutonomousEnabled = board.AutonomousEnabled
            };

            _boardEventPipeline.ApplyAndLog(@event, "AI Agent");
            _hubContext.Clients.Group(_boardId.ToString()).SendAsync("BoardContextUpdated", @event);

            return string.IsNullOrWhiteSpace(normalizedSessionScope)
                ? "Cleared board session scope."
                : $"Set board session scope to \"{normalizedSessionScope}\".";
        }

        [Description("Sets the current Event Storming workshop phase")]
        public string SetPhase(
            [Description("The phase to set")] EventStormingPhase phase)
        {
            var board = _repository.GetById(_boardId);
            if (board == null) return "Board not found.";

            if (board.Phase == phase)
                return $"Phase is already set to {phase}.";

            var @event = new BoardContextUpdatedEvent
            {
                BoardId = _boardId,
                OldDomain = board.Domain,
                NewDomain = board.Domain,
                OldSessionScope = board.SessionScope,
                NewSessionScope = board.SessionScope,
                OldAgentInstructions = board.AgentInstructions,
                NewAgentInstructions = board.AgentInstructions,
                OldPhase = board.Phase,
                NewPhase = phase,
                OldAutonomousEnabled = board.AutonomousEnabled,
                NewAutonomousEnabled = board.AutonomousEnabled
            };

            _boardEventPipeline.ApplyAndLog(@event, "AI Agent");
            _hubContext.Clients.Group(_boardId.ToString()).SendAsync("BoardContextUpdated", @event);

            return $"Set board phase to {phase}.";
        }

        [Description("Marks the autonomous facilitation session as complete and disables autonomous mode for this board.")]
        public string CompleteAutonomousSession(
            [Description("A short summary of why the session is complete")] string? summary = null)
        {
            var board = _repository.GetById(_boardId);
            if (board == null) return "Board not found.";

            if (!board.AutonomousEnabled)
                return "Autonomous mode is already disabled.";

            var @event = new BoardContextUpdatedEvent
            {
                BoardId = _boardId,
                OldDomain = board.Domain,
                NewDomain = board.Domain,
                OldSessionScope = board.SessionScope,
                NewSessionScope = board.SessionScope,
                OldAgentInstructions = board.AgentInstructions,
                NewAgentInstructions = board.AgentInstructions,
                OldPhase = board.Phase,
                NewPhase = board.Phase,
                OldAutonomousEnabled = board.AutonomousEnabled,
                NewAutonomousEnabled = false
            };

            _boardEventPipeline.ApplyAndLog(@event, "AI Agent");
            _hubContext.Clients.Group(_boardId.ToString()).SendAsync("BoardContextUpdated", @event);

            return string.IsNullOrWhiteSpace(summary)
                ? "Autonomous facilitation completed."
                : $"Autonomous facilitation completed. {summary.Trim()}";
        }

        [Description("Creates a new sticky note on the board. Valid note types for Event Storming are: Event (something that happened, past tense), Command (an action/intent triggered by a user or system), Aggregate (a cluster of domain objects), User (an actor/persona), Policy (a business rule or automated reaction, 'when X then Y'), ReadModel (a view/projection of data), ExternalSystem (an outside dependency), Concern (a problem, risk, or question).")]
        public string CreateNote(
            [Description("The text label for the note")] string text,
            [Description("The note type: Event, Command, Aggregate, User, Policy, ReadModel, ExternalSystem, or Concern")] NoteType type,
            [Description("X coordinate for placement on the canvas")] double x,
            [Description("Y coordinate for placement on the canvas")] double y)
        {
            var noteId = Guid.NewGuid();
            var width = type == NoteType.User ? 60.0 : 120.0;
            var height = type == NoteType.User ? 60.0 : 120.0;

            var noteDto = new NoteDto
            {
                Id = noteId,
                Text = text,
                X = x,
                Y = y,
                Width = width,
                Height = height,
                Type = type
            };

            var @event = new NoteCreatedEvent
            {
                BoardId = _boardId,
                Note = noteDto
            };

            _boardEventPipeline.ApplyAndLog(@event, "AI Agent");
            _hubContext.Clients.Group(_boardId.ToString()).SendAsync("NoteCreated", @event);

            return $"Created {type} note \"{text}\" (id: {noteId})";
        }

        [Description("Creates a connection (arrow) between two existing notes on the board. Use note IDs from GetBoardState.")]
        public string CreateConnection(
            [Description("The ID of the source note (where the arrow starts)")] Guid fromNoteId,
            [Description("The ID of the target note (where the arrow ends)")] Guid toNoteId)
        {
            var board = _repository.GetById(_boardId);
            if (board == null) return "Board not found.";

            var fromNote = board.Notes.FirstOrDefault(n => n.Id == fromNoteId);
            var toNote = board.Notes.FirstOrDefault(n => n.Id == toNoteId);
            if (fromNote == null) return $"Source note {fromNoteId} not found.";
            if (toNote == null) return $"Target note {toNoteId} not found.";

            var connDto = new ConnectionDto
            {
                FromNoteId = fromNoteId,
                ToNoteId = toNoteId
            };

            var @event = new ConnectionCreatedEvent
            {
                BoardId = _boardId,
                Connection = connDto
            };

            _boardEventPipeline.ApplyAndLog(@event, "AI Agent");
            _hubContext.Clients.Group(_boardId.ToString()).SendAsync("ConnectionCreated", @event);

            return $"Connected \"{fromNote.Text}\" → \"{toNote.Text}\"";
        }

        [Description("Edits the text of an existing note. Use note IDs from GetBoardState.")]
        public string EditNoteText(
            [Description("The ID of the note to edit")] Guid noteId,
            [Description("The new text for the note")] string newText)
        {
            var board = _repository.GetById(_boardId);
            if (board == null) return "Board not found.";

            var note = board.Notes.FirstOrDefault(n => n.Id == noteId);
            if (note == null) return $"Note {noteId} not found.";

            var oldText = note.Text;
            var @event = new NoteTextEditedEvent
            {
                BoardId = _boardId,
                NoteId = noteId,
                FromText = oldText,
                ToText = newText
            };

            _boardEventPipeline.ApplyAndLog(@event, "AI Agent");
            _hubContext.Clients.Group(_boardId.ToString()).SendAsync("NoteTextEdited", @event);

            return $"Updated note text from \"{oldText}\" to \"{newText}\"";
        }

        [Description("Moves one or more notes to new positions on the board. Use this to reorganise the board layout. Use note IDs from GetBoardState.")]
        public string MoveNotes(
            [Description("The list of note moves specifying each note's ID and target coordinates")] List<NoteMoveInput> moves)
        {
            var board = _repository.GetById(_boardId);
            if (board == null) return "Board not found.";

            if (moves.Count == 0)
                return "No moves provided.";

            var from = new List<NoteMoveDto>();
            var to = new List<NoteMoveDto>();

            foreach (var move in moves)
            {
                var note = board.Notes.FirstOrDefault(n => n.Id == move.NoteId);
                if (note == null) continue;

                from.Add(new NoteMoveDto
                {
                    NoteId = note.Id,
                    Coordinates = new CoordinatesDto { X = note.X, Y = note.Y }
                });
                to.Add(new NoteMoveDto
                {
                    NoteId = note.Id,
                    Coordinates = new CoordinatesDto { X = move.X, Y = move.Y }
                });
            }

            if (from.Count == 0)
                return "No matching notes found to move.";

            var @event = new NotesMovedEvent
            {
                BoardId = _boardId,
                From = from,
                To = to
            };

            _boardEventPipeline.ApplyAndLog(@event, "AI Agent");
            _hubContext.Clients.Group(_boardId.ToString()).SendAsync("NotesMoved", @event);

            return $"Moved {from.Count} note(s) to new positions.";
        }

        [Description("Creates multiple sticky notes on the board in a single call. Returns the generated IDs for each note so you can use them in CreateConnections. Prefer this over CreateNote when adding more than one note.")]
        public string CreateNotes(
            [Description("The list of notes to create")] List<CreateNoteInput> notes)
        {
            var board = _repository.GetById(_boardId);
            if (board == null) return "Board not found.";

            if (notes.Count == 0)
                return "No notes provided.";

            var results = new StringBuilder();
            var createdIds = new List<(Guid Id, string Text)>();

            foreach (var input in notes)
            {
                var noteId = Guid.NewGuid();
                var width = input.Type == NoteType.User ? 60.0 : 120.0;
                var height = input.Type == NoteType.User ? 60.0 : 120.0;

                var noteDto = new NoteDto
                {
                    Id = noteId,
                    Text = input.Text,
                    X = input.X,
                    Y = input.Y,
                    Width = width,
                    Height = height,
                    Type = input.Type
                };

                var @event = new NoteCreatedEvent
                {
                    BoardId = _boardId,
                    Note = noteDto
                };

                _boardEventPipeline.ApplyAndLog(@event, "AI Agent");
                _hubContext.Clients.Group(_boardId.ToString()).SendAsync("NoteCreated", @event);

                createdIds.Add((noteId, input.Text));
            }

            results.AppendLine($"Created {createdIds.Count} note(s):");
            foreach (var (id, text) in createdIds)
            {
                results.AppendLine($"  - \"{text}\" (id: {id})");
            }
            return results.ToString();
        }

        [Description("Creates multiple connections (arrows) between existing notes in a single call. Use note IDs from GetBoardState or from the IDs returned by CreateNotes. Prefer this over CreateConnection when adding more than one connection.")]
        public string CreateConnections(
            [Description("The list of connections to create")] List<CreateConnectionInput> connections)
        {
            var board = _repository.GetById(_boardId);
            if (board == null) return "Board not found.";

            if (connections.Count == 0)
                return "No connections provided.";

            var results = new StringBuilder();
            var created = 0;

            foreach (var input in connections)
            {
                var fromNote = board.Notes.FirstOrDefault(n => n.Id == input.FromNoteId);
                var toNote = board.Notes.FirstOrDefault(n => n.Id == input.ToNoteId);
                if (fromNote == null)
                {
                    results.AppendLine($"  - Skipped: source note {input.FromNoteId} not found.");
                    continue;
                }
                if (toNote == null)
                {
                    results.AppendLine($"  - Skipped: target note {input.ToNoteId} not found.");
                    continue;
                }

                var connDto = new ConnectionDto
                {
                    FromNoteId = input.FromNoteId,
                    ToNoteId = input.ToNoteId
                };

                var @event = new ConnectionCreatedEvent
                {
                    BoardId = _boardId,
                    Connection = connDto
                };

                _boardEventPipeline.ApplyAndLog(@event, "AI Agent");
                _hubContext.Clients.Group(_boardId.ToString()).SendAsync("ConnectionCreated", @event);

                results.AppendLine($"  - \"{fromNote.Text}\" → \"{toNote.Text}\"");
                created++;
            }

            return $"Created {created} connection(s):\n{results}";
        }

        [Description("Deletes one or more notes from the board and any connections involving those notes. Use note IDs from GetBoardState.")]
        public string DeleteNotes(
            [Description("The list of note IDs to delete")] List<Guid> noteIds)
        {
            var board = _repository.GetById(_boardId);
            if (board == null) return "Board not found.";

            var notesToDelete = board.Notes.Where(n => noteIds.Contains(n.Id)).ToList();
            if (notesToDelete.Count == 0)
                return "No matching notes found.";

            var noteIdSet = notesToDelete.Select(n => n.Id).ToHashSet();
            var connectionsToDelete = board.Connections
                .Where(c => noteIdSet.Contains(c.FromNoteId) || noteIdSet.Contains(c.ToNoteId))
                .ToList();

            var @event = new NotesDeletedEvent
            {
                BoardId = _boardId,
                Notes = notesToDelete.Select(n => new NoteDto
                {
                    Id = n.Id,
                    Text = n.Text,
                    X = n.X,
                    Y = n.Y,
                    Width = n.Width,
                    Height = n.Height,
                    Type = n.Type
                }).ToList(),
                Connections = connectionsToDelete.Select(c => new ConnectionDto
                {
                    FromNoteId = c.FromNoteId,
                    ToNoteId = c.ToNoteId
                }).ToList()
            };

            _boardEventPipeline.ApplyAndLog(@event, "AI Agent");
            _hubContext.Clients.Group(_boardId.ToString()).SendAsync("NotesDeleted", @event);

            return $"Deleted {notesToDelete.Count} note(s) and {connectionsToDelete.Count} connection(s)";
        }

        private static string? NormalizeOptional(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
