import { primePlayback } from './speechPlayer.js';

const TargetSampleRate = 16000;
const LevelGain = 8;
const recorders = new WeakMap();

export function attach(element, callbackTarget, settings) {
    detach(element);

    const recorder = {
        element,
        callbackTarget,
        settings,
        phase: 'idle',
        releaseRequested: false,
        activePointerId: null,
        stream: null,
        context: null,
        source: null,
        worklet: null,
        chunks: [],
        sampleCount: 0,
        sampleRate: TargetSampleRate,
        stopTimer: 0,
        pendingRecording: null,
        handlers: null
    };

    recorder.handlers = {
        pointerdown: event => onPointerDown(recorder, event),
        pointerup: event => onPointerUp(recorder, event),
        pointercancel: event => onPointerUp(recorder, event),
        lostpointercapture: event => onPointerUp(recorder, event),
        keydown: event => onKeyDown(recorder, event),
        keyup: event => onKeyUp(recorder, event),
        blur: () => release(recorder),
        contextmenu: event => event.preventDefault()
    };

    for (const [type, handler] of Object.entries(recorder.handlers))
        element.addEventListener(type, handler);

    recorders.set(element, recorder);
}

export function detach(element) {
    const recorder = element ? recorders.get(element) : null;
    if (!recorder)
        return;

    for (const [type, handler] of Object.entries(recorder.handlers))
        element.removeEventListener(type, handler);

    clearTimeout(recorder.stopTimer);
    releaseMicrophone(recorder);
    recorders.delete(element);
}

export function takeRecording(element) {
    const recorder = recorders.get(element);
    const recording = recorder ? recorder.pendingRecording : null;

    if (!recording)
        throw new Error('No recording is pending.');

    recorder.pendingRecording = null;
    return recording;
}

function onPointerDown(recorder, event) {
    if (event.button !== 0 || recorder.phase !== 'idle' || recorder.element.disabled)
        return;

    event.preventDefault();
    recorder.activePointerId = event.pointerId;

    try {
        recorder.element.setPointerCapture(event.pointerId);
    } catch {
    }

    press(recorder);
}

function onPointerUp(recorder, event) {
    if (recorder.activePointerId !== event.pointerId)
        return;

    recorder.activePointerId = null;
    release(recorder);
}

function isTalkKey(event) {
    return event.key === ' ' || event.key === 'Enter';
}

function onKeyDown(recorder, event) {
    if (!isTalkKey(event))
        return;

    event.preventDefault();

    if (event.repeat || recorder.phase !== 'idle' || recorder.element.disabled)
        return;

    press(recorder);
}

function onKeyUp(recorder, event) {
    if (!isTalkKey(event))
        return;

    event.preventDefault();
    release(recorder);
}

async function press(recorder) {
    recorder.phase = 'starting';
    recorder.releaseRequested = false;
    recorder.chunks = [];
    recorder.sampleCount = 0;
    recorder.pendingRecording = null;

    try {
        primePlayback();
    } catch {
    }

    if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia || !window.AudioWorkletNode) {
        fail(recorder, 'Unsupported');
        return;
    }

    try {
        await openMicrophone(recorder);
    } catch (error) {
        fail(recorder, classify(error));
        return;
    }

    if (recorder.releaseRequested) {
        discard(recorder);
        return;
    }

    recorder.phase = 'recording';
    recorder.stopTimer = setTimeout(() => release(recorder), recorder.settings.maximumDurationMilliseconds);
    recorder.callbackTarget.invokeMethodAsync('NotifyRecordingStarted');
}

async function openMicrophone(recorder) {
    recorder.stream = await navigator.mediaDevices.getUserMedia({
        audio: {
            channelCount: 1,
            echoCancellation: true,
            noiseSuppression: true,
            autoGainControl: true
        }
    });

    const AudioContextType = window.AudioContext || window.webkitAudioContext;
    recorder.context = new AudioContextType();
    recorder.sampleRate = recorder.context.sampleRate;

    await recorder.context.audioWorklet.addModule(new URL('./pcmCaptureProcessor.js', import.meta.url));

    recorder.source = recorder.context.createMediaStreamSource(recorder.stream);
    recorder.worklet = new AudioWorkletNode(recorder.context, 'blazorbase-pcm-capture');
    recorder.worklet.port.onmessage = message => collect(recorder, message.data);
    recorder.source.connect(recorder.worklet);
    recorder.worklet.connect(recorder.context.destination);

    if (recorder.context.state === 'suspended')
        await recorder.context.resume();
}

function collect(recorder, samples) {
    recorder.chunks.push(samples);
    recorder.sampleCount += samples.length;

    let sumOfSquares = 0;
    for (const sample of samples)
        sumOfSquares += sample * sample;

    const level = Math.min(1, Math.sqrt(sumOfSquares / samples.length) * LevelGain);
    recorder.element.style.setProperty('--speech-level', level.toFixed(3));
}

