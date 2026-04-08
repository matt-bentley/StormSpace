using System.ComponentModel;

namespace EventStormingBoard.Server.Models
{
    public sealed class CreateBoundedContextInput
    {
        [Description("The name/title of the bounded context")]
        public string Name { get; set; } = string.Empty;

        [Description("X coordinate for the top-left corner of the frame")]
        public double X { get; set; }

        [Description("Y coordinate for the top-left corner of the frame")]
        public double Y { get; set; }

        [Description("Width of the bounded context frame")]
        public double Width { get; set; }

        [Description("Height of the bounded context frame")]
        public double Height { get; set; }
    }
}
