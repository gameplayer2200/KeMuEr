using UnityEngine;

/// <summary>
/// 运动学自行车模型：不依赖 WheelCollider，转向和速度都是"要跟手"的。
/// 前轮转角经过一阶滤波，速度用油门/刹车积分，倒挡时油门反向。
/// </summary>
[RequireComponent(typeof(CarInput))]
public class CarController : MonoBehaviour
{
    [Header("车身几何")]
    public float wheelbase = 2.6f;          // 轴距 L
    public float maxSteerDeg = 35f;         // 最大前轮转角
    public float steerResponse = 0.12f;     // 转向一阶滤波时间常数

    [Header("动力")]
    public float maxForwardSpeed = 5.5f;    // m/s ≈ 20 km/h
    public float maxReverseSpeed = 2.2f;    // m/s ≈ 8 km/h
    public float engineAccel = 2.4f;        // m/s²
    public float brakeDecel = 6.0f;         // m/s²
    public float rollingDecel = 0.7f;       // 松油门后的自然减速
    public float shiftSpeedLimit = 0.4f;    // 低于这个速度才能换挡

    [Header("碰撞")]
    public LayerMask obstacleMask = 1 << 8; // Obstacle 层：建筑/树/路牌/道具
    public float bodyHalfWidth = 0.82f;     // 车身半宽（略小于 0.85，留一点余量）
    public float bodyHeight = 1.55f;        // 车身高度
    public float bodyHalfLength = 2.2f;     // 车身半长

    [Header("坡道跟随")]
    [Tooltip("坡道碰撞体所在层（Ramp 层，默认 9）。")]
    public LayerMask rampMask = 1 << 9;
    [Tooltip("坡道模型自身最低路面高度，标定用：把模型高度换算成相对平地的高差。")]
    public float rampCalibrationY = 2.18f;
    [Tooltip("车身高度跟随坡面的响应速度（越大越贴地，越小越软）。")]
    public float groundFollowSpeed = 10f;
    [Tooltip("是否让车身俯仰跟随坡度。")]
    public bool alignToSlope = true;
    [Tooltip("俯仰角度上限，防止模型边缘把车身翻过去。")]
    public float maxSlopeDeg = 25f;

    [Header("状态（只读）")]
    [Tooltip("硬件反馈（档位 LED / 震动）。没有硬件时留空即可，内部用 NullDrivingFeedback 兜底。")]
    public MonoBehaviour feedbackSource;

    IDrivingFeedback _feedback = NullDrivingFeedback.Instance;

    // 手刹拉住还踩油门时的熄火抖动节奏控制
    float _lastStallPulse = -1f;

    public DriveGear Gear = DriveGear.Drive;
    public float Speed { get; private set; }          // m/s，带符号
    public float SpeedKmh { get { return Mathf.Abs(Speed) * 3.6f; } }
    public float SteerAngleDeg { get; private set; }
    public bool CanShift { get { return Mathf.Abs(Speed) <= shiftSpeedLimit; } }

    // 平地基准高度与坡道探针状态
    float _baseY;
    float _frontY, _rearY;
    bool _hasFront, _hasRear;

    CarInput _input;          // 场景里的具体实现（保留以便 Inspector 序列化）
    IDrivingInput _driving;   // EA 期换输入源时只换这个，逻辑不变

    void Awake()
    {
        _input = GetComponent<CarInput>();
        _driving = _input as IDrivingInput;
        _baseY = transform.position.y;

        if (feedbackSource is IDrivingFeedback fb) _feedback = fb;
        else _feedback = NullDrivingFeedback.Instance;

        _feedback.OnGearChanged(Gear);
    }

    /// <summary>运行时替换输入源（EA 期接 Input System 时用）。</summary>
    public void SetDrivingInput(IDrivingInput source)
    {
        _driving = source;
    }

    void Update()
    {
        if (_driving == null) return;

        if (_driving.ConsumeDrive()) TryShift(DriveGear.Drive);
        if (_driving.ConsumeReverse()) TryShift(DriveGear.Reverse);

        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        UpdateSteering(dt);
        UpdateSpeed(dt);
        UpdatePose(dt);
    }

