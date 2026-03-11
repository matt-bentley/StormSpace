using System.ComponentModel;

namespace EventStormingBoard.Server.Models
{
    public sealed class CreateConnectionInput
    {
        [Description("The ID of the source note (where the arrow starts)")]
        public Guid FromNoteId { get; set; }

        [Description("The ID of the target note (where the arrow ends)")]
        public Guid ToNoteId { get; set; }
    }
}
