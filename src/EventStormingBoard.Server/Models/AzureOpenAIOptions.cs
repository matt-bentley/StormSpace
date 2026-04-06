using System.ComponentModel.DataAnnotations;

namespace EventStormingBoard.Server.Models
{
    public sealed class AzureOpenAIOptions
    {
        public const string SectionName = "AzureOpenAI";

        [Required]
        public string Endpoint { get; set; } = string.Empty;

        [Required]
        public string Gpt41DeploymentName { get; set; } = string.Empty;

        [Required]
        public string Gpt52DeploymentName { get; set; } = string.Empty;

        public string? ApiKey { get; set; }
    }
}