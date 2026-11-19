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
    // ---- 第一关 · 教学 ----
    public const string TutIntro = "tut_intro";
    public const string TutReset = "tut_reset";
    public const string TutCone = "tut_cone";
    public const string TutDone = "tut_done";

    // ---- 科目二 ----
    public const string Km2Start = "km2_start";
    public const string Km2Done = "km2_done";
    public const string Km2Reset = "km2_reset";

    // ---- 通用 ----
    public const string ReverseWarn = "reverse_warn";

    static readonly Dictionary<string, string> Texts = new Dictionary<string, string>
    {
        { TutIntro, "欢迎来到科目二考场。第一关，教学。请从 A 点出发，按提示开到 B 点并停稳。" },
        { "tut_wp1", "起步直行。" },
        { "tut_wp2", "前方路口，右转。" },
        { "tut_wp3", "沿横路直行。" },
        { "tut_wp4", "到达 B 点，请停稳。" },
        { TutDone, "第一关完成。" },
        { TutCone, "碰倒了锥桶。教学关不扣分，正式考试扣十分。" },
        { TutReset, "已回到 A 点，请重新出发。" },

        { Km2Start, "科目二考试开始。" },
        { "km2_s1", "第一项，倒车入库。请把车开到库前停止线，挂倒挡，倒入右侧库位，车身摆正后停稳。" },
        { "km2_s2", "第二项，侧方停车。请把车开过车位，挂倒挡，倒入车位停稳。" },
        { "km2_s3", "第三项，坡道定点停车和起步。请上坡，在前方停车线停稳，再起步下坡。" },
        { "km2_s4", "第四项，直角转弯。请减速，沿内侧通过直角弯。" },
        { "km2_s5", "第五项，曲线行驶。请保持低速，沿曲线通过。" },
        { Km2Done, "科目二路线走完。考试结束。" },
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
