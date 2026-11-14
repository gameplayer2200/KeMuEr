using UnityEngine;

/// <summary>
/// 挂在锥桶上：被撞倒或被撞离原位就算"碰倒"。
/// 教学关只做提示，不扣分；科目二正式关会接扣分表。
/// </summary>
public class ConeKnock : MonoBehaviour
{
    public static event System.Action<ConeKnock> Knocked;

    public float tiltThreshold = 0.6f;   // up·worldUp 低于这个值算倒
    public float moveThreshold = 0.7f;   // 位移超过这个算撞飞

    public bool IsKnocked { get; private set; }

    Vector3 _spawnPosition;

    void Start()
    {
        _spawnPosition = transform.position;
    }

    void Update()
    {
        if (IsKnocked) return;

        bool tipped = Vector3.Dot(transform.up, Vector3.up) < tiltThreshold;
        bool moved = Vector3.Distance(transform.position, _spawnPosition) > moveThreshold;

        if (tipped || moved)
        {
            IsKnocked = true;
            if (Knocked != null) Knocked(this);
        }
    }
}
