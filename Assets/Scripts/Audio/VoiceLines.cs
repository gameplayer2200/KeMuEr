using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 语音资源清单：key ↔ 台词。
///
/// key 就是 Resources/Audio/Voice/&lt;key&gt;.mp3 的文件名，改台词要重新跑
/// Tools/AudioGen/gen_voice.py（台词也写在那边的 LINES 表里，两边保持一致）。
/// 这里的中文文本另有用途：底部字幕、日志排查。
/// </summary>
public static class VoiceLines
{
    // ---- 第一关 · 按键教学（7 步，对应 TutorialDirector.Steps）----
    public const string TutIntro = "tut_intro";
    public const string TutReset = "tut_reset";
    public const string TutCone = "tut_cone";
    public const string TutDone = "tut_done";

    /// <summary>教学第 index 步（0 基）的语音 key。</summary>
    public static string TutorialStepKey(int index)
    {
        return "tut_s" + Mathf.Clamp(index + 1, 1, 7);
    }

    // ---- 科目二 ----
    public const string Km2Start = "km2_start";
    public const string Km2Done = "km2_done";
    public const string Km2Reset = "km2_reset";

    // ---- 通用 ----
    public const string ReverseWarn = "reverse_warn";

    static readonly Dictionary<string, string> Texts = new Dictionary<string, string>
    {
        { TutIntro, "欢迎来到科目二考场。先跟着屏幕左上角的提示，把摇杆和四个按键认一遍。" },
        { "tut_s1", "第一步，挂前进挡。按绿色按键。" },
        { "tut_s2", "第二步，松手刹。按黄色按键。" },
        { "tut_s3", "第三步，给油。把摇杆轻轻向前推。" },
        { "tut_s4", "第四步，打方向。摇杆向左向右推。" },
        { "tut_s5", "第五步，切视角。按蓝色按键。" },
        { "tut_s6", "第六步，挂倒挡。先停稳，再按红色按键。" },
        { "tut_s7", "最后一步，复位。按键盘退格，回到起点重来。" },
        { TutDone, "按键教学完成。接下来跑一圈。" },
        { TutCone, "碰倒了锥桶。教学关不扣分，正式考试扣十分。" },
        { TutReset, "已回到起点，按键教学从头开始。" },

        { Km2Start, "科目二考试开始。第一项，倒车入库。" },
        { "km2_s1", "第一项，倒车入库。请把车开到库前停止线，挂倒挡，倒入右侧库位，车身摆正后停稳。" },
        { "km2_s2", "第二项，侧方停车。请把车开过车位，挂倒挡，倒入车位停稳。" },
        { "km2_s3", "第三项，坡道定点停车和起步。请上坡，在前方停车线停稳，再起步下坡。" },
        { "km2_s4", "第四项，直角转弯。请减速，沿内侧通过直角弯。" },
        { "km2_s5", "第五项，曲线行驶。请保持低速，沿曲线通过。" },
        { Km2Done, "科目二完成，这一圈跑完了。" },
        { Km2Reset, "已回到本项起点。" },

        { ReverseWarn, "倒车，请注意。" },
    };

    public static string Text(string key)
    {
        if (string.IsNullOrEmpty(key)) return "";
        string s;
        return Texts.TryGetValue(key, out s) ? s : "";
    }

    /// <summary>科目编号（1..5）→ 语音 key。</summary>
    public static string StageKey(int number)
    {
        int n = Mathf.Clamp(number, 1, 5);
        return "km2_s" + n;
    }
}
