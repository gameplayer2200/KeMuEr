# -*- coding: utf-8 -*-
"""把本轮新增的内容补进《科目二》汇报 PPT：新建 5 页，插入到对应位置。"""
import os
from pptx import Presentation
from pptx.util import Inches, Pt
from pptx.dml.color import RGBColor
from pptx.enum.text import MSO_ANCHOR
from pptx.enum.shapes import MSO_SHAPE
from PIL import Image

SRC = r"C:\Users\14763\Desktop\科目二PPT.pptx"
DST = r"C:\Users\14763\Desktop\科目二PPT_补充版.pptx"
IMG = os.path.join(os.path.dirname(os.path.abspath(__file__)), "img")

INK = RGBColor(0x1A, 0x1D, 0x21)
BODY = RGBColor(0x3A, 0x40, 0x46)
MUTED = RGBColor(0x8A, 0x91, 0x99)
BG = RGBColor(0xF2, 0xF3, 0xF5)
WHITE = RGBColor(0xFF, 0xFF, 0xFF)
BORDER = RGBColor(0xDD, 0xE1, 0xE6)
TH = RGBColor(0xE6, 0xE9, 0xED)
AMBER = RGBColor(0xE8, 0xB3, 0x3C)
RED = RGBColor(0xE0, 0x5C, 0x4A)
BLUE = RGBColor(0x5B, 0x9B, 0xD5)
GREEN = RGBColor(0x4C, 0xAF, 0x6E)
GREY = RGBColor(0xC7, 0xCD, 0xD4)
FONT = "微软雅黑"

prs = Presentation(SRC)


def rect(sl, x, y, w, h, fill, rounded=False, line=None, lw=1.0):
    shp = sl.shapes.add_shape(
        MSO_SHAPE.ROUNDED_RECTANGLE if rounded else MSO_SHAPE.RECTANGLE,
        Inches(x), Inches(y), Inches(w), Inches(h))
    if rounded:
        shp.adjustments[0] = 0.08
    if fill is None:
        shp.fill.background()
    else:
        shp.fill.solid()
        shp.fill.fore_color.rgb = fill
    if line is None:
        shp.line.fill.background()
    else:
        shp.line.color.rgb = line
        shp.line.width = Pt(lw)
    shp.shadow.inherit = False
    tf = shp.text_frame
    tf.word_wrap = True
    tf.margin_left = tf.margin_right = Inches(0.05)
    tf.margin_top = tf.margin_bottom = 0
    return shp


def txt(sl, x, y, w, h, text, size, color=INK, bold=False, spacing=1.3, anchor=MSO_ANCHOR.TOP):
    box = sl.shapes.add_textbox(Inches(x), Inches(y), Inches(w), Inches(h))
    tf = box.text_frame
    tf.word_wrap = True
    tf.margin_left = tf.margin_right = 0
    tf.margin_top = tf.margin_bottom = 0
    tf.vertical_anchor = anchor
    for i, ln in enumerate(text.split("\n")):
        p = tf.paragraphs[0] if i == 0 else tf.add_paragraph()
        p.line_spacing = spacing
        r = p.add_run()
        r.text = ln
        r.font.size = Pt(size)
        r.font.bold = bold
        r.font.name = FONT
        r.font.color.rgb = color
    return box


def page(title, kicker):
    sl = prs.slides.add_slide(prs.slide_layouts[6])
    rect(sl, 0, 0, 10.0, 5.62, BG)
    txt(sl, 0.67, 0.54, 8.0, 0.39, title, 18, INK, True)
    txt(sl, 0.67, 0.97, 8.0, 0.21, kicker, 9.75, MUTED)
    return sl


def card(sl, x, y, w, h, accent, title, body, body_size=8.5):
    rect(sl, x, y, w, h, WHITE, rounded=True, line=BORDER, lw=1.0)
    rect(sl, x, y, 0.04, h, accent)
    txt(sl, x + 0.22, y + 0.11, w - 0.38, 0.24, title, 11.25, INK, True)
    txt(sl, x + 0.22, y + 0.38, w - 0.38, h - 0.44, body, body_size, BODY, spacing=1.38)


def code_image(sl, name, x, y, w, caption=None):
    path = os.path.join(IMG, name)
    with Image.open(path) as im:
        ratio = im.height / im.width
    h = w * ratio
    sl.shapes.add_picture(path, Inches(x), Inches(y), width=Inches(w))
    if caption:
        txt(sl, x, y + h + 0.10, w, 0.42, caption, 8.5, MUTED, spacing=1.35)
    return y + h + 0.10 + (0.42 if caption else 0)


def footnote(sl, y, text):
    txt(sl, 5.15, y, 4.18, 0.80, text, 8.5, MUTED, spacing=1.4)