    void UpdateSteering(float dt)
    {
        float target = _driving.Steer * maxSteerDeg;
        float k = 1f - Mathf.Exp(-dt / Mathf.Max(0.001f, steerResponse));
        SteerAngleDeg = Mathf.Lerp(SteerAngleDeg, target, k);
    }

    /// <summary>
    /// 速度更新。三条约束决定了挡位必须有用：
    ///   1) 油门只把车往「当前挡位的方向」推；
    ///   2) 刹车只把速度拉回 0，绝不越过 0 —— 摇杆往后推是刹车，不是倒车；
    ///   3) 想后退只能挂 R 挡，想前进只能挂 1 挡。
    /// 上一版把刹车当成负加速度直接叠加，速度会被推成负值，于是 1 挡下拉一下摇杆车就倒着走，换挡形同虚设。
    /// </summary>
    void UpdateSpeed(float dt)
    {
        float speed = Speed;

        // ---- 1) 动力：只朝挡位方向推；手刹拉住时动力被彻底切断 ----
        // 不能只靠"手刹的减速度比油门大"来压住 —— 那样调参就破。
        // 教学关要的就是"灯亮着，车不走"，所以这里是从逻辑上禁掉。
        float powerDir = Gear == DriveGear.Drive ? 1f
                       : Gear == DriveGear.Reverse ? -1f
                       : 0f;

        bool engineCut = _driving.Handbrake;   // 手刹 = 离合断开，油门空转

        if (powerDir != 0f && _driving.Throttle > 0f && !engineCut)
        {
            float power = _driving.Throttle * engineAccel * dt;
            if (Gear == DriveGear.Reverse) power *= 0.85f;
            speed = Advance(speed, powerDir * power);
        }

        // ---- 2) 刹车：只减到 0，不反向 ----
        if (_driving.Brake > 0f)
            speed = TowardZero(speed, _driving.Brake * brakeDecel * dt);

        // ---- 3) 手刹：同样只减到 0 ----
        if (_driving.Handbrake)
            speed = TowardZero(speed, brakeDecel * 1.2f * dt);

        // ---- 4) 自然滚阻 ----
        float drag = rollingDecel * dt * (Gear == DriveGear.Neutral ? 2f : 1f);
        speed = TowardZero(speed, drag);

        Speed = Mathf.Clamp(speed, -maxReverseSpeed, maxForwardSpeed);

        // 手刹静止时彻底锁死
        if (_driving.Handbrake && Mathf.Abs(Speed) < 0.05f) Speed = 0f;

        // 手刹拉住还在踩油门 = 熄火抖动。不用任何文字提示，让手上/画面上有感觉。
        bool stalling = engineCut && _driving.Throttle > 0.05f && Mathf.Abs(Speed) < 0.05f;
        if (stalling && Time.time - _lastStallPulse > 0.35f)
        {
            _lastStallPulse = Time.time;
            _feedback.Pulse(90f);
        }
    }

    /// <summary>朝 delta 的方向推进；若会跨过 0 就停在 0（退着的车不会被油门推成前进）。</summary>
    static float Advance(float speed, float delta)
    {
        if (Mathf.Approximately(delta, 0f)) return speed;

        float next = speed + delta;

        if (!Mathf.Approximately(speed, 0f) &&
            Mathf.Sign(delta) != Mathf.Sign(speed) &&
            Mathf.Sign(next) != Mathf.Sign(speed))
            return 0f;

        return next;
    }

    /// <summary>把速度朝 0 拉，最多拉到 0，永不推到反方向。</summary>
    static float TowardZero(float speed, float amount)
    {
        if (amount <= 0f || Mathf.Approximately(speed, 0f)) return speed;
        return Mathf.Abs(speed) <= amount ? 0f : speed - Mathf.Sign(speed) * amount;
    }

