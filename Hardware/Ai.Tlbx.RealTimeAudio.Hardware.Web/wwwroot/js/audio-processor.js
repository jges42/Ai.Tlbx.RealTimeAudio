// audio-processor.js
class AudioRecorderProcessor extends AudioWorkletProcessor {
    constructor() {
        super();
        // Create a buffer to store audio data (4096 samples)
        this.buffer = new Int16Array(4096);
        this.bufferIndex = 0;
        this.isActive = true;
        
        // Listen for messages from the main thread
        this.port.onmessage = (event) => {
            if (event.data?.command === 'stop') {
                console.log("[AudioProcessor] Received stop command");
                this.isActive = false;
            }
        };
    }

    process(inputs, outputs) {
        // If we've been told to stop, return false to terminate the processor
        if (!this.isActive) {
            console.log("[AudioProcessor] Stopping processor");
            return false;
        }
        
        // Get the first channel of the first input
        const input = inputs[0]?.[0];
        
        if (!input || input.length === 0) {
            // No input data, continue processing
            return true;
        }

        // Process the audio data
        for (let i = 0; i < input.length; i++) {
            // Convert float [-1, 1] to 16-bit PCM [-32768, 32767]
            const sample = Math.max(-1, Math.min(1, input[i]));
            const pcmValue = sample < 0 ? sample * 32768 : sample * 32767;
            
            // Store in buffer
            this.buffer[this.bufferIndex++] = Math.floor(pcmValue);
            
            // When buffer is full, send it to the main thread
            if (this.bufferIndex >= this.buffer.length) {
                this.port.postMessage({
                    audioData: this.buffer.slice(0)  // Send a copy of the buffer
                });
                
                // Reset buffer index
                this.bufferIndex = 0;
            }
        }
        
        // Keep processor alive
        return true;
    }
}

registerProcessor('audio-recorder-processor', AudioRecorderProcessor);
