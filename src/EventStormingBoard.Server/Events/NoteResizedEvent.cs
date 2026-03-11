using EventStormingBoard.Server.Models;

namespace EventStormingBoard.Server.Events
{
    public sealed class NoteResizedEvent : BoardEvent
    {
        public Guid NoteId { get; set; }
        public required NoteSizeDto From { get; set; }
        public required NoteSizeDto To { get; set; }
    }
}
