using UnityEngine;

/// <summary>
/// 硬件反馈抽象（配置图的 ⑧ 档位 LED / ⑨ 震动马达）。
/// mini-project 期没有这些元件，用 <see cref="NullDrivingFeedback"/> 占位，
/// 保证调用方可以无条件调用、永远不会因为"没接硬件"而报错。
///
/// EA 期接上固件后，新增 SerialDrivingFeedback（HID + CDC 双接口，串口发
/// "LED,R,1" / "VIB,200" 这类纯文本行）即可，游戏逻辑不用改。
/// </summary>
public interface IDrivingFeedback
{
    /// <summary>档位变化 → 点亮对应的档位 LED。</summary>
    void OnGearChanged(DriveGear gear);

    /// <summary>短促震动，单位毫秒。</summary>
    void Pulse(float milliseconds);

    /// <summary>当前有没有真实硬件连着（HUD 可据此决定要不要提示）。</summary>
    bool IsConnected { get; }
}

/// <summary>没有硬件的默认实现：全部空转。</summary>
public class NullDrivingFeedback : IDrivingFeedback
{
    public static readonly NullDrivingFeedback Instance = new NullDrivingFeedback();

    public bool IsConnected { get { return false; } }
    public void OnGearChanged(DriveGear gear) { }
    public void Pulse(float milliseconds) { }
}
