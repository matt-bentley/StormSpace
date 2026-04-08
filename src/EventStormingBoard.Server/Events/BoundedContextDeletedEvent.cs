using EventStormingBoard.Server.Models;

namespace EventStormingBoard.Server.Events
{
    public sealed class BoundedContextDeletedEvent : BoardEvent
    {
        public required BoundedContextDto BoundedContext { get; set; }
    }
}
