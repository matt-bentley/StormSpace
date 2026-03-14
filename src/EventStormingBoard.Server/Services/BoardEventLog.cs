using EventStormingBoard.Server.Events;
using System.Collections.Concurrent;

namespace EventStormingBoard.Server.Services
{
    public sealed class BoardEventEntry
    {
        public required DateTimeOffset Timestamp { get; set; }
        public required string EventType { get; set; }
        public required string Summary { get; set; }
        public string? UserName { get; set; }
    }

    public interface IBoardEventLog
    {
        void Append(Guid boardId, BoardEvent @event, string? userName);
        List<BoardEventEntry> GetRecent(Guid boardId, int count = 50);
    }

    public sealed class BoardEventLog : IBoardEventLog
    {
        private const int MaxEntries = 50;
        private readonly ConcurrentDictionary<Guid, LinkedList<BoardEventEntry>> _logs = new();

        public void Append(Guid boardId, BoardEvent @event, string? userName)
        {
            var log = _logs.GetOrAdd(boardId, _ => new LinkedList<BoardEventEntry>());
            var entry = new BoardEventEntry
            {
                Timestamp = DateTimeOffset.UtcNow,
                EventType = @event.GetType().Name,
                Summary = BuildSummary(@event),
                UserName = userName
            };

            lock (log)
            {
                log.AddLast(entry);
                while (log.Count > MaxEntries)
                {
                    log.RemoveFirst();
                }
            }
        }

        public List<BoardEventEntry> GetRecent(Guid boardId, int count = 50)
        {
            if (!_logs.TryGetValue(boardId, out var log))
            {
                return [];
            }

            lock (log)
            {
                return log.TakeLast(Math.Min(count, MaxEntries)).ToList();
            }
        }

        private static string BuildSummary(BoardEvent @event)
        {
            return @event switch
            {
                BoardContextUpdatedEvent e => BuildBoardContextSummary(e),
                NoteCreatedEvent e => $"Created {e.Note.Type} note \"{e.Note.Text}\"",
                NotesMovedEvent e => $"Moved {e.To.Count} note(s)",
                NoteResizedEvent e => $"Resized note {e.NoteId}",
                NotesDeletedEvent e => $"Deleted {e.Notes.Count} note(s) and {e.Connections.Count} connection(s)",
                NoteTextEditedEvent e => $"Edited note text to \"{e.ToText}\"",
                ConnectionCreatedEvent e => $"Connected {e.Connection.FromNoteId} → {e.Connection.ToNoteId}",
                PastedEvent e => $"Pasted {e.Notes.Count} note(s) and {e.Connections.Count} connection(s)",
                BoardNameUpdatedEvent e => $"Renamed board to \"{e.NewName}\"",
                _ => @event.GetType().Name
            };
        }

        private static string BuildBoardContextSummary(BoardContextUpdatedEvent @event)
        {
            if (@event.OldPhase != @event.NewPhase)
            {
                var oldPhase = @event.OldPhase?.ToString() ?? "none";
                var newPhase = @event.NewPhase?.ToString() ?? "none";
                return $"Changed phase from {oldPhase} to {newPhase}";
            }

            if (!string.Equals(@event.OldDomain, @event.NewDomain, StringComparison.Ordinal))
            {
                return string.IsNullOrWhiteSpace(@event.NewDomain)
                    ? "Cleared domain context"
                    : $"Updated domain context to \"{@event.NewDomain}\"";
            }

            if (!string.Equals(@event.OldSessionScope, @event.NewSessionScope, StringComparison.Ordinal))
            {
                return string.IsNullOrWhiteSpace(@event.NewSessionScope)
                    ? "Cleared session scope"
                    : $"Updated session scope to \"{@event.NewSessionScope}\"";
            }

            if (!string.Equals(@event.OldAgentInstructions, @event.NewAgentInstructions, StringComparison.Ordinal))
            {
                return string.IsNullOrWhiteSpace(@event.NewAgentInstructions)
                    ? "Cleared facilitator instructions"
                    : "Updated facilitator instructions";
            }

            if (@event.OldAutonomousEnabled != @event.NewAutonomousEnabled)
            {
                return @event.NewAutonomousEnabled
                    ? "Enabled autonomous facilitation"
                    : "Disabled autonomous facilitation";
            }

            return nameof(BoardContextUpdatedEvent);
        }
    }
}