function release(recorder) {
    if (recorder.phase === 'starting') {
        recorder.releaseRequested = true;
        return;
    }

    if (recorder.phase !== 'recording')
        return;

    clearTimeout(recorder.stopTimer);
    recorder.phase = 'idle';

    const chunks = recorder.chunks;
    const sampleCount = recorder.sampleCount;
    const sampleRate = recorder.sampleRate;
    const durationMilliseconds = Math.round(sampleCount / sampleRate * 1000);

    releaseMicrophone(recorder);
    recorder.chunks = [];

    if (durationMilliseconds < recorder.settings.minimumDurationMilliseconds) {
        recorder.callbackTarget.invokeMethodAsync('NotifyRecordingDiscarded');
        return;
    }

    const outputRate = Math.min(sampleRate, TargetSampleRate);
    const samples = downsample(merge(chunks, sampleCount), sampleRate, outputRate);
    recorder.pendingRecording = encodeWav(samples, outputRate);
    recorder.callbackTarget.invokeMethodAsync('NotifyRecordingCompleted', durationMilliseconds);
}

function discard(recorder) {
    releaseMicrophone(recorder);
    recorder.phase = 'idle';
    recorder.chunks = [];
    recorder.callbackTarget.invokeMethodAsync('NotifyRecordingDiscarded');
}

function fail(recorder, error) {
    releaseMicrophone(recorder);
    recorder.phase = 'idle';
    recorder.chunks = [];
    recorder.callbackTarget.invokeMethodAsync('NotifyRecordingFailed', error);
}

function classify(error) {
    const name = error && error.name;

    if (name === 'NotAllowedError' || name === 'SecurityError')
        return 'PermissionDenied';

    if (name === 'NotFoundError' || name === 'OverconstrainedError')
        return 'NoMicrophone';

    if (name === 'NotSupportedError')
        return 'Unsupported';

    return 'Failed';
}

function releaseMicrophone(recorder) {
    recorder.element.style.removeProperty('--speech-level');

    if (recorder.worklet) {
        recorder.worklet.port.onmessage = null;
        recorder.worklet.disconnect();
        recorder.worklet = null;
    }

    if (recorder.source) {
        recorder.source.disconnect();
        recorder.source = null;
    }

    if (recorder.stream) {
        for (const track of recorder.stream.getTracks())
            track.stop();

        recorder.stream = null;
    }

    if (recorder.context) {
        recorder.context.close();
        recorder.context = null;
    }
}

function merge(chunks, sampleCount) {
    const samples = new Float32Array(sampleCount);
    let offset = 0;

    for (const chunk of chunks) {
        samples.set(chunk, offset);
        offset += chunk.length;
    }

    return samples;
}

function downsample(samples, sourceRate, targetRate) {
    if (sourceRate === targetRate)
        return samples;

    const ratio = sourceRate / targetRate;
    const length = Math.floor(samples.length / ratio);
    const result = new Float32Array(length);

    for (let index = 0; index < length; index++) {
        const start = Math.floor(index * ratio);
        const end = Math.min(samples.length, Math.floor((index + 1) * ratio));
        let sum = 0;

        for (let position = start; position < end; position++)
            sum += samples[position];

        result[index] = end > start ? sum / (end - start) : samples[start];
    }

    return result;
}

function encodeWav(samples, sampleRate) {
    const bytesPerSample = 2;
    const dataLength = samples.length * bytesPerSample;
    const buffer = new ArrayBuffer(44 + dataLength);
    const view = new DataView(buffer);

    writeAscii(view, 0, 'RIFF');
    view.setUint32(4, 36 + dataLength, true);
    writeAscii(view, 8, 'WAVE');
    writeAscii(view, 12, 'fmt ');
    view.setUint32(16, 16, true);
    view.setUint16(20, 1, true);
    view.setUint16(22, 1, true);
    view.setUint32(24, sampleRate, true);
    view.setUint32(28, sampleRate * bytesPerSample, true);
    view.setUint16(32, bytesPerSample, true);
    view.setUint16(34, 16, true);
    writeAscii(view, 36, 'data');
    view.setUint32(40, dataLength, true);

    let offset = 44;
    for (const sample of samples) {
        const clamped = Math.max(-1, Math.min(1, sample));
        view.setInt16(offset, clamped < 0 ? clamped * 0x8000 : clamped * 0x7fff, true);
        offset += bytesPerSample;
    }

    return new Uint8Array(buffer);
}

function writeAscii(view, offset, text) {
    for (let index = 0; index < text.length; index++)
        view.setUint8(offset + index, text.charCodeAt(index));
}
