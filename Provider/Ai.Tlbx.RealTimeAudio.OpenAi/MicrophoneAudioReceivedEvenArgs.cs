namespace Ai.Tlbx.RealTimeAudio.OpenAi
{
    public class MicrophoneAudioReceivedEvenArgs : EventArgs
    {
        public string Base64EncodedPcm16Audio { get; set; }

        public MicrophoneAudioReceivedEvenArgs(string base64EncodedPcm16Audio)
        {
            Base64EncodedPcm16Audio = base64EncodedPcm16Audio;

        }
    }
}
