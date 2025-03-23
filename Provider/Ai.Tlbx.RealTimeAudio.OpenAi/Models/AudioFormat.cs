using System.Text.Json.Serialization;

namespace Ai.Tlbx.RealTimeAudio.OpenAi.Models
{
    /// <summary>
    /// Specifies the audio format for input/output audio in the API
    /// </summary>
    public enum AudioFormat
    {
        /// <summary>
        /// PCM 16-bit audio format
        /// </summary>
        [JsonPropertyName("pcm16")]
        Pcm16
    }
} 