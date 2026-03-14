using System.Text.Json.Serialization;

namespace EventStormingBoard.Server.Models
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum AgentType
    {
        Facilitator,
        BoardBuilder,
        BoardReviewer
    }
}
