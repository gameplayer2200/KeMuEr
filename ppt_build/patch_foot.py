import io

p = "build_pptx.py"
s = io.open(p, encoding="utf-8").read()
old = '    txt(sl, 5.15, y, 4.18, 1.0, text, 8.5, MUTED, spacing=1.4)'
new = '    txt(sl, 5.15, y, 4.18, 0.80, text, 8.5, MUTED, spacing=1.4)'
assert old in s
s = s.replace(old, new)
io.open(p, "w", encoding="utf-8", newline="").write(s)
print("patched")
