namespace EventStormingBoard.Server.Events
{
    public sealed class CursorPositionUpdatedEvent : BoardEvent
    {
        public required string ConnectionId { get; set; }
        public required string UserName { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
    }
}