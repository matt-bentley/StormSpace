using System.ComponentModel;

namespace EventStormingBoard.Server.Models
{
    public sealed class CreateConnectionInput
    {
        [Description("The ID of the source note (where the arrow starts)")]
        public string FromNoteId { get; set; } = string.Empty;

        [Description("The ID of the target note (where the arrow ends)")]
        public string ToNoteId { get; set; } = string.Empty;
    }
}
