using UnityEngine;

public enum DriveGear
{
    Neutral = 0,
    Reverse = 1,
    Drive = 2
}

/// <summary>
/// 手柄 + 键盘的统一输入层。
/// 手柄：X 轴 = 方向盘，Y 轴 = 油门/刹车，4 键 = R/1/手刹/转向灯，摇杆按下 = 切镜头。
/// 键盘：A/D 或 ←/→ 方向盘，W/↑ 油门，S/↓ 刹车，R 倒挡，1 前进挡，空格/黄键 手刹，Q 切镜头。
/// 摇杆轴名需要在 ProjectSettings/InputManager.asset 里存在（Stick X / Stick Y）。
/// 上电后前 0.4 秒做中位校准：把静止时的轴值记成 0，避免自制手柄的偏置导致车自己跑。
/// </summary>
public class CarInput : MonoBehaviour
{
    [Header("摇杆轴名（InputManager）")]
    public string steerAxis = "Stick X";
    public string throttleAxis = "Stick Y";

    [Header("摇杆按键编号")]
    public int buttonReverse = 0;     // 红
    public int buttonDrive = 1;       // 绿
    public int buttonHandbrake = 2;   // 黄
    public int buttonSignal = 3;      // 蓝
    public int buttonCamera = 4;      // 摇杆按下

    [Header("手感参数")]
    [Range(0f, 0.3f)] public float deadzone = 0.08f;
    public bool invertSteer = false;
    public bool invertThrottle = false;
    public bool useJoystick = true;

    // 当前帧的连续量
    public float Steer { get; private set; }       // -1..1
    public float Throttle { get; private set; }    // 0..1
    public float Brake { get; private set; }       // 0..1
    public bool Handbrake { get; private set; }
    public bool TurnSignalOn { get; private set; }
    public bool AxesAvailable { get; private set; }
    public bool JoystickConnected { get; private set; }

    // 一次性事件
    bool _wantDrive;
    bool _wantReverse;
    bool _wantCamera;

    // 中位校准
    bool _calibrated;
    float _calTimer, _calSumX, _calSumY;
    int _calFrames;
    float _centerX, _centerY;

    void Start()
    {
        AxesAvailable = useJoystick && ProbeAxes();
        _calibrated = !AxesAvailable;
    }

    /// <summary>检测 InputManager 里是否配置了摇杆轴，没有就纯键盘。</summary>
    bool ProbeAxes()
    {
        try
        {
            Input.GetAxisRaw(steerAxis);
            Input.GetAxisRaw(throttleAxis);
            return true;
        }
        catch (System.ArgumentException)
        {
            return false;
        }
    }

    void Update()
    {
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

        ReadButtons();
    }

    void CalibrateAxes()
    {
        if (_calibrated) return;

        if (TryAxis(steerAxis, out float rx) && TryAxis(throttleAxis, out float ry))
        {
            _calSumX += rx;
            _calSumY += ry;
            _calFrames++;
            _calTimer += Time.unscaledDeltaTime;

            if (_calTimer >= 0.4f && _calFrames > 3)
            {
                _centerX = _calSumX / _calFrames;
                _centerY = _calSumY / _calFrames;
                _calibrated = true;
            }
        }
        else
        {
            _calibrated = true;
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

    void ReadSteering()
    {
        float stick = 0f;
        if (AxesAvailable && TryAxis(steerAxis, out float raw))
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
        if (AxesAvailable && TryAxis(throttleAxis, out float raw))
            y = NormalizeAxis(raw, _centerY, invertThrottle);

        float stickThrottle = y > 0.05f ? y : 0f;
        float stickBrake = y < -0.05f ? -y : 0f;

        float keyThrottle = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) ? 1f : 0f;
        float keyBrake = (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) ? 1f : 0f;

        Throttle = Mathf.Max(stickThrottle, keyThrottle);
        Brake = Mathf.Max(stickBrake, keyBrake);
    }

    void ReadButtons()
    {
        JoystickConnected = AxesAvailable && Input.GetJoystickNames().Length > 0;

        if (JoyDown(buttonDrive)) _wantDrive = true;
        if (JoyDown(buttonReverse)) _wantReverse = true;
        if (Input.GetKeyDown(KeyCode.Alpha1)) _wantDrive = true;
        if (Input.GetKeyDown(KeyCode.R)) _wantReverse = true;

        if (JoyDown(buttonHandbrake) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.G))
            Handbrake = !Handbrake;

        if (JoyDown(buttonSignal) || Input.GetKeyDown(KeyCode.T))
            TurnSignalOn = !TurnSignalOn;

        if (JoyDown(buttonCamera) || Input.GetKeyDown(KeyCode.Q))
            _wantCamera = true;
    }

    bool JoyDown(int index)
    {
        if (!AxesAvailable || index < 0) return false;
        KeyCode k = (KeyCode)((int)KeyCode.JoystickButton0 + index);
        return Input.GetKeyDown(k);
    }

    bool TryAxis(string axisName, out float value)
    {
        try
        {
            value = Input.GetAxisRaw(axisName);
            return true;
        }
        catch (System.ArgumentException)
        {
            value = 0f;
            AxesAvailable = false;
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

    public bool ConsumeCamera()
    {
        bool v = _wantCamera;
        _wantCamera = false;
        return v;
    }
}
