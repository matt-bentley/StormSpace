using System.Text.Json.Serialization;

namespace EventStormingBoard.Server.Models
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum NoteType
    {
        Event,
        Command,
        Aggregate,
        User,
        Policy,
        ReadModel,
        ExternalSystem,
        Concern
    }
}