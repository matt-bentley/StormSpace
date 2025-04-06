using EventStormingBoard.Server.Models;

namespace EventStormingBoard.Server.Events
{
    public sealed class NoteCreatedEvent : BoardEvent
    {
        public required NoteDto Note { get; set; }
    }
}
