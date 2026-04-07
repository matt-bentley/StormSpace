using System.ComponentModel;

namespace EventStormingBoard.Server.Models
{
    public sealed class EditNoteTextInput
    {
        [Description("The ID of the note to edit")]
        public string NoteId { get; set; } = string.Empty;

        [Description("The new text for the note")]
        public string NewText { get; set; } = string.Empty;
    }
}