    void UpdatePose(float dt)
    {
        UpdateGroundFollow(dt);

        if (Mathf.Abs(Speed) < 0.001f) return;

        float yawRateDeg = Speed * Mathf.Tan(SteerAngleDeg * Mathf.Deg2Rad) / wheelbase * Mathf.Rad2Deg;
        float yawStep = yawRateDeg * dt;

        Vector3 step = Quaternion.Euler(0f, yawStep, 0f) * transform.forward * (Speed * dt);
        if (IsBlocked(step))
        {
            // 撞上障碍物：这一次位移整个作废，速度归零
            Speed = 0f;
            _feedback.Pulse(180f);
            return;
        }

        transform.Rotate(0f, yawStep, 0f, Space.World);
        transform.position += step;
    }

    /// <summary>
    /// 坡道跟随：车是 kinematic、不靠重力，所以高度完全由这里决定。
    /// 用两根前/后探针去探坡道表面，算出高度和俯仰角，再平滑贴上去。
    /// 探针打不到坡道（平地）时目标高度就是 baseY，俯仰归零。
    /// </summary>
    void UpdateGroundFollow(float dt)
    {
        float probe = bodyHalfLength * 0.8f;

        float hFront, hRear;
        bool gotFront = ProbeRampHeight(transform.position + transform.forward * probe, out hFront);
        bool gotRear  = ProbeRampHeight(transform.position - transform.forward * probe, out hRear);

        if (gotFront) _frontY = hFront;
        if (gotRear)  _rearY  = hRear;
        _hasFront = gotFront;
        _hasRear  = gotRear;

        float targetY = _baseY;
        float targetPitch = 0f;

        if (_hasFront && _hasRear)
        {
            targetY = Mathf.Max(_frontY, _rearY);
            targetPitch = -Mathf.Atan2(_frontY - _rearY, probe * 2f) * Mathf.Rad2Deg;
        }
        else if (_hasFront) targetY = _frontY;
        else if (_hasRear)  targetY = _rearY;

        targetPitch = Mathf.Clamp(targetPitch, -maxSlopeDeg, maxSlopeDeg);

        float k = 1f - Mathf.Exp(-dt * Mathf.Max(0.001f, groundFollowSpeed));
        Vector3 p = transform.position;
        p.y = Mathf.Lerp(p.y, targetY, k);
        transform.position = p;

        Quaternion desired = Quaternion.Euler(alignToSlope ? targetPitch : 0f, transform.eulerAngles.y, 0f);
        transform.rotation = Quaternion.Slerp(transform.rotation, desired, k);
    }

    bool ProbeRampHeight(Vector3 worldPos, out float y)
    {
        y = 0f;
        RaycastHit hit;
        Vector3 origin = new Vector3(worldPos.x, _baseY + 30f, worldPos.z);
        if (!Physics.Raycast(origin, Vector3.down, out hit, 60f, rampMask, QueryTriggerInteraction.Ignore))
            return false;
        y = Mathf.Max(_baseY, hit.point.y - rampCalibrationY + _baseY);
        return true;
    }

    /// <summary>沿位移方向扫一个车身大小的盒子，判断这一帧会不会穿进障碍物。</summary>
    bool IsBlocked(Vector3 step)
    {
        float dist = step.magnitude;
        if (dist < 0.0001f) return false;

        Vector3 dir = step / dist;
        Vector3 center = transform.position + Vector3.up * (bodyHeight * 0.5f + 0.02f);
        // 盒子只取中间一小段，靠 cast 距离覆盖到前后保险杠
        Vector3 half = new Vector3(bodyHalfWidth, bodyHeight * 0.5f, 0.3f);

        return Physics.BoxCast(center, half, dir, out _, Quaternion.LookRotation(dir, Vector3.up),
            dist + bodyHalfLength, obstacleMask, QueryTriggerInteraction.Ignore);
    }

    void TryShift(DriveGear target)
    {
        if (target == Gear) return;
        if (!CanShift)
        {
            _feedback.Pulse(80f);   // 车还在动，换挡被拒绝 —— 让手上有感觉
            return;
        }
        Gear = target;
        _feedback.OnGearChanged(Gear);
    }

    public void ResetToPose(Vector3 position, Quaternion rotation)
    {
        Speed = 0f;
        SteerAngleDeg = 0f;
        Gear = DriveGear.Drive;
        transform.SetPositionAndRotation(position, rotation);
        _baseY = position.y;
        _hasFront = _hasRear = false;
    }
}
