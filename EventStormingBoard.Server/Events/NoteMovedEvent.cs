using EventStormingBoard.Server.Models;

namespace EventStormingBoard.Server.Events
{
    public sealed class NotesMovedEvent : BoardEvent
    {
        public List<NoteMoveDto> From { get; set; } = new List<NoteMoveDto>();
        public List<NoteMoveDto> To { get; set; } = new List<NoteMoveDto>();
    }
}
