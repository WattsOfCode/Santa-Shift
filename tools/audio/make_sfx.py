"""
Synthesises every Santa Shift sound effect (GDD section 17) as a 16-bit mono 44.1 kHz WAV.

No samples or outside assets: each sound is built from sine waves, filtered noise and envelopes below, so they are
original work of the project (all rights reserved, see LICENSE.txt). Python 3.8+, standard library only.

    py tools/audio/make_sfx.py            # writes every sound to src/SantaShift.Game/Content/audio/
    py tools/audio/make_sfx.py hammer_hit # only the named ones

Each sound has a fixed random seed, so running it again gives the same files. To change a sound, edit its function
and run the script; or replace the .wav with your own (same name) and record it in ASSET_CREDITS.md.
"""
import math
import os
import random
import struct
import sys
import wave

RATE = 44100
OUT = os.path.join(os.path.dirname(__file__), "..", "..", "src", "SantaShift.Game", "Content", "audio")


# ---------------------------------------------------------------- building blocks

def silence(seconds):
    return [0.0] * int(seconds * RATE)


def mix_into(dst, src, at=0.0, gain=1.0):
    """Adds src into dst starting at `at` seconds, growing dst if needed."""
    start = int(at * RATE)
    if start + len(src) > len(dst):
        dst.extend([0.0] * (start + len(src) - len(dst)))
    for i, v in enumerate(src):
        dst[start + i] += v * gain
    return dst


def tone(freq, seconds, decay=None, attack=0.003, wave_kind="sine", glide_to=None, vibrato=(0, 0), phase=0.0):
    """One voice. freq may glide (exponentially) to glide_to; decay is an exponential time constant in seconds,
    or None for a flat level (shape it with env())."""
    n = int(seconds * RATE)
    out = []
    ph = phase
    vib_rate, vib_depth = vibrato
    for i in range(n):
        t = i / RATE
        f = freq if glide_to is None else freq * (glide_to / freq) ** (t / seconds)
        if vib_depth:
            f *= 1 + vib_depth * math.sin(2 * math.pi * vib_rate * t)
        ph += 2 * math.pi * f / RATE
        if wave_kind == "sine":
            s = math.sin(ph)
        elif wave_kind == "triangle":
            s = 2 / math.pi * math.asin(math.sin(ph))
        else:  # soft square: a few odd harmonics
            s = math.sin(ph) + math.sin(3 * ph) / 3 + math.sin(5 * ph) / 5
        a = min(1.0, t / attack) if attack > 0 else 1.0
        if decay is not None:
            a *= math.exp(-t / decay)
        out.append(s * a)
    return out


def bell(freq, seconds, decay=0.25, bright=1.0):
    """Struck bell / glockenspiel: inharmonic partials, the high ones dying first."""
    partials = [(1.0, 1.0, 1.0), (2.76, 0.45 * bright, 0.55), (5.40, 0.25 * bright, 0.35), (8.93, 0.12 * bright, 0.2)]
    out = silence(seconds)
    for ratio, amp, life in partials:
        mix_into(out, tone(freq * ratio, seconds, decay=decay * life, attack=0.001), gain=amp)
    return out


def marimba(freq, seconds, decay=0.12):
    """Wooden bar: fundamental plus a quick 4x partial."""
    out = tone(freq, seconds, decay=decay, attack=0.001)
    mix_into(out, tone(freq * 3.93, seconds, decay=decay * 0.25, attack=0.001), gain=0.35)
    return out


def noise(seconds, rng):
    return [rng.uniform(-1, 1) for _ in range(int(seconds * RATE))]


def env(samples, points):
    """Piecewise-linear envelope: points are (seconds, level)."""
    out = []
    for i, v in enumerate(samples):
        t = i / RATE
        level = points[-1][1]
        for (t0, l0), (t1, l1) in zip(points, points[1:]):
            if t0 <= t <= t1:
                level = l0 + (l1 - l0) * ((t - t0) / (t1 - t0) if t1 > t0 else 1)
                break
        out.append(v * level)
    return out


