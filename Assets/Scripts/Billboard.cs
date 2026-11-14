using UnityEngine;

/// <summary>让文字始终面向主相机（A/B 点标牌用）。</summary>
public class Billboard : MonoBehaviour
{
    Camera _cam;

    void LateUpdate()
    {
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;

        Vector3 dir = transform.position - _cam.transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        transform.rotation = Quaternion.LookRotation(dir);
    }
}
