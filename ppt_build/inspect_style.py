import sys
from pptx import Presentation
from pptx.util import Emu

path = r"C:\Users\14763\Desktop\科目二PPT.pptx"
p = Presentation(path)

def color_of(fmt):
    try:
        if fmt.type is None:
            return None
        if fmt.type == 1:  # solid
            c = fmt.fore_color
            if c.type == 1:
                return "rgb(%02X%02X%02X)" % (c.rgb[0], c.rgb[1], c.rgb[2])
            return "theme:%s" % c.theme_color
    except Exception as e:
        return "err:%s" % e
    return str(fmt.type)

for si in (0, 1, 3, 7, 8):
    s = p.slides[si]
    print("\n=== SLIDE", si + 1, "===")
    for sh in s.shapes:
        line = []
        try:
            line.append("fill=" + str(color_of(sh.fill)))
        except Exception:
            pass
        if sh.line.fill.type is not None:
            try:
                line.append("line=" + str(color_of(sh.line.fill)) + " w=" + str(sh.line.width))
            except Exception:
                pass
        if sh.has_text_frame:
            for para in sh.text_frame.paragraphs:
                for r in para.runs:
                    c = None
                    try:
                        c = r.font.color.rgb
                    except Exception:
                        try:
                            c = "theme:" + str(r.font.color.theme_color)
                        except Exception:
                            c = None
                    if r.text.strip():
                        print(f"  {sh.name}: '{r.text[:28]}' size={r.font.size.pt if r.font.size else None} bold={r.font.bold} italic={r.font.italic} font={r.font.name} color={c} | shape: {' '.join(line)}")
                        break
        else:
            print(f"  {sh.name}: shape: {' '.join(line)}")

print("\ntheme colors:")
th = p.slide_masters[0].part.part_related_by("http://schemas.openxmlformats.org/officeDocument/2006/relationships/theme")
import re
xml = th.blob.decode("utf-8", "ignore")
m = re.search(r"<a:clrScheme.*?</a:clrScheme>", xml, re.S)
print(m.group(0)[:1500] if m else "none")
mf = re.search(r"<a:fontScheme.*?</a:fontScheme>", xml, re.S)
print(mf.group(0)[:600] if mf else "no font scheme")
