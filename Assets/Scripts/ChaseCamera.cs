using UnityEngine;

/// <summary>
/// 第三视角自动导播（教学关只用跟车 + 正俯视两档）。
/// 默认车后上方，阻尼跟随；按摇杆 / Q 切正俯视。
/// 键盘和手柄的按键全部由 CarInput 统一读，这里只订阅动作事件，不再自己看 Input。
/// </summary>
public class ChaseCamera : MonoBehaviour
{
    public Transform target;
    public Vector3 chaseOffset = new Vector3(0f, 4f, -7f);
    public float lookHeight = 1.2f;
    public float smoothTime = 0.15f;
    public float topDownHeight = 16f;
    public float topDownSmoothTime = 0.4f;

    [Tooltip("已废弃：视角切换改由 CarInput 统一处理。保留字段只为兼容旧场景。")]
    public KeyCode toggleKey = KeyCode.Q;

    public bool TopDown { get; private set; }

    CarInput _input;
    Vector3 _velocity;

    void OnEnable() { ResolveInput(); }
    void OnDisable() { Unhook(); }

    void Start() { ResolveInput(); }

    void ResolveInput()
    {
        CarInput found = target != null ? target.GetComponent<CarInput>() : null;
        if (found == _input) return;

        Unhook();
        _input = found;
        if (_input != null) _input.OnAction += OnCarAction;

        // target 可能比本脚本晚赋值，这里兜底再找一次
        if (_input == null && target == null)
        {
            var any = FindObjectOfType<CarInput>();
            if (any != null)
            {
                _input = any;
                _input.OnAction += OnCarAction;
            }
        }
    }

    void Unhook()
    {
        if (_input != null) _input.OnAction -= OnCarAction;
    }

    void OnCarAction(CarAction action)
    {
        if (action == CarAction.Camera) TopDown = !TopDown;
    }

    void LateUpdate()
    {
        if (target == null) return;
        if (_input == null) ResolveInput();

        Vector3 desired;
        float st;

        if (TopDown)
        {
            desired = target.position + Vector3.up * topDownHeight;
            st = topDownSmoothTime;
        }
        else
        {
            desired = target.position + target.rotation * chaseOffset;
            st = smoothTime;
        }

        transform.position = Vector3.SmoothDamp(transform.position, desired, ref _velocity, st);

        Vector3 dir = (target.position + Vector3.up * lookHeight) - transform.position;
        if (dir.sqrMagnitude < 0.0001f) return;

        Vector3 upHint = Mathf.Abs(Vector3.Dot(dir.normalized, Vector3.up)) > 0.99f
            ? Vector3.forward
            : Vector3.up;

        Quaternion look = Quaternion.LookRotation(dir, upHint);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, 1f - Mathf.Exp(-12f * Time.deltaTime));
    }
}
