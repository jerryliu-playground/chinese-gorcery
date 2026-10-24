"""Synthesises the placeholder 8-bit sound effects for chinese-grocery (stdlib only, deterministic).

Run:  python3 tools/audio/make_sounds.py
Writes 16-bit mono WAVs to game/assets/audio/. Background music is not made here; drop a music.ogg into that
folder when there is one (the game stays silent without it).
"""
import math
import random
import struct
import wave
from pathlib import Path

RATE = 22050
OUT_DIR = Path(__file__).resolve().parents[2] / "game" / "assets" / "audio"


def square(f, t):
    return 1.0 if math.sin(2 * math.pi * f * t) >= 0 else -1.0


def sine(f, t):
    return math.sin(2 * math.pi * f * t)


def env(t, length, attack=0.005, release=None):
    """Fast attack, then decay to silence at the end."""
    if t < attack:
        return t / attack
    release = release or length
    return max(0.0, 1 - (t - attack) / max(1e-6, release - attack))


def tone(freqs, length, wave_fn=square, volume=0.35, slide=0.0):
    """One or more notes played back to back; slide bends pitch over each note (fraction per note)."""
    out = []
    per = length / len(freqs)
    for f in freqs:
        n = int(per * RATE)
        for i in range(n):
            t = i / RATE
            freq = f * (1 + slide * t / per)
            out.append(wave_fn(freq, t) * env(t, per) * volume)
    return out


def noise(length, volume=0.3, smooth=0.0, seed=1):
    rnd = random.Random(seed)
    out, prev = [], 0.0
    n = int(length * RATE)
    for i in range(n):
        v = rnd.uniform(-1, 1)
        prev = prev * smooth + v * (1 - smooth)
        out.append(prev * env(i / RATE, length) * volume)
    return out


def mix(*tracks):
    n = max(len(t) for t in tracks)
    return [sum(t[i] if i < len(t) else 0 for t in tracks) for i in range(n)]


def ambient_loop(length=4.5, fade_seconds=0.5):
    """Fridge hum plus soft murmur that loops seamlessly: the noise tail is crossfaded into its start, leaving a
    4 s loop in which the hum and the slow wobble complete whole cycles."""
    n = int(length * RATE)
    rnd = random.Random(7)
    murmur, prev = [], 0.0
    for _ in range(n):
        prev = prev * 0.97 + rnd.uniform(-1, 1) * 0.03
        murmur.append(prev)
    fade = int(fade_seconds * RATE)
    for i in range(fade):
        w = i / fade
        murmur[i] = murmur[i] * w + murmur[n - fade + i] * (1 - w)
    murmur = murmur[: n - fade]
    n = len(murmur)
    out = []
    for i in range(n):
        t = i / RATE
        hum = 0.05 * sine(60, t) + 0.03 * sine(120, t) + 0.01 * sine(180, t)
        wobble = 0.5 + 0.5 * sine(0.25, t)
        out.append(hum + murmur[i] * (2.5 + wobble))
    return out


def write(name, samples):
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    peak = max(1e-6, max(abs(s) for s in samples))
    scale = min(1.0, 0.95 / peak)
    with wave.open(str(OUT_DIR / f"{name}.wav"), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(b"".join(struct.pack("<h", int(max(-1, min(1, s * scale)) * 32767)) for s in samples))


def main():
    sounds = {
        "cash_ding": mix(tone([1568, 2093], 0.35, sine, 0.5), tone([3136], 0.2, square, 0.08)),
        "shelve": mix(tone([180], 0.1, square, 0.25, slide=-0.4), noise(0.08, 0.15, smooth=0.6, seed=2)),
        "footstep": noise(0.05, 0.25, smooth=0.8, seed=3),
        "chop": mix(noise(0.12, 0.5, smooth=0.3, seed=4), tone([90], 0.12, square, 0.3, slide=-0.5)),
        "door_bell": tone([1319, 1047], 0.6, sine, 0.45),
        "angry": tone([330, 247], 0.35, square, 0.3, slide=-0.15),
        "celebrate": tone([523, 659, 784, 1047], 0.6, square, 0.3),
        "ui_click": tone([1200], 0.04, square, 0.2),
        "ambient_shop": ambient_loop(),
    }
    for name, samples in sounds.items():
        write(name, samples)
    print(f"{len(sounds)} sounds written to {OUT_DIR}")


if __name__ == "__main__":
    main()
