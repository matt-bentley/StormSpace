namespace EventStormingBoard.Server.Events
{
    public sealed class NoteTextEditedEvent : BoardEvent
    {
        public Guid NoteId { get; set; }
        public string? ToText { get; set; }
        public string? FromText { get; set; }
    }
}
