using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Ai.Tlbx.RealTimeAudio.OpenAi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.IO;
using System.Collections.Concurrent;

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
        private MicrophoneAudioReceivedEventHandler _audioDataReceivedHandler;
        private readonly ConcurrentQueue<string> _audioQueue;
        private bool _isPlayingAudio;
        private bool _isInitialized = false;
        private MemoryStream _currentAudioStream;
        private RawSourceWaveStream _currentWaveProvider;
        
        public event EventHandler<string> AudioError;
        
        public WindowsAudioHardware(int sampleRate = 24000, int channelCount = 1, int bitsPerSample = 16)
        {
            _sampleRate = sampleRate;
            _channelCount = channelCount;
            _bitsPerSample = bitsPerSample;
            _isRecording = false;
            _audioQueue = new ConcurrentQueue<string>();
            _isPlayingAudio = false;
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
                
                // Check available input devices and log them
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

                // Initialize audio output
                _waveOut = new WaveOutEvent();
                
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
                
                // Create new WaveInEvent with device 0 (default device)
                _waveIn = new WaveInEvent
                {
                    DeviceNumber = 0,
                    WaveFormat = new WaveFormat(_sampleRate, _bitsPerSample, _channelCount),
                    BufferMilliseconds = 50
                };
                
                // Handle potential errors
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
                
                Debug.WriteLine($"Queueing audio for playback, length: {base64EncodedPcm16Audio.Length}");
                _audioQueue.Enqueue(base64EncodedPcm16Audio);
                
                if (!_isPlayingAudio)
                {
                    PlayNextAudioFromQueue();
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
        
        private void PlayNextAudioFromQueue()
        {
            if (_audioQueue.IsEmpty)
            {
                _isPlayingAudio = false;
                return;
            }
            
            _isPlayingAudio = true;
            
            try
            {
                if (_audioQueue.TryDequeue(out string base64Audio))
                {
                    Debug.WriteLine($"Playing audio from queue, data length: {base64Audio.Length}");
                    byte[] audioData = Convert.FromBase64String(base64Audio);
                    
                    // Clean up previous resources
                    DisposeCurrentPlayback();
                    
                    // Create new stream that will stay alive during playback
                    _currentAudioStream = new MemoryStream(audioData);
                    
                    var waveFormat = new WaveFormat(_sampleRate, _bitsPerSample, _channelCount);
                    Debug.WriteLine($"Creating raw source wave stream with format: {waveFormat}");
                    
                    _currentWaveProvider = new RawSourceWaveStream(_currentAudioStream, waveFormat);
                    var sampleProvider = _currentWaveProvider.ToSampleProvider();
                    
                    // Handle playback completion to clean up resources
                    _waveOut.PlaybackStopped -= OnPlaybackStopped;
                    _waveOut.PlaybackStopped += OnPlaybackStopped;
                    
                    Debug.WriteLine("Initializing wave out player");
                    _waveOut.Stop();
                    _waveOut.Init(sampleProvider);
                    _waveOut.Play();
                    Debug.WriteLine("Audio playback started");
                }
            }
            catch (Exception ex)
            {
                _isPlayingAudio = false;
                string error = $"Error playing audio from queue: {ex.Message}";
                Debug.WriteLine($"{error}\nStackTrace: {ex.StackTrace}");
                AudioError?.Invoke(this, error);
            }
        }
        
        private void OnPlaybackStopped(object sender, StoppedEventArgs e)
        {
            try
            {
                // Check if there's more audio to play
                if (_audioQueue.Count > 0 && _isPlayingAudio)
                {
                    PlayNextAudioFromQueue();
                }
                else
                {
                    _isPlayingAudio = false;
                    DisposeCurrentPlayback();
                }
                
                if (e.Exception != null)
                {
                    Debug.WriteLine($"Playback stopped with error: {e.Exception.Message}");
                    AudioError?.Invoke(this, $"Audio playback error: {e.Exception.Message}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in playback stopped handler: {ex.Message}");
            }
        }
        
        private void DisposeCurrentPlayback()
        {
            if (_currentWaveProvider != null)
            {
                _currentWaveProvider.Dispose();
                _currentWaveProvider = null;
            }
            
            if (_currentAudioStream != null)
            {
                _currentAudioStream.Dispose();
                _currentAudioStream = null;
            }
        }
        
        public async Task ClearAudioQueue()
        {
            try
            {
                Debug.WriteLine("Clearing audio queue...");
                while (_audioQueue.TryDequeue(out _)) { }
                
                if (_waveOut != null)
                {
                    _waveOut.Stop();
                    Debug.WriteLine("Stopped current audio playback");
                }
                
                DisposeCurrentPlayback();
                _isPlayingAudio = false;
                Debug.WriteLine("Audio queue cleared");
            }
            catch (Exception ex)
            {
                string error = $"Error clearing audio queue: {ex.Message}";
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
                    
                    // Convert to base64 for the event handler
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
                    _waveOut.PlaybackStopped -= OnPlaybackStopped;
                    _waveOut.Stop();
                    _waveOut.Dispose();
                    _waveOut = null;
                    Debug.WriteLine("Wave out player disposed");
                }
                
                DisposeCurrentPlayback();
                while (_audioQueue.TryDequeue(out _)) { }
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