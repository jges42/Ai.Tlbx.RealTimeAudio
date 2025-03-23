using System.Text.Json.Serialization;

namespace Ai.Tlbx.RealTimeAudio.OpenAi.Models
{
    /// <summary>
    /// Specifies the model to use for audio transcription
    /// </summary>
    public enum TranscriptionModel
    {
        /// <summary>
        /// Whisper-1 transcription model
        /// </summary>
        [JsonPropertyName("whisper-1")]
        Whisper1
    }
} 