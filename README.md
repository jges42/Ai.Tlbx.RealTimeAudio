# Ai.Tlbx.RealTimeAudio

A toolkit for real-time audio processing in .NET applications with seamless integration with AI services.

## Packages

This toolkit offers the following NuGet packages:

### Ai.Tlbx.RealTimeAudio.OpenAi

Real-time audio processing library with OpenAI integration for speech recognition, transcription, and analysis.

#### Installation

```
dotnet add package Ai.Tlbx.RealTimeAudio.OpenAi
```

#### Usage

```csharp
using Ai.Tlbx.RealTimeAudio.OpenAi;

// Initialize the OpenAI client
var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
var openAiClient = new OpenAiAudioClient(apiKey);

// Process audio in real-time
await using (var audioStream = await openAiClient.CreateStreamingSessionAsync())
{
    // Connect to audio input source
    audioStream.StartCapture();
    
    // Handle real-time transcription
    audioStream.OnTranscription += (sender, text) =>
    {
        Console.WriteLine($"Transcription: {text}");
    };
    
    // Wait for completion
    await Task.Delay(TimeSpan.FromMinutes(1));
    
    // Stop capturing
    audioStream.StopCapture();
}
```

### Ai.Tlbx.RealTimeAudio.Hardware.Windows

Windows-specific hardware integration for real-time audio processing, supporting microphone access, audio device management, and low-latency playback.

#### Installation

```
dotnet add package Ai.Tlbx.RealTimeAudio.Hardware.Windows
```

#### Usage

```csharp
using Ai.Tlbx.RealTimeAudio.Hardware.Windows;

// List available audio devices
var devices = AudioDeviceManager.GetAvailableInputDevices();
foreach (var device in devices)
{
    Console.WriteLine($"Device: {device.Name}, ID: {device.Id}");
}

// Initialize an audio capture session
var captureSession = new AudioCaptureSession(sampleRate: 44100, channels: 1);

// Start capturing audio
captureSession.StartCapture(deviceId: devices[0].Id);

// Handle audio data
captureSession.OnAudioDataAvailable += (sender, data) =>
{
    // Process audio buffer
    Console.WriteLine($"Received {data.Length} audio samples");
};

// Wait for some time
await Task.Delay(TimeSpan.FromSeconds(10));

// Stop capturing
captureSession.StopCapture();
```

## Requirements

- .NET 9.0 or later
- Windows 10 or later (for Windows-specific components)

## License

MIT 
