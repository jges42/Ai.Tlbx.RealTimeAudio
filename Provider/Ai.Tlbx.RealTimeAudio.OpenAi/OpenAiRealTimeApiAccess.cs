using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ai.Tlbx.RealTimeAudio.OpenAi
{
    public class OpenAiRealTimeApiAccess : IDisposable
    {
        private const string REALTIME_WEBSOCKET_ENDPOINT = "wss://api.openai.com/v1/realtime";
        private const int CONNECTION_TIMEOUT_MS = 10000;
        private const int MAX_RETRY_ATTEMPTS = 3;

        private readonly IAudioHardwareAccess _hardwareAccess;
        private readonly string _apiKey;
        private string _currentVoice = "alloy";
        private bool _isInitialized = false;
        private bool _isRecording = false;
        private bool _isConnecting = false;
        private string? _lastErrorMessage = null;
        private string _connectionStatus = string.Empty;

        private ClientWebSocket? _webSocket;
        private Task? _receiveTask;
        private CancellationTokenSource? _cts;
        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private DateTime _lastStatusUpdate = DateTime.MinValue;
        private const int STATUS_UPDATE_INTERVAL_MS = 500;
        private string _lastRaisedStatus = string.Empty;

        private List<OpenAiChatMessage> _chatHistory = new List<OpenAiChatMessage>();
        private StringBuilder _currentAiMessage = new StringBuilder();
        private StringBuilder _currentUserMessage = new StringBuilder();

        public event EventHandler<string>? ConnectionStatusChanged;
        public event EventHandler<OpenAiChatMessage>? MessageAdded;

        // Public readonly properties to expose internal state
        public bool IsInitialized => _isInitialized;
        public bool IsRecording => _isRecording;
        public bool IsConnecting => _isConnecting;
        public string? LastErrorMessage => _lastErrorMessage;
        public string ConnectionStatus => _connectionStatus;
        public IReadOnlyList<OpenAiChatMessage> ChatHistory => _chatHistory.AsReadOnly();

        public string CurrentVoice
        {
            get => _currentVoice;
            set
            {
                _currentVoice = value switch
                {
                    "alloy" or "ash" or "ballad" or "coral" or "echo" or "sage" or "shimmer" or "verse" => value,
                    _ => throw new ArgumentException("Invalid voice. Supported voices are: alloy, ash, ballad, coral, echo, sage, shimmer, and verse.")
                };
                SetVoice(value);
            }
        }

        public OpenAiRealTimeApiAccess(IAudioHardwareAccess hardwareAccess)
        {
            _hardwareAccess = hardwareAccess;
            _apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ??
                throw new InvalidOperationException("OPENAI_API_KEY not set");
        }

        public async Task InitializeConnection()
        {
            if (_isInitialized && _webSocket?.State == WebSocketState.Open)
            {
                RaiseStatus("Already initialized");
                return;
            }

            _isConnecting = true;
            _lastErrorMessage = null;
            await Cleanup();

            try
            {
                RaiseStatus("Initializing audio system...");
                await _hardwareAccess.InitAudio();

                RaiseStatus("Connecting to OpenAI API...");
                await Connect();

                RaiseStatus("Configuring session...");
                await ConfigureSession();

                _isInitialized = true;
                _isConnecting = false;
                RaiseStatus("Connection initialized, ready for voice selection or recording");
            }
            catch (Exception ex)
            {
                _lastErrorMessage = ex.Message;
                _isConnecting = false;
                _isInitialized = false;
                RaiseStatus($"Initialization failed: {ex.Message}");
                throw;
            }
        }

        public async Task Init()
        {
            await InitializeConnection();
        }

        /// <summary>
        /// Starts the full lifecycle - initializes the connection if needed and starts recording audio
        /// </summary>
        public async Task Start()
        {
            try
            {
                _lastErrorMessage = null;
                
                if (!_isInitialized || _webSocket?.State != WebSocketState.Open)
                {
                    _isConnecting = true;
                    RaiseStatus("Connection not initialized, connecting first...");
                    await InitializeConnection();
                }

                RaiseStatus("Starting audio recording...");
                bool success = await _hardwareAccess.StartRecordingAudio(OnAudioDataReceived);

                if (success)
                {
                    _isRecording = true;
                    RaiseStatus("Recording started successfully");
                }
                else
                {
                    RaiseStatus("Failed to start recording, attempting to reinitialize");
                    await Cleanup();
                    await Task.Delay(500);
                    _isConnecting = true;
                    await InitializeConnection();
                    success = await _hardwareAccess.StartRecordingAudio(OnAudioDataReceived);
                    
                    if (success)
                    {
                        _isRecording = true;
                        RaiseStatus("Recording started after reconnection");
                    }
                    else
                    {
                        _lastErrorMessage = "Failed to start recording after reconnection";
                        RaiseStatus("Recording failed, please reload the page");
                    }
                }
            }
            catch (Exception ex)
            {
                _lastErrorMessage = ex.Message;
                _isRecording = false;
                _isConnecting = false;
                RaiseStatus($"Error starting: {ex.Message}");
            }
        }

        /// <summary>
        /// Stops everything - clears audio queue, stops recording, and closes the connection
        /// </summary>
        public async Task Stop()
        {
            try
            {
                // First clear any playing audio
                await _hardwareAccess.ClearAudioQueue();
                
                // Stop recording
                if (_isRecording)
                {
                    await _hardwareAccess.StopRecordingAudio();
                    _isRecording = false;
                }
                
                // Close connection
                if (_isInitialized)
                {
                    await Cleanup();
                }
                
                RaiseStatus("Stopped recording and closed connection");
            }
            catch (Exception ex)
            {
                _lastErrorMessage = ex.Message;
                RaiseStatus($"Error stopping: {ex.Message}");
            }
        }

        private void SetVoice(string voice)
        {
            try
            {
                _lastErrorMessage = null;                
                
                if (_isInitialized && _webSocket?.State == WebSocketState.Open)
                {
                    _ = SendAsync(new
                    {
                        type = "session.update",
                        session = new { voice = _currentVoice }
                    });
                    RaiseStatus($"Voice updated to: {_currentVoice}");
                }
                else
                {
                    RaiseStatus($"Voice set to: {_currentVoice} (will be applied when connected)");
                }
            }
            catch (Exception ex)
            {
                _lastErrorMessage = ex.Message;
                RaiseStatus($"Error setting voice: {ex.Message}");
            }
        }

        private async Task ConfigureSession()
        {
            await SendAsync(new
            {
                type = "session.update",
                session = new
                {
                    turn_detection = new
                    {
                        type = "server_vad",
                        threshold = 0.2,
                        prefix_padding_ms = 500,
                        silence_duration_ms = 300
                    },
                    voice = _currentVoice,
                    modalities = new[] { "audio", "text" },
                    input_audio_format = "pcm16",
                    output_audio_format = "pcm16",
                    input_audio_transcription = new
                    {
                        model = "whisper-1"
                    },                    
                    instructions = "You are a helpful AI assistant. Be friendly, conversational, helpful, and engaging."
                }
            });
            RaiseStatus("Session configured with voice: " + _currentVoice + " and whisper transcription enabled");
        }

        private async Task Connect()
        {
            for (int i = 0; i < MAX_RETRY_ATTEMPTS; i++)
            {
                try
                {
                    _webSocket = new ClientWebSocket();
                    _webSocket.Options.SetRequestHeader("Authorization", $"Bearer {_apiKey}");
                    _webSocket.Options.SetRequestHeader("openai-beta", "realtime=v1");

                    using var cts = new CancellationTokenSource(CONNECTION_TIMEOUT_MS);
                    await _webSocket.ConnectAsync(
                        new Uri($"{REALTIME_WEBSOCKET_ENDPOINT}?model=gpt-4o-realtime-preview-2024-12-17"),
                        cts.Token);

                    _cts = new CancellationTokenSource();
                    _receiveTask = ReceiveAsync(_cts.Token);
                    return;
                }
                catch (Exception ex)
                {
                    _webSocket?.Dispose();
                    _webSocket = null;
                    RaiseStatus($"Connect attempt {i + 1} failed: {ex.Message}");
                    if (i < MAX_RETRY_ATTEMPTS - 1) 
                    {
                        await Task.Delay(1000 * (1 << i));
                    }
                }
            }
            throw new InvalidOperationException("Connection failed after retries");
        }

        private async Task ReceiveAsync(CancellationToken ct)
        {
            var buffer = new byte[32384];
            while (_webSocket?.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                try
                {
                    using var ms = new MemoryStream();
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await _webSocket.ReceiveAsync(buffer, ct);
                        if (result.MessageType == WebSocketMessageType.Close) return;
                        ms.Write(buffer, 0, result.Count);
                    } while (!result.EndOfMessage && _webSocket?.State == WebSocketState.Open);

                    var json = Encoding.UTF8.GetString(ms.ToArray());
                    await HandleMessageAsync(json);
                }
                catch (Exception ex)
                {
                    RaiseStatus($"Receive error: {ex.Message}");
                }
            }
        }

        private async Task HandleMessageAsync(string json)
        {
            try
            {
                var doc = JsonDocument.Parse(json);
                var type = doc.RootElement.GetProperty("type").GetString();

                Console.WriteLine($"[WebSocket] Received message type: {type}");
                
                // Also log the full JSON for all messages to help with debugging
                Console.WriteLine($"[WebSocket] Full message: {json}");

                switch (type)
                {
                    case "response.audio.delta":
                        var audio = doc.RootElement.GetProperty("delta").GetString();
                        Console.WriteLine($"[WebSocket] Audio delta received, length: {audio?.Length ?? 0}");
                        if (!string.IsNullOrEmpty(audio))
                        {
                            try
                            {
                                Console.WriteLine("[WebSocket] Attempting to play audio...");
                                await _hardwareAccess.PlayAudio(audio, 24000);
                                Console.WriteLine("[WebSocket] PlayAudio called successfully");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[WebSocket] ERROR playing audio: {ex.Message}");
                                Console.WriteLine($"[WebSocket] Stack trace: {ex.StackTrace}");
                            }
                        }
                        break;

                    case "response.audio.done":
                        // The user has interrupted the AI response, so clear any queued audio
                        Console.WriteLine("[WebSocket] Audio response completed or interrupted");
                        RaiseStatus("Speech interrupted, clearing audio queue");
                        await _hardwareAccess.ClearAudioQueue();
                        break;

                    case "response.text.delta":
                        if (doc.RootElement.TryGetProperty("delta", out var deltaElem) && 
                            deltaElem.TryGetProperty("text", out var textElem))
                        {
                            string deltaText = textElem.GetString() ?? string.Empty;
                            _currentAiMessage.Append(deltaText);
                            Console.WriteLine($"[WebSocket] Text delta received: '{deltaText}'");
                        }
                        break;

                    case "response.text.done":
                        if (_currentAiMessage.Length > 0)
                        {
                            string messageText = _currentAiMessage.ToString();
                            Console.WriteLine($"[WebSocket] Text done received, message: '{messageText}'");
                            
                            // Check if we should add this message to the chat history
                            // We might get both response.text.done and response.output_item.done,
                            // so we need to check if we've already added a message with this text
                            if (_chatHistory.Count == 0 || 
                               _chatHistory[_chatHistory.Count - 1].IsFromUser || 
                               _chatHistory[_chatHistory.Count - 1].Text != messageText)
                            {
                                var message = new OpenAiChatMessage(messageText, false);
                                _chatHistory.Add(message);
                                MessageAdded?.Invoke(this, message);
                                Console.WriteLine("[WebSocket] Added message to chat history via text.done");
                            }
                            
                            _currentAiMessage.Clear();
                        }
                        break;

                    case "conversation.item.input_audio_transcription.completed":
                        if (doc.RootElement.TryGetProperty("transcript", out var transcriptElem))
                        {
                            string transcript = transcriptElem.GetString() ?? string.Empty;
                            if (!string.IsNullOrWhiteSpace(transcript))
                            {
                                var message = new OpenAiChatMessage(transcript, true);
                                _chatHistory.Add(message);
                                MessageAdded?.Invoke(this, message);
                                _currentUserMessage.Clear();
                                RaiseStatus("User said: " + transcript);
                            }
                        }
                        break;

                    case "error":
                        var errorMsg = doc.RootElement.GetProperty("error").GetProperty("message").GetString();
                        RaiseStatus($"API Error: {errorMsg}");
                        break;

                    case "input_audio_buffer.speech_started":
                        RaiseStatus("Speech detected");
                        await SendAsync(new
                        {
                            type = "response.cancel",                            
                        });
                        await _hardwareAccess.ClearAudioQueue(); // when user speaks, open ai needs to shut up
                        break;

                    case "input_audio_buffer.speech_stopped":
                        RaiseStatus("Speech ended");
                        break;

                    case "conversation.item.start":
                        Console.WriteLine("[WebSocket] New conversation item started");
                        if (doc.RootElement.TryGetProperty("role", out var roleElem))
                        {
                            string role = roleElem.GetString() ?? string.Empty;
                            Console.WriteLine($"[WebSocket] Item role: {role}");
                            if (role == "assistant")
                            {
                                // Reset AI message for new response
                                _currentAiMessage.Clear();
                            }
                        }
                        break;

                    case "conversation.item.end":
                        Console.WriteLine("[WebSocket] Conversation item ended");
                        break;

                    case "response.output_item.done":
                        Console.WriteLine("[WebSocket] Received complete message from assistant");
                        try
                        {
                            if (doc.RootElement.TryGetProperty("item", out var itemElem) && 
                                itemElem.TryGetProperty("content", out var contentArray))
                            {
                                StringBuilder completeMessage = new StringBuilder();
                                
                                // Content is an array of content parts
                                foreach (var content in contentArray.EnumerateArray())
                                {
                                    if (content.TryGetProperty("type", out var contentTypeElem) && 
                                        contentTypeElem.GetString() == "text" &&
                                        content.TryGetProperty("text", out var contentTextElem))
                                    {
                                        string text = contentTextElem.GetString() ?? string.Empty;
                                        completeMessage.Append(text);
                                    }
                                }
                                
                                string messageText = completeMessage.ToString();
                                Console.WriteLine($"[WebSocket] Complete message text: {messageText}");
                                
                                // Only add if we have content and haven't already added via deltas
                                if (!string.IsNullOrWhiteSpace(messageText) && 
                                    (_chatHistory.Count == 0 || 
                                     _chatHistory[_chatHistory.Count - 1].IsFromUser || 
                                     _chatHistory[_chatHistory.Count - 1].Text != messageText))
                                {
                                    var message = new OpenAiChatMessage(messageText, false);
                                    _chatHistory.Add(message);
                                    MessageAdded?.Invoke(this, message);
                                    
                                    // Clear the current message since we've now got the complete version
                                    _currentAiMessage.Clear();
                                    
                                    RaiseStatus("Received complete message from assistant");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[WebSocket] Error processing complete message: {ex.Message}");
                        }
                        break;

                    default:
                        if (DateTime.Now.Second % 10 == 0)
                            RaiseStatus($"Received message: {type}");
                        break;
                }
            }
            catch (Exception ex)
            {
                RaiseStatus($"Error handling message: {ex.Message}. JSON: {json.Substring(0, Math.Min(100, json.Length))}...");
            }
        }

        private async void OnAudioDataReceived(object sender, MicrophoneAudioReceivedEvenArgs e)
        {
            try
            {
                if (_webSocket?.State != WebSocketState.Open)
                {
                    RaiseStatus("Warning: Received audio data but WebSocket is not open");
                    return;
                }

                if (string.IsNullOrEmpty(e.Base64EncodedPcm16Audio))
                {
                    RaiseStatus("Warning: Received empty audio data");
                    return;
                }

                Debug.WriteLine($"input_audio_buffer.append {e.Base64EncodedPcm16Audio.Length}");

                await SendAsync(new
                {
                    type = "input_audio_buffer.append",
                    audio = e.Base64EncodedPcm16Audio
                });
            }
            catch (Exception ex)
            {
                RaiseStatus($"Error sending audio data: {ex.Message}");
            }
        }

        private async Task SendAsync(object message)
        {
            string? json = null;

            try
            {
                if (_webSocket?.State != WebSocketState.Open) return;
                json = JsonSerializer.Serialize(message, _jsonOptions);
                var bytes = Encoding.UTF8.GetBytes(json);
                await _webSocket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch (Exception ex)
            {
                RaiseStatus($"Error sending message to OpenAI: {ex.Message} \n {json}");
            }
        }

        private async Task Cleanup()
        {
            try
            {
                // Mark as not connected first
                _isInitialized = false;
                _isRecording = false;
                
                // First stop any ongoing recording
                await _hardwareAccess.StopRecordingAudio();

                // Cancel the receive task if it exists
                if (_cts != null)
                {
                    _cts.Cancel();
                    
                    if (_receiveTask != null)
                    {
                        try
                        {
                            // Give the receive task a chance to complete gracefully
                            await Task.WhenAny(_receiveTask, Task.Delay(2000));
                        }
                        catch (Exception ex)
                        {
                            RaiseStatus($"Error waiting for receive task to complete: {ex.Message}");
                        }
                    }
                }

                // Close the WebSocket connection if it's open
                if (_webSocket?.State == WebSocketState.Open)
                {
                    try
                    {
                        await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Cleanup", CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        RaiseStatus($"Error closing WebSocket: {ex.Message}");
                    }
                }

                // Dispose of resources
                _webSocket?.Dispose();
                _webSocket = null;
                _cts?.Dispose();
                _cts = null;
                _receiveTask = null;
            }
            catch (Exception ex)
            {
                RaiseStatus($"Error during cleanup: {ex.Message}");
            }
        }

        public async Task DisposeAsync()
        {
            await Cleanup();
        }

        public void Dispose()
        {
            // Use ConfigureAwait(false) to prevent deadlocks
            Cleanup().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        private void RaiseStatus(string status)
        {
            _connectionStatus = status;

            bool isCriticalStatus = status.Contains("Error", StringComparison.OrdinalIgnoreCase) ||
                                    status.Contains("Failed", StringComparison.OrdinalIgnoreCase) ||
                                    status.Contains("Exception", StringComparison.OrdinalIgnoreCase) ||
                                    status.Contains("initialized", StringComparison.OrdinalIgnoreCase) ||
                                    status.Contains("Recording started", StringComparison.OrdinalIgnoreCase) ||
                                    status.Contains("Recording stopped", StringComparison.OrdinalIgnoreCase);

            if (isCriticalStatus ||
                (DateTime.Now - _lastStatusUpdate).TotalMilliseconds >= STATUS_UPDATE_INTERVAL_MS && status != _lastRaisedStatus)
            {
                if (isCriticalStatus)
                {
                    _lastErrorMessage = status;
                    _isConnecting = false;
                }

                ConnectionStatusChanged?.Invoke(this, status);
                _lastStatusUpdate = DateTime.Now;
                _lastRaisedStatus = status;
            }
        }

        /// <summary>
        /// Interrupts the AI's speech without stopping the session
        /// </summary>
        public async Task InterruptSpeech()
        {
            try
            {
                if (!_isInitialized || _webSocket?.State != WebSocketState.Open)
                {
                    RaiseStatus("Cannot interrupt: Connection not initialized");
                    return;
                }
                
                RaiseStatus("Manually interrupting AI speech...");
                
                // Send the response.cancel message to the server
                await SendAsync(new
                {
                    type = "response.cancel"
                });
                
                // Clear the audio queue to stop current playback
                await _hardwareAccess.ClearAudioQueue();
                
                RaiseStatus("AI speech interrupted");
            }
            catch (Exception ex)
            {
                _lastErrorMessage = ex.Message;
                RaiseStatus($"Error interrupting speech: {ex.Message}");
            }
        }

        public void ClearChatHistory()
        {
            _chatHistory.Clear();
            _currentAiMessage.Clear();
            _currentUserMessage.Clear();
        }
    }    
}
