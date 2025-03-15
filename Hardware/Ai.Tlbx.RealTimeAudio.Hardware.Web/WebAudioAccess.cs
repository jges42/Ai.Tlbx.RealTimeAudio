// WebAudioAccess.cs
using Ai.Tlbx.RealTimeAudio.OpenAi;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Ai.Tlbx.RealTimeAudio.Hardware.Web
{
    public class WebAudioAccess : IAudioHardwareAccess, IAsyncDisposable
    {
        private readonly IJSRuntime _jsRuntime;
        private IJSObjectReference? _audioModule;
        private readonly Queue<string> _audioQueue = new Queue<string>();
        private readonly object _audioLock = new object();
        private bool _isPlaying = false;
        private bool _isRecording = false;
        
        // Add these fields for recording        
        private MicrophoneAudioReceivedEventHandler? _audioDataReceivedHandler;
        
        // Store a reference to the DotNetObjectReference to prevent it from being garbage collected
        private DotNetObjectReference<WebAudioAccess>? _dotNetReference;

        public WebAudioAccess(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }

        public async Task InitAudio()
        {
            if (_audioModule == null)
            {
                _audioModule = await _jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/webAudioAccess.js");
                
                // Make sure audio permissions are properly requested and the AudioContext is activated
                var permissionResult = await _audioModule.InvokeAsync<bool>("initAudioWithUserInteraction");
                if (!permissionResult)
                {
                    throw new InvalidOperationException("Failed to initialize audio system. Microphone permission might be denied.");
                }
            }
        }

        public async Task<bool> PlayAudio(string base64EncodedPcm16Audio)
        {
            // Default to 24000 Hz sample rate for OpenAI Realtime API
            return await PlayAudio(base64EncodedPcm16Audio, 24000);
        }

        public async Task<bool> PlayAudio(string base64EncodedPcm16Audio, int sampleRate = 24000)
        {   
            if (_audioModule == null)
            {                
                return false;
            }

            if (string.IsNullOrEmpty(base64EncodedPcm16Audio))
            {                
                return false;
            }

            bool shouldPlayDirectly = false;

            lock (_audioLock)
            {
                if (!_isPlaying)
                {
                    _isPlaying = true;
                    shouldPlayDirectly = true;                    
                }
                else
                {
                    // Store both the audio data and sample rate
                    _audioQueue.Enqueue($"{base64EncodedPcm16Audio}|{sampleRate}");                    
                }
            }

            try
            {
                if (shouldPlayDirectly)
                {
                    await PlayAudioChunk(base64EncodedPcm16Audio, sampleRate);
                    await ProcessAudioQueue();
                }
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WebAudioAccess] Error playing audio: {ex.Message}");
                Console.WriteLine($"[WebAudioAccess] Stack trace: {ex.StackTrace}");
                return false;
            }
            finally
            {
                if (shouldPlayDirectly)
                {
                    lock (_audioLock)
                    {
                        _isPlaying = false;
                        Console.WriteLine("[WebAudioAccess] Playback finished, isPlaying set to false");
                    }
                }
            }
        }

        private async Task PlayAudioChunk(string base64EncodedPcm16Audio, int sampleRate = 24000)
        {
            if( _audioModule == null) return;

            try
            {                 
                await _audioModule.InvokeVoidAsync("playAudio", base64EncodedPcm16Audio, sampleRate);
                
            }
            catch (JSDisconnectedException jsEx)
            {
                // Circuit has disconnected
                Console.WriteLine($"[WebAudioAccess] JSDisconnectedException: {jsEx.Message}");
                lock (_audioLock)
                {
                    _audioQueue.Clear();
                    _isPlaying = false;
                }
                throw; // Rethrow to inform caller
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WebAudioAccess] Error in PlayAudioChunk: {ex.Message}");
                Console.WriteLine($"[WebAudioAccess] Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        // Helper to validate base64 strings
        private bool IsValidBase64(string base64)
        {
            try
            {
                // Try to decode a small sample to validate
                if (string.IsNullOrEmpty(base64) || base64.Length < 4)
                    return false;
                    
                Convert.FromBase64String(base64);
                return true;
            }
            catch
            {
                return false;
            }
        }

        // Try to fix base64 padding
        private string FixBase64Padding(string base64)
        {
            try
            {
                // Base64 strings should have a length that is a multiple of 4
                var remainder = base64.Length % 4;
                if (remainder > 0)
                {
                    // Add padding to make length a multiple of 4
                    base64 += new string('=', 4 - remainder);
                    Console.WriteLine("[WebAudioAccess] Fixed base64 padding");
                }
                
                // Test if it's valid now
                Convert.FromBase64String(base64);
                return base64;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WebAudioAccess] Error fixing base64: {ex.Message}");
                return base64; // Return original if fix fails
            }
        }

        private async Task ProcessAudioQueue()
        {
            while (true)
            {
                string? nextChunk = null;

                lock (_audioLock)
                {
                    if (_audioQueue.Count > 0)
                    {
                        nextChunk = _audioQueue.Dequeue();
                    }
                    else
                    {
                        // No more chunks to process
                        return;
                    }
                }

                // Parse the audio data and sample rate
                string[] parts = nextChunk.Split('|');
                string audioData = parts[0];
                int sampleRate = parts.Length > 1 && int.TryParse(parts[1], out int rate) ? rate : 24000;

                // Play the chunk outside the lock
                await PlayAudioChunk(audioData, sampleRate);
            }
        }

        public async Task<bool> StartRecordingAudio(MicrophoneAudioReceivedEventHandler audioDataReceivedHandler)
        {
            if (_audioModule == null) throw new InvalidOperationException("Audio module not initialized");

            if (_isRecording)
            {
                return false;
            }

            try
            {
                _audioDataReceivedHandler = audioDataReceivedHandler ??
                    throw new ArgumentNullException(nameof(audioDataReceivedHandler));

                // Create a reference to this object that JavaScript can call back into
                // Store it in a field to prevent garbage collection
                _dotNetReference = DotNetObjectReference.Create(this);
                Console.WriteLine("[WebAudioAccess] Created DotNetObjectReference for JS callbacks");

                // Make sure the AudioContext is resumed before starting recording
                await _audioModule.InvokeVoidAsync("ensureAudioContextResumed");
                
                // Add a small delay to ensure everything is initialized
                await Task.Delay(200);

                // Start recording with a 200ms interval
                Console.WriteLine("[WebAudioAccess] Calling startRecording with DotNetObjectReference");
                bool success = await _audioModule.InvokeAsync<bool>("startRecording", _dotNetReference, 200);

                if (success)
                {
                    _isRecording = true;
                    Console.WriteLine("[WebAudioAccess] Recording started successfully");
                    return true;
                }
                else
                {
                    Console.WriteLine("[WebAudioAccess] Failed to start recording");
                    CleanupRecording();
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WebAudioAccess] Error starting recording: {ex.Message}");
                CleanupRecording();
                return false;
            }
        }

        [JSInvokable]
        public Task ReceiveAudioData(string base64EncodedPcm16Audio)
        {
            // Log that the method was called using console logging
            Console.WriteLine($"[WebAudioAccess] ReceiveAudioData called, data length: {base64EncodedPcm16Audio?.Length ?? 0}");

            try
            {
                // Check if the handler is null
                if (_audioDataReceivedHandler == null)
                {
                    Console.WriteLine("[WebAudioAccess] Error: _audioDataReceivedHandler is null");
                    return Task.CompletedTask;
                }

                // Check if the data is valid
                if (string.IsNullOrEmpty(base64EncodedPcm16Audio))
                {
                    Console.WriteLine("[WebAudioAccess] Error: Received empty audio data");
                    return Task.CompletedTask;
                }

                // Invoke the callback with the received audio data
                _audioDataReceivedHandler.Invoke(this, new MicrophoneAudioReceivedEvenArgs(base64EncodedPcm16Audio));
                Console.WriteLine("[WebAudioAccess] Successfully invoked _audioDataReceivedHandler");
            }
            catch (Exception ex)
            {
                // Log the error with console logging
                Console.WriteLine($"[WebAudioAccess] Error in ReceiveAudioData: {ex.Message}");
                Console.WriteLine($"[WebAudioAccess] Stack trace: {ex.StackTrace}");
            }

            return Task.CompletedTask;
        }

        public async Task<bool> StopRecordingAudio()
        {
            if (_audioModule == null) return false;

            if (!_isRecording)
            {
                return false;
            }

            try
            {
                bool success = await _audioModule.InvokeAsync<bool>("stopRecording");
                CleanupRecording();
                return success;
            }
            catch (JSDisconnectedException)
            {
                // Handle circuit disconnection gracefully
                CleanupRecording();
                return true; // Pretend success since we can't actually verify
            }
            catch (Exception)
            {
                CleanupRecording();
                return false;
            }
        }

        private void CleanupRecording()
        {
            _isRecording = false;
            
            // Dispose the DotNetObjectReference to prevent memory leaks
            if (_dotNetReference != null)
            {
                try
                {
                    _dotNetReference.Dispose();
                    Console.WriteLine("[WebAudioAccess] Disposed DotNetObjectReference");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WebAudioAccess] Error disposing DotNetObjectReference: {ex.Message}");
                }
                finally
                {
                    _dotNetReference = null;
                }
            }
            
            _audioDataReceivedHandler = null;
        }

        public async Task ClearAudioQueue()
        {
            if (_audioModule == null) return;

            try
            {
                // Clear the queue first
                lock (_audioLock)
                {
                    _audioQueue.Clear();
                }

                // Stop any current audio playback
                await _audioModule.InvokeVoidAsync("stopAudioPlayback");

                // Reset playing state
                lock (_audioLock)
                {
                    _isPlaying = false;
                }
            }
            catch (JSDisconnectedException)
            {
                // Handle circuit disconnection gracefully
                lock (_audioLock)
                {
                    _audioQueue.Clear();
                    _isPlaying = false;
                }
            }
            catch (Exception)
            {
                // Still clear local state even if JS fails
                lock (_audioLock)
                {
                    _audioQueue.Clear();
                    _isPlaying = false;
                }
            }
        }

        async ValueTask IAsyncDisposable.DisposeAsync()
        {
            // Make sure recording is stopped
            if (_isRecording)
            {
                try 
                {
                    await StopRecordingAudio();
                }
                catch (JSDisconnectedException)
                {
                    // Circuit already disconnected, can't stop recording via JS
                    _isRecording = false;
                    _audioDataReceivedHandler = null;
                }
                catch (Exception)
                {
                    // Handle silently
                }
            }

            // Dispose the JS module
            if (_audioModule != null)
            {
                try
                {
                    await _audioModule.DisposeAsync();
                }
                catch (JSDisconnectedException)
                {
                    // Circuit already disconnected, can't dispose via JS
                }
                catch (Exception)
                {
                    // Handle silently
                }
                finally
                {
                    // Ensure the reference is cleared even if disposal fails
                    _audioModule = null;
                }
            }
        }
    }
}
