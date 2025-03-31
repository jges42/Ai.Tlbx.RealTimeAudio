using System.Text.Json.Serialization;

namespace Ai.Tlbx.RealTimeAudio.OpenAi.Tools.Models
{
    /// <summary>
    /// For direct mapping to the OpenAI API JSON format
    /// </summary>
    public class ToolDefinition
    {
        [JsonPropertyName("Name")]
        public string Name { get; set; } = "function";

        [JsonPropertyName("type")]
        public string Type { get; set; } = "function";

        [JsonPropertyName("function")]
        public ToolFunctionDefinition Function { get; set; } = new ToolFunctionDefinition();
    }
} 