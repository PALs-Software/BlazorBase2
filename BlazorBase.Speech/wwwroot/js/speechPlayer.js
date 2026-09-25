let audioContext = null;
let currentSource = null;
let currentResolve = null;

function ensureContext() {
    if (audioContext)
        return audioContext;

    const AudioContextType = window.AudioContext || window.webkitAudioContext;
    audioContext = new AudioContextType();
    return audioContext;
}

export function primePlayback() {
    const context = ensureContext();
    if (context.state === 'suspended')
        context.resume();

    const silence = context.createBuffer(1, 1, 22050);
    const source = context.createBufferSource();
    source.buffer = silence;
    source.connect(context.destination);
    source.start(0);
}

export async function play(streamReference) {
    const data = await streamReference.arrayBuffer();
    stop();

    const context = ensureContext();
    if (context.state === 'suspended')
        await context.resume();

    const buffer = await context.decodeAudioData(data);

    return await new Promise(resolve => {
        const source = context.createBufferSource();
        source.buffer = buffer;
        source.connect(context.destination);
        source.onended = () => {
            if (currentSource !== source)
                return;

            currentSource = null;
            currentResolve = null;
            resolve(true);
        };

        currentSource = source;
        currentResolve = resolve;
        source.start();
    });
}

export function stop() {
    if (!currentSource)
        return;

    const source = currentSource;
    const resolve = currentResolve;
    currentSource = null;
    currentResolve = null;
    source.onended = null;

    try {
        source.stop();
    } catch {
    }

    if (resolve)
        resolve(false);
}
