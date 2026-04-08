using EventStormingBoard.Server.Models;

namespace EventStormingBoard.Server.Events
{
    public sealed class BoundedContextCreatedEvent : BoardEvent
    {
        public required BoundedContextDto BoundedContext { get; set; }
    }
}
