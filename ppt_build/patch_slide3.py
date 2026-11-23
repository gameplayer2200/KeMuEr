import io

p = "build_pptx.py"
s = io.open(p, encoding="utf-8").read()

# 1) C 页：7 步 -> 6 步（代码里 Steps 现在只有 6 条，复位步骤已移除）
pairs = [
    ('"教学关重写：从 A / B 路点到 7 步按键"', '"教学关重写：从 A / B 路点到 6 步按键"'),
    ('"挂 1 档 → 松手刹 → 给油起步 → 打方向 → 切视角 → 挂倒挡 → 复位。\\n每步都读真实状态：挡位、手刹、车速、转向角，做出来才放行。"',
     '"挂 1 档 → 松手刹 → 给油起步 → 打方向 → 切视角 → 挂倒挡。\\n每步都读真实状态：挡位、手刹、车速、转向角，做出来才放行。"'),
    ('"复位的语义也跟着变：教学没做完按退格 = 本步重讲；\\n做到最后一步按退格 = 整段教学重来，不新增键位。"',
     '"卡住不会跳过：判定只读真实状态，按错键不往前跳；\\nHUD 顶部一直停在这一步，直到动作真的做出来。"'),
    ('"去掉 A / B 路点，改成 7 步必须真的做出来的按键教学"',
     '"去掉 A / B 路点，改成 6 步必须真的做出来的按键教学"'),
]
for old, new in pairs:
    assert old in s, old[:40]
    s = s.replace(old, new)

# 2) 第 3 页换图
fix = '''
# ---- 第 3 页：换成当前场景的真实截图 ----
def fix_slide3():
    s3 = prs.slides[2]
    for sh in list(s3.shapes):
        if sh.shape_type == 13:
            sh._element.getparent().remove(sh._element)
    s3.shapes.add_picture(os.path.join(IMG, "slide3_hero.png"),
                          Inches(0.70), Inches(1.40), Inches(4.78), Inches(3.10))
    s3.shapes.add_picture(os.path.join(IMG, "slide3_game.png"),
                          Inches(5.83), Inches(1.40), Inches(3.47), Inches(1.95))
    s3.shapes.add_picture(os.path.join(IMG, "slide3_scene.png"),
                          Inches(5.83), Inches(3.45), Inches(3.47), Inches(1.05))
    for sh in s3.shapes:
        if sh.has_text_frame and sh.text_frame.text.startswith("左为整圈路网俯瞰"):
            p0 = sh.text_frame.paragraphs[0]
            if p0.runs:
                p0.runs[0].text = "左为整圈路网俯瞰；右上为玩家视角的开局画面，右下为编辑器里的同一片场景"
                for r in p0.runs[1:]:
                    r.text = ""


fix_slide3()


'''
anchor = "prs.save(DST)"
assert anchor in s
s = s.replace(anchor, fix.lstrip("\n") + anchor, 1)

io.open(p, "w", encoding="utf-8", newline="").write(s)
print("patched slide3 + 6 steps")
