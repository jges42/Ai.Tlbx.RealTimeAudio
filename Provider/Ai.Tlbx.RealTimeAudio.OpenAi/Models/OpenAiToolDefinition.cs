using System.Text.Json.Serialization;

namespace Ai.Tlbx.RealTimeAudio.OpenAi.Models
{
    /// <summary>
    /// Represents the definition of a tool that the AI model can call.
    /// </summary>
    public class OpenAiToolDefinition
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "function"; // Currently only "function" is supported

        [JsonPropertyName("function")]
        public OpenAiFunctionDefinition Function { get; set; } = new();
    }

    /// <summary>
    /// Defines the structure of a function tool.
    /// </summary>
    public class OpenAiFunctionDefinition
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("parameters")]
        public OpenAiFunctionParameters Parameters { get; set; } = new();
    }

    /// <summary>
    /// Defines the parameters for a function tool using JSON Schema.
    /// </summary>
    public class OpenAiFunctionParameters
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "object";

        [JsonPropertyName("properties")]
        public Dictionary<string, OpenAiParameterProperty> Properties { get; set; } = new();

        [JsonPropertyName("required")]
        public List<string> Required { get; set; } = new();
    }

    /// <summary>
    /// Describes a single parameter property.
    /// </summary>
    public class OpenAiParameterProperty
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty; // e.g., "string", "number", "integer", "boolean", "array", "object"

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("enum")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? Enum { get; set; } // Optional: for restricted string values

        // Add other JSON schema properties as needed (e.g., format, items for arrays, etc.)
    }
} 