import io

p = "build_pptx.py"
s = io.open(p, encoding="utf-8").read()
pairs = [
    ('"把「会不会开」拆成 7 个必须真的做出来的动作"', '"把「会不会开」拆成 6 个必须真的做出来的动作"'),
    ('"新机制：一张 7 步的步骤表"', '"新机制：一张 6 步的步骤表"'),
]
for old, new in pairs:
    assert old in s, old
    s = s.replace(old, new)
io.open(p, "w", encoding="utf-8", newline="").write(s)
print("patched 6 steps everywhere")
