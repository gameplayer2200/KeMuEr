import io

p = "build_pptx.py"
s = io.open(p, encoding="utf-8").read()

old = ('    code_image(sl, "code_scan.png", 5.15, 1.42, 4.18,\n'
       '               "CarInput.UpdateScan：扫到哪个键就写回哪一个动作，扫完直接生效")')
new = ('    bottom = code_image(sl, "code_scan.png", 5.15, 1.42, 4.18,\n'
       '                        "CarInput.UpdateScan：扫到哪个键就写回哪一个动作，扫完直接生效")\n'
       '    footnote(sl, bottom + 0.14,\n'
       '             "重绑只写进运行时的 CarInput，不落场景文件：\\n"\n'
       '             "换回自己的板子，重开一次 Play 就恢复成预设。")')

assert old in s
s = s.replace(old, new)
io.open(p, "w", encoding="utf-8", newline="").write(s)
print("patched")
