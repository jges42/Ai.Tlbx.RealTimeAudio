using System.Text.Json.Serialization;

namespace Ai.Tlbx.RealTimeAudio.OpenAi.Models
{
    /// <summary>
    /// Specifies the voice to use for the assistant's speech
    /// </summary>
    public enum AssistantVoice
    {
        /// <summary>
        /// Alloy voice - Neutral, balanced voice
        /// </summary>
        [JsonPropertyName("alloy")]
        Alloy,
        
        /// <summary>
        /// Ash voice - Warm, clear voice
        /// </summary>
        [JsonPropertyName("ash")]
        Ash,
        
        /// <summary>
        /// Ballad voice - Expressive, emotional voice
        /// </summary>
        [JsonPropertyName("ballad")]
        Ballad, 
        
        /// <summary>
        /// Coral voice - Soft, gentle voice
        /// </summary>
        [JsonPropertyName("coral")]
        Coral,
        
        /// <summary>
        /// Echo voice - Deeper, resonant voice
        /// </summary>
        [JsonPropertyName("echo")]
        Echo,
        
        /// <summary>
        /// Fable voice - Narrative, storytelling voice
        /// </summary>
        [JsonPropertyName("fable")]
        Fable,
        
        /// <summary>
        /// Nova voice - Energetic, lively voice
        /// </summary>
        [JsonPropertyName("nova")]
        Nova,
        
        /// <summary>
        /// Onyx voice - Deep, authoritative voice
        /// </summary>
        [JsonPropertyName("onyx")]
        Onyx,
        
        /// <summary>
        /// Sage voice - Thoughtful, measured voice
        /// </summary>
        [JsonPropertyName("sage")]
        Sage,
        
        /// <summary>
        /// Shimmer voice - Bright, energetic voice
        /// </summary>
        [JsonPropertyName("shimmer")]
        Shimmer,
        
        /// <summary>
        /// Verse voice - Clear, versatile voice
        /// </summary>
        [JsonPropertyName("verse")]
        Verse
    }
} 