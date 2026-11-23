import sys
from pptx import Presentation
from pptx.util import Emu

path = sys.argv[1]
p = Presentation(path)
print("slide size:", p.slide_width, p.slide_height, "=", Emu(p.slide_width).inches, "x", Emu(p.slide_height).inches, "in")
print("slides:", len(p.slides))
print("layouts:")
for i, lay in enumerate(p.slide_layouts):
    print("  ", i, lay.name)
print("masters:", len(p.slide_masters))

for si, s in enumerate(p.slides, 1):
    print("\n=== SLIDE", si, "layout=", s.slide_layout.name, "===")
    for sh in s.shapes:
        t = ""
        if sh.has_text_frame:
            t = " | ".join(par.text for par in sh.text_frame.paragraphs if par.text.strip())
        kind = sh.shape_type
        print(f"  [{kind}] name={sh.name!r} pos=({Emu(sh.left).inches:.2f},{Emu(sh.top).inches:.2f}) size=({Emu(sh.width).inches:.2f}x{Emu(sh.height).inches:.2f})")
        if t:
            print("      text:", t[:300])
        if sh.has_table:
            print("      table rows=", len(sh.table.rows), "cols=", len(sh.table.columns))
        if sh.shape_type == 13:  # picture
            print("      picture:", sh.image.filename, sh.image.size)
    if s.has_notes_slide:
        n = s.notes_slide.notes_text_frame.text.strip()
        if n:
            print("  notes:", n[:200])
