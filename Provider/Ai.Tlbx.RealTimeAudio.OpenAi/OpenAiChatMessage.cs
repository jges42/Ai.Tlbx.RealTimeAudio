namespace Ai.Tlbx.RealTimeAudio.OpenAi;

public class OpenAiChatMessage
{
    // Constants for Roles
    public const string UserRole = "user";
    public const string AssistantRole = "assistant";
    public const string ToolCallRole = "tool_call";
    public const string ToolResultRole = "tool_result";

    // Core Properties
    public string Content { get; private set; } // Allow setting content in factories
    public string Role { get; private set; }
    public DateTime Timestamp { get; }

    // Tool-related Properties (nullable)
    public string? ToolCallId { get; private set; }
    public string? ToolName { get; private set; }
    public string? ToolArgumentsJson { get; private set; }
    public string? ToolResultJson { get; private set; } // Only for ToolResultRole

    // Private constructor to control instantiation via factory methods
    private OpenAiChatMessage(string role)
    {
        Role = role;
        Timestamp = DateTime.Now;
        Content = string.Empty; // Initialize Content
    }

    // Constructor for User/Assistant messages (public)
    public OpenAiChatMessage(string content, string role)
    {
        if (role != UserRole && role != AssistantRole)
        {
            throw new ArgumentException($"Invalid role '{role}' for standard message constructor. Use factory methods for tool messages.", nameof(role));
        }
        Content = content;
        Role = role;
        Timestamp = DateTime.Now;
    }
    
    // Static factory method for creating Tool Call messages
    public static OpenAiChatMessage CreateToolCallMessage(string toolCallId, string toolName, string argumentsJson)
    {
        var message = new OpenAiChatMessage(ToolCallRole) // Use private constructor
        {
            ToolCallId = toolCallId,
            ToolName = toolName,
            ToolArgumentsJson = argumentsJson,
            Content = $"AI requested tool: {toolName}" // Default content
        };
        return message;
    }
    
    // Static factory method for creating Tool Result messages
    public static OpenAiChatMessage CreateToolResultMessage(string toolCallId, string toolName, string resultJson)
    {
        var message = new OpenAiChatMessage(ToolResultRole) // Use private constructor
        {
            ToolCallId = toolCallId,
            ToolName = toolName,
            ToolResultJson = resultJson,
            Content = $"Tool result for: {toolName}" // Default content
        };
        return message;
    }
}