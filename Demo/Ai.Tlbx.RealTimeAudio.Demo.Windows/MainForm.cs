using Ai.Tlbx.RealTimeAudio.Hardware.Windows;
using Ai.Tlbx.RealTimeAudio.OpenAi;
using Ai.Tlbx.RealTimeAudio.OpenAi.Models;
using System.Diagnostics;

namespace Ai.Tlbx.RealTimeAudio.Demo.Windows
{
    public partial class MainForm : Form
    {
        private readonly IAudioHardwareAccess _audioHardware;
        private readonly OpenAiRealTimeApiAccess _audioService;
        private bool _isRecording = false;
        
        public MainForm()
        {
            InitializeComponent();
            
            // Create the audio hardware instance for Windows
            _audioHardware = new WindowsAudioHardware();
            
            // Create the OpenAI service
            _audioService = new OpenAiRealTimeApiAccess(_audioHardware);
            
            // Hook up events
            _audioService.MessageAdded += OnMessageAdded;
            _audioService.ConnectionStatusChanged += OnConnectionStatusChanged;
            _audioService.MicrophoneTestStatusChanged += OnMicrophoneTestStatusChanged;
            
            // Set default voice
            _audioService.CurrentVoice = AssistantVoice.Alloy;
            
            // Initial UI state
            UpdateUIState();
        }
        
        private void OnMessageAdded(object? sender, OpenAiChatMessage message)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => OnMessageAdded(sender, message)));
                return;
            }
            
            // Add the message to the transcript
            string rolePrefix = message.Role == "user" ? "You: " : "AI: ";
            txtTranscription.AppendText($"{rolePrefix}{message.Content}\r\n\r\n");
        }
        
        private void OnConnectionStatusChanged(object? sender, string status)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => OnConnectionStatusChanged(sender, status)));
                return;
            }
            
            lblStatus.Text = status;
            UpdateUIState();
        }
        
        private void OnMicrophoneTestStatusChanged(object? sender, string status)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => OnMicrophoneTestStatusChanged(sender, status)));
                return;
            }
            
            lblStatus.Text = status;
            UpdateUIState();
        }
        
        private async void btnTestMic_Click(object sender, EventArgs e)
        {
            try
            {
                btnTestMic.Enabled = false;
                lblStatus.Text = "Testing microphone...";
                
                await _audioService.TestMicrophone();
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"Error testing microphone: {ex.Message}";
                Debug.WriteLine($"Microphone test error: {ex}");
            }
            finally
            {
                btnTestMic.Enabled = true;
                UpdateUIState();
            }
        }
        
        private async void btnStart_Click(object sender, EventArgs e)
        {
            if (_isRecording)
            {
                return;
            }
            
            try
            {
                btnStart.Enabled = false;
                lblStatus.Text = "Starting...";
                
                await _audioService.Start();
                _isRecording = true;
                lblStatus.Text = "Recording in progress...";
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"Error starting: {ex.Message}";
                Debug.WriteLine($"Start recording error: {ex}");
            }
            finally
            {
                UpdateUIState();
            }
        }
        
        private async void btnInterrupt_Click(object sender, EventArgs e)
        {
            try
            {
                btnInterrupt.Enabled = false;
                lblStatus.Text = "Interrupting...";
                
                await _audioService.InterruptSpeech();
                lblStatus.Text = "Speech interrupted";
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"Error interrupting: {ex.Message}";
                Debug.WriteLine($"Interrupt error: {ex}");
            }
            finally
            {
                UpdateUIState();
            }
        }
        
        private async void btnEnd_Click(object sender, EventArgs e)
        {
            if (!_isRecording)
            {
                return;
            }
            
            try
            {
                btnEnd.Enabled = false;
                lblStatus.Text = "Ending recording...";
                
                await _audioService.Stop();
                _isRecording = false;
                lblStatus.Text = "Recording ended";
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"Error ending recording: {ex.Message}";
                Debug.WriteLine($"End recording error: {ex}");
            }
            finally
            {
                UpdateUIState();
            }
        }
        
        private void UpdateUIState()
        {
            bool isConnecting = _audioService?.IsConnecting ?? false;
            bool isInitialized = _audioService?.IsInitialized ?? false;
            bool isMicTesting = _audioService?.IsMicrophoneTesting ?? false;
            
            btnTestMic.Enabled = !_isRecording && !isMicTesting && !isConnecting;
            btnStart.Enabled = !_isRecording && !isMicTesting && !isConnecting;
            btnInterrupt.Enabled = isInitialized && !isConnecting;
            btnEnd.Enabled = _isRecording && !isConnecting;
        }
        
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Cleanup
            if (_audioService != null)
            {
                _audioService.MessageAdded -= OnMessageAdded;
                _audioService.ConnectionStatusChanged -= OnConnectionStatusChanged;
                _audioService.MicrophoneTestStatusChanged -= OnMicrophoneTestStatusChanged;
            }
            
            base.OnFormClosing(e);
        }
    }
}
