namespace EventStormingBoard.Server.Entities
{
    public class Connection
    {
        public Guid FromNoteId { get; set; }
        public Guid ToNoteId { get; set; }
    }
}
