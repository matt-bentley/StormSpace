namespace EventStormingBoard.Server.Models
{
    public sealed class ConnectionDto
    {
        public Guid FromNoteId { get; set; }
        public Guid ToNoteId { get; set; }
    }
}
