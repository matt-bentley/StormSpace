using System.Text.Json.Serialization;

namespace EventStormingBoard.Server.Models
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum EventStormingPhase
    {
        SetContext,
        IdentifyEvents,
        AddCommandsAndPolicies,
        DefineAggregates,
        BreakItDown
    }
}
