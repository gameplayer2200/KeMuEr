# -*- coding: utf-8 -*-
"""用 edge-tts 批量生成中文语音引导，输出到 Assets/Resources/Audio/Voice/*.mp3。

依赖： pip install edge-tts
用法： python Tools/AudioGen/gen_voice.py [key ...]
    不带参数 = 生成全部；带 key = 只重生成指定条目。
"""
import asyncio
import json
import os
import sys

import edge_tts

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.dirname(os.path.dirname(HERE))
OUT_DIR = os.path.join(PROJECT, "Assets", "Resources", "Audio", "Voice")

VOICE = "zh-CN-XiaoxiaoNeural"
RATE = "-4%"

# key -> 台词。key 与 C# VoiceId 常量一一对应，文件名即 key。
LINES = {
    # ---- 第一关 · 按键教学（7 步，见 TutorialDirector.Steps）----
    "tut_intro": "欢迎来到科目二考场。先跟着屏幕左上角的提示，把摇杆和四个按键认一遍。",
    "tut_s1": "第一步，挂前进挡。按绿色按键。",
    "tut_s2": "第二步，松手刹。按黄色按键。",
    "tut_s3": "第三步，给油。把摇杆轻轻向前推。",
    "tut_s4": "第四步，打方向。摇杆向左向右推。",
    "tut_s5": "第五步，切视角。按蓝色按键。",
    "tut_s6": "第六步，挂倒挡。先停稳，再按红色按键。",
    "tut_s7": "最后一步，复位。按键盘退格，回到起点重来。",
    "tut_done": "按键教学完成。接下来跑一圈。",
    "tut_cone": "碰倒了锥桶。教学关不扣分，正式考试扣十分。",
    "tut_reset": "已回到起点，按键教学从头开始。",
    # ---- 科目二 · 五项 ----
    "km2_start": "科目二考试开始。第一项，倒车入库。",
    "km2_s1": "第一项，倒车入库。请把车开到库前停止线，挂倒挡，倒入右侧库位，车身摆正后停稳。",
    "km2_s2": "第二项，侧方停车。请把车开过车位，挂倒挡，倒入车位停稳。",
    "km2_s3": "第三项，坡道定点停车和起步。请上坡，在前方停车线停稳，再起步下坡。",
    "km2_s4": "第四项，直角转弯。请减速，沿内侧通过直角弯。",
    "km2_s5": "第五项，曲线行驶。请保持低速，沿曲线通过。",
    "km2_done": "科目二完成，这一圈跑完了。",
    "km2_reset": "已回到本项起点。",
    # ---- 通用提示 ----
    "reverse_warn": "倒车，请注意。",
}


async def synth(key: str, text: str) -> None:
    path = os.path.join(OUT_DIR, key + ".mp3")
    comm = edge_tts.Communicate(text, VOICE, rate=RATE)
    await comm.save(path)
    print("%-14s %6d B  %s" % (key + ".mp3", os.path.getsize(path), text))


async def main(keys):
    os.makedirs(OUT_DIR, exist_ok=True)
    todo = [(k, LINES[k]) for k in (keys or LINES)]
    for i, (k, t) in enumerate(todo, 1):
        print("[%2d/%d] %s" % (i, len(todo), k), flush=True)
        for attempt in range(1, 4):
            try:
                await synth(k, t)
                break
            except Exception as exc:  # 网络抖动重试
                print("    ! 第 %d 次失败：%s" % (attempt, exc), flush=True)
                if attempt == 3:
                    raise
                await asyncio.sleep(1.5)
    with open(os.path.join(HERE, "voice_lines.json"), "w", encoding="utf-8") as f:
        json.dump({"voice": VOICE, "rate": RATE, "lines": LINES}, f, ensure_ascii=False, indent=2)
    print("\n完成 ->", OUT_DIR)


if __name__ == "__main__":
    asyncio.run(main(sys.argv[1:]))
