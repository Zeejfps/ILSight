# Edits a recorded mouse press and release into the click of the trailer: cuts both ticks out of the
# recording, closes the second of silence between them, makes the press the main hit, softens the
# release and rolls off the harsh top. Run from the trailer folder, with the recording as argument.
import subprocess
import sys
import wave

import numpy as np

RATE = 44100
PEAK = 0.7
PRESS_MS = (146, 215)
RELEASE_MS = (1121, 1190)
RELEASE_AT_MS = 80
RELEASE_LEVEL = 0.4
LOWPASS_HZ = 4000
HIGHPASS_HZ = 150
EDGE_FADE_MS = 3


def load(path):
    filters = f'highpass=f={HIGHPASS_HZ},lowpass=f={LOWPASS_HZ},lowpass=f={LOWPASS_HZ}'
    raw = subprocess.run(
        ['ffmpeg', '-v', 'error', '-i', path, '-af', filters, '-ac', '1', '-ar', str(RATE),
         '-f', 's16le', '-'],
        capture_output=True, check=True).stdout
    return np.frombuffer(raw, dtype=np.int16).astype(float) / 32768


def tick(sound, span):
    start, end = (int(RATE * ms / 1000) for ms in span)
    part = sound[start:end].copy()
    fade = int(RATE * EDGE_FADE_MS / 1000)
    part[:fade] *= np.linspace(0, 1, fade)
    part[-fade * 4:] *= np.linspace(1, 0, fade * 4)
    return part / np.abs(part).max()


source = load(sys.argv[1])
press = tick(source, PRESS_MS)
release = tick(source, RELEASE_MS) * RELEASE_LEVEL
at = int(RATE * RELEASE_AT_MS / 1000)
click = np.zeros(at + len(release))
click[: len(press)] += press
click[at:] += release
click = click / np.abs(click).max() * PEAK
with wave.open('public/audio/mouse-click.wav', 'wb') as out:
    out.setnchannels(1)
    out.setsampwidth(2)
    out.setframerate(RATE)
    out.writeframes((click * 32767).astype(np.int16).tobytes())
