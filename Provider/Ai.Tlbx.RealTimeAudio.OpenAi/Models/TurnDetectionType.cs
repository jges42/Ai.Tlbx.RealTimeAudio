using System.Text.Json.Serialization;

namespace Ai.Tlbx.RealTimeAudio.OpenAi.Models
{
    /// <summary>
    /// Specifies the type of turn detection to use in the conversation
    /// </summary>
    public enum TurnDetectionType
    {
        /// <summary>
        /// Server VAD - Simpler volume-based voice activity detection
        /// </summary>
        [JsonPropertyName("server_vad")]
        ServerVad,
        
        /// <summary>
        /// Semantic VAD - Advanced turn detection that considers semantic meaning in addition to audio
        /// </summary>
        [JsonPropertyName("semantic_vad")]
        SemanticVad,
        
        /// <summary>
        /// No turn detection - Client must manually trigger model responses
        /// </summary>
        None
    }
} 