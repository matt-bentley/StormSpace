namespace EventStormingBoard.Server.Models
{
    public sealed class BoardUpdateDto
    {
        public required string Name { get; set; }
        public List<NoteDto> Notes { get; set; } = new List<NoteDto>();
        public List<ConnectionDto> Connections { get; set; } = new List<ConnectionDto>();
    }
}
