namespace EventStormingBoard.Server.Models
{
    public sealed class BoardCreateDto
    {
        public required string Name { get; set; }
        public string? Domain { get; set; }
        public string? SessionScope { get; set; }
        public string? AgentInstructions { get; set; }
        public EventStormingPhase? Phase { get; set; }
    }
}