# ---------------- A：手柄接进引擎 ----------------
def slide_a():
    sl = page("手柄不是插上就能用：先把键号问出来",
              "文档上的 1 / 4 / 7 / 10 是 HID 编号，Unity 里是另一套")
    card(sl, 0.67, 1.42, 4.28, 1.16, RED, "坑一 · 包装不上",
         "MCP 插件原本是 git 依赖，UPM 一直克隆失败。把包整个复制进\nPackages/ 变成内嵌包，锁文件自动改成 \"source\": \"embedded\"，\n编辑器这边才连得上。")
    card(sl, 0.67, 2.73, 4.28, 1.16, AMBER, "坑二 · 键号对不上",
         "写了个临时探针，每帧扫 JoystickButton0..19。\n"
         "按红 → 黄 → 绿 → 蓝 按下去，日志依次是 9 / 6 / 3 / 0 ——\n"
         "接线顺序和编号正好相反。")
    card(sl, 0.67, 4.04, 4.28, 1.16, BLUE, "坑三 · 油门是反的",
         "实测「推 = 刹车、拉 = 前进」，这只通用 USB 摇杆上推为负值，\n预设里 invertThrottle 必须打开，否则一推油门就倒车。")
    bottom = code_image(sl, "code_profile.png", 5.15, 1.42, 4.18,
                        "ControlProfile.External4：实测键号写成预设，场景里钉死，不再自动猜")
    footnote(sl, bottom + 0.12,
             "接入方式：stdio 桥在每次域重载后自动挂上 6400 端口，场景、组件、\nPlay 模式都能被直接读写，不用来回切窗口点面板。")


# ---------------- B：按键的绑定与调配 ----------------
def slide_b():
    sl = page("所有按键的绑定与调配",
              "功能名对四种输入；键号只活在一份数据里，不散在代码各处")
    cols = [0.89, 2.08, 3.12, 3.92]
    rows = [
        ("功能", "四键板", "Xbox", "键盘"),
        ("1 前进挡", "键 3", "A", "1"),
        ("R 倒挡", "键 9", "B", "R"),
        ("手刹", "键 6", "Y", "空格/G"),
        ("切视角", "键 0", "L3", "Q"),
        ("复位", "不占键", "X", "退格"),
    ]
    y0, rh, gap = 1.40, 0.38, 0.05
    for i, row in enumerate(rows):
        if i == 0:
            rect(sl, 0.67, y0, 4.10, 0.30, TH)
            for j, v in enumerate(row):
                txt(sl, cols[j], y0 + 0.06, 1.05, 0.2, v, 8.5, MUTED, True)
            continue
        hy = y0 + 0.30 + 0.06 + (i - 1) * (rh + gap)
        rect(sl, 0.67, hy, 4.10, rh, WHITE, rounded=True, line=BORDER, lw=1.0)
        rect(sl, 0.67, hy, 0.04, rh, [AMBER, RED, AMBER, BLUE, GREEN][i - 1])
        for j, v in enumerate(row):
            txt(sl, cols[j], hy + 0.10, 1.05 if j != 3 else 0.85, 0.2, v, 8.8, INK if j == 0 else BODY, j == 0)
    txt(sl, 0.67, 4.02, 4.10, 0.62,
        "方向盘与油门不占键：Stick X 左右打方向，Stick Y 推拉分油门 / 刹车；\n同一根轴两头用，中位做 0.4 秒自校准。",
        8.25, MUTED, spacing=1.4)
    txt(sl, 0.67, 4.72, 4.10, 0.62,
        "改键不用改代码：长按 F1（或长按视角键 1.5 秒）进扫描，\n按顺序按 5 个键即重绑，Esc 取消；HUD 左下角实时显示当前这套绑定。",
        8.25, BODY, spacing=1.4)
    bottom = code_image(sl, "code_scan.png", 5.15, 1.42, 4.18,
                        "CarInput.UpdateScan：扫到哪个键就写回哪一个动作，扫完直接生效")
    footnote(sl, bottom + 0.14,
             "重绑只写进运行时的 CarInput，不落场景文件：\n"
             "换回自己的板子，重开一次 Play 就恢复成预设。")


# ---------------- C：教学关重写 ----------------
def slide_c():
    sl = page("教学关重写：从 A / B 路点到 7 步按键",
              "把「会不会开」拆成 7 个必须真的做出来的动作")
    card(sl, 0.67, 1.42, 4.28, 1.06, GREY, "旧机制：两个标记点",
         "车开进 A 点半径、再到 B 点半径就算过。\n它考的是「会不会开过去」，跟按键一个都没关系。")
    card(sl, 0.67, 2.62, 4.28, 1.20, GREEN, "新机制：一张 7 步的步骤表",
         "挂 1 档 → 松手刹 → 给油起步 → 打方向 → 切视角 → 挂倒挡 → 复位。\n每步都读真实状态：挡位、手刹、车速、转向角，做出来才放行。")
    card(sl, 0.67, 3.96, 4.28, 1.12, AMBER, "开局先难住你",
         "出生在空挡 + 手刹拉着：不挂 1 档、不松手刹，油门给到底车也不动。\n教学的第二步就是「把黄键按一下」。")
    bottom = code_image(sl, "code_steps.png", 5.15, 1.42, 4.18,
                        "TutorialDirector.Steps：标题、讲解、判定条件都写在表里，加一步就是加一条")
    footnote(sl, bottom + 0.14,
             "复位的语义也跟着变：教学没做完按退格 = 本步重讲；\n做到最后一步按退格 = 整段教学重来，不新增键位。")


