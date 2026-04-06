namespace EventStormingBoard.Server.Models
{
    public sealed class AgentConfigurationDto
    {
        public Guid Id { get; set; }
        public required string Name { get; set; }
        public bool IsFacilitator { get; set; }
        public required string SystemPrompt { get; set; }
        public string Icon { get; set; } = "smart_toy";
        public string Color { get; set; } = "#607d8b";
        public List<EventStormingPhase>? ActivePhases { get; set; }
        public List<string> AllowedTools { get; set; } = new();
        public List<string>? CanAskAgents { get; set; }
        public int Order { get; set; }
        public string ModelType { get; set; } = "gpt-4.1";
        public float? Temperature { get; set; }
        public string? ReasoningEffort { get; set; }
    }

    public sealed class AgentConfigurationCreateDto
    {
        public required string Name { get; set; }
        public required string SystemPrompt { get; set; }
        public string Icon { get; set; } = "smart_toy";
        public string Color { get; set; } = "#607d8b";
        public List<EventStormingPhase>? ActivePhases { get; set; }
        public List<string> AllowedTools { get; set; } = new();
        public List<string>? CanAskAgents { get; set; }
        public int Order { get; set; }
        public string ModelType { get; set; } = "gpt-4.1";
        public float? Temperature { get; set; }
        public string? ReasoningEffort { get; set; }
    }

    public sealed class AgentConfigurationUpdateDto
    {
        public required string Name { get; set; }
        public required string SystemPrompt { get; set; }
        public string Icon { get; set; } = "smart_toy";
        public string Color { get; set; } = "#607d8b";
        public List<EventStormingPhase>? ActivePhases { get; set; }
        public List<string> AllowedTools { get; set; } = new();
        public List<string>? CanAskAgents { get; set; }
        public int Order { get; set; }
        public string ModelType { get; set; } = "gpt-4.1";
        public float? Temperature { get; set; }
        public string? ReasoningEffort { get; set; }
    }

    public sealed class ToolDefinitionDto
    {
        public required string Name { get; set; }
        public string? Description { get; set; }
    }
}
