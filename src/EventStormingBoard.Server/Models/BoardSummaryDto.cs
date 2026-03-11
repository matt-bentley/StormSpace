namespace EventStormingBoard.Server.Models
{
    public sealed class BoardSummaryDto
    {
        public Guid Id { get; set; }
        public required string Name { get; set; }
    }
}
