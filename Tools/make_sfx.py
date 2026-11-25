# Synthesises Shape-Shifty's placeholder sound effects as 16-bit mono WAVs.
# Usage: python make_sfx.py <output dir>
import math, random, struct, sys, wave, os

SR = 22050
OUT = sys.argv[1]
TAU = 2 * math.pi


def n(sec):
    return int(sec * SR)


def silence(sec):
    return [0.0] * n(sec)


def mix(dst, src, at=0.0, gain=1.0):
    o = n(at)
    need = o + len(src)
    if need > len(dst):
        dst.extend([0.0] * (need - len(dst)))
    for i, v in enumerate(src):
        dst[o + i] += v * gain
    return dst


def osc(sec, freq, shape="sine", harmonics=None):
    """freq: number or function of t (seconds)."""
    out, phase = [], 0.0
    f = freq if callable(freq) else (lambda t, c=freq: c)
    for i in range(n(sec)):
        t = i / SR
        phase += f(t) / SR
        p = phase % 1.0
        if shape == "sine":
            v = math.sin(TAU * phase)
        elif shape == "square":
            v = 1.0 if p < 0.5 else -1.0
        elif shape == "saw":
            v = 2 * p - 1
        elif shape == "tri":
            v = 4 * abs(p - 0.5) - 1
        if harmonics:
            v = math.sin(TAU * phase) + sum(a * math.sin(TAU * phase * k) for k, a in harmonics)
        out.append(v)
    return out


def noise(sec, seed=None):
    r = random.Random(seed)
    return [r.uniform(-1, 1) for _ in range(n(sec))]


def env(sig, attack=0.005, decay=0.1, hold=0.0):
    """Linear attack, optional hold, then exponential decay (time constant `decay`)."""
    out = []
    for i, v in enumerate(sig):
        t = i / SR
        if t < attack:
            g = t / attack
        elif t < attack + hold:
            g = 1.0
        else:
            g = math.exp(-(t - attack - hold) / decay)
        out.append(v * g)
    return out


def shape(sig, fn):
    """Multiply by fn(t, length)."""
    length = len(sig) / SR
    return [v * fn(i / SR, length) for i, v in enumerate(sig)]


def lowpass(sig, cutoff):
    out, y = [], 0.0
    c = cutoff if callable(cutoff) else (lambda t, k=cutoff: k)
    for i, v in enumerate(sig):
        a = 1 - math.exp(-TAU * c(i / SR) / SR)
        y += a * (v - y)
        out.append(y)
    return out


def highpass(sig, cutoff):
    lp = lowpass(sig, cutoff)
    return [a - b for a, b in zip(sig, lp)]


def bandpass(sig, f0, q=1.0):
    w = TAU * f0 / SR
    alpha = math.sin(w) / (2 * q)
    b0, b1, b2 = alpha, 0.0, -alpha
    a0, a1, a2 = 1 + alpha, -2 * math.cos(w), 1 - alpha
    x1 = x2 = y1 = y2 = 0.0
    out = []
    for x in sig:
        y = (b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2) / a0
        x2, x1, y2, y1 = x1, x, y1, y
        out.append(y)
    return out


def bell(freq, sec, decay, partials=((1, 1.0), (2.0, 0.35), (2.76, 0.2), (5.4, 0.08))):
    out = silence(sec)
    for ratio, amp in partials:
        mix(out, env(osc(sec, freq * ratio), 0.002, decay / ratio ** 0.5), 0, amp)
    return out


def exp_sweep(f0, f1, sec):
    return lambda t: f0 * (f1 / f0) ** min(t / sec, 1.0)


