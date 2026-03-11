using EventStormingBoard.Server.Models;

namespace EventStormingBoard.Server.Events
{
    public sealed class ConnectionCreatedEvent : BoardEvent
    {
        public required ConnectionDto Connection { get; set; }
    }
}
