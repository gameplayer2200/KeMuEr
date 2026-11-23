import io

p = "build_pptx.py"
s = io.open(p, encoding="utf-8").read()
needle = "进扫描，按顺序按 5 个键即重绑，\\nEsc 取消；"
if needle in s:
    s = s.replace(needle, "进扫描，\\n按顺序按 5 个键即重绑，Esc 取消；")
    io.open(p, "w", encoding="utf-8", newline="").write(s)
    print("patched")
else:
    print("needle not found")
    i = s.find("改键不用改代码")
    print(repr(s[i:i + 120]))
