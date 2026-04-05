namespace EventStormingBoard.Server.Models
{
    public sealed class AgentChatMessageDto
    {
        public required string Role { get; set; }
        public string? UserName { get; set; }
        public string? AgentName { get; set; }
        public string? Content { get; set; }
        public string? Prompt { get; set; }
        public List<AgentToolCallDto>? ToolCalls { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public sealed class AgentToolCallDto
    {
        public required string Name { get; set; }
        public required string Arguments { get; set; }
    }
}
