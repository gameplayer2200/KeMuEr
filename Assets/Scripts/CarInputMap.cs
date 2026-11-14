using UnityEngine;

/// <summary>一次性的驾驶动作（键盘和手柄都归到同一组动作名上）。</summary>
public enum CarAction
{
    Reverse = 0,    // 倒挡 R
    Drive = 1,      // 前进挡 1
    Handbrake = 2,  // 手刹（开关式）
    Signal = 3,     // 转向灯（开关式）
    Camera = 4,     // 切换视角
    Reset = 5,      // 回到 A 点
    Help = 6        // 自检面板
}

/// <summary>
/// 动作名 ↔ 键盘键 ↔ 手柄键的对照表（全工程唯一的一份文案来源）。
/// HUD、自检面板、日志都从这里取字，改键位不用到处找字符串。
/// </summary>
public static class CarInputMap
{
    public const int MaxJoystickButtons = 20;   // KeyCode.JoystickButton0..19

    public static string Label(CarAction a)
    {
        switch (a)
        {
            case CarAction.Reverse: return "倒挡";
            case CarAction.Drive: return "前进挡";
            case CarAction.Handbrake: return "手刹";
            case CarAction.Signal: return "转向灯";
            case CarAction.Camera: return "切视角";
            case CarAction.Reset: return "回起点";
            case CarAction.Help: return "自检面板";
        }
        return a.ToString();
    }

    public static string KeyLabel(CarAction a)
    {
        switch (a)
        {
            case CarAction.Reverse: return "R";
            case CarAction.Drive: return "1";
            case CarAction.Handbrake: return "空格/G";
            case CarAction.Signal: return "T";
            case CarAction.Camera: return "Q";
            case CarAction.Reset: return "退格";
            case CarAction.Help: return "P/F2";
        }
        return "-";
    }

    public static string JoyLabel(CarInput input, CarAction a)
    {
        if (input == null) return "未检测到";
        int idx = input.GetButtonIndex(a);
        return idx < 0 ? "未绑定" : ("键" + idx);
    }

    /// <summary>HUD 左下角那段操作说明，键位直接从运行中的 CarInput 读。</summary>
    public static string HelpLines(CarInput input)
    {
        string padSteer = input != null && input.JoystickAxesOk ? input.ActiveSteerAxis : "未检测到手柄";
        string padGas = input != null && input.JoystickAxesOk ? input.ActiveThrottleAxis : "—";

        return
            "手柄布局：" + (input != null ? input.CurrentProfileName : "键盘") + "\n" +
            "键盘：W/↑ 油门   S/↓ 刹车   A D / ← → 方向盘\n" +
            "手柄：" + padGas + " 推/拉 = 油门/刹车    " + padSteer + " 左右 = 方向盘\n" +
            "键盘 " + KeyLabel(CarAction.Drive) + " / 手柄" + JoyLabel(input, CarAction.Drive) + " = 前进挡        " +
            "键盘 " + KeyLabel(CarAction.Reverse) + " / 手柄" + JoyLabel(input, CarAction.Reverse) + " = 倒挡\n" +
            "键盘 " + KeyLabel(CarAction.Handbrake) + " / 手柄" + JoyLabel(input, CarAction.Handbrake) + " = 手刹        " +
            "键盘 " + KeyLabel(CarAction.Signal) + " / 手柄" + JoyLabel(input, CarAction.Signal) + " = 转向灯\n" +
            "键盘 " + KeyLabel(CarAction.Camera) + " / 手柄" + JoyLabel(input, CarAction.Camera) + " = 切视角        " +
            "键盘 " + KeyLabel(CarAction.Reset) + " / 手柄" + JoyLabel(input, CarAction.Reset) + " = 回 A 点\n" +
            "键盘 " + KeyLabel(CarAction.Help) + " / 手柄" + JoyLabel(input, CarAction.Help) + " = 自检面板（轴/键位对不上时按它）";
    }
}
