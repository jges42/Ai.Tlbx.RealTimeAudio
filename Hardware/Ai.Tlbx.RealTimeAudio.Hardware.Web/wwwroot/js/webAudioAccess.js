// webAudioAccess.js
let mediaStream = null;
let audioWorkletNode = null;
let dotNetReference = null;
let isRecording = false;
let audioContext = null;
let audioInitialized = false;
let recordingInterval = null;
let audioChunks = [];

// Track active audio sources
let activeSources = [];

// Utility function to load the audio worklet
async function loadAudioWorklet() {
    if (!audioContext) {
        console.error("Cannot load audio worklet: audioContext is null");
        return false;
    }
    
    try {
        await audioContext.audioWorklet.addModule('./js/audio-processor.js');
        console.log("AudioWorklet module loaded successfully");
        return true;
    } catch (err) {
        // Check if error is about module already being loaded
        if (err.message && err.message.includes('already been added')) {
            console.log("AudioWorklet module already loaded");
            return true;
        }
        console.error("Failed to load AudioWorklet module:", err);
        return false;
    }
}

// This function needs an actual user interaction before it's called
async function initAudioWithUserInteraction() {
    try {
        console.log("Initializing audio with user interaction");
        
        // Create AudioContext with the correct sample rate for OpenAI
        audioContext = new (window.AudioContext || window.webkitAudioContext)({ sampleRate: 24000 });
        console.log("AudioContext created, state:", audioContext.state);
        
        // Force resume the AudioContext - this requires user interaction in many browsers
        if (audioContext.state === 'suspended') {
            await audioContext.resume();
            console.log("AudioContext resumed, new state:", audioContext.state);
        }
        
        // Request microphone permission - this shows the permission dialog to the user
        const stream = await navigator.mediaDevices.getUserMedia({
            audio: {
                channelCount: 1,
                sampleRate: 24000, // Match OpenAI requirement
                echoCancellation: true,
                noiseSuppression: true,
                autoGainControl: true
            },
            video: false
        });
        
        // Load the AudioWorklet module once
        if (!await loadAudioWorklet()) {
            throw new Error("Failed to load AudioWorklet module");
        }
        
        // Test the microphone by creating a dummy recording
        const source = audioContext.createMediaStreamSource(stream);
        const testNode = new AudioWorkletNode(audioContext, 'audio-recorder-processor');
        source.connect(testNode);
        
        // Wait a moment to ensure everything is initialized
        await new Promise(resolve => setTimeout(resolve, 500));
        
        // Clean up the test
        testNode.disconnect();
        stream.getTracks().forEach(track => track.stop());
        
        audioInitialized = true;
        console.log("Audio system fully initialized");
        return true;
    } catch (error) {
        console.error('Audio initialization error:', error);
        audioInitialized = false;
        return false;
    }
}

// Legacy function for compatibility
async function initAudioPermissions() {
    return await initAudioWithUserInteraction();
}

// Make sure AudioContext is resumed - must be called after user interaction
async function ensureAudioContextResumed() {
    if (!audioContext) {
        audioContext = new (window.AudioContext || window.webkitAudioContext)({ sampleRate: 24000 });
        console.log("AudioContext created in ensure function, state:", audioContext.state);
    }
    
    if (audioContext.state === 'suspended') {
        try {
            console.log("Attempting to resume AudioContext in ensure function");
            await audioContext.resume();
            console.log("AudioContext resumed, new state:", audioContext.state);
        } catch (error) {
            console.error("Failed to resume AudioContext:", error);
            return false;
        }
    }
    
    return true;
}