def decay_env(samples, tau, attack=0.002):
    return [v * min(1.0, (i / RATE) / attack) * math.exp(-(i / RATE) / tau) for i, v in enumerate(samples)]


def biquad(samples, kind, freq, q=0.707, freq_to=None):
    """RBJ biquad (lowpass / highpass / bandpass); the centre may sweep exponentially to freq_to."""
    out = []
    x1 = x2 = y1 = y2 = 0.0
    n = max(1, len(samples))
    for i, x in enumerate(samples):
        f = freq if freq_to is None else freq * (freq_to / freq) ** (i / n)
        w = 2 * math.pi * min(f, RATE * 0.45) / RATE
        cw, sw = math.cos(w), math.sin(w)
        alpha = sw / (2 * q)
        if kind == "lowpass":
            b0, b1, b2 = (1 - cw) / 2, 1 - cw, (1 - cw) / 2
        elif kind == "highpass":
            b0, b1, b2 = (1 + cw) / 2, -(1 + cw), (1 + cw) / 2
        else:  # bandpass, 0 dB peak
            b0, b1, b2 = alpha, 0.0, -alpha
        a0, a1, a2 = 1 + alpha, -2 * cw, 1 - alpha
        y = (b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2) / a0
        x2, x1, y2, y1 = x1, x, y1, y
        out.append(y)
    return out


def click(rng, seconds=0.012, centre=2500, q=1.5):
    """A short tick of band-passed noise."""
    return decay_env(biquad(noise(seconds, rng), "bandpass", centre, q), seconds / 4, attack=0.0005)


def jingle(rng, seconds, density, start=0.0, rising=False):
    """Sleigh bells: many tiny high bells at random times. density = hits per second (doubling over time if rising)."""
    out = silence(seconds)
    t = start
    while t < seconds - 0.05:
        f = rng.uniform(4200, 7200)
        mix_into(out, bell(f, 0.08, decay=0.03, bright=0.4), at=t, gain=rng.uniform(0.25, 0.5))
        d = density * (1 + 2 * t / seconds) if rising else density
        t += rng.expovariate(d)
    return biquad(out, "highpass", 2500)


def note(name):
    """'C5' -> Hz (A4 = 440)."""
    names = {"C": -9, "D": -7, "E": -5, "F": -4, "G": -2, "A": 0, "B": 2}
    semis = names[name[0]] + (1 if "#" in name else 0) + 12 * (int(name[-1]) - 4)
    return 440.0 * 2 ** (semis / 12)


def finish(samples, peak):
    """Removes DC, fades the ends (no clicks) and normalises to `peak` (0..1 of full scale)."""
    if not samples:
        return samples
    mean = sum(samples) / len(samples)
    s = [v - mean for v in samples]
    fade_in, fade_out = int(0.001 * RATE), int(0.012 * RATE)
    for i in range(min(fade_in, len(s))):
        s[i] *= i / fade_in
    for i in range(min(fade_out, len(s))):
        s[-1 - i] *= i / fade_out
    top = max(abs(v) for v in s) or 1.0
    return [v * peak / top for v in s]


# ---------------------------------------------------------------- the sounds (peaks set the relative loudness)

def ui_confirm(rng):
    out = click(rng, 0.03, 1800, 2.0)
    mix_into(out, tone(950, 0.06, decay=0.012, attack=0.0005), gain=0.6)
    return finish(out, 0.45)


def ui_move(rng):
    return finish(click(rng, 0.02, 3600, 3.0), 0.25)


def kit_pickup(rng):
    lift = env(biquad(noise(0.14, rng), "lowpass", 900), [(0, 0), (0.03, 1), (0.14, 0)])
    for at in (0.04, 0.075, 0.1):  # parts rattling inside
        mix_into(lift, click(rng, 0.01, rng.uniform(2500, 3500), 4), at=at, gain=0.5)
    mix_into(lift, tone(160, 0.08, decay=0.02), gain=0.4)
    return finish(lift, 0.55)


