using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ai.Tlbx.RealTimeAudio.OpenAi
{
    public interface IAudioHardwareAccess : IAsyncDisposable
    {
        Task InitAudio();

        Task<bool> StartRecordingAudio(MicrophoneAudioReceivedEventHandler audioDataReceivedHandler);
        
        Task<bool> PlayAudio(string base64EncodedPcm16Audio, int sampleRate);
        
        Task<bool> StopRecordingAudio();

        /// <summary>
        /// Clears any pending audio in the queue and stops the current playback
        /// Used when the user interrupts the AI's response
        /// </summary>
        Task ClearAudioQueue();
    }

    public delegate void MicrophoneAudioReceivedEventHandler(object sender, MicrophoneAudioReceivedEvenArgs e);
}