async function startRecording(dotNetObj, intervalMs = 500) {
    try {
        console.log("Starting recording process");
        
        if (isRecording) {
            console.warn("Recording already in progress, stopping first");
            await stopRecording();
        }
        
        // Store the .NET reference for callbacks
        dotNetReference = dotNetObj;
        console.log("dotNetReference set:", dotNetObj ? "valid object" : "null");
        
        // Make sure audio is initialized
        if (!audioInitialized) {
            console.log("Audio not initialized, initializing now");
            if (!await initAudioWithUserInteraction()) {
                console.error("Failed to initialize audio");
                return false;
            }
        }
        
        // Ensure AudioContext is in running state
        if (!await ensureAudioContextResumed()) {
            console.error("Failed to resume AudioContext");
            return false;
        }
        
        console.log("Requesting microphone access");
        // Get microphone stream with specific parameters for OpenAI
        mediaStream = await navigator.mediaDevices.getUserMedia({
            audio: {
                channelCount: 1,
                sampleRate: 24000, // Match OpenAI requirement
                echoCancellation: true,
                noiseSuppression: true,
                autoGainControl: true
            },
            video: false
        });
        
        console.log("Microphone access granted, creating processing pipeline");
        
        // Ensure the audio-processor worklet is loaded using our utility function
        if (!await loadAudioWorklet()) {
            throw new Error("Failed to load AudioWorklet module");
        }
        
        // Create a MediaStreamSource from the stream
        const source = audioContext.createMediaStreamSource(mediaStream);
        
        // Create an AudioWorkletNode for processing
        try {
            audioWorkletNode = new AudioWorkletNode(audioContext, 'audio-recorder-processor');
            console.log("AudioWorkletNode created successfully");
        } catch (err) {
            console.error("Failed to create AudioWorkletNode:", err);
            throw err; // Re-throw to be caught by the outer try-catch
        }
        
        // Set up message handling from the worklet with more detailed logging
        audioWorkletNode.port.onmessage = (event) => {
            if (event.data.audioData) {
                audioChunks.push(event.data.audioData);
                // Log occasionally to avoid flooding the console
                if (audioChunks.length % 10 === 0) {
                    console.log(`Received audio chunk: ${audioChunks.length} chunks collected`);
                }
            } else {
                console.warn("Received message from worklet without audioData:", event.data);
            }
        };
        
        // Connect the nodes with error handling
        try {
            source.connect(audioWorkletNode);
            audioWorkletNode.connect(audioContext.destination);
            console.log("Audio processing pipeline connected");
        } catch (err) {
            console.error("Failed to connect audio nodes:", err);
            throw err;
        }
        
        // Set up interval to send audio chunks
        audioChunks = []; // Clear any previous chunks
        recordingInterval = setInterval(() => {
            if (audioChunks.length > 0) {
                sendAudioChunk();
            }
        }, intervalMs);
        
        isRecording = true;
        console.log("Recording started successfully");
        return true;
    } catch (error) {
        console.error('Error starting recording:', error);
        await stopRecording();
        return false;
    }
}

// Sends collected audio chunks to .NET
function sendAudioChunk() {
    try {
        // First check if we're still recording
        if (!isRecording) {
            console.log("Recording stopped, not sending audio chunks");
            audioChunks = []; // Clear any remaining chunks
            return;
        }
        
        if (!dotNetReference) {
            console.error("Cannot send audio chunk: dotNetReference is null or undefined");
            return;
        }
        
        if (audioChunks.length === 0) {
            console.warn("No audio chunks to send");
            return;
        }
        
        // Combine all chunks into a single Int16Array
        let totalLength = 0;
        for (const chunk of audioChunks) {
            totalLength += chunk.length;
        }
        
        const combinedChunk = new Int16Array(totalLength);
        let offset = 0;
        
        for (const chunk of audioChunks) {
            combinedChunk.set(chunk, offset);
            offset += chunk.length;
        }
        
        // Convert to Uint8Array for base64 encoding
        const uint8Array = new Uint8Array(combinedChunk.buffer);
        
        // Convert to base64
        let binary = '';
        for (let i = 0; i < uint8Array.length; i++) {
            binary += String.fromCharCode(uint8Array[i]);
        }
        const base64Data = btoa(binary);
        
        // Send to .NET with proper error handling
        dotNetReference.invokeMethodAsync('ReceiveAudioData', base64Data)
            .then(() => {
                // Successfully invoked the method                
            })
            .catch(error => {
                console.error("Failed to invoke ReceiveAudioData method:", error);
                // Try to log details about the dotNetReference
                console.error("dotNetReference details:", 
                    JSON.stringify({
                        type: typeof dotNetReference,
                        hasInvokeMethod: dotNetReference && typeof dotNetReference.invokeMethodAsync === 'function',
                        hasReceiveAudioData: dotNetReference && dotNetReference.__dotNetObject && typeof dotNetReference.__dotNetObject.ReceiveAudioData === 'function'
                    }));
            });
        
        // Clear the chunks
        audioChunks = [];
    } catch (error) {
        console.error('Error sending audio chunk:', error);
    }
}

