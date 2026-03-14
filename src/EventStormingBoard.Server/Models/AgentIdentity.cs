namespace EventStormingBoard.Server.Models
{
    public sealed class AgentIdentity
    {
        public string Name { get; }
        public string Icon { get; }
        public string Color { get; }

        private AgentIdentity(string name, string icon, string color)
        {
            Name = name;
            Icon = icon;
            Color = color;
        }

        public static AgentIdentity ForType(AgentType type) => type switch
        {
            AgentType.Facilitator => new AgentIdentity("Facilitator", "school", "#3f51b5"),
            AgentType.BoardBuilder => new AgentIdentity("Board Builder", "construction", "#009688"),
            AgentType.BoardReviewer => new AgentIdentity("Board Reviewer", "checklist", "#ff8f00"),
            _ => new AgentIdentity("Facilitator", "school", "#3f51b5")
        };
    }
}
