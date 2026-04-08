namespace EventStormingBoard.Server.Events
{
    public sealed class BoundedContextUpdatedEvent : BoardEvent
    {
        public Guid BoundedContextId { get; set; }
        public string? OldName { get; set; }
        public string? NewName { get; set; }
        public double? OldX { get; set; }
        public double? NewX { get; set; }
        public double? OldY { get; set; }
        public double? NewY { get; set; }
        public double? OldWidth { get; set; }
        public double? NewWidth { get; set; }
        public double? OldHeight { get; set; }
        public double? NewHeight { get; set; }
    }
}
