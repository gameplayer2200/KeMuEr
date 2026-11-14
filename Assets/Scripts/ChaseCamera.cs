using UnityEngine;

/// <summary>
/// 第三视角自动导播（教学关只用跟车 + 正俯视两档）。
/// 默认车后上方，阻尼跟随；按摇杆 / Q 切正俯视。
/// </summary>
public class ChaseCamera : MonoBehaviour
{
    public Transform target;
    public Vector3 chaseOffset = new Vector3(0f, 4f, -7f);
    public float lookHeight = 1.2f;
    public float smoothTime = 0.15f;
    public float topDownHeight = 16f;
    public float topDownSmoothTime = 0.4f;
    public KeyCode toggleKey = KeyCode.Q;

    public bool TopDown { get; private set; }

    Vector3 _velocity;

    void LateUpdate()
    {
        if (target == null) return;

        if (Input.GetKeyDown(toggleKey)) TopDown = !TopDown;

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
