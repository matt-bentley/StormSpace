namespace EventStormingBoard.Server.Events
{
    public abstract class BoardEvent
    {
        public Guid BoardId { get; set; }
        public bool IsUndo { get; set; }
    }
}