# ---------------- D：碰撞与装饰 ----------------
def slide_d():
    sl = page("地图：先补碰撞，再把路两侧铺满",
              "113 个网格里有 67 个没有碰撞体 —— 撞不到墙，也压不到地")
    card(sl, 0.67, 1.42, 4.28, 1.05, BLUE, "补碰撞",
         "7 棵树补树干胶囊体（layer 8 障碍），地面与全部路面补 MeshCollider\n（layer 9 坡道），车能撞停，也能顺着拱桥的坡走。")
    card(sl, 0.67, 2.56, 4.28, 1.15, RED, "踩过的坑：一个 220 米高的胶囊",
         "树模型缩放了 15 倍，第一版碰撞体照搬局部尺寸，被放大成 13 米粗、\n220 米高的柱子，把整条路封死 —— 换成世界尺寸后半径 0.45 米。")
    card(sl, 0.67, 3.80, 4.28, 1.30, GREEN, "铺装饰：位图 + 距离变换",
         "射线把全图扫成 2 米网格的「路面 / 非路面」位图，再算两遍距离变换，\n按 11–30m / 6.5–10.5m / 4–6m 三条环带撒楼、行道树、灌木，\n共 92 幢 + 151 棵 + 77 丛，0 个压在路面上。")
    bottom = code_image(sl, "code_decor.png", 5.15, 1.42, 4.18,
                        "RoadsideDecorator：正向 + 反向两遍 chamfer，得到每格离路面的真实距离")
    footnote(sl, bottom + 0.14,
             "菜单「科目二 / 装饰 / 沿途铺建筑与行道树」可随时重跑：\n先清掉旧的 Roadside，再按同一套规则重新撒一遍，换一批也不用手摆。")


# ---------------- E：跑出跑道回退 ----------------
def slide_e():
    sl = page("跑出跑道：退回到 3 秒前，而不是重开一局",
              "一段环形缓冲 + 一条向下的射线，出界就回滚，不黑屏不读条")
    card(sl, 0.67, 1.42, 4.28, 1.02, BLUE, "记录",
         "每帧存（时间, 位置, 朝向），只留最近 5 秒；\n超出窗口的从表头丢掉，内存不涨。")
    card(sl, 0.67, 2.56, 4.28, 1.12, AMBER, "判定",
         "从车上方往下打一条 8 米射线，只认 Road 下的路面；\n打空、或掉出地图，连续 0.4 秒就认定出界。")
    card(sl, 0.67, 3.80, 4.28, 1.20, GREEN, "回退",
         "取「3 秒前」那一刻的采样点，WarpTo 回去：速度清零，挡位和手刹\n保持不变，不用重新挂挡；回退后清空历史，避免连环回滚。")
    txt(sl, 0.67, 5.14, 4.28, 0.34,
        "实测：传送到草地 (40, -20)，2 秒内自动回到 3 秒前的位置，rewinds = 1。",
        8.25, MUTED, spacing=1.3)
    bottom = code_image(sl, "code_offtrack.png", 5.15, 1.42, 4.18,
                        "OffTrackResetter：向下探针判定出界 + 取 3 秒前样本回滚")
    footnote(sl, bottom + 0.14,
             "为什么不直接重开关卡：科目二一圈好几分钟，出界就重来太重；\n退回 3 秒既保住了处罚（白跑三秒），也不打断这一圈的节奏。")


slide_a()
slide_b()
slide_c()
slide_d()
slide_e()

# ---- 重新排序 ----
lst = list(prs.slides._sldIdLst)
A, B, C, D, E = lst[11], lst[12], lst[13], lst[14], lst[15]


def move(el, target):
    prs.slides._sldIdLst.remove(el)
    prs.slides._sldIdLst.insert(target, el)


move(A, 5)
move(B, 6)
move(C, 11)
move(D, 12)
move(E, 13)

# ---- 修正原第 5 页：Xbox 的「切视角」是 L3，不是 X（代码 buttonCamera = 8） ----
s5 = prs.slides[4]
for sh in s5.shapes:
    if sh.has_text_frame and sh.text_frame.text.strip() == "X" and abs(sh.left.inches - 6.98) < 0.05:
        for p in sh.text_frame.paragraphs:
            for r in p.runs:
                r.text = "L3"

prs.save(DST)
print("saved", DST, "slides:", len(prs.slides))
