import sys
from pptx import Presentation
from pptx.util import Emu

p = Presentation(r"C:\Users\14763\Desktop\科目二PPT_补充版.pptx")
W, H = Emu(p.slide_width).inches, Emu(p.slide_height).inches
print("size %.2f x %.2f, slides %d" % (W, H, len(p.slides)))
problems = 0
for i, s in enumerate(p.slides, 1):
    title = ""
    pics = 0
    for sh in s.shapes:
        if sh.has_text_frame and not title:
            t = sh.text_frame.text.strip().replace("\n", " ")
            if t:
                title = t[:34]
        if sh.shape_type == 13:
            pics += 1
        l, t_ = Emu(sh.left).inches, Emu(sh.top).inches
        r, b = l + Emu(sh.width).inches, t_ + Emu(sh.height).inches
        if l < -0.01 or t_ < -0.01 or r > W + 0.01 or b > H + 0.01:
            problems += 1
            print("  ! slide %d out of bounds: %s (%.2f,%.2f)-(%.2f,%.2f)" % (i, sh.name, l, t_, r, b))
    print("%2d | pics=%d | %s" % (i, pics, title))
print("out-of-bounds shapes:", problems)
