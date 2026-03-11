namespace EventStormingBoard.Server.Models
{
    public sealed class BoardDto
    {
        public Guid Id { get; set; }
        public required string Name { get; set; }
        public List<NoteDto> Notes { get; set; } = new List<NoteDto>();
        public List<ConnectionDto> Connections { get; set; } = new List<ConnectionDto>();
    }
}
