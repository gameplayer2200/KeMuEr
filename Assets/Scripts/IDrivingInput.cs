using System;

/// <summary>
/// 驾驶输入源抽象。EA 期换成 Unity Input System 时，只需要新增一个
/// InputSystemProvider : MonoBehaviour, IDrivingInput，
/// CarController / ChaseCamera / TutorialDirector 一行都不用改。
///
/// 目前唯一实现：<see cref="CarInput"/>（Legacy Input Manager，兼容自制 X1K4 板与市售手柄）。
/// </summary>
public interface IDrivingInput
{
    // ---- 连续量 ----
    float Steer { get; }        // -1..1
    float Throttle { get; }     // 0..1
    float Brake { get; }        // 0..1

    // ---- 状态 ----
    bool Handbrake { get; }

    // ---- 自检信息 ----
    bool JoystickAxesOk { get; }
    string CurrentProfileName { get; }

    // ---- 一次性动作 ----
    event Action<CarAction> OnAction;

    // ---- 兼容旧接口（CarController 用）----
    bool ConsumeDrive();
    bool ConsumeReverse();
}
