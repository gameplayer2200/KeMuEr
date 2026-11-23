import io

p = "build_pptx.py"
s = io.open(p, encoding="utf-8").read()

divider = '''
# ---------------- 分节页：这一轮做的事 ----------------
def slide_divider():
    sl = page("这一轮做的事：五件",
              "从「手柄接不进来」到「跑出跑道能退回去」，下面五页逐条展开")
    rows = [
        ("01", "手柄接进引擎", "UPM 克隆失败，改成内嵌包；写探针问出真实键号 9 / 6 / 3 / 0；油门反向修正"),
        ("02", "所有按键的绑定与调配", "四键板 / Xbox / 键盘三套输入对同一张功能表，长按 F1 现场重绑"),
        ("03", "教学关重写", "去掉 A / B 路点，改成 7 步必须真的做出来的按键教学"),
        ("04", "地图：碰撞与装饰", "树与路面补碰撞；按距离变换沿路撒 92 楼 / 151 树 / 77 灌木"),
        ("05", "跑出跑道回退", "3 秒环形缓冲 + 向下探针，出界退回 3 秒前，不重开一局"),
    ]
    y = 1.46
    for num, title, desc in rows:
        txt(sl, 0.67, y + 0.02, 0.62, 0.30, num, 15, AMBER, True)
        txt(sl, 1.34, y, 7.6, 0.28, title, 12.0, INK, True)
        txt(sl, 1.34, y + 0.30, 7.7, 0.24, desc, 8.5, BODY)
        rect(sl, 0.67, y + 0.60, 8.66, 0.008, BORDER)
        y += 0.78
    txt(sl, 0.67, 5.16, 8.66, 0.24,
        "前五页是原来的汇报线（考点 / 入库判定 / 两套手柄）；这五页之后接着讲手柄适配、分层与一局流程。",
        8.25, MUTED)


'''

# 在 slide_a 定义前插入分节页函数
anchor = "# ---------------- A：手柄接进引擎 ----------------"
assert anchor in s
s = s.replace(anchor, divider.lstrip("\n") + anchor, 1)

# 组装顺序
s = s.replace("""slide_a()
slide_b()
slide_c()
slide_d()
slide_e()""",
"""slide_a()
slide_b()
slide_c()
slide_d()
slide_e()
slide_divider()""")

s = s.replace("""lst = list(prs.slides._sldIdLst)
A, B, C, D, E = lst[11], lst[12], lst[13], lst[14], lst[15]""",
"""lst = list(prs.slides._sldIdLst)
A, B, C, D, E, DIV = lst[11], lst[12], lst[13], lst[14], lst[15], lst[16]""")

s = s.replace("""move(A, 5)
move(B, 6)
move(C, 11)
move(D, 12)
move(E, 13)""",
"""# 我做的部分集中成一段：分节页 + A/B/C/D/E 连续放在原第 5 页之后
for i, el in enumerate([DIV, A, B, C, D, E]):
    move(el, 5 + i)""")

io.open(p, "w", encoding="utf-8", newline="").write(s)
print("patched divider + order")
