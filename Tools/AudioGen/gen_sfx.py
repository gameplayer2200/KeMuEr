# -*- coding: utf-8 -*-
"""合成科目二项目需要的车辆音效，输出到 Assets/Resources/Audio/Sfx。

全部程序化合成，不依赖任何外部素材，避免版权问题。
运行： python Tools/AudioGen/gen_sfx.py
"""
import os
import wave

import numpy as np

SR = 44100
OUT = os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))),
                   "Assets", "Resources", "Audio", "Sfx")
rng = np.random.default_rng(20261119)


def save(name, x, sr=SR):
    os.makedirs(OUT, exist_ok=True)
    x = np.asarray(x, dtype=np.float64)
    peak = np.max(np.abs(x)) or 1.0
    x = np.clip(x / peak * 0.85, -1.0, 1.0)
    data = (x * 32767.0).astype("<i2")
    with wave.open(os.path.join(OUT, name), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(sr)
        w.writeframes(data.tobytes())
    print("%-22s %5.2fs  %6d B" % (name, len(x) / sr, data.nbytes))


def fft_filter(x, lo=None, hi=None, order=2):
    """在频域做零相位滤波；循环素材也适用（循环卷积）。"""
    n = len(x)
    spec = np.fft.rfft(x)
    freqs = np.fft.rfftfreq(n, 1.0 / SR)
    mask = np.ones_like(freqs)
    if lo is not None:
        mask *= 1.0 / (1.0 + (lo / np.maximum(freqs, 1e-6)) ** (2 * order))
    if hi is not None:
        mask *= 1.0 / (1.0 + (freqs / hi) ** (2 * order))
    return np.fft.irfft(spec * mask, n)


def loopify(x, xfade=0.06):
    """把尾巴淡入头部，得到无缝循环。"""
    n = int(xfade * SR)
    if n <= 0 or n * 2 >= len(x):
        return x
    head = x[:n].copy()
    tail = x[-n:].copy()
    ramp = np.linspace(0.0, 1.0, n)
    x = x[:-n]
    x[:n] = head * ramp + tail * (1.0 - ramp)
    return x


def engine_loop():
    t = np.arange(SR) / SR
    f0 = 45.0                      # 1 秒整数个周期 → 天然可循环
    sig = np.zeros_like(t)
    for k in range(1, 16):
        amp = 1.0 / (k ** 0.95)
        sig += amp * np.sin(2 * np.pi * k * f0 * t + (k * 1.7) % (2 * np.pi))
    # 点火的气浪感
    sig *= 1.0 + 0.22 * np.sin(2 * np.pi * 2.0 * t)
    noise = fft_filter(rng.normal(0, 1, SR), lo=80, hi=1100)
    pulse = (0.5 + 0.5 * np.sin(2 * np.pi * f0 * t)) ** 3
    sig += 0.55 * noise * pulse
    save("engine_loop.wav", loopify(sig))


def wind_loop():
    t = np.arange(2 * SR) / SR
    x = fft_filter(rng.normal(0, 1, 2 * SR), lo=60, hi=700)
    x *= 0.75 + 0.25 * np.sin(2 * np.pi * 0.5 * t)   # 0.5 Hz，2 秒整数周期
    save("wind_loop.wav", loopify(x))


def skid_loop():
    t = np.arange(SR) / SR
    tone = (0.55 * np.sin(2 * np.pi * 1150 * t) +
            0.22 * np.sin(2 * np.pi * 2300 * t + 0.6) +
            0.10 * np.sin(2 * np.pi * 3450 * t + 1.1))
    noise = fft_filter(rng.normal(0, 1, SR), lo=800, hi=1900)
    body = 0.45 * tone + 0.55 * noise
    am = 0.70 + 0.30 * np.sin(2 * np.pi * 7.0 * t)     # 7 Hz 抖动
    body *= am * (0.85 + 0.15 * np.sin(2 * np.pi * 3.0 * t + 0.9))
    save("skid_loop.wav", loopify(body))


def brake_loop():
    t = np.arange(SR) / SR
    x = fft_filter(rng.normal(0, 1, SR), lo=1800, hi=7500)
    x *= 0.8 + 0.2 * np.sin(2 * np.pi * 3.0 * t)
    save("brake_loop.wav", loopify(x) * 0.55)


def impact():
    n = int(0.38 * SR)
    t = np.arange(n) / SR
    thump = 0.95 * np.sin(2 * np.pi * 72 * t) * np.exp(-t * 15.0)
    crunch = fft_filter(rng.normal(0, 1, n), lo=250, hi=3200) * np.exp(-t * 26.0)
    x = thump * 0.9 + crunch * 0.8
    save("impact.wav", x)


def cone_hit():
    n = int(0.28 * SR)
    t = np.arange(n) / SR
    click = fft_filter(rng.normal(0, 1, n), lo=900, hi=6000) * np.exp(-t * 120.0)
    r1 = 0.6 * np.sin(2 * np.pi * 470 * t) * np.exp(-t * 26.0)
    r2 = 0.3 * np.sin(2 * np.pi * 1320 * t + 0.4) * np.exp(-t * 40.0)
    save("cone_hit.wav", click * 0.7 + r1 + r2)


def gear_shift():
    n = int(0.17 * SR)
    t = np.arange(n) / SR
    click = fft_filter(rng.normal(0, 1, n), lo=1400, hi=5200) * np.exp(-t * 110.0)
    thud = 0.7 * np.sin(2 * np.pi * 108 * t) * np.exp(-t * 34.0)
    gate = np.zeros(n)
    gate[int(0.045 * SR):] = 1.0       # 第二段：卡入
    click2 = fft_filter(rng.normal(0, 1, n), lo=900, hi=3800) * np.exp(-np.maximum(t - 0.045, 0) * 150.0) * gate
    save("gear_shift.wav", click * 0.8 + thud * 0.9 + click2 * 0.5)


def handbrake():
    n = int(0.55 * SR)
    t = np.arange(n) / SR
    x = np.zeros(n)
    for i in range(7):
        t0 = 0.01 + i * 0.07
        pass_click = fft_filter(rng.normal(0, 1, n), lo=2200, hi=6500)
        x += pass_click * np.exp(-np.maximum(t - t0, 0) * 150.0) * (t >= t0)
        x += 0.25 * np.sin(2 * np.pi * 3100 * t + i) * np.exp(-np.maximum(t - t0, 0) * 90.0) * (t >= t0)
    save("handbrake.wav", x)


def reverse_beep():
    n = int(0.26 * SR)
    t = np.arange(n) / SR
    x = np.sin(2 * np.pi * 960 * t)
    fade = min(int(0.012 * SR), n // 2)
    env = np.ones(n)
    env[:fade] = np.linspace(0, 1, fade)
    env[-fade:] = np.linspace(1, 0, fade)
    save("reverse_beep.wav", x * env)


def horn():
    n = int(0.55 * SR)
    t = np.arange(n) / SR
    x = (np.sin(2 * np.pi * 392 * t) + 0.6 * np.sin(2 * np.pi * 494 * t) +
         0.35 * np.sin(2 * np.pi * 784 * t))
    env = np.minimum(1.0, np.minimum(t * 40.0, (0.55 - t) * 30.0).clip(0, 1))
    save("horn.wav", x * env)


if __name__ == "__main__":
    engine_loop()
    wind_loop()
    skid_loop()
    brake_loop()
    impact()
    cone_hit()
    gear_shift()
    handbrake()
    reverse_beep()
    horn()
