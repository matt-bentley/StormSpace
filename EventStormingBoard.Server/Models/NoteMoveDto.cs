namespace EventStormingBoard.Server.Models
{
    public sealed class NoteMoveDto
    {
        public Guid NoteId { get; set; }
        public required CoordinatesDto Coordinates { get; set; }
    }
}
