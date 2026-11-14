using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 手柄自检面板（游戏内按 P，或长按手柄切视角键）。
/// 自制手柄最常见的三个坑，这一屏全看得到：
///   1) 系统到底认没认出这块板子（设备名列表）；
///   2) 摇杆被报在哪根轴上（实时轴值，推一下就看得见），以及当前生效的是哪根；
///   3) 四颗键的实际编号（按下即高亮），配合扫描模式可以直接重绑。
/// 依赖 InputManager 里预置的 Joy Axis 1..8 代理轴（type = Joystick Axis，axis = 0..7）。
/// </summary>
public class InputDiagnostics : MonoBehaviour
{
    public CarInput input;

    [Header("要监视的轴名（InputManager 里必须存在）")]
    public string[] axisProbeNames = { "Joy Axis 1", "Joy Axis 2", "Joy Axis 3", "Joy Axis 4",
                                       "Joy Axis 5", "Joy Axis 6", "Joy Axis 7", "Joy Axis 8" };

    [Header("开关")]
    public KeyCode toggleKey = KeyCode.P;
    public bool startVisible = false;

    public bool Visible { get; private set; }

    /// <summary>外部打开/关闭面板（设置菜单、教程引导都会用到）。</summary>
    public void SetVisible(bool value) { Visible = value; }
    public void Toggle() { Visible = !Visible; }

    readonly Dictionary<string, float> _values = new Dictionary<string, float>();
    readonly Dictionary<string, bool> _exists = new Dictionary<string, bool>();
    int _pressedButton = -1;
    float _refreshTimer;

    GUIStyle _title, _body, _mono, _warn, _ok;

    void Awake()
    {
        if (input == null) input = GetComponent<CarInput>();
        Visible = startVisible;
    }

    void OnEnable()
    {
        if (input != null)
        {
            input.OnAction += OnAction;
            input.OnScanModeChanged += OnScanModeChanged;
        }
    }

    void OnDisable()
    {
        if (input != null)
        {
            input.OnAction -= OnAction;
            input.OnScanModeChanged -= OnScanModeChanged;
        }
    }

    void OnAction(CarAction action)
    {
        if (action == CarAction.Help) Visible = !Visible;
    }

    void OnScanModeChanged(bool scanning)
    {
        if (scanning) Visible = true;
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey)) Visible = !Visible;

        _refreshTimer -= Time.unscaledDeltaTime;
        if (_refreshTimer > 0f) return;
        _refreshTimer = 0.05f;   // 20Hz 刷新，避免每帧反射轴名

        _pressedButton = -1;
        for (int i = 0; i < CarInputMap.MaxJoystickButtons; i++)
        {
            KeyCode k = (KeyCode)((int)KeyCode.JoystickButton0 + i);
            if (Input.GetKey(k)) { _pressedButton = i; break; }
        }

        _values.Clear();
        _exists.Clear();
        if (axisProbeNames == null) return;

        for (int i = 0; i < axisProbeNames.Length; i++)
        {
            string name = axisProbeNames[i];
            if (string.IsNullOrEmpty(name)) continue;
            try
            {
                _values[name] = Input.GetAxisRaw(name);
                _exists[name] = true;
            }
            catch (System.ArgumentException)
            {
                _values[name] = 0f;
                _exists[name] = false;
            }
        }
    }

    void EnsureStyles()
    {
        if (_title != null) return;
        _title = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
        _title.normal.textColor = new Color(1f, 0.93f, 0.82f);
        _body = new GUIStyle(GUI.skin.label) { fontSize = 14 };
        _body.normal.textColor = Color.white;
        _mono = new GUIStyle(GUI.skin.label) { fontSize = 13, font = Font.CreateDynamicFontFromOSFont("Consolas", 13) };
        _mono.normal.textColor = new Color(0.82f, 0.88f, 0.95f);
        _warn = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
        _warn.normal.textColor = new Color(1f, 0.55f, 0.45f);
        _ok = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
        _ok.normal.textColor = new Color(0.55f, 1f, 0.55f);
    }

    void OnGUI()
    {
        if (!Visible) return;
        EnsureStyles();

        float w = 470f, h = 700f;
        Rect panel = new Rect(Screen.width - w - 24f, 130f, w, h);
        var old = GUI.color;
        GUI.color = new Color(0.03f, 0.04f, 0.06f, 0.88f);
        GUI.DrawTexture(panel, Texture2D.whiteTexture);
        GUI.color = old;

        float x = panel.x + 14f, y = panel.y + 10f, line = 20f;

        GUI.Label(new Rect(x, y, w - 28f, 24f), "手柄自检  [P 关闭]", _title); y += 26f;

        // ---- 布局预设 ----
        if (input != null)
        {
            GUI.Label(new Rect(x, y, w - 28f, 20f), "布局：" + input.CurrentProfileName, _ok);
            y += 22f;

            float bw = 96f, gap = 6f;
            if (GUI.Button(new Rect(x + 12f, y, bw, 22f), "自动识别"))
                input.SetProfile(ControlProfileKind.Auto);
            if (GUI.Button(new Rect(x + 12f + bw + gap, y, bw, 22f), "标准手柄"))
                input.SetProfile(ControlProfileKind.XboxGamepad);
            if (GUI.Button(new Rect(x + 12f + (bw + gap) * 2f, y, bw, 22f), "自制板 X1K4"))
                input.SetProfile(ControlProfileKind.CustomX1K4);
            y += 28f;

            // 左右推 / 上下推之后，这两行数字就是判断极性和方向的依据
            GUI.Label(new Rect(x + 12f, y, w - 40f, 20f),
                string.Format("方向盘 {0,6:F2}    油门 {1,5:F2}    刹车 {2,5:F2}",
                    input.Steer, input.Throttle, input.Brake), _mono);
            y += 20f;

            if (GUI.Button(new Rect(x + 12f, y, 150f, 22f),
                    "方向盘反向：" + (input.invertSteer ? "已反" : "正常")))
                input.invertSteer = !input.invertSteer;
            if (GUI.Button(new Rect(x + 170f, y, 150f, 22f),
                    "油门反向：" + (input.invertThrottle ? "已反" : "正常")))
            {
                input.invertThrottle = !input.invertThrottle;
                input.RefreshAxes();   // 反向会重置校准，重新取中位
            }
            y += 28f;
        }

        // ---- 设备 ----
        if (!Application.isEditor && input != null && !input.JoystickAxesOk)
        {
            GUI.Label(new Rect(x, y, w - 28f, 20f), "× 没找到手柄轴：InputManager 缺 Stick X/Y 或 Joy Axis 1..8", _warn);
            y += line;
        }

        string[] names = input != null ? input.JoystickNames : Input.GetJoystickNames();
        int real = 0;
        for (int i = 0; i < names.Length; i++) if (!string.IsNullOrEmpty(names[i])) real++;

        GUI.Label(new Rect(x, y, w - 28f, 20f),
            real > 0 ? ("√ 检测到 " + real + " 个设备") : "× 没检测到手柄设备（插上板子后等 0.5 秒）",
            real > 0 ? _ok : _warn);
        y += line;

        for (int i = 0; i < names.Length && i < 4; i++)
        {
            if (string.IsNullOrEmpty(names[i])) continue;
            GUI.Label(new Rect(x + 12f, y, w - 40f, 20f), "[" + i + "] " + names[i], _mono);
            y += 18f;
        }

        y += 6f;

        // ---- 轴 ----
        GUI.Label(new Rect(x, y, w - 28f, 20f), "轴（推一下摇杆看哪个在动）", _title); y += 22f;

        if (axisProbeNames != null)
        {
            for (int i = 0; i < axisProbeNames.Length; i++)
            {
                string name = axisProbeNames[i];
                if (string.IsNullOrEmpty(name)) continue;

                bool exists = _exists.TryGetValue(name, out bool ok) && ok;
                float v = exists && _values.TryGetValue(name, out float val) ? val : 0f;

                string mark = exists ? (Mathf.Abs(v) > 0.15f ? " <<<" : "") : "  (InputManager 无此轴)";
                GUI.Label(new Rect(x + 12f, y, w - 40f, 18f),
                    string.Format("{0,-10} {1,7:F3}{2}", name, v, mark),
                    exists && Mathf.Abs(v) > 0.15f ? _ok : _mono);
                y += 18f;
            }
        }

        y += 6f;

        // ---- 当前生效轴 ----
        if (input != null)
        {
            GUI.Label(new Rect(x, y, w - 28f, 20f),
                "生效轴：方向盘 = " + (input.JoystickAxesOk ? input.ActiveSteerAxis : "（键盘）") +
                "   油门/刹车 = " + (input.JoystickAxesOk ? input.ActiveThrottleAxis : "（键盘）"), _body);
            y += 22f;
        }

        // ---- 按键 ----
        GUI.Label(new Rect(x, y, w - 28f, 20f), "当前按下的手柄键：", _title); y += 22f;
        GUI.Label(new Rect(x + 12f, y, w - 40f, 20f),
            _pressedButton >= 0 ? ("JoystickButton" + _pressedButton) : "（没有）",
            _pressedButton >= 0 ? _ok : _mono);
        y += 24f;

        // ---- 绑定表 ----
        GUI.Label(new Rect(x, y, w - 28f, 20f), "绑定", _title); y += 22f;

        if (input != null)
        {
            if (input.Scanning)
            {
                GUI.Label(new Rect(x + 12f, y, w - 40f, 20f),
                    "扫描中：请按「" + CarInputMap.Label(input.ScanAction) + "」的实体键（Esc 取消）", _warn);
                y += 20f;
                GUI.Label(new Rect(x + 12f, y, w - 40f, 20f),
                    "进度 " + (input.ScanStep + 1) + " / 5", _mono);
                y += 22f;
            }

            CarAction[] all = { CarAction.Reverse, CarAction.Drive, CarAction.Handbrake,
                                CarAction.Signal, CarAction.Camera, CarAction.Reset, CarAction.Help };
            for (int i = 0; i < all.Length; i++)
            {
                int idx = input.GetButtonIndex(all[i]);
                GUI.Label(new Rect(x + 12f, y, w - 40f, 18f),
                    string.Format("{0,-6} 键盘 {1,-6} 手柄 {2}", CarInputMap.Label(all[i]),
                        CarInputMap.KeyLabel(all[i]), idx < 0 ? "未绑定" : ("键" + idx)), _mono);
                y += 18f;
            }

            y += 6f;
            GUI.Label(new Rect(x + 12f, y, w - 40f, 20f), "长按手柄切视角键（或键盘 F2）1.5 秒 → 重新扫描按键", _body);
            y += 20f;

            if (GUI.Button(new Rect(x + 12f, y, 130f, 24f), "重新探测轴"))
                input.RefreshAxes();
            if (GUI.Button(new Rect(x + 152f, y, 130f, 24f), "开始按键扫描"))
                input.BeginScan();
        }
    }
}
