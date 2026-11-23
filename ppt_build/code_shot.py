# -*- coding: utf-8 -*-
"""把源码的指定行区间渲染成一张「编辑器截图」PNG。"""
import re
import sys
from PIL import Image, ImageDraw, ImageFont

MONO = r"C:\Windows\Fonts\CascadiaMono.ttf"
CJK = r"C:\Windows\Fonts\msyh.ttc"

SCALE = 2
FS = 13
LH = 20
PAD = 16
GUTTER = 44
HEADER = 30

BG = (30, 30, 30)
HEADER_BG = (37, 37, 38)
FG = (212, 212, 212)
GUTTER_FG = (110, 110, 110)
KEYWORD = (86, 156, 214)
CONTROL = (197, 134, 192)
TYPE = (78, 201, 176)
STRING = (206, 145, 120)
COMMENT = (106, 153, 85)
NUMBER = (181, 206, 168)
FUNC = (220, 220, 170)
PUNCT = (200, 200, 200)

KEYWORDS = {
    "using", "public", "private", "protected", "static", "readonly", "const", "class",
    "struct", "enum", "void", "int", "float", "bool", "string", "var", "new", "return",
    "if", "else", "for", "while", "foreach", "in", "break", "continue", "switch", "case",
    "default", "true", "false", "null", "this", "base", "override", "virtual", "out", "ref",
    "get", "set", "namespace", "sealed", "partial", "internal", "float[]", "string[]",
}
CONTROLS = {"if", "else", "for", "while", "foreach", "switch", "case", "return", "break", "continue"}


def font(ch):
    path = CJK if ord(ch) > 127 else MONO
    try:
        return ImageFont.truetype(path, FS * SCALE)
    except Exception:
        return ImageFont.truetype(MONO, FS * SCALE)


_cache = {}


def _f(ch):
    k = ord(ch) > 127
    if k not in _cache:
        _cache[k] = font(ch)
    return _cache[k]


def tokenize(line):
    out = []
    i = 0
    n = len(line)
    while i < n:
        c = line[i]
        if c == "/" and i + 1 < n and line[i + 1] == "/":
            out.append((line[i:], COMMENT))
            return out
        if c == '"':
            j = i + 1
            while j < n and not (line[j] == '"' and line[j - 1] != "\\"):
                j += 1
            out.append((line[i:j + 1], STRING))
            i = j + 1
            continue
        if c.isalpha() or c == "_":
            j = i
            while j < n and (line[j].isalnum() or line[j] == "_"):
                j += 1
            word = line[i:j]
            if word in CONTROLS:
                col = CONTROL
            elif word in KEYWORDS:
                col = KEYWORD
            elif word in ("Mathf", "Vector3", "Quaternion", "Transform", "GameObject", "Input", "Physics", "Debug", "System", "UnityEngine", "ControlProfileKind", "ProfileData", "Sample", "List", "LessonStep", "DriveGear", "CarAction"):
                col = TYPE
            elif j < n and line[j] == "(":
                col = FUNC
            else:
                col = FG
            out.append((word, col))
            i = j
            continue
        if c.isdigit():
            j = i
            while j < n and (line[j].isdigit() or line[j] in "._f"):
                j += 1
            out.append((line[i:j], NUMBER))
            i = j
            continue
        out.append((c, PUNCT))
        i += 1
    return out


def render(src, start, end, out_png, title=None, width_px=None):
    with open(src, encoding="utf-8") as f:
        lines = f.read().split("\n")
    lines = lines[start - 1:end]
    nums = list(range(start, start + len(lines)))
    title = title or src.split("/")[-1]

    max_w = 0
    for ln in lines:
        w = sum(_f(ch).getlength(ch) for ch in ln)
        max_w = max(max_w, w)
    body_w = int(max_w) + PAD * 2
    gutter = GUTTER * SCALE
    W = max(int(width_px or 0), gutter + body_w + PAD)
    H = HEADER * SCALE + PAD + len(lines) * LH * SCALE + PAD

    img = Image.new("RGB", (W, H), BG)
    d = ImageDraw.Draw(img)
    d.rectangle([0, 0, W, HEADER * SCALE], fill=HEADER_BG)
    for i, col in enumerate([(255, 95, 86), (255, 189, 46), (39, 201, 63)]):
        cx = 14 * SCALE + i * 14 * SCALE
        d.ellipse([cx - 5 * SCALE, HEADER * SCALE // 2 - 5 * SCALE,
                   cx + 5 * SCALE, HEADER * SCALE // 2 + 5 * SCALE], fill=col)
    tf = ImageFont.truetype(CJK, 11 * SCALE)
    d.text((60 * SCALE, HEADER * SCALE // 2 - 7 * SCALE), title, font=tf, fill=(160, 160, 160))

    y = HEADER * SCALE + PAD
    nf = ImageFont.truetype(MONO, 12 * SCALE)
    for idx, ln in enumerate(lines):
        d.text((PAD, y + 2 * SCALE), str(nums[idx]), font=nf, fill=GUTTER_FG)
        x = gutter + PAD // 2
        for text, col in tokenize(ln):
            for ch in text:
                fnt = _f(ch)
                d.text((x, y), ch, font=fnt, fill=col)
                x += fnt.getlength(ch)
        y += LH * SCALE

    img.save(out_png)
    print("saved", out_png, img.size)


if __name__ == "__main__":
    src, s, e, out = sys.argv[1], int(sys.argv[2]), int(sys.argv[3]), sys.argv[4]
    title = sys.argv[5] if len(sys.argv) > 5 else None
    render(src, s, e, out, title)