async function stopRecording() {
    try {
        console.log("Stopping recording");
        
        // Clear the interval
        if (recordingInterval) {
            clearInterval(recordingInterval);
            recordingInterval = null;
        }
        
        // Send a stop message to the worklet processor
        if (audioWorkletNode) {
            try {
                // Tell the processor to stop
                audioWorkletNode.port.postMessage({ command: 'stop' });
                console.log("Sent stop command to audio processor");
                
                // Give it a moment to process the message
                await new Promise(resolve => setTimeout(resolve, 100));
                
                // Now disconnect
                audioWorkletNode.disconnect();
                console.log("Disconnected audioWorkletNode");
                audioWorkletNode = null;
            } catch (error) {
                console.error("Error stopping audio worklet:", error);
            }
        }
        
        // Stop all tracks in the media stream
        if (mediaStream) {
            mediaStream.getTracks().forEach(track => {
                track.stop();
                console.log("Stopped media track:", track.kind);
            });
            mediaStream = null;
        }
        
        // Clear other variables
        audioChunks = [];
        isRecording = false;
        
        console.log("Recording stopped successfully");
        return true;
    } catch (error) {
        console.error('Error stopping recording:', error);
        return false;
    }
}

/**
 * Plays a base64 encoded PCM 16-bit audio chunk
 * @param {string} base64Audio - Base64 encoded PCM 16-bit audio data
 * @param {number} sampleRate - Sample rate of the audio (default: 24000)
 * @returns {Promise<void>}
 */
async function playAudio(base64Audio, sampleRate = 24000) {
    try {
        // Validate input
        if (!base64Audio || base64Audio.length === 0) {            
            return Promise.resolve(); // Nothing to play
        }
        
        // Make sure we have an AudioContext
        if (!audioContext) {            
            audioContext = new (window.AudioContext || window.webkitAudioContext)();
        }
        
        // Resume the audio context if it's suspended
        if (audioContext.state === 'suspended') 
        {            
            await audioContext.resume();
        }

        // Decode the base64 string to binary data        
        let binaryString;
        try {
            binaryString = atob(base64Audio);
        } catch (e) {
            console.error('[playAudio] Failed to decode base64 data:', e);
            return Promise.reject(new Error('Invalid base64 data'));
        }
        
        const len = binaryString.length;        
        
        const bytes = new Uint8Array(len);
        for (let i = 0; i < len; i++) {
            bytes[i] = binaryString.charCodeAt(i);
        }
        
        // Convert to Int16Array (PCM 16-bit)
        const int16Array = new Int16Array(bytes.buffer);        
        
        // Convert Int16Array to Float32Array for Web Audio API
        const float32Array = new Float32Array(int16Array.length);
        for (let i = 0; i < int16Array.length; i++) {
            // Convert from Int16 (-32768 to 32767) to Float32 (-1.0 to 1.0)
            float32Array[i] = int16Array[i] / 32768.0;
        }
        
        // Create an audio buffer        
        const audioBuffer = audioContext.createBuffer(1, float32Array.length, sampleRate);
        
        // Fill the buffer with our audio data
        audioBuffer.getChannelData(0).set(float32Array);
        
        // Create a buffer source node
        const source = audioContext.createBufferSource();
        source.buffer = audioBuffer;
        
        // Connect to the audio output
        source.connect(audioContext.destination);
        
        // Add to active sources
        activeSources.push(source);        
        
        // Remove from active sources when done
        source.onended = () => {
            const index = activeSources.indexOf(source);
            if (index !== -1) {
                activeSources.splice(index, 1);                
            }
        };
        
        // Play the audio
        source.start();

        // Return a promise that resolves when the audio finishes playing
        return new Promise(resolve => {
            source.onended = () => {                
                resolve();
                // Make sure to remove from active sources
                const index = activeSources.indexOf(source);
                if (index !== -1) {
                    activeSources.splice(index, 1);
                }
            };
        });
    } catch (error) {
        throw new Error('Failed to play audio: ' + error.message);
    }
}

/**
 * Stops all currently playing audio
 * Used when the user interrupts the AI
 */
async function stopAudioPlayback() {
    try {
        console.log(`Stopping ${activeSources.length} active audio sources`);
        
        // Stop all active audio sources
        for (const source of activeSources) {
            try {
                source.stop();
            } catch (e) {
                console.warn("Error stopping audio source:", e);
            }
        }
        
        // Clear the array
        activeSources = [];        
        
        return true;
    } catch (error) {
        console.error('Error stopping audio playback:', error);
        return false;
    }
}

// Export the functions
export {
    initAudioPermissions,
    initAudioWithUserInteraction,
    ensureAudioContextResumed,
    startRecording,
    stopRecording,
    playAudio,
    stopAudioPlayback
}
