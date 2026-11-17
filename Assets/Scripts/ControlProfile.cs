using UnityEngine;

/// <summary>用哪种手柄布局。</summary>
public enum ControlProfileKind
{
    Auto = 0,        // 看设备名自动判断
    CustomX1K4 = 1,  // 自制纸板控制器（X1K4 摇杆 + 4 颗彩键 + Pro Micro）
    XboxGamepad = 2  // 市售标准手柄（Xbox / PS 的 ABXY 布局）
}

/// <summary>一份完整的输入布局数据。</summary>
[System.Serializable]
public class ProfileData
{
    public string displayName;
    public string steerAxes;        // 逗号分隔的候选轴名
    public string throttleAxes;
    public int buttonDrive;         // 绿 → 1 档
    public int buttonReverse;       // 红 → R 档
    public int buttonHandbrake;     // 黄 → 手刹
    public int buttonReset;         // 蓝 → 复位（科目二回当前科目起点）
    public int buttonCamera;        // 摇杆下压 → 切视角
    public bool invertThrottle;
    public bool invertSteer;
    public string note;
}

/// <summary>
/// 把「自制控制器配置图」翻译成具体的手柄键号。
///
/// 配置图（俯视 160×90mm）的 5 个输入元件与标准手柄的一一对应：
///   ①X 轴 → 方向盘      → 左摇杆左右
///   ②Y 轴 → 油门/刹车   → 左摇杆上下（Xbox 上推为负，需要反向）
///   ③摇杆下压 → 切视角  → L3（左摇杆按下）
///   ⑥绿键 → 1 档        → A
///   ④红键 → R 档        → B
///   ⑦蓝键 → 复位        → X
///   ⑤黄键 → 手刹        → Y
/// 颜色正好对上 Xbox 的 A绿 / B红 / X蓝 / Y黄，所以这不是硬凑，是把自制板的配色逻辑搬到标准手柄上。
/// </summary>
public static class ControlProfiles
{
    public static ProfileData Get(ControlProfileKind kind)
    {
        switch (kind)
        {
            case ControlProfileKind.XboxGamepad: return Xbox();
            case ControlProfileKind.CustomX1K4: return CustomBoard();
            default: return Xbox();
        }
    }

    /// <summary>标准手柄（Xbox 布局）。PS 手柄键号相同，扫描一次即可。</summary>
    static ProfileData Xbox()
    {
        return new ProfileData
        {
            displayName = "标准手柄（Xbox 布局）",
            steerAxes = "Stick X",
            throttleAxes = "Stick Y",
            buttonDrive = 0,      // A 绿
            buttonReverse = 1,    // B 红
            buttonHandbrake = 3,  // Y 黄
            buttonReset = 2,      // X 蓝 → 复位
            buttonCamera = 8,     // L3 左摇杆按下
            invertThrottle = true,   // Windows 下 Xbox 左摇杆上推为负值，必须反向
            invertSteer = false,
            note = "A=1档 B=R档 Y=手刹 X=复位 L3=切视角（与纸板五个输入严格一一对应）"
        };
    }

    /// <summary>自制纸板控制器：键号取决于固件里 Joystick.button() 的调用顺序。</summary>
    static ProfileData CustomBoard()
    {
        return new ProfileData
        {
            displayName = "自制控制器 X1K4",
            steerAxes = "Stick X,Stick X2",
            throttleAxes = "Stick Y,Stick Y2",
            buttonDrive = 1,      // 绿
            buttonReverse = 0,    // 红
            buttonHandbrake = 2,  // 黄
            buttonReset = 3,      // 蓝 → 复位
            buttonCamera = 4,     // 摇杆下压
            invertThrottle = false,
            invertSteer = false,
            note = "键号由固件决定，对不上就长按切视角键扫描一次"
        };
    }

    /// <summary>按设备名猜：认到 Xbox / XInput / Wireless Controller 就走标准手柄预设。</summary>
    public static ControlProfileKind Detect()
    {
        var names = Input.GetJoystickNames();
        int slots = 0;
        int named = 0;

        for (int i = 0; i < names.Length; i++)
        {
            string n = names[i];
            if (string.IsNullOrEmpty(n)) { if (n != null) slots++; continue; }

            slots++;
            named++;

            string lower = n.ToLowerInvariant();
            if (lower.Contains("xbox") || lower.Contains("xinput") ||
                lower.Contains("wireless controller") || lower.Contains("dualshock") ||
                lower.Contains("dualsense") || lower.Contains("gamepad") ||
                lower.Contains("controller") || lower.Contains("joystick"))
                return ControlProfileKind.XboxGamepad;
        }

        // 有摇杆槽位但名字全是空的 —— 典型是 Steam Input 在中间接管了设备，
        // 它对外表现为"有一个名额但没有名字"。这种几乎都是市售标准手柄。
        if (slots > 0 && named == 0) return ControlProfileKind.XboxGamepad;

        return ControlProfileKind.CustomX1K4;
    }

    /// <summary>把预设写进 CarInput（只在切预设时调用，不是每帧）。</summary>
    public static void Apply(CarInput input, ControlProfileKind kind)
    {
        if (input == null) return;

        ProfileData data = Get(kind);
        input.steerAxisCandidates = Split(data.steerAxes);
        input.throttleAxisCandidates = Split(data.throttleAxes);
        input.buttonDrive = data.buttonDrive;
        input.buttonReverse = data.buttonReverse;
        input.buttonHandbrake = data.buttonHandbrake;
        input.buttonReset = data.buttonReset;
        input.buttonCamera = data.buttonCamera;
        input.invertThrottle = data.invertThrottle;
        input.invertSteer = data.invertSteer;
    }

    static string[] Split(string csv)
    {
        if (string.IsNullOrEmpty(csv)) return new string[0];
        string[] parts = csv.Split(',');
        for (int i = 0; i < parts.Length; i++) parts[i] = parts[i].Trim();
        return parts;
    }
}
