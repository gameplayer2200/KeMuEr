import io

p = "build_pptx.py"
lines = io.open(p, encoding="utf-8").read().split("\n")
for i, ln in enumerate(lines):
    if "临时探针" in ln:
        lines[i] = ('         "写了个临时探针，每帧扫 JoystickButton0..19。"')
        lines[i + 1] = ('         "按红 → 黄 → 绿 → 蓝 依次按下去，日志是 9 / 6 / 3 / 0 ——\\n"\n'
                        '         "接线顺序和编号正好相反。")')
        print("replaced at", i + 1)
        break
io.open(p, "w", encoding="utf-8", newline="").write("\n".join(lines))
