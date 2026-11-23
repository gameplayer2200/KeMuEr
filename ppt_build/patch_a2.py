import io

p = "build_pptx.py"
src = io.open(p, encoding="utf-8").read()
lines = src.split("\n")
# 找到被 patch 打乱的三行（"写了个临时探针" 起）
start = None
for i, ln in enumerate(lines):
    if "写了个临时探针" in ln:
        start = i
        break
assert start is not None
# 该块到 "接线顺序和编号正好相反。") 结束，再紧跟一条孤立的实测字符串
end = None
for j in range(start, start + 6):
    if "正好相反" in lines[j]:
        end = j
        break
assert end is not None
new_block = [
    '         "写了个临时探针，每帧扫 JoystickButton0..19。\\n"',
    '         "按红 → 黄 → 绿 → 蓝 按下去，日志依次是 9 / 6 / 3 / 0 ——\\n"',
    '         "接线顺序和编号正好相反。")',
    '    card(sl, 0.67, 4.04, 4.28, 1.16, BLUE, "坑三 · 油门是反的",',
]
# 保留紧随其后的那句实测正文（它是被吃掉的 card 正文）
body = None
for j in range(end + 1, end + 3):
    if "实测" in lines[j]:
        body = lines[j]
        break
assert body is not None
new_block.append(body)
lines[start:end + 2] = new_block
io.open(p, "w", encoding="utf-8", newline="").write("\n".join(lines))
print("fixed")
