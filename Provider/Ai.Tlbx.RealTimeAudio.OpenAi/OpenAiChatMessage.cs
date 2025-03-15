namespace Ai.Tlbx.RealTimeAudio.OpenAi;

public class OpenAiChatMessage
{
    public string Text { get; }
    public bool IsFromUser { get; }
    public DateTime Timestamp { get; }

    public OpenAiChatMessage(string text, bool isFromUser)
    {
        Text = text;
        IsFromUser = isFromUser;
        Timestamp = DateTime.Now;
    }
}