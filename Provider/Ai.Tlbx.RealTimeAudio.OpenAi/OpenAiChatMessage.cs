namespace Ai.Tlbx.RealTimeAudio.OpenAi;

public class OpenAiChatMessage
{
    public string Content { get; }
    public string Role { get; }
    public DateTime Timestamp { get; }

    public OpenAiChatMessage(string content, string role)
    {
        Content = content;
        Role = role;
        Timestamp = DateTime.Now;
    }
    
    public OpenAiChatMessage(string content, bool isFromUser)
    {
        Content = content;
        Role = isFromUser ? "user" : "assistant";
        Timestamp = DateTime.Now;
    }
}