def hammer_hit(rng):  # pitched up per part by the game
    out = tone(620, 0.1, decay=0.025, attack=0.0005, glide_to=520)
    mix_into(out, tone(1530, 0.1, decay=0.008, attack=0.0005), gain=0.4)
    mix_into(out, click(rng, 0.01, 3000, 1.0), gain=0.7)
    return finish(out, 0.6)


def assembly_complete(rng):
    out = silence(0.45)
    for i, n in enumerate(("C5", "E5", "G5")):
        mix_into(out, marimba(note(n), 0.3, decay=0.14 if i == 2 else 0.08), at=i * 0.07)
    mix_into(out, bell(note("C7"), 0.3, decay=0.12), at=0.14, gain=0.18)
    return finish(out, 0.7)


def assembly_botched(rng):
    out = tone(320, 0.38, decay=0.16, glide_to=120, vibrato=(16, 0.08), wave_kind="triangle")
    for at in (0.03, 0.07, 0.12, 0.2):  # bits falling off
        mix_into(out, click(rng, 0.02, rng.uniform(1200, 2600), 2), at=at, gain=0.45)
    return finish(out, 0.6)


def ribbon_pick(rng):
    out = tone(1700, 0.1, attack=0.01, glide_to=2400, vibrato=(40, 0.02))
    return finish(env(out, [(0, 0), (0.015, 1), (0.06, 0.7), (0.1, 0)]), 0.2)


def ribbon_complete(rng):
    swish = env(biquad(noise(0.18, rng), "bandpass", 1500, 1.2, freq_to=6000), [(0, 0), (0.08, 1), (0.18, 0)])
    mix_into(swish, bell(note("G6"), 0.3, decay=0.15), at=0.12, gain=0.45)
    return finish(swish, 0.5)


def coal_shovel(rng):
    out = env(biquad(noise(0.3, rng), "lowpass", 2200), [(0, 0), (0.05, 0.5), (0.22, 0.4), (0.3, 0)])
    t = 0.02
    while t < 0.27:  # grit
        mix_into(out, click(rng, 0.008, rng.uniform(1500, 4000), 3), at=t, gain=rng.uniform(0.4, 1.0))
        t += rng.uniform(0.008, 0.02)
    mix_into(out, tone(110, 0.1, decay=0.03), at=0.02, gain=0.5)
    return finish(out, 0.5)


def crate_pickup(rng):
    out = tone(95, 0.2, decay=0.05, glide_to=70)
    mix_into(out, decay_env(biquad(noise(0.2, rng), "lowpass", 700), 0.04), gain=0.8)
    mix_into(out, click(rng, 0.015, 1400, 2), gain=0.4)
    return finish(out, 0.65)


def bin_restock(rng):
    out = silence(0.4)
    for i, at in enumerate((0.0, 0.07, 0.12, 0.2, 0.26)):
        knock = tone(rng.uniform(180, 260), 0.1, decay=0.03)
        mix_into(knock, decay_env(biquad(noise(0.1, rng), "lowpass", 1200), 0.02), gain=0.7)
        mix_into(out, knock, at=at, gain=1.0 - i * 0.13)
    return finish(out, 0.6)


def delivery_success(rng):
    whoosh = env(biquad(noise(0.35, rng), "bandpass", 500, 0.9, freq_to=3500), [(0, 0), (0.15, 1), (0.35, 0)])
    out = mix_into(silence(0.6), whoosh, gain=0.7)
    mix_into(out, jingle(rng, 0.45, 40), at=0.1, gain=0.7)
    mix_into(out, bell(note("E6"), 0.4, decay=0.15), at=0.18, gain=0.4)
    mix_into(out, bell(note("B6"), 0.35, decay=0.13), at=0.26, gain=0.35)
    return finish(out, 0.7)


