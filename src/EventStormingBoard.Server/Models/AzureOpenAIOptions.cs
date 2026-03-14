using System.ComponentModel.DataAnnotations;

namespace EventStormingBoard.Server.Models
{
    public sealed class AzureOpenAIOptions
    {
        public const string SectionName = "AzureOpenAI";

        [Required]
        public string Endpoint { get; set; } = string.Empty;

        [Required]
        public string DeploymentName { get; set; } = string.Empty;

        public string? ApiKey { get; set; }

        public string? ReasoningEffort { get; set; } = "medium";
    }
}