using System.Text.Json.Serialization;

namespace Ai.Tlbx.RealTimeAudio.OpenAi.Tools.Models
{
    /// <summary>
    /// For direct mapping to the OpenAI API JSON format
    /// </summary>
    public class ToolFunctionDefinition
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("parameters")]
        public object Parameters { get; set; } = new { type = "object", properties = new { } };
    }
} 