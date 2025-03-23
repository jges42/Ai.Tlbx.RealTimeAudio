namespace Ai.Tlbx.RealTimeAudio.OpenAi.Models
{
    /// <summary>
    /// Settings for audio transcription
    /// </summary>
    public class TranscriptionSettings
    {
        /// <summary>
        /// Model to use for transcription
        /// </summary>
        public TranscriptionModel Model { get; set; } = TranscriptionModel.Whisper1;
    }
} 