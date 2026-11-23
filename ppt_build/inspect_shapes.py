from pptx import Presentation
from pptx.util import Emu

p = Presentation(r"C:\Users\14763\Desktop\科目二PPT.pptx")

def fill_of(sh):
    try:
        f = sh.fill
        if f.type is None:
            return "-"
        if f.type == 1:
            c = f.fore_color
            if c.type == 1:
                return "#%02X%02X%02X" % (c.rgb[0], c.rgb[1], c.rgb[2])
            return "theme:%s" % c.theme_color
        return "type%s" % f.type
    except Exception as e:
        return "err"

def line_of(sh):
    try:
        l = sh.line
        if l.fill.type is None:
            return "-"
        if l.fill.type == 1:
            c = l.fill.fore_color
            if c.type == 1:
                return "#%02X%02X%02X/%s" % (c.rgb[0], c.rgb[1], c.rgb[2], l.width)
        return "type%s/%s" % (l.fill.type, l.width)
    except Exception:
        return "-"

for si in (0, 1, 3, 6, 7, 8):
    s = p.slides[si]
    print("=== SLIDE", si + 1, "===")
    for sh in s.shapes:
        print("  %-22s %-12s fill=%-10s line=%-16s pos=(%.2f,%.2f,%.2f,%.2f)" % (
            sh.name[:22], str(sh.shape_type)[:12], fill_of(sh), line_of(sh),
            Emu(sh.left).inches, Emu(sh.top).inches, Emu(sh.width).inches, Emu(sh.height).inches))
