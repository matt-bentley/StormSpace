namespace EventStormingBoard.Server.Models
{
    public sealed class BoardDto
    {
        public Guid Id { get; set; }
        public required string Name { get; set; }
        public string? Domain { get; set; }
        public string? SessionScope { get; set; }
        public EventStormingPhase? Phase { get; set; }
        public bool AutonomousEnabled { get; set; }
        public List<NoteDto> Notes { get; set; } = new List<NoteDto>();
        public List<ConnectionDto> Connections { get; set; } = new List<ConnectionDto>();
        public List<AgentConfigurationDto> AgentConfigurations { get; set; } = new List<AgentConfigurationDto>();
    }
}
