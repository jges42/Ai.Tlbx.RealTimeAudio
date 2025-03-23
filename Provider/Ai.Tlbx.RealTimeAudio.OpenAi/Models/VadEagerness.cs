using System.Text.Json.Serialization;

namespace Ai.Tlbx.RealTimeAudio.OpenAi.Models
{
    /// <summary>
    /// Specifies the eagerness with which the model will respond during turn detection
    /// </summary>
    public enum VadEagerness
    {
        /// <summary>
        /// Low eagerness - wait longer for the user to continue speaking
        /// </summary>
        [JsonPropertyName("low")]
        Low,
        
        /// <summary>
        /// Medium eagerness - balanced approach to turn taking
        /// </summary>
        [JsonPropertyName("medium")]
        Medium,
        
        /// <summary>
        /// High eagerness - respond more quickly when the user pauses
        /// </summary>
        [JsonPropertyName("high")]
        High,
        
        /// <summary>
        /// Auto eagerness - let the model decide (equivalent to Medium)
        /// </summary>
        [JsonPropertyName("auto")]
        Auto
    }
} 