using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Ai.Tlbx.RealTimeAudio.OpenAi;
using NAudio.Wave;

namespace Ai.Tlbx.RealTimeAudio.Hardware.Windows
{
    public class WindowsAudioHardware : IAudioHardwareAccess
    {
        private WaveInEvent _waveIn;
        private bool _isRecording;
        private CancellationTokenSource _cancellationTokenSource;
        private readonly int _sampleRate;
        private readonly int _channelCount;
        private readonly int _bitsPerSample;
        private WaveOutEvent _waveOut;
        private BufferedWaveProvider _bufferedWaveProvider;
        private MicrophoneAudioReceivedEventHandler _audioDataReceivedHandler;
        private bool _isInitialized = false;

        public event EventHandler<string> AudioError;

        public WindowsAudioHardware(int sampleRate = 24000, int channelCount = 1, int bitsPerSample = 16)
        {
            _sampleRate = sampleRate;
            _channelCount = channelCount;
            _bitsPerSample = bitsPerSample;
            _isRecording = false;
        }

        public async Task InitAudio()
        {
            if (_isInitialized)
            {
                return;
            }

            try
            {
                Debug.WriteLine("Initializing Windows audio hardware...");

                int deviceCount = WaveInEvent.DeviceCount;
                if (deviceCount == 0)
                {
                    string error = "No audio input devices detected";
                    Debug.WriteLine(error);
                    AudioError?.Invoke(this, error);
                    return;
                }

                Debug.WriteLine($"Found {deviceCount} input devices:");
                for (int i = 0; i < deviceCount; i++)
                {
                    var capabilities = WaveInEvent.GetCapabilities(i);
                    Debug.WriteLine($"Device {i}: {capabilities.ProductName}");
                }

                _waveOut = new WaveOutEvent();
                _bufferedWaveProvider = new BufferedWaveProvider(new WaveFormat(_sampleRate, _bitsPerSample, _channelCount))
                {
                    DiscardOnBufferOverflow = true
                };
                _waveOut.Init(_bufferedWaveProvider);

                _isInitialized = true;
                Debug.WriteLine("Windows audio hardware initialized successfully");
            }
            catch (Exception ex)
            {
                string error = $"Error initializing audio: {ex.Message}";
                Debug.WriteLine($"{error}\nStackTrace: {ex.StackTrace}");
                AudioError?.Invoke(this, error);
            }
        }

        public async Task<bool> StartRecordingAudio(MicrophoneAudioReceivedEventHandler audioDataReceivedHandler)
        {
            if (_isRecording)
            {
                return true;
            }

            try
            {
                if (!_isInitialized)
                {
                    await InitAudio();
                    if (!_isInitialized)
                    {
                        return false;
                    }
                }

                Debug.WriteLine("Starting audio recording with parameters:");
                Debug.WriteLine($"  Sample rate: {_sampleRate}");
                Debug.WriteLine($"  Channel count: {_channelCount}");
                Debug.WriteLine($"  Bits per sample: {_bitsPerSample}");

                _audioDataReceivedHandler = audioDataReceivedHandler;
                _cancellationTokenSource = new CancellationTokenSource();

                _waveIn = new WaveInEvent
                {
                    DeviceNumber = 0,
                    WaveFormat = new WaveFormat(_sampleRate, _bitsPerSample, _channelCount),
                    BufferMilliseconds = 50
                };

                _waveIn.RecordingStopped += (s, e) =>
                {
                    if (e.Exception != null)
                    {
                        Debug.WriteLine($"Recording stopped with error: {e.Exception.Message}");
                        AudioError?.Invoke(this, $"Recording error: {e.Exception.Message}");
                    }
                };

                _waveIn.DataAvailable += OnDataAvailable;
                _waveIn.StartRecording();
                _isRecording = true;

                Debug.WriteLine("Recording started successfully");
                return true;
            }
            catch (Exception ex)
            {
                string error = $"Error starting recording: {ex.Message}";
                Debug.WriteLine($"{error}\nStackTrace: {ex.StackTrace}");
                AudioError?.Invoke(this, error);
                return false;
            }
        }

        public async Task<bool> StopRecordingAudio()
        {
            if (!_isRecording)
            {
                return true;
            }

            try
            {
                Debug.WriteLine("Stopping audio recording...");

                _waveIn.StopRecording();
                _waveIn.DataAvailable -= OnDataAvailable;
                _waveIn.Dispose();
                _waveIn = null;

                _cancellationTokenSource.Cancel();
                _cancellationTokenSource.Dispose();
                _cancellationTokenSource = null;

                _isRecording = false;
                _audioDataReceivedHandler = null;

                Debug.WriteLine("Recording stopped successfully");
                return true;
            }
            catch (Exception ex)
            {
                string error = $"Error stopping recording: {ex.Message}";
                Debug.WriteLine($"{error}\nStackTrace: {ex.StackTrace}");
                AudioError?.Invoke(this, error);
                return false;
            }
        }

        public bool PlayAudio(string base64EncodedPcm16Audio, int sampleRate)
        {
            try
            {
                if (string.IsNullOrEmpty(base64EncodedPcm16Audio))
                {
                    Debug.WriteLine("Warning: Attempted to play empty audio data");
                    return false;
                }

                byte[] audioData = Convert.FromBase64String(base64EncodedPcm16Audio);
                _bufferedWaveProvider.AddSamples(audioData, 0, audioData.Length);

                if (_waveOut.PlaybackState != PlaybackState.Playing)
                {
                    _waveOut.Play();
                }

                return true;
            }
            catch (Exception ex)
            {
                string error = $"Error playing audio: {ex.Message}";
                Debug.WriteLine($"{error}\nStackTrace: {ex.StackTrace}");
                AudioError?.Invoke(this, error);
                return false;
            }
        }

        public async Task ClearAudioQueue()
        {
            try
            {
                Debug.WriteLine("Clearing audio buffer...");
                _bufferedWaveProvider.ClearBuffer();
                _waveOut.Stop();
                Debug.WriteLine("Audio buffer cleared");
            }
            catch (Exception ex)
            {
                string error = $"Error clearing audio buffer: {ex.Message}";
                Debug.WriteLine($"{error}\nStackTrace: {ex.StackTrace}");
                AudioError?.Invoke(this, error);
            }
        }

        private void OnDataAvailable(object sender, WaveInEventArgs e)
        {
            try
            {
                if (e.BytesRecorded > 0 && _audioDataReceivedHandler != null)
                {
                    var buffer = new byte[e.BytesRecorded];
                    Array.Copy(e.Buffer, buffer, e.BytesRecorded);

                    string base64Audio = Convert.ToBase64String(buffer);
                    Debug.WriteLine($"Audio data recorded: {e.BytesRecorded} bytes, base64 length: {base64Audio.Length}");

                    _audioDataReceivedHandler?.Invoke(this, new MicrophoneAudioReceivedEvenArgs(base64Audio));
                }
            }
            catch (Exception ex)
            {
                string error = $"Error processing audio data: {ex.Message}";
                Debug.WriteLine($"{error}\nStackTrace: {ex.StackTrace}");
                AudioError?.Invoke(this, error);
            }
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                Debug.WriteLine("Disposing Windows audio hardware...");
                await StopRecordingAudio();

                if (_waveOut != null)
                {
                    _waveOut.Stop();
                    _waveOut.Dispose();
                    _waveOut = null;
                    Debug.WriteLine("Wave out player disposed");
                }

                _bufferedWaveProvider = null;
                _isInitialized = false;
                Debug.WriteLine("Windows audio hardware disposed");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during disposal: {ex}");
            }
        }
    }
}