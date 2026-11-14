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

    [Header("状态（只读）")]
    public DriveGear Gear = DriveGear.Drive;
    public float Speed { get; private set; }          // m/s，带符号
    public float SpeedKmh { get { return Mathf.Abs(Speed) * 3.6f; } }
    public float SteerAngleDeg { get; private set; }
    public bool CanShift { get { return Mathf.Abs(Speed) <= shiftSpeedLimit; } }

    CarInput _input;

    void Awake()
    {
        _input = GetComponent<CarInput>();
    }

    void Update()
    {
        if (_input.ConsumeDrive()) TryShift(DriveGear.Drive);
        if (_input.ConsumeReverse()) TryShift(DriveGear.Reverse);

        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        UpdateSteering(dt);
        UpdateSpeed(dt);
        UpdatePose(dt);
    }

    void UpdateSteering(float dt)
    {
        float target = _input.Steer * maxSteerDeg;
        float k = 1f - Mathf.Exp(-dt / Mathf.Max(0.001f, steerResponse));
        SteerAngleDeg = Mathf.Lerp(SteerAngleDeg, target, k);
    }

    void UpdateSpeed(float dt)
    {
        float accel = 0f;

        if (Gear == DriveGear.Drive)
        {
            accel += _input.Throttle * engineAccel;
            accel -= _input.Brake * brakeDecel;
        }
        else if (Gear == DriveGear.Reverse)
        {
            accel -= _input.Throttle * engineAccel * 0.85f;
            accel += _input.Brake * brakeDecel;
        }

        if (_input.Handbrake)
            accel -= Mathf.Sign(Speed) * brakeDecel * 1.2f;

        float next = Speed + accel * dt;

        // 自然滚阻，且不允许被阻力推着反向
        float drag = rollingDecel * dt * (Gear == DriveGear.Neutral ? 2f : 1f);
        if (Mathf.Abs(next) <= drag) next = 0f;
        else next -= Mathf.Sign(next) * drag;

        Speed = Mathf.Clamp(next, -maxReverseSpeed, maxForwardSpeed);

        // 手刹静止时彻底锁死
        if (_input.Handbrake && Mathf.Abs(Speed) < 0.05f) Speed = 0f;
    }

    void UpdatePose(float dt)
    {
        if (Mathf.Abs(Speed) < 0.001f) return;

        float yawRateDeg = Speed * Mathf.Tan(SteerAngleDeg * Mathf.Deg2Rad) / wheelbase * Mathf.Rad2Deg;
        float yawStep = yawRateDeg * dt;

        Vector3 step = Quaternion.Euler(0f, yawStep, 0f) * transform.forward * (Speed * dt);
        if (IsBlocked(step))
        {
            // 撞上障碍物：这一次位移整个作废，速度归零
            Speed = 0f;
            return;
        }

        transform.Rotate(0f, yawStep, 0f, Space.World);
        transform.position += step;
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
        if (!CanShift) return;   // 车在动的时候换挡无效
        Gear = target;
    }

    public void ResetToPose(Vector3 position, Quaternion rotation)
    {
        Speed = 0f;
        SteerAngleDeg = 0f;
        Gear = DriveGear.Drive;
        transform.SetPositionAndRotation(position, rotation);
    }
}
