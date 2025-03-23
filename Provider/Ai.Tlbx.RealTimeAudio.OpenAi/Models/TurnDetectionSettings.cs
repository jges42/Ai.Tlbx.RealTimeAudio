using System;

namespace Ai.Tlbx.RealTimeAudio.OpenAi.Models
{
    /// <summary>
    /// Configuration for voice activity detection and turn-taking behavior
    /// </summary>
    public class TurnDetectionSettings
    {
        /// <summary>
        /// Type of turn detection to use
        /// </summary>
        public TurnDetectionType Type { get; set; } = TurnDetectionType.SemanticVad;
        
        /// <summary>
        /// Eagerness setting for semantic VAD
        /// </summary>
        public VadEagerness Eagerness { get; set; } = VadEagerness.Medium;
        
        /// <summary>
        /// Whether to automatically generate a response when a VAD stop event occurs
        /// </summary>
        public bool CreateResponse { get; set; } = true;
        
        /// <summary>
        /// Whether to automatically interrupt any ongoing response when a VAD start event occurs
        /// </summary>
        public bool InterruptResponse { get; set; } = true;
        
        /// <summary>
        /// Activation threshold for server VAD (0.0 to 1.0), defaults to 0.5
        /// </summary>
        public float? Threshold { get; set; } = null;
        
        /// <summary>
        /// Amount of audio to include before the VAD detected speech (in milliseconds), defaults to 300ms
        /// </summary>
        public int? PrefixPaddingMs { get; set; } = null;
        
        /// <summary>
        /// Duration of silence to detect speech stop (in milliseconds), defaults to 500ms
        /// </summary>
        public int? SilenceDurationMs { get; set; } = null;
    }
} 