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
            try
            {
                // Initialize audio output
                _waveOut = new WaveOutEvent();
                _waveOut.PlaybackStopped += (s, e) => 
                {
                    if (_audioQueue.Count > 0 && !_isPlayingAudio)
                    {
                        PlayNextAudioFromQueue();
                    }
                };
                
                // Test microphone
                bool micWorks = await TestMicrophoneAsync();
                if (!micWorks)
                {
                    AudioError?.Invoke(this, "Microphone test failed. No microphone detected or access denied.");
                }
            }
            catch (Exception ex)
            {
                AudioError?.Invoke(this, $"Error initializing audio: {ex.Message}");
                Debug.WriteLine($"Error initializing audio: {ex}");
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
                _audioDataReceivedHandler = audioDataReceivedHandler;
                _cancellationTokenSource = new CancellationTokenSource();
                _waveIn = new WaveInEvent
                {
                    DeviceNumber = 0,
                    WaveFormat = new WaveFormat(_sampleRate, _bitsPerSample, _channelCount),
                    BufferMilliseconds = 50
                };
                
                _waveIn.DataAvailable += OnDataAvailable;
                _waveIn.StartRecording();
                _isRecording = true;
                
                Debug.WriteLine("Recording started");
                return true;
            }
            catch (Exception ex)
            {
                AudioError?.Invoke(this, $"Error starting recording: {ex.Message}");
                Debug.WriteLine($"Error starting recording: {ex}");
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
                _waveIn.StopRecording();
                _waveIn.DataAvailable -= OnDataAvailable;
                _waveIn.Dispose();
                _waveIn = null;
                
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource.Dispose();
                _cancellationTokenSource = null;
                
                _isRecording = false;
                _audioDataReceivedHandler = null;
                
                Debug.WriteLine("Recording stopped");
                return true;
            }
            catch (Exception ex)
            {
                AudioError?.Invoke(this, $"Error stopping recording: {ex.Message}");
                Debug.WriteLine($"Error stopping recording: {ex}");
                return false;
            }
        }
        
        public bool PlayAudio(string base64EncodedPcm16Audio, int sampleRate)
        {
            try
            {
                _audioQueue.Enqueue(base64EncodedPcm16Audio);
                
                if (!_isPlayingAudio)
                {
                    PlayNextAudioFromQueue();
                }
                
                return true;
            }
            catch (Exception ex)
            {
                AudioError?.Invoke(this, $"Error playing audio: {ex.Message}");
                Debug.WriteLine($"Error playing audio: {ex}");
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
                    byte[] audioData = Convert.FromBase64String(base64Audio);
                    
                    using (var memoryStream = new MemoryStream(audioData))
                    {
                        var waveFormat = new WaveFormat(_sampleRate, _bitsPerSample, _channelCount);
                        var waveProvider = new RawSourceWaveStream(memoryStream, waveFormat);
                        var sampleProvider = waveProvider.ToSampleProvider();
                        
                        _waveOut.Stop();
                        _waveOut.Init(sampleProvider);
                        _waveOut.Play();
                    }
                }
            }
            catch (Exception ex)
            {
                _isPlayingAudio = false;
                AudioError?.Invoke(this, $"Error playing audio from queue: {ex.Message}");
                Debug.WriteLine($"Error playing audio from queue: {ex}");
            }
        }
        
        public async Task ClearAudioQueue()
        {
            try
            {
                while (_audioQueue.TryDequeue(out _)) { }
                
                if (_waveOut != null && _isPlayingAudio)
                {
                    _waveOut.Stop();
                }
                
                _isPlayingAudio = false;
            }
            catch (Exception ex)
            {
                AudioError?.Invoke(this, $"Error clearing audio queue: {ex.Message}");
                Debug.WriteLine($"Error clearing audio queue: {ex}");
            }
        }
        
        private void OnDataAvailable(object sender, WaveInEventArgs e)
        {
            if (e.BytesRecorded > 0 && _audioDataReceivedHandler != null)
            {
                try
                {
                    var buffer = new byte[e.BytesRecorded];
                    Array.Copy(e.Buffer, buffer, e.BytesRecorded);
                    
                    // Convert to base64 for the event handler
                    string base64Audio = Convert.ToBase64String(buffer);
                    
                    _audioDataReceivedHandler?.Invoke(this, new MicrophoneAudioReceivedEvenArgs(base64Audio));
                }
                catch (Exception ex)
                {
                    AudioError?.Invoke(this, $"Error processing audio data: {ex.Message}");
                    Debug.WriteLine($"Error processing audio data: {ex}");
                }
            }
        }
        
        private Task<bool> TestMicrophoneAsync()
        {
            return Task.Run(() =>
            {
                try
                {
                    using (var testWaveIn = new WaveInEvent())
                    {
                        testWaveIn.DeviceNumber = 0;
                        testWaveIn.WaveFormat = new WaveFormat(_sampleRate, _bitsPerSample, _channelCount);
                        
                        bool deviceWorks = false;
                        var resetEvent = new ManualResetEvent(false);
                        
                        testWaveIn.DataAvailable += (s, e) =>
                        {
                            if (e.BytesRecorded > 0)
                            {
                                deviceWorks = true;
                                resetEvent.Set();
                            }
                        };
                        
                        testWaveIn.StartRecording();
                        
                        resetEvent.WaitOne(TimeSpan.FromSeconds(2));
                        testWaveIn.StopRecording();
                        
                        return deviceWorks;
                    }
                }
                catch (Exception ex)
                {
                    AudioError?.Invoke(this, $"Error testing microphone: {ex.Message}");
                    Debug.WriteLine($"Error testing microphone: {ex.Message}");
                    return false;
                }
            });
        }
        
        public async ValueTask DisposeAsync()
        {
            try
            {
                await StopRecordingAudio();
                
                if (_waveOut != null)
                {
                    _waveOut.Stop();
                    _waveOut.Dispose();
                    _waveOut = null;
                }
                
                while (_audioQueue.TryDequeue(out _)) { }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during disposal: {ex}");
            }
        }
    }
} 