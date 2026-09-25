const BatchSize = 2048;

class PcmCaptureProcessor extends AudioWorkletProcessor {
    constructor() {
        super();
        this.batch = new Float32Array(BatchSize);
        this.filled = 0;
    }

    process(inputs) {
        const channel = inputs[0] && inputs[0][0];
        if (!channel)
            return true;

        let offset = 0;
        while (offset < channel.length) {
            const count = Math.min(channel.length - offset, BatchSize - this.filled);
            this.batch.set(channel.subarray(offset, offset + count), this.filled);
            this.filled += count;
            offset += count;

            if (this.filled === BatchSize) {
                this.port.postMessage(this.batch.slice(0));
                this.filled = 0;
            }
        }

        return true;
    }
}

registerProcessor('blazorbase-pcm-capture', PcmCaptureProcessor);