def coal_delivered(rng):
    out = tone(80, 0.25, decay=0.06)
    mix_into(out, decay_env(biquad(noise(0.25, rng), "lowpass", 500), 0.05), gain=0.8)
    mix_into(out, env(tone(170, 0.25, glide_to=95, wave_kind="square"), [(0, 0), (0.03, 0.6), (0.25, 0)]), at=0.08, gain=0.5)
    return finish(biquad(out, "lowpass", 2500), 0.65)


def action_rejected(rng):
    out = silence(0.2)
    mix_into(out, env(tone(330, 0.07, wave_kind="triangle"), [(0, 0), (0.005, 1), (0.05, 0.8), (0.07, 0)]))
    mix_into(out, env(tone(262, 0.08, wave_kind="triangle"), [(0, 0), (0.005, 1), (0.06, 0.8), (0.08, 0)]), at=0.09)
    return finish(out, 0.4)


def recycle(rng):
    out = silence(0.3)
    for f, amp in ((170, 1.0), (415, 0.5), (690, 0.3), (1130, 0.15)):  # barrel
        mix_into(out, tone(f, 0.25, decay=0.07, attack=0.0005), gain=amp)
    t = 0.04
    while t < 0.2:  # crunch
        mix_into(out, click(rng, 0.01, rng.uniform(800, 2500), 2), at=t, gain=rng.uniform(0.3, 0.7))
        t += rng.uniform(0.01, 0.025)
    return finish(out, 0.55)


def order_new(rng):
    paper = biquad(noise(0.22, rng), "highpass", 2500)
    flutter = [v * (0.5 + 0.5 * math.sin(2 * math.pi * 32 * i / RATE)) ** 2 for i, v in enumerate(paper)]
    return finish(env(flutter, [(0, 0), (0.04, 1), (0.22, 0)]), 0.3)


def order_expired(rng):
    out = silence(0.4)
    mix_into(out, decay_env(tone(note("G4"), 0.16, wave_kind="triangle"), 0.08))
    mix_into(out, decay_env(tone(note("D4"), 0.25, wave_kind="triangle"), 0.12), at=0.13)
    return finish(out, 0.45)


def policy_chime(rng):
    out = silence(0.8)
    mix_into(out, bell(note("E5"), 0.5, decay=0.3, bright=0.6))
    mix_into(out, bell(note("C5"), 0.6, decay=0.35, bright=0.6), at=0.2)
    return finish(out, 0.6)


def clock_tick(rng):
    return finish(click(rng, 0.025, 4200, 4), 0.2)


def shift_end(rng):
    whistle = tone(1080, 0.85, glide_to=1180, vibrato=(5.5, 0.006))
    mix_into(whistle, tone(2160, 0.85, glide_to=2360), gain=0.25)
    mix_into(whistle, biquad(noise(0.85, rng), "bandpass", 1150, 3), gain=0.6)  # breath
    return finish(env(whistle, [(0, 0), (0.06, 1), (0.65, 0.9), (0.85, 0)]), 0.36)


def punch_clock(rng):
    out = silence(0.4)
    for at, f in ((0.0, 1800), (0.09, 900)):  # ka- chunk
        clunk = click(rng, 0.03, f, 1.5)
        mix_into(clunk, tone(140 if at else 220, 0.08, decay=0.02), gain=0.7)
        mix_into(out, clunk, at=at)
    mix_into(out, bell(note("C7"), 0.25, decay=0.08), at=0.13, gain=0.3)
    return finish(out, 0.6)


def round_clear(rng):
    out = silence(1.1)
    for i, n in enumerate(("C5", "E5", "G5")):
        mix_into(out, marimba(note(n), 0.25, decay=0.09), at=i * 0.11)
        mix_into(out, bell(note(n) * 2, 0.25, decay=0.1), at=i * 0.11, gain=0.25)
    mix_into(out, marimba(note("C6"), 0.7, decay=0.3), at=0.36)
    mix_into(out, bell(note("C6"), 0.7, decay=0.35), at=0.36, gain=0.5)
    mix_into(out, jingle(rng, 0.8, 35), at=0.3, gain=0.6)
    return finish(out, 0.75)


