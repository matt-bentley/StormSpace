using System.Text.Json.Serialization;

namespace EventStormingBoard.Server.Agents
{
    public enum AgentRole
    {
        LeadFacilitator,
        EventExplorer,
        TriggerMapper,
        DomainDesigner,
        WallScribe,
        Challenger
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum SpecialistAgent
    {
        EventExplorer,
        TriggerMapper,
        DomainDesigner
    }
}