def save(name, sig, peak=0.9, fade=0.004, loop=False):
    m = max(1e-9, max(abs(v) for v in sig))
    sig = [v * peak / m for v in sig]
    if not loop:
        k = n(fade)
        for i in range(min(k, len(sig))):
            sig[-1 - i] *= i / k
    with wave.open(os.path.join(OUT, name + ".wav"), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(b"".join(struct.pack("<h", int(max(-1, min(1, v)) * 32767)) for v in sig))
    print(name, round(len(sig) / SR, 2), "s")


# --- Player movement -------------------------------------------------------
# Soft padded steps: a muffled brush (noise filtered twice, so no hiss) over a low thump,
# with a slow attack so there's no click at the start.
for i, cut in enumerate((260, 320, 380)):
    s = env(lowpass(lowpass(noise(0.12, 10 + i), cut), cut), 0.012, 0.035)
    mix(s, env(osc(0.1, exp_sweep(85, 55, 0.08)), 0.01, 0.03), 0, 1.2)
    save(f"Footstep_{i + 1}", s, 0.5)

save("Jump", env(osc(0.16, exp_sweep(260, 680, 0.14), harmonics=((3, 0.25),)), 0.004, 0.07, 0.03), 0.7)

s = env(osc(0.2, exp_sweep(120, 45, 0.15)), 0.002, 0.06)
mix(s, env(lowpass(noise(0.08, 3), 500), 0.002, 0.03), 0, 0.8)
save("Land", s)

s = env(bandpass(noise(0.65, 4), 1400, 0.6), 0.006, 0.16)
r = random.Random(5)
for _ in range(8):
    f = r.uniform(450, 900)
    mix(s, env(osc(0.05, exp_sweep(f, f * 1.8, 0.05)), 0.002, 0.015), r.uniform(0.05, 0.45), 0.35)
save("Splash", s)

# --- Shapeshifting and scanning ---------------------------------------------
s = shape(lowpass(noise(0.45, 6), exp_sweep(400, 5000, 0.45)), lambda t, L: math.sin(math.pi * t / L) ** 1.5)
shimmer = shape(osc(0.45, exp_sweep(350, 1500, 0.45), harmonics=((2, 0.4), (3, 0.2))),
                lambda t, L: math.sin(math.pi * t / L) * (0.6 + 0.4 * math.sin(TAU * 28 * t)))
mix(s, shimmer, 0, 0.5)
save("Shift", s, 0.8)

s = []
for k in range(2):
    mix(s, env(lowpass(osc(0.1, 155, "square"), 1800), 0.003, 0.2, 0.06), k * 0.13)
save("Denied", s, 0.6)

s = []
for k, f in enumerate((1047, 1319, 1568, 2093)):
    mix(s, bell(f, 0.5, 0.18), k * 0.075, 0.8 if k < 3 else 1.0)
save("Scanned", s, 0.75)

# --- Interaction --------------------------------------------------------------
s = env(highpass(noise(0.03, 7), 2000), 0.001, 0.006)
mix(s, env(osc(0.04, 1250), 0.001, 0.012), 0, 0.7)
save("Interact", s, 0.7)

s = env(osc(0.25, exp_sweep(150, 85, 0.12)), 0.002, 0.06)
mix(s, env(highpass(noise(0.02, 8), 1500), 0.001, 0.005), 0, 0.6)
mix(s, bell(820, 0.22, 0.06, ((1, 1.0), (1.67, 0.7), (2.9, 0.3))), 0.035, 0.45)
save("Lever", s)

grind = shape(lowpass(noise(0.65, 9), 500), lambda t, L: 0.7 + 0.3 * math.sin(TAU * 17 * t))
mix(grind, osc(0.65, 68, harmonics=((2, 0.5), (3, 0.25))), 0, 0.25)
grind = shape(grind, lambda t, L: min(1, t / 0.05) * min(1, (L - t) / 0.15))
save("HatchMove", grind, 0.55)

s = env(osc(0.4, exp_sweep(95, 55, 0.2)), 0.002, 0.08)
mix(s, env(lowpass(noise(0.12, 11), 2500), 0.001, 0.025), 0, 0.9)
mix(s, bell(523, 0.4, 0.09, ((1, 1.0), (2.26, 0.6), (3.23, 0.4))), 0.005, 0.35)
save("HatchShut", s)

# --- Combat ---------------------------------------------------------------------
s = shape(lowpass(highpass(noise(0.2, 12), exp_sweep(300, 1400, 0.2)), exp_sweep(900, 3500, 0.2)),
          lambda t, L: math.sin(math.pi * t / L) ** 2)
save("Throw", s, 0.6)

for i in range(2):
    s = env(lowpass(noise(0.3, 13 + i), 1300), 0.002, 0.05)
    mix(s, env(osc(0.14, exp_sweep(320 + 60 * i, 85, 0.12)), 0.002, 0.05), 0, 0.9)
    r = random.Random(20 + i)
    for _ in range(6):
        mix(s, env(bandpass(noise(0.03, r.randint(0, 999)), r.uniform(1500, 3000), 2), 0.001, 0.006), r.uniform(0.02, 0.2), 0.5)
    save(f"Splat_{i + 1}", s, 0.8)

s = env(bandpass(osc(0.24, lambda t: (330 - 600 * t) * (1 + 0.03 * math.sin(TAU * 30 * t)), "saw"), 800, 1.2), 0.01, 0.09, 0.05)
save("Hurt", s, 0.8)

s = env(osc(0.2, lambda t: (900 + 1700 * t if t < 0.07 else 1020 - 1600 * (t - 0.07)) * (1 + 0.05 * math.sin(TAU * 32 * t)),
            harmonics=((2, 0.3), (3, 0.15))), 0.006, 0.07, 0.06)
save("Squeak", s, 0.65)

s = env(lowpass(osc(0.55, exp_sweep(520, 140, 0.5), "square"), 1400), 0.005, 0.2, 0.15)
mix(s, env(lowpass(noise(0.3, 15), 900), 0.005, 0.08), 0.05, 0.6)
save("Defeat", s, 0.7)

s = []
mix(s, env(lowpass(osc(0.07, 988, "square"), 3500), 0.002, 0.05, 0.03))
mix(s, env(lowpass(osc(0.2, 1480, "square"), 3500), 0.002, 0.08, 0.03), 0.065)
save("Pickup", s, 0.55)

s = bell(1760, 1.0, 0.35, ((1, 1.0), (2.76, 0.35), (5.4, 0.12), (0.5, 0.25)))
mix(s, env(highpass(noise(0.01, 16), 3000), 0.0005, 0.002), 0, 0.6)
save("Ding", s, 0.75)

# --- Progress and stingers -------------------------------------------------------
s = []
mix(s, bell(784, 0.6, 0.2), 0)
mix(s, bell(1047, 0.9, 0.3), 0.11)
mix(s, bell(1319, 0.9, 0.3), 0.11, 0.5)
save("Solved", s, 0.75)

s = []
notes = ((392, 0.22), (370, 0.22), (349, 0.22), (330, 0.75))
at = 0.0
for k, (f, d) in enumerate(notes):
    vib = (lambda t, f=f: f * (1 + 0.025 * math.sin(TAU * 6 * t) * min(1, t / 0.2))) if k == 3 else f
    tone = lowpass(osc(d, vib, "saw"), 1100)
    tone = shape(tone, lambda t, L: min(1, t / 0.015) * min(1, (L - t) / 0.06))
    mix(s, tone, at)
    at += d + 0.03
save("Caught", s, 0.6)

s = []
for k, f in enumerate((523, 659, 784, 1047)):
    d = 0.5 if k == 3 else 0.1
    tone = mix(lowpass(osc(d, f, "square"), 2500), osc(d, f * 2), 0, 0.3)
    mix(s, env(tone, 0.004, 0.25 if k == 3 else 0.08, 0.05 if k == 3 else 0.03), k * 0.1)
save("LevelDone", s, 0.55)

save("UiClick", env(osc(0.035, 1500, harmonics=((2, 0.2),)), 0.001, 0.008), 0.5)
save("Talk", env(lowpass(osc(0.06, 520, "square"), 1600), 0.003, 0.025, 0.015), 0.45)

# --- Final level: animals, alarm, lasers ----------------------------------------------
for i, base in enumerate((560, 480)):
    voice = bandpass(osc(0.18, exp_sweep(base, base * 0.55, 0.12), "saw"), 900, 1.4)
    breath = bandpass(noise(0.18, 30 + i), 1600, 1.0)
    s = env(mix(voice, breath, 0, 0.6), 0.004, 0.045, 0.03)
    save(f"Bark_{i + 1}", s, 0.8)

for i, base in enumerate((430, 380)):
    f = lambda t, b=base: b * (1 + 0.12 * math.sin(math.pi * min(t / 0.3, 1))) * (1 + 0.015 * math.sin(TAU * 38 * t))
    raw = osc(0.3, f, "saw")
    s = mix(bandpass(raw, 1100, 2.5), bandpass(raw, 2600, 3.0), 0, 0.6)
    s = shape(s, lambda t, L: min(1, t / 0.02) * min(1, (L - t) / 0.08))
    save(f"Honk_{i + 1}", s, 0.75)

# Siren: 1.2 s per wail, a whole number of cycles at every harmonic so it loops cleanly.
# Two wails are filtered and the second kept, so the filter has settled at the loop point.
s = lowpass(osc(2.4, lambda t: 750 + 260 * math.sin(TAU * t / 1.2), harmonics=((3, 0.3), (5, 0.12))), 3000)
save("Alarm", s[n(1.2):], 0.5, loop=True)

buzz = osc(0.4, 110, "square")
crackle = highpass(noise(0.4, 40), 2500)
r = random.Random(41)
gate = [1.0 if r.random() < 0.35 else 0.15 for _ in range(int(0.4 * 60) + 1)]
s = [b * 0.5 + c * gate[int(i / SR * 60)] for i, (b, c) in enumerate(zip(buzz, crackle))]
save("Zap", env(s, 0.002, 0.12, 0.08), 0.6)