def run_over(rng):
    out = tone(880, 0.8, glide_to=240, vibrato=(6, 0.015))
    mix_into(out, biquad(noise(0.8, rng), "bandpass", 900, 2, freq_to=260), gain=0.3)
    return finish(env(out, [(0, 0), (0.04, 1), (0.6, 0.8), (0.8, 0)]), 0.32)


def perk_pick(rng):
    flick = env(biquad(noise(0.06, rng), "bandpass", 3000, 1.0), [(0, 0), (0.01, 1), (0.06, 0)])
    out = mix_into(silence(0.5), flick, gain=0.5)
    mix_into(out, bell(note("A5"), 0.45, decay=0.2, bright=0.5), at=0.05)
    mix_into(out, marimba(note("A4"), 0.3, decay=0.12), at=0.05, gain=0.5)
    return finish(out, 0.6)


def unlock(rng):
    out = silence(0.75)
    for i, n in enumerate(("G5", "A5", "C6", "D6", "E6", "G6")):  # music box: soft, high, tinkly
        mix_into(out, bell(note(n), 0.35, decay=0.18, bright=0.3), at=i * 0.065, gain=0.8 - i * 0.05)
    return finish(out, 0.55)


def jolly_on(rng):
    out = jingle(rng, 0.5, 30, rising=True)
    for i, n in enumerate(("D6", "G6")):
        mix_into(out, bell(note(n), 0.3, decay=0.14, bright=0.4), at=0.12 + i * 0.12, gain=0.45)
    return finish(out, 0.55)


def spirit_low(rng):
    wind = biquad(noise(0.7, rng), "bandpass", 300, 4, freq_to=650)
    wind = [v * (1 + 0.3 * math.sin(2 * math.pi * 3 * i / RATE)) for i, v in enumerate(wind)]
    return finish(env(wind, [(0, 0), (0.25, 1), (0.45, 0.7), (0.7, 0)]), 0.4)


def make_footstep(variant):
    def footstep(rng):
        out = tone(110 + variant * 12, 0.08, decay=0.018, attack=0.002)
        mix_into(out, decay_env(biquad(noise(0.08, rng), "lowpass", 900 + variant * 150), 0.015, attack=0.003), gain=0.9)
        return finish(out, 0.28)
    footstep.__name__ = "footstep_%d" % (variant + 1)
    return footstep


SOUNDS = [ui_confirm, ui_move, kit_pickup, hammer_hit, assembly_complete, assembly_botched, ribbon_pick, ribbon_complete,
          coal_shovel, crate_pickup, bin_restock, delivery_success, coal_delivered, action_rejected, recycle, order_new,
          order_expired, policy_chime, clock_tick, shift_end, punch_clock, round_clear, run_over, perk_pick, unlock,
          jolly_on, spirit_low] + [make_footstep(v) for v in range(4)]


def write_wav(path, samples):
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(b"".join(struct.pack("<h", int(max(-1.0, min(1.0, v)) * 32767)) for v in samples))


def main(names):
    os.makedirs(OUT, exist_ok=True)
    unknown = set(names) - {s.__name__ for s in SOUNDS}
    if unknown:
        sys.exit("unknown sound(s): " + ", ".join(sorted(unknown)))
    for i, make in enumerate(SOUNDS):
        if names and make.__name__ not in names:
            continue
        samples = make(random.Random(1000 + i))
        path = os.path.join(OUT, make.__name__ + ".wav")
        write_wav(path, samples)
        print("%-18s %.2f s" % (make.__name__, len(samples) / RATE))


if __name__ == "__main__":
    main(sys.argv[1:])
