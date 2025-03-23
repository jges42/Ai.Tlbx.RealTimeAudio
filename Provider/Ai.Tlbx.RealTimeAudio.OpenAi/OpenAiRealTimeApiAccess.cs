using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Reflection;
using Ai.Tlbx.RealTimeAudio.OpenAi.Models;

namespace Ai.Tlbx.RealTimeAudio.OpenAi
{
    public class OpenAiRealTimeApiAccess : IAsyncDisposable
    {
        private const string REALTIME_WEBSOCKET_ENDPOINT = "wss://api.openai.com/v1/realtime";
        private const int CONNECTION_TIMEOUT_MS = 10000;
        private const int MAX_RETRY_ATTEMPTS = 3;

        private readonly IAudioHardwareAccess _hardwareAccess;
        private readonly string _apiKey;
        private bool _isInitialized = false;
        private bool _isRecording = false;
        private bool _isConnecting = false;
        private bool _isDisposed = false;
        private string? _lastErrorMessage = null;
        private string _connectionStatus = string.Empty;
        private OpenAiRealTimeSettings _settings = new OpenAiRealTimeSettings();

        private ClientWebSocket? _webSocket;
        private Task? _receiveTask;
        private CancellationTokenSource? _cts;
        private CancellationTokenSource? _cancellationTokenSource;
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

        // Add these fields for microphone testing
        private bool _isMicrophoneTesting = false;
        private List<string> _micTestAudioChunks = new List<string>();
        private readonly object _micTestLock = new object();
        private CancellationTokenSource? _micTestCancellation;

        public event EventHandler<string>? ConnectionStatusChanged;
        public event EventHandler<OpenAiChatMessage>? MessageAdded;
        public event EventHandler<string>? MicrophoneTestStatusChanged;

        // Public readonly properties to expose internal state
        public bool IsInitialized => _isInitialized;
        public bool IsRecording => _isRecording;
        public bool IsConnecting => _isConnecting;
        public string? LastErrorMessage => _lastErrorMessage;
        public string ConnectionStatus => _connectionStatus;
        public IReadOnlyList<OpenAiChatMessage> ChatHistory => _chatHistory.AsReadOnly();
        
        public OpenAiRealTimeSettings Settings => _settings;

        // For backward compatibility
        public TurnDetectionSettings TurnDetectionSettings
        {
            get => _settings.TurnDetection;
        }

        public AssistantVoice CurrentVoice
        {
            get => _settings.Voice;
            set
            {
                _settings.Voice = value;
                SetVoice(value);
            }
        }

        public bool IsMicrophoneTesting => _isMicrophoneTesting;

