using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ai.Tlbx.RealTimeAudio.OpenAi
{
    public interface IAudioHardwareAccess : IAsyncDisposable
    {
        /// <summary>
        /// Event that fires when an audio error occurs in the hardware
        /// </summary>
        event EventHandler<string>? AudioError;

        Task InitAudio();

        Task<bool> StartRecordingAudio(MicrophoneAudioReceivedEventHandler audioDataReceivedHandler);
        
        bool PlayAudio(string base64EncodedPcm16Audio, int sampleRate);
        
        Task<bool> StopRecordingAudio();

        /// <summary>
        /// Clears any pending audio in the queue and stops the current playback
        /// Used when the user interrupts the AI's response
        /// </summary>
        Task ClearAudioQueue();
    }

    public delegate void MicrophoneAudioReceivedEventHandler(object sender, MicrophoneAudioReceivedEvenArgs e);
}
