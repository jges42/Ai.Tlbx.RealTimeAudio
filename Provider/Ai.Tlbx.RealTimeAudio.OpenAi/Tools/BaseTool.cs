using Ai.Tlbx.RealTimeAudio.OpenAi.Models;
using Ai.Tlbx.RealTimeAudio.OpenAi.Tools.Models;
using System.Text.Json;
using System.Threading.Tasks;

namespace Ai.Tlbx.RealTimeAudio.OpenAi.Tools
{
    /// <summary>
    /// Abstract base class for defining tools that the AI can use.
    /// Provides structure for defining the tool to OpenAI and executing it.
    /// </summary>
    public abstract class BaseTool
    {
        /// <summary>
        /// Gets the name of the function tool. Must match the name provided to OpenAI.
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// Gets a description of what the tool does. Used by OpenAI to understand the tool's purpose.
        /// </summary>
        public abstract string Description { get; }

        /// <summary>
        /// Gets the definition of the parameters the tool accepts, using JSON Schema format.
        /// Defaults to an empty object schema (no parameters). Override if parameters are needed.
        /// </summary>
        public virtual OpenAiFunctionParameters Parameters { get; } = new OpenAiFunctionParameters
        {
            Type = "object",
            Properties = new Dictionary<string, OpenAiParameterProperty>(),
            Required = new List<string>()
        };

        /// <summary>
        /// Generates the full tool definition required by the OpenAI API.
        /// </summary>
        public OpenAiToolDefinition GetToolDefinition()
        {
            return new OpenAiToolDefinition
            {
                Type = "function", // Only function type is currently supported
                Function = new OpenAiFunctionDefinition
                {
                    Name = Name,
                    Description = Description,
                    Parameters = Parameters
                }
            };
        }

        /// <summary>
        /// Gets a tool definition directly mapped to JSON format expected by OpenAI
        /// </summary>
        public ToolDefinition GetDirectToolDefinition()
        {
            // This creates an object that will be serialized in the exact format OpenAI expects
            var toolDef = new ToolDefinition
            {
                Type = "function",
                Name = Name,

                Function = new ToolFunctionDefinition
                {
                    Name = Name,
                    Description = Description,
                    // Always use a simple anonymous object with lowercase property names
                    Parameters = new
                    {
                        type = "object",
                        properties = new { },
                        // Add required empty array if your Parameters object has any requirements
                        required = new string[] { }
                    }
                }
            };
            return toolDef;
        }

        /// <summary>
        /// Executes the tool's logic with the provided arguments.
        /// </summary>
        /// <param name="argumentsJson">A JSON string containing the arguments provided by the AI.</param>
        /// <returns>A string result to be sent back to the AI. Can be simple text or JSON.</returns>
        public abstract Task<string> ExecuteAsync(string argumentsJson);

        /// <summary>
        /// Helper method to deserialize JSON arguments into a specific type.
        /// </summary>
        /// <typeparam name="T">The type to deserialize the arguments into.</typeparam>
        /// <param name="argumentsJson">The JSON string arguments from the AI.</param>
        /// <param name="options">Optional JsonSerializerOptions.</param>
        /// <returns>An instance of T populated with the arguments, or null if deserialization fails.</returns>
        protected T? DeserializeArguments<T>(string argumentsJson, JsonSerializerOptions? options = null) where T : class
        {
            try
            {
                // Use default camelCase options if none provided
                var effectiveOptions = options ?? new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                return JsonSerializer.Deserialize<T>(argumentsJson, effectiveOptions);
            }
            catch (JsonException ex)
            {
                // Log the error appropriately
                System.Diagnostics.Debug.WriteLine($"[BaseTool] Error deserializing arguments for tool '{Name}': {ex.Message}. JSON: {argumentsJson}");
                return null;
            }
        }
    }
} 