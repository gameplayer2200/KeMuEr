using System;
using UnityEngine;

public enum DriveGear
{
    Neutral = 0,
    Reverse = 1,
    Drive = 2
}

/// <summary>
/// 手柄 + 键盘的统一输入层（全工程唯一读取 Input 的地方）。
///
/// 手柄（自制 X1K4 + Pro Micro，模拟标准 HID Gamepad）：
///   X 轴 = 方向盘，Y 轴 = 油门/刹车，5 个按键走 <see cref="buttonIndices"/>，摇杆按下 = 切镜头。
/// 键盘：A/D 或 ←/→ 方向盘，W/↑ 油门，S/↓ 刹车，R 倒挡，1 前进挡，空格/G 手刹，Q 切视角。
///
/// 三件为自制手柄专门做的事：
///   1) 轴名候选列表：不同的 HID 描述符会把摇杆报在 axis 0/1（X/Y）或 2/3（Z/Rz）上，
///      运行时依次探测，第一个有效的胜出，探不到就静默退回键盘。
///   2) 上电中位校准：把静止时的轴值记成 0，避免 PS2 摇杆 ±10% 的偏置让车自己跑。
///   3) 按键扫描（Rebind）：板子上 K1–K4 的实际编号取决于固件里 Joystick.button() 的调用顺序，
///      长按「切视角」键 1.5 秒进入扫描，依次按 倒挡→前进挡→手刹→转向灯→切视角 即可重新绑好。
///
/// 所有一次性动作都走事件（<see cref="OnAction"/>），CarController / ChaseCamera / TutorialDirector
/// 各自订阅，这样不再出现「CarInput 在 Update 里置位、别人在 Update 里消费」的执行顺序隐患。
/// </summary>
public class CarInput : MonoBehaviour, IDrivingInput
{
    [Header("手柄布局预设")]
    [Tooltip("Auto = 按设备名自动判断；也可以强制指定自制板或标准手柄")]
    public ControlProfileKind profile = ControlProfileKind.Auto;

    [Header("摇杆轴名候选（InputManager，按顺序探测）")]
    public string[] steerAxisCandidates = { "Stick X", "Stick X2" };
    public string[] throttleAxisCandidates = { "Stick Y", "Stick Y2" };

    [Header("手柄按键编号")]
    public int buttonReverse = 0;     // 红
    public int buttonDrive = 1;       // 绿
    public int buttonHandbrake = 2;   // 黄
    public int buttonCamera = 4;      // 摇杆按下

    [Header("手感参数")]
    [Range(0f, 0.3f)] public float deadzone = 0.08f;
    public bool invertSteer = false;
    public bool invertThrottle = false;
    public bool useJoystick = true;
    [Tooltip("上电后多少秒内取均值当中位")]
    public float calibrateSeconds = 0.4f;

    [Header("按键扫描")]
    [Tooltip("长按切视角键多久进入扫描模式")]
    public float scanHoldSeconds = 1.5f;

    // ---------------- 对外状态 ----------------

    /// <summary>手柄轴可用（InputManager 里配了轴，且至少探测到一个）。</summary>
    public bool JoystickAxesOk { get; private set; }
    /// <summary>手柄轴可用，且系统里确实插着手柄。</summary>
    public bool JoystickConnected { get; private set; }

    public string ActiveSteerAxis { get; private set; } = "";
    public string ActiveThrottleAxis { get; private set; } = "";

    public float Steer { get; private set; }       // -1..1
    public float Throttle { get; private set; }    // 0..1
    public float Brake { get; private set; }       // 0..1
    public bool Handbrake { get; private set; }

    /// <summary>由车控在开局/复位时直接设定手刹状态（不触发按键事件）。</summary>
    public void SetHandbrake(bool on)
    {
        Handbrake = on;
    }

    /// <summary>任意驾驶动作触发（键盘或手柄）。</summary>
    public event Action<CarAction> OnAction;
    /// <summary>进入/退出按键扫描模式。参数 true = 进入。</summary>
    public event Action<bool> OnScanModeChanged;
    /// <summary>扫描模式里已经绑好的键，参数 (第几个槽位, 按钮号)。</summary>
    public event Action<int, int> OnScanBound;

    // 一次性事件（保留旧的 Consume 接口给 CarController）
    bool _wantDrive;
    bool _wantReverse;

    // 中位校准
    bool _calibrated;
    float _calTimer, _calSumX, _calSumY;
    int _calFrames;
    float _centerX, _centerY;

    // 设备轮询（避免每帧 GetJoystickNames 产生 GC）
    float _pollTimer;
    string[] _joyNames = new string[0];

    // 扫描模式（BeginScan 时按当前是否有空余键重建：切视角没绑就只扫三步）
    static readonly CarAction[] FullScanOrder =
    {
        CarAction.Reverse, CarAction.Drive, CarAction.Handbrake, CarAction.Camera
    };
    static readonly CarAction[] FourKeyScanOrder =
    {
        CarAction.Reverse, CarAction.Drive, CarAction.Handbrake
    };
    CarAction[] _scanOrder = FullScanOrder;
    int _scanStep = -1;
    float _cameraHold;

    public bool Scanning { get { return _scanStep >= 0; } }
    /// <summary>扫描进度：当前要按第几个槽位（从 0 开始）。</summary>
    public int ScanStep { get { return _scanStep; } }
    public CarAction ScanAction
    {
        get { return _scanStep >= 0 && _scanStep < _scanOrder.Length ? _scanOrder[_scanStep] : CarAction.Reverse; }
    }

    public string[] JoystickNames { get { return _joyNames; } }

    // ---------------- 生命周期 ----------------

    static readonly string[] DefaultSteerAxes = { "Stick X", "Stick X2" };
    static readonly string[] DefaultThrottleAxes = { "Stick Y", "Stick Y2" };

    /// <summary>当前生效的布局名，给 HUD / 自检面板显示。</summary>
    public string CurrentProfileName { get; private set; } = "未初始化";

    /// <summary>实际生效的预设（Auto 解析之后）。</summary>
    public ControlProfileKind ResolvedProfile { get; private set; } = ControlProfileKind.CustomX1K4;

    void Awake()
    {
        EnsureDefaults();
        ApplyProfile();
    }

    /// <summary>按 profile 字段（或自动识别）写入轴候选与键号。</summary>
    void ApplyProfile()
    {
        ResolvedProfile = profile == ControlProfileKind.Auto ? ControlProfiles.Detect() : profile;
        ControlProfiles.Apply(this, ResolvedProfile);
        CurrentProfileName = ControlProfiles.Get(ResolvedProfile).displayName;
    }

    /// <summary>运行时切换布局。</summary>
    public void SetProfile(ControlProfileKind kind)
    {
        profile = kind;
        ApplyProfile();
        RefreshAxes();
        Debug.Log("[输入] 切换布局 → " + CurrentProfileName);
    }

    /// <summary>
    /// 老场景里没有这两个数组的序列化数据，Unity 载入后会是空数组 → 手柄静默失效。
    /// 这里兜底填默认值，保证不重建场景也能用。
    /// </summary>
    void EnsureDefaults()
    {
        if (steerAxisCandidates == null || steerAxisCandidates.Length == 0)
            steerAxisCandidates = (string[])DefaultSteerAxes.Clone();
        if (throttleAxisCandidates == null || throttleAxisCandidates.Length == 0)
            throttleAxisCandidates = (string[])DefaultThrottleAxes.Clone();
    }

    void Start()
    {
        ProbeAxes();
        PollDevices(force: true);
        _calibrated = !JoystickAxesOk;
    }

    void Update()
    {
        PollDevices(false);
        CalibrateAxes();

        if (_calibrated)
        {
            ReadSteering();
            ReadPedals();
        }
        else
        {
            Steer = 0f;
            Throttle = 0f;
            Brake = 0f;
        }

        if (Scanning) UpdateScan();
        else ReadButtons();
    }

    // ---------------- 设备与轴探测 ----------------

    void PollDevices(bool force)
    {
        _pollTimer -= Time.unscaledDeltaTime;
        if (!force && _pollTimer > 0f) return;
        _pollTimer = 0.5f;

        _joyNames = Input.GetJoystickNames();
        int count = 0;
        for (int i = 0; i < _joyNames.Length; i++)
            if (!string.IsNullOrEmpty(_joyNames[i])) count++;

        JoystickConnected = useJoystick && count > 0;
    }

    /// <summary>依次尝试候选轴名，第一个能读到的胜出。读不到就退回键盘。</summary>
    void ProbeAxes()
    {
        EnsureDefaults();
        ActiveSteerAxis = "";
        ActiveThrottleAxis = "";
        JoystickAxesOk = false;

        if (!useJoystick) return;

        string sx = FirstWorkingAxis(steerAxisCandidates);
        string sy = FirstWorkingAxis(throttleAxisCandidates);
        if (sx == null || sy == null || sx == sy) return;

        ActiveSteerAxis = sx;
        ActiveThrottleAxis = sy;
        JoystickAxesOk = true;
    }

    /// <summary>InputManager 里没配这个名字会抛 ArgumentException，正好用来探测。</summary>
    static string FirstWorkingAxis(string[] candidates)
    {
        if (candidates == null) return null;
        for (int i = 0; i < candidates.Length; i++)
        {
            string name = candidates[i];
            if (string.IsNullOrEmpty(name)) continue;
            try
            {
                Input.GetAxisRaw(name);
                return name;
            }
            catch (ArgumentException)
            {
                // 这个轴名不存在，试下一个
            }
        }
        return null;
    }

    /// <summary>重新探测（插拔手柄、改 InputManager 之后可调用）。</summary>
    public void RefreshAxes()
    {
        CurrentProfileName = ControlProfiles.Get(ResolvedProfile).displayName;
        ProbeAxes();
        PollDevices(true);
        _calibrated = !JoystickAxesOk;
        _calTimer = 0f;
        _calSumX = 0f;
        _calSumY = 0f;
        _calFrames = 0;
        Debug.Log("[输入] 轴探测 → " + (JoystickAxesOk
            ? ("手柄 " + ActiveSteerAxis + " / " + ActiveThrottleAxis)
            : "未找到手柄轴，使用键盘"));
    }

    // ---------------- 校准 ----------------

    void CalibrateAxes()
    {
        if (_calibrated) return;

        if (!TryAxis(ActiveSteerAxis, out float rx) || !TryAxis(ActiveThrottleAxis, out float ry))
        {
            _calibrated = true;
            return;
        }

        _calSumX += rx;
        _calSumY += ry;
        _calFrames++;
        _calTimer += Time.unscaledDeltaTime;

        if (_calTimer >= calibrateSeconds && _calFrames > 3)
        {
            _centerX = _calSumX / _calFrames;
            _centerY = _calSumY / _calFrames;
            _calibrated = true;
            Debug.Log(string.Format("[输入] 中位校准完成 X={0:F3} Y={1:F3}（{2} 帧，布局={3}）",
                _centerX, _centerY, _calFrames, CurrentProfileName));
        }
    }

    /// <summary>减去中位、按可行的行程归一化，再按需反向。</summary>
    static float NormalizeAxis(float raw, float center, bool invert)
    {
        float v = raw - center;
        float range = v >= 0f ? Mathf.Max(1f - center, 0.2f) : Mathf.Max(center, 0.2f);
        v = Mathf.Clamp(v / range, -1f, 1f);
        return invert ? -v : v;
    }

    // ---------------- 连续量 ----------------

    void ReadSteering()
    {
        float stick = 0f;
        if (JoystickAxesOk && TryAxis(ActiveSteerAxis, out float raw))
            stick = NormalizeAxis(raw, _centerX, invertSteer);

        float keys = 0f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) keys += 1f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) keys -= 1f;

        // 键盘优先（绝对值大者胜），避免手柄没校准时的漂移
        float v = Mathf.Abs(keys) > Mathf.Abs(stick) ? keys : stick;
        Steer = ApplyDeadzone(v);
    }

    void ReadPedals()
    {
        float y = 0f;
        if (JoystickAxesOk && TryAxis(ActiveThrottleAxis, out float raw))
            y = NormalizeAxis(raw, _centerY, invertThrottle);

        float stickThrottle = y > 0.05f ? y : 0f;
        float stickBrake = y < -0.05f ? -y : 0f;

        float keyThrottle = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) ? 1f : 0f;
        float keyBrake = (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) ? 1f : 0f;

        Throttle = Mathf.Max(stickThrottle, keyThrottle);
        Brake = Mathf.Max(stickBrake, keyBrake);
    }

    bool TryAxis(string axisName, out float value)
    {
        if (string.IsNullOrEmpty(axisName))
        {
            value = 0f;
            return false;
        }
        try
        {
            value = Input.GetAxisRaw(axisName);
            return true;
        }
        catch (ArgumentException)
        {
            value = 0f;
            JoystickAxesOk = false;
            return false;
        }
    }

    float ApplyDeadzone(float v)
    {
        float a = Mathf.Abs(v);
        if (a <= deadzone) return 0f;
        float t = Mathf.InverseLerp(deadzone, 1f, a);
        return Mathf.Sign(v) * t;
    }

    // ---------------- 按键 ----------------

    void ReadButtons()
    {
        // 长按切视角键 → 进入扫描；外接四键控制器没有空余键，用 F1 长按也能进
        if (JoyHeld(buttonCamera) || Input.GetKey(KeyCode.F1))
        {
            _cameraHold += Time.unscaledDeltaTime;
            if (_cameraHold >= scanHoldSeconds)
            {
                _cameraHold = 0f;
                BeginScan();
                return;
            }
        }
        else
        {
            _cameraHold = 0f;
        }

        if (JoyDown(buttonDrive) || Input.GetKeyDown(KeyCode.Alpha1)) Fire(CarAction.Drive);
        if (JoyDown(buttonReverse) || Input.GetKeyDown(KeyCode.R)) Fire(CarAction.Reverse);

        if (JoyDown(buttonHandbrake) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.G))
        {
            Handbrake = !Handbrake;
            Fire(CarAction.Handbrake);
        }

        if (JoyDown(buttonCamera) || Input.GetKeyDown(KeyCode.Q)) Fire(CarAction.Camera);
    }

    void Fire(CarAction action)
    {
        if (action == CarAction.Drive) _wantDrive = true;
        else if (action == CarAction.Reverse) _wantReverse = true;

        var handler = OnAction;
        if (handler != null) handler(action);
    }

    // ---------------- 按键扫描（Rebind） ----------------

    public void BeginScan()
    {
        if (Scanning) return;
        _scanStep = 0;
        _scanOrder = buttonCamera >= 0 ? FullScanOrder : FourKeyScanOrder;
        Debug.Log("[输入] 进入按键扫描：请依次按 " + _scanOrder.Length + " 个键");
        var h = OnScanModeChanged;
        if (h != null) h(true);
    }

    public void CancelScan()
    {
        if (!Scanning) return;
        _scanStep = -1;
        var h = OnScanModeChanged;
        if (h != null) h(false);
    }

    void UpdateScan()
    {
        // 扫描期间按键盘 Esc 取消
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CancelScan();
            return;
        }

        int pressed = FirstJoyDown();
        if (pressed < 0) return;

        SetButtonIndex(_scanOrder[_scanStep], pressed);
        Debug.Log("[输入] " + CarInputMap.Label(_scanOrder[_scanStep]) + " ← 手柄键" + pressed);

        var h = OnScanBound;
        if (h != null) h(_scanStep, pressed);

        _scanStep++;
        if (_scanStep >= _scanOrder.Length)
        {
            _scanStep = -1;
            Debug.Log("[输入] 扫描完成：倒挡=" + buttonReverse + " 前进挡=" + buttonDrive +
                      " 手刹=" + buttonHandbrake + " 切视角=" + buttonCamera);
            var done = OnScanModeChanged;
            if (done != null) done(false);
        }
    }

    int FirstJoyDown()
    {
        for (int i = 0; i < CarInputMap.MaxJoystickButtons; i++)
        {
            KeyCode k = (KeyCode)((int)KeyCode.JoystickButton0 + i);
            if (Input.GetKeyDown(k)) return i;
        }
        return -1;
    }

    // ---------------- 绑定查询 / 修改 ----------------

    public int GetButtonIndex(CarAction action)
    {
        switch (action)
        {
            case CarAction.Reverse: return buttonReverse;
            case CarAction.Drive: return buttonDrive;
            case CarAction.Handbrake: return buttonHandbrake;
            case CarAction.Camera: return buttonCamera;
        }
        return -1;
    }

    public void SetButtonIndex(CarAction action, int index)
    {
        switch (action)
        {
            case CarAction.Reverse: buttonReverse = index; break;
            case CarAction.Drive: buttonDrive = index; break;
            case CarAction.Handbrake: buttonHandbrake = index; break;
            case CarAction.Camera: buttonCamera = index; break;
        }
    }

    bool JoyDown(int index)
    {
        if (index < 0 || index >= CarInputMap.MaxJoystickButtons) return false;
        KeyCode k = (KeyCode)((int)KeyCode.JoystickButton0 + index);
        return Input.GetKeyDown(k);
    }

    bool JoyHeld(int index)
    {
        if (index < 0 || index >= CarInputMap.MaxJoystickButtons) return false;
        KeyCode k = (KeyCode)((int)KeyCode.JoystickButton0 + index);
        return Input.GetKey(k);
    }

    // ---------------- 兼容旧接口 ----------------

    public bool ConsumeDrive()
    {
        bool v = _wantDrive;
        _wantDrive = false;
        return v;
    }

    public bool ConsumeReverse()
    {
        bool v = _wantReverse;
        _wantReverse = false;
        return v;
    }
}
