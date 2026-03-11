namespace EventStormingBoard.Server.Events
{
    public sealed class BoardNameUpdatedEvent : BoardEvent
    {
        public required string NewName { get; set; }
        public required string OldName { get; set; }
    }
}