        public OpenAiRealTimeApiAccess(IAudioHardwareAccess hardwareAccess)
        {
            _hardwareAccess = hardwareAccess;
            _apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ??
                throw new InvalidOperationException("OPENAI_API_KEY not set");

            // Subscribe to audio error events through the interface
            _hardwareAccess.AudioError += OnAudioError;
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
        /// <param name="settings">Optional settings to configure the API behavior</param>
        public async Task Start(OpenAiRealTimeSettings? settings = null)
        {
            try
            {
                _lastErrorMessage = null;
                
                // If settings are provided, store them for this session
                if (settings != null)
                {
                    _settings = settings;
                }
                
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
        /// For backward compatibility - starts the session with just turn detection settings
        /// </summary>
        public async Task Start(TurnDetectionSettings? turnDetectionSettings)
        {
            if (turnDetectionSettings != null)
            {
                _settings.TurnDetection = turnDetectionSettings;
            }
            await Start();
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

        private void SetVoice(AssistantVoice voice)
        {
            try
            {
                _lastErrorMessage = null;                
                
                if (_isInitialized && _webSocket?.State == WebSocketState.Open)
                {
                    _ = SendAsync(new
                    {
                        type = "session.update",
                        session = new { voice = _settings.GetVoiceString() }
                    });
                    RaiseStatus($"Voice updated to: {voice}");
                }
                else
                {
                    RaiseStatus($"Voice set to: {voice} (will be applied when connected)");
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
            var sessionConfig = new
            {
                type = "session.update",
                session = new
                {
                    turn_detection = BuildTurnDetectionConfig(),
                    model = "gpt-4o-realtime-preview-2024-12-17",
                    voice = _settings.GetVoiceString(),
                    modalities = _settings.Modalities.ToArray(),
                    input_audio_format = _settings.GetAudioFormatString(_settings.InputAudioFormat),
                    output_audio_format = _settings.GetAudioFormatString(_settings.OutputAudioFormat),
                    input_audio_transcription = new
                    {
                        model = _settings.GetTranscriptionModelString()
                    },                    
                    instructions = _settings.Instructions
                }
            };
            
            await SendAsync(sessionConfig);
            
            string turnTypeDesc = _settings.TurnDetection.Type switch 
            {
                TurnDetectionType.SemanticVad => "semantic VAD",
                TurnDetectionType.ServerVad => "server VAD",
                TurnDetectionType.None => "no turn detection",
                _ => "unknown turn detection"
            };
            
            RaiseStatus($"Session configured with voice: {_settings.GetVoiceString()} and {turnTypeDesc}");
        }
        
        private object? BuildTurnDetectionConfig()
        {
            // If turn detection is disabled, return null
            if (_settings.TurnDetection.Type == TurnDetectionType.None)
            {
                return null;
            }
            
            // For semantic VAD
            if (_settings.TurnDetection.Type == TurnDetectionType.SemanticVad)
            {
                return new 
                {
                    // Use "semantic_vad" as defined by JsonPropertyName attribute
                    type = GetJsonPropertyName(_settings.TurnDetection.Type) ?? "semantic_vad",
                    // Use value from JsonPropertyName attribute
                    eagerness = GetJsonPropertyName(_settings.TurnDetection.Eagerness) ?? "auto",
                    create_response = _settings.TurnDetection.CreateResponse,
                    interrupt_response = _settings.TurnDetection.InterruptResponse
                };
            }
            
            // For server VAD
            return new
            {
                // Use "server_vad" as defined by JsonPropertyName attribute
                type = GetJsonPropertyName(_settings.TurnDetection.Type) ?? "server_vad",
                threshold = _settings.TurnDetection.Threshold ?? 0.5f,
                prefix_padding_ms = _settings.TurnDetection.PrefixPaddingMs ?? 300,
                silence_duration_ms = _settings.TurnDetection.SilenceDurationMs ?? 500
            };
        }
        
        /// <summary>
        /// Get the JsonPropertyName attribute value for an enum
        /// </summary>
        private string? GetJsonPropertyName<T>(T enumValue) where T : Enum
        {
            var enumType = typeof(T);
            var memberInfo = enumType.GetMember(enumValue.ToString());
            
            if (memberInfo.Length > 0)
            {
                var attributes = memberInfo[0].GetCustomAttributes(typeof(JsonPropertyNameAttribute), false);
                if (attributes.Length > 0)
                {
                    return ((JsonPropertyNameAttribute)attributes[0]).Name;
                }
            }
            
            return null;
        }

        private async Task Connect()
        {
            int delayMs = 1000;  // Start with 1 second delay between retries
            
            for (int i = 0; i < MAX_RETRY_ATTEMPTS; i++)
            {
                try
                {
                    // Dispose of any existing WebSocket
                    if (_webSocket != null)
                    {
                        try 
                        {
                            _webSocket.Dispose();
                        }
                        catch { /* Ignore any errors during disposal */ }
                        _webSocket = null;
                    }
                    
                    // Create a new WebSocket
                    _webSocket = new ClientWebSocket();
                    _webSocket.Options.SetRequestHeader("Authorization", $"Bearer {_apiKey}");
                    _webSocket.Options.SetRequestHeader("openai-beta", "realtime=v1");
                    
                    // Set sensible timeouts
                    _webSocket.Options.KeepAliveInterval = TimeSpan.FromSeconds(30);
                    
                    Debug.WriteLine($"[WebSocket] Connecting to OpenAI API, attempt {i + 1} of {MAX_RETRY_ATTEMPTS}...");
                    RaiseStatus($"Connecting to OpenAI API ({i + 1}/{MAX_RETRY_ATTEMPTS})...");

                    using var cts = new CancellationTokenSource(CONNECTION_TIMEOUT_MS);
                    await _webSocket.ConnectAsync(
                        new Uri($"{REALTIME_WEBSOCKET_ENDPOINT}?model=gpt-4o-realtime-preview-2024-12-17"),
                        cts.Token);

                    Debug.WriteLine("[WebSocket] Connected successfully");
                    
                    // Create a new cancellation token source for the receive task
                    _cts?.Dispose();
                    _cts = new CancellationTokenSource();
                    
                    // Start the receive task
                    _receiveTask = ReceiveAsync(_cts.Token);
                    return;
                }
                catch (WebSocketException wsEx)
                {
                    // Handle WebSocket specific exceptions
                    _webSocket?.Dispose();
                    _webSocket = null;
                    
                    Debug.WriteLine($"[WebSocket] WebSocket error on connect attempt {i + 1}: {wsEx.Message}, WebSocketErrorCode: {wsEx.WebSocketErrorCode}");
                    RaiseStatus($"Connection error: {wsEx.Message}");
                    
                    if (i < MAX_RETRY_ATTEMPTS - 1) 
                    {
                        await Task.Delay(delayMs);
                        delayMs = Math.Min(delayMs * 2, 10000); // Exponential backoff, max 10 seconds
                    }
                }
                catch (TaskCanceledException)
                {
                    // Connection timeout
                    _webSocket?.Dispose();
                    _webSocket = null;
                    
                    Debug.WriteLine($"[WebSocket] Connection timeout on attempt {i + 1}");
                    RaiseStatus($"Connection timeout");
                    
                    if (i < MAX_RETRY_ATTEMPTS - 1) 
                    {
                        await Task.Delay(delayMs);
                        delayMs = Math.Min(delayMs * 2, 10000);
                    }
                }
                catch (Exception ex)
                {
                    // Handle other exceptions
                    _webSocket?.Dispose();
                    _webSocket = null;
                    
                    Debug.WriteLine($"[WebSocket] Connect attempt {i + 1} failed: {ex.Message}");
                    RaiseStatus($"Connection error: {ex.Message}");
                    
                    if (i < MAX_RETRY_ATTEMPTS - 1) 
                    {
                        await Task.Delay(delayMs);
                        delayMs = Math.Min(delayMs * 2, 10000);
                    }
                }
            }
            
            throw new InvalidOperationException("Connection failed after maximum retry attempts");
        }

        private async Task ReceiveAsync(CancellationToken ct)
        {
            var buffer = new byte[32384];
            int consecutiveErrorCount = 0;
            
            while (_webSocket?.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                try
                {
                    using var ms = new MemoryStream();
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await _webSocket.ReceiveAsync(buffer, ct);
                        if (result.MessageType == WebSocketMessageType.Close) 
                        {
                            Debug.WriteLine($"[WebSocket] Received close message with status: {result.CloseStatus}, description: {result.CloseStatusDescription}");
                            return;
                        }
                        ms.Write(buffer, 0, result.Count);
                    } while (!result.EndOfMessage && _webSocket?.State == WebSocketState.Open);

                    var json = Encoding.UTF8.GetString(ms.ToArray());
                    await HandleMessageAsync(json);
                    
                    // Reset error counter on successful message
                    consecutiveErrorCount = 0;
                }
                catch (WebSocketException wsEx)
                {
                    consecutiveErrorCount++;
                    
                    // Log but don't treat as critical if it's a normal closure
                    if (wsEx.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
                    {
                        Debug.WriteLine("[WebSocket] Connection closed prematurely by server");
                        RaiseStatus("Connection closed by server, will attempt to reconnect if needed");
                        break; // Exit the loop to allow reconnection logic to run
                    }
                    else
                    {
                        Debug.WriteLine($"[WebSocket] WebSocket error: {wsEx.Message}, ErrorCode: {wsEx.WebSocketErrorCode}");
                        RaiseStatus($"WebSocket error: {wsEx.Message}");
                        
                        if (consecutiveErrorCount > 3)
                        {
                            RaiseStatus("Too many consecutive WebSocket errors, reconnecting...");
                            // Force a reconnection
                            break;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    Debug.WriteLine("[WebSocket] Receive operation canceled");
                    break;
                }
                catch (Exception ex)
                {
                    consecutiveErrorCount++;
                    Debug.WriteLine($"[WebSocket] Receive error: {ex.Message}");
                    RaiseStatus($"Receive error: {ex.Message}");
                    
                    if (consecutiveErrorCount > 3)
                    {
                        RaiseStatus("Too many consecutive receive errors, reconnecting...");
                        break;
                    }
                    
                    // Add a small delay before trying again to avoid hammering the server
                    await Task.Delay(500, CancellationToken.None);
                }
            }
            
            // If we exited the loop and the connection is still active, try to restart it
            if (!ct.IsCancellationRequested && _isInitialized && _webSocket != null)
            {
                Debug.WriteLine("[WebSocket] WebSocket loop exited, attempting to reconnect...");
                _ = Task.Run(async () => 
                {
                    await Task.Delay(1000); // Wait a moment before reconnecting
                    await ReconnectAsync();
                }, CancellationToken.None);
            }
        }

        private async Task HandleMessageAsync(string json)
        {
            try
            {
                var doc = JsonDocument.Parse(json);
                var type = doc.RootElement.GetProperty("type").GetString();

                switch (type)
                {
                    case "error":
                        // Extract and log detailed error information
                        string errorMessage = "Unknown error";
                        string errorType = "unknown";
                        string errorCode = "unknown";                        
                        
                        if (doc.RootElement.TryGetProperty("error", out var errorObj))
                        {
                            if (errorObj.TryGetProperty("message", out var msgElement))
                                errorMessage = msgElement.GetString() ?? errorMessage;
                                
                            if (errorObj.TryGetProperty("type", out var typeElement))
                                errorType = typeElement.GetString() ?? errorType;
                                
                            if (errorObj.TryGetProperty("code", out var codeElement))
                                errorCode = codeElement.GetString() ?? errorCode;                                
                            
                        }
                        
                        string errorDetails = $"Error: {errorType}, Code: {errorCode}, Message: {errorMessage}";
                                                
                        Debug.WriteLine($"[WebSocket] {errorDetails}");
                        RaiseStatus($"OpenAI API Error: {errorMessage}");
                        break;

                    case "rate_limits.updated":
                        Debug.WriteLine($"[WebSocket] Rate Limit Update: {json}");
                        break;

                    case "response.audio.delta":
                        var audio = doc.RootElement.GetProperty("delta").GetString();
                        Debug.WriteLine($"[WebSocket] Audio delta received, length: {audio?.Length ?? 0}");
                        if (!string.IsNullOrEmpty(audio))
                        {
                            try
                            {
                                Debug.WriteLine("[WebSocket] Attempting to play audio...");
                                await _hardwareAccess.PlayAudio(audio, 24000);
                                Debug.WriteLine("[WebSocket] PlayAudio called successfully");
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"[WebSocket] ERROR playing audio: {ex.Message}");
                                Debug.WriteLine($"[WebSocket] Stack trace: {ex.StackTrace}");
                            }
                        }
                        break;

                    case "response.audio_transcript.done":
                        if ((doc.RootElement.TryGetProperty("transcript", out var spokenText)))
                        {
                            Debug.WriteLine($"[WebSocket] Transcript received: {spokenText}");
                        }
                        break;

                    case "response.done":
                        Debug.WriteLine("[WebSocket] Full response completed");
                        try
                        {
                            // Extract server response text from the 'response.done' message
                            if (doc.RootElement.TryGetProperty("response", out var responseObj) &&
                                responseObj.TryGetProperty("output", out var outputArray) &&
                                outputArray.GetArrayLength() > 0)
                            {
                                var firstOutput = outputArray[0];
                                if (firstOutput.TryGetProperty("content", out var contentArray) &&
                                    contentArray.GetArrayLength() > 0)
                                {
                                    StringBuilder fullText = new StringBuilder();
                                    
                                    // Process all content items
                                    foreach (var content in contentArray.EnumerateArray())
                                    {
                                        // Handle text content
                                        if (content.TryGetProperty("type", out var contentType))
                                        {
                                            string contentTypeStr = contentType.GetString() ?? string.Empty;
                                            
                                            if (contentTypeStr == "text" && content.TryGetProperty("text", out var textElement))
                                            {
                                                string text = textElement.GetString() ?? string.Empty;
                                                fullText.Append(text);
                                                Debug.WriteLine($"[WebSocket] Extracted text from response.done: {text}");
                                            }
                                            // Handle audio transcript
                                            else if (contentTypeStr == "audio" && content.TryGetProperty("transcript", out var transcriptElement))
                                            {
                                                string transcript = transcriptElement.GetString() ?? string.Empty;
                                                fullText.Append(transcript);
                                                Debug.WriteLine($"[WebSocket] Extracted audio transcript from response.done: {transcript}");
                                            }
                                        }
                                    }
                                    
                                    string completeMessage = fullText.ToString();
                                    if (!string.IsNullOrWhiteSpace(completeMessage))
                                    {
                                        Debug.WriteLine($"[WebSocket] Final extracted message from response.done: {completeMessage}");
                                        
                                        // Add to chat history if new or different from last message
                                        if (_chatHistory.Count == 0 || 
                                           _chatHistory[_chatHistory.Count - 1].IsFromUser || 
                                           _chatHistory[_chatHistory.Count - 1].Text != completeMessage)
                                        {
                                            var message = new OpenAiChatMessage(completeMessage, false);
                                            _chatHistory.Add(message);
                                            MessageAdded?.Invoke(this, message);
                                            Debug.WriteLine("[WebSocket] Added message to chat history via response.done");
                                        }
                                        
                                        // Clear the AI message buffer since we've got the complete message
                                        _currentAiMessage.Clear();
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[WebSocket] Error processing response.done message: {ex.Message}");
                        }
                        break;

                    case "response.text.delta":
                        if (doc.RootElement.TryGetProperty("delta", out var deltaElem) && 
                            deltaElem.TryGetProperty("text", out var textElem))
                        {
                            string deltaText = textElem.GetString() ?? string.Empty;
                            _currentAiMessage.Append(deltaText);
                            Debug.WriteLine($"[WebSocket] Text delta received: '{deltaText}'");
                        }
                        break;

                    case "response.text.done":
                        if (_currentAiMessage.Length > 0)
                        {
                            string messageText = _currentAiMessage.ToString();
                            Debug.WriteLine($"[WebSocket] Text done received, message: '{messageText}'");
                            
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
                                Debug.WriteLine("[WebSocket] Added message to chat history via text.done");
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
                        Debug.WriteLine("[WebSocket] New conversation item started");
                        if (doc.RootElement.TryGetProperty("role", out var roleElem))
                        {
                            string role = roleElem.GetString() ?? string.Empty;
                            Debug.WriteLine($"[WebSocket] Item role: {role}");
                            if (role == "assistant")
                            {
                                // Reset AI message for new response
                                _currentAiMessage.Clear();
                            }
                        }
                        break;

                    case "conversation.item.end":
                        Debug.WriteLine("[WebSocket] Conversation item ended");
                        break;

                    case "response.output_item.done":
                        Debug.WriteLine("[WebSocket] Received complete message from assistant");
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
                                Debug.WriteLine($"[WebSocket] Complete message text: {messageText}");
                                
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
                            Debug.WriteLine($"[WebSocket] Error processing complete message: {ex.Message}");
                        }
                        break;

                    default:
                        // Log all unhandled message types to debug output
                        Debug.WriteLine($"[WebSocket] Unhandled message type: {type} - Content: {json.Substring(0, Math.Min(100, json.Length))}...");
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

                // Send audio data to OpenAI
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
                try 
                {
                    await _hardwareAccess.StopRecordingAudio();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[WebSocket] Error stopping recording during cleanup: {ex.Message}");
                }

                // Cancel the receive task if it exists
                if (_cts != null)
                {
                    try
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
                                Debug.WriteLine($"[WebSocket] Error waiting for receive task to complete: {ex.Message}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[WebSocket] Error canceling receive task: {ex.Message}");
                    }
                }

                // Close the WebSocket connection if it's open
                if (_webSocket != null)
                {
                    try
                    {
                        if (_webSocket.State == WebSocketState.Open)
                        {
                            try
                            {
                                var closeTask = _webSocket.CloseAsync(
                                    WebSocketCloseStatus.NormalClosure, 
                                    "Cleanup", 
                                    CancellationToken.None);
                                
                                // Add a timeout to the close operation
                                await Task.WhenAny(closeTask, Task.Delay(3000));
                                
                                if (!closeTask.IsCompleted)
                                {
                                    Debug.WriteLine("[WebSocket] Close operation timed out");
                                }
                            }
                            catch (WebSocketException wsEx)
                            {
                                Debug.WriteLine($"[WebSocket] WebSocket error during close: {wsEx.Message}");
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"[WebSocket] Error closing WebSocket: {ex.Message}");
                            }
                        }
                        
                        // Dispose WebSocket regardless of close success
                        _webSocket.Dispose();
                    }
                    catch (Exception ex) 
                    {
                        Debug.WriteLine($"[WebSocket] Error disposing WebSocket: {ex.Message}");
                    }
                    finally
                    {
                        _webSocket = null;
                    }
                }

                // Dispose of other resources
                try
                {
                    _cts?.Dispose();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[WebSocket] Error disposing CTS: {ex.Message}");
                }
                
                _cts = null;
                _receiveTask = null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebSocket] Error during cleanup: {ex.Message}");
                RaiseStatus($"Error during cleanup: {ex.Message}");
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_isDisposed) return;
            
            try
            {
                // Unsubscribe from audio error events
                _hardwareAccess.AudioError -= OnAudioError;
                
                // Close the WebSocket connection if open
                await StopWebSocketReceive();
                
                _webSocket?.Dispose();
                _webSocket = null;
                
                // Cancel any operations
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
                
                _isDisposed = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during disposal: {ex.Message}");
            }
        }

        public void Dispose()
        {
            // Use ConfigureAwait(false) to prevent deadlocks
            Cleanup().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        private void RaiseStatus(string status)
        {
            _connectionStatus = status;

            bool isError = status.Contains("Error", StringComparison.OrdinalIgnoreCase) ||
                          status.Contains("Failed", StringComparison.OrdinalIgnoreCase) ||
                          status.Contains("Exception", StringComparison.OrdinalIgnoreCase);
                          
            bool isHighPriorityInfo = status.Contains("initialized", StringComparison.OrdinalIgnoreCase) ||
                                    status.Contains("Recording started", StringComparison.OrdinalIgnoreCase) ||
                                    status.Contains("Recording stopped", StringComparison.OrdinalIgnoreCase);

            bool shouldUpdate = isError || isHighPriorityInfo ||
                (DateTime.Now - _lastStatusUpdate).TotalMilliseconds >= STATUS_UPDATE_INTERVAL_MS && status != _lastRaisedStatus;

            if (shouldUpdate)
            {
                if (isError)
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

        // Handle audio hardware errors
        private void OnAudioError(object? sender, string errorMessage)
        {
            _lastErrorMessage = errorMessage;
            
            // If we're in the middle of recording, stop it to avoid further issues
            if (IsRecording)
            {
                Task.Run(async () => await Stop()).ConfigureAwait(false);
            }
        }

        private async Task StopWebSocketReceive()
        {
            try
            {
                if (_cts != null)
                {
                    _cts.Cancel();
                    _cts.Dispose();
                    _cts = null;
                }

                if (_receiveTask != null)
                {
                    try
                    {
                        await Task.WhenAny(_receiveTask, Task.Delay(2000));
                    }
                    catch (Exception ex)
                    {
                        RaiseStatus($"Error waiting for receive task to complete: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                RaiseStatus($"Error stopping WebSocket receive: {ex.Message}");
            }
        }

        // Added to handle automatic reconnection when the WebSocket connection drops
        private async Task ReconnectAsync()
        {
            try
            {
                Debug.WriteLine("[WebSocket] Attempting to reconnect...");
                RaiseStatus("Connection lost, attempting to reconnect...");
                
                // Clean up existing resources
                await Cleanup();
                
                // Give a short delay before reconnecting
                await Task.Delay(1000);
                
                // Try to reconnect
                await Connect();
                
                // Reconfigure the session with the saved turn detection settings
                await ConfigureSession();
                
                RaiseStatus("Successfully reconnected");
                Debug.WriteLine("[WebSocket] Reconnection successful");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebSocket] Reconnection failed: {ex.Message}");
                RaiseStatus($"Reconnection failed: {ex.Message}");
                
                // Try again with exponential backoff if we're still initialized
                if (_isInitialized)
                {
                    _ = Task.Run(async () => 
                    {
                        await Task.Delay(5000); // Wait longer before the next attempt
                        await ReconnectAsync();
                    });
                }
            }
        }

        /// <summary>
        /// Updates the turn detection settings without starting recording
        /// </summary>
        /// <param name="settings">The new turn detection settings to use</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task UpdateTurnDetectionSettings(TurnDetectionSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));
                
            _settings.TurnDetection = settings;
            
            if (_isInitialized && _webSocket?.State == WebSocketState.Open)
            {
                // If we're already connected, update the session immediately
                await SendAsync(new
                {
                    type = "session.update",
                    session = new { turn_detection = BuildTurnDetectionConfig() }
                });
                
                string turnTypeDesc = _settings.TurnDetection.Type switch 
                {
                    TurnDetectionType.SemanticVad => "semantic VAD",
                    TurnDetectionType.ServerVad => "server VAD",
                    TurnDetectionType.None => "no turn detection",
                    _ => "unknown turn detection"
                };
                
                RaiseStatus($"Turn detection updated to {turnTypeDesc}");
            }
        }
        
        /// <summary>
        /// Updates all settings for the API
        /// </summary>
        /// <param name="settings">The new settings to use</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task UpdateSettings(OpenAiRealTimeSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));
                
            // Store the settings
            _settings = settings;
            
            // If we're already connected, update the session with all new settings
            if (_isInitialized && _webSocket?.State == WebSocketState.Open)
            {
                await ConfigureSession();
                RaiseStatus("Settings updated and applied");
            }
            else
            {
                RaiseStatus("Settings updated, will be applied when connected");
            }
        }
        
        /// <summary>
        /// Validates that the settings are correct and complete
        /// </summary>
        /// <returns>True if settings are valid</returns>
        public bool ValidateSettings()
        {
            if (_settings == null)
                return false;
                
            // Check modalities
            if (_settings.Modalities == null || _settings.Modalities.Count == 0)
                return false;
                
            // Check instructions
            if (string.IsNullOrWhiteSpace(_settings.Instructions))
                return false;
                
            // Settings are valid
            return true;
        }

        /// <summary>
        /// Tests the microphone by recording 5 seconds of audio and playing it back
        /// </summary>
        /// <returns>A task representing the asynchronous operation</returns>
        public async Task<bool> TestMicrophone()
        {
            // Can't test while recording or already testing
            if (_isRecording || _isMicrophoneTesting)
            {
                RaiseMicTestStatus("Cannot test microphone while recording or already testing");
                return false;
            }

            try
            {
                _isMicrophoneTesting = true;
                _micTestCancellation = new CancellationTokenSource();
                
                // Clear any previous test data
                lock (_micTestLock)
                {
                    _micTestAudioChunks.Clear();
                }
                
                // Initialize audio system if needed
                await _hardwareAccess.InitAudio();
                
                // Start recording audio
                RaiseMicTestStatus("Recording 5 seconds of audio...");
                
                bool success = await _hardwareAccess.StartRecordingAudio(OnMicTestAudioReceived);
                if (!success)
                {
                    _isMicrophoneTesting = false;
                    RaiseMicTestStatus("Failed to start recording for microphone test");
                    return false;
                }
                
                // Record for 5 seconds
                try
                {
                    await Task.Delay(5000, _micTestCancellation.Token);
                }
                catch (TaskCanceledException)
                {
                    // Test was canceled
                    RaiseMicTestStatus("Microphone test canceled");
                    await _hardwareAccess.StopRecordingAudio();
                    _isMicrophoneTesting = false;
                    return false;
                }
                
                // Stop recording
                await _hardwareAccess.StopRecordingAudio();
                
                // Play back the recorded audio
                RaiseMicTestStatus("Playing back recorded audio...");
                
                List<string> audioChunks;
                lock (_micTestLock)
                {
                    audioChunks = new List<string>(_micTestAudioChunks);
                }
                
                if (audioChunks.Count == 0)
                {
                    RaiseMicTestStatus("No audio was recorded. Check your microphone settings.");
                    _isMicrophoneTesting = false;
                    return false;
                }
                
                // Play back each chunk
                foreach (var chunk in audioChunks)
                {
                    await _hardwareAccess.PlayAudio(chunk, 24000);
                    
                    // Check if canceled
                    if (_micTestCancellation.Token.IsCancellationRequested)
                    {
                        break;
                    }
                }
                
                RaiseMicTestStatus("Microphone test completed");
                _isMicrophoneTesting = false;
                return true;
            }
            catch (Exception ex)
            {
                RaiseMicTestStatus($"Error during microphone test: {ex.Message}");
                _isMicrophoneTesting = false;
                return false;
            }
            finally
            {
                _micTestCancellation?.Dispose();
                _micTestCancellation = null;
            }
        }
        
        /// <summary>
        /// Cancels an in-progress microphone test
        /// </summary>
        public void CancelMicrophoneTest()
        {
            if (_isMicrophoneTesting)
            {
                _micTestCancellation?.Cancel();
                RaiseMicTestStatus("Microphone test canceled");
                
                // Clear the audio queue to stop playback
                _ = _hardwareAccess.ClearAudioQueue();
            }
        }
        
        private void OnMicTestAudioReceived(object sender, MicrophoneAudioReceivedEvenArgs e)
        {
            if (_isMicrophoneTesting && !string.IsNullOrEmpty(e.Base64EncodedPcm16Audio))
            {
                lock (_micTestLock)
                {
                    _micTestAudioChunks.Add(e.Base64EncodedPcm16Audio);
                    Debug.WriteLine($"[MicTest] Recorded audio chunk: {e.Base64EncodedPcm16Audio.Length} chars");
                }
            }
        }
        
        private void RaiseMicTestStatus(string status)
        {
            Debug.WriteLine($"[MicTest] {status}");
            MicrophoneTestStatusChanged?.Invoke(this, status);
        }
    }    
}
