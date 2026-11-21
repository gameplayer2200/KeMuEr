using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 跑出跑道自动回退。
/// 每帧记录车的姿态，保留最近 rewindSeconds + 余量 秒的历史；
/// 车轮下方不再是路面（草地/掉出地图）持续 offTrackGrace 秒，就把车送回「出界前 rewindSeconds 秒」的位置。
/// 挡位和手刹保持不变 —— 回退是"修正位置"，不是"重新上车"，玩家接着开就行。
/// </summary>
[RequireComponent(typeof(CarController))]
public class OffTrackResetter : MonoBehaviour
{
    [Tooltip("回退到出界前多少秒的位置。")]
    public float rewindSeconds = 3f;
    [Tooltip("离路多少秒后判定为跑出跑道（防止压边线误判）。")]
    public float offTrackGrace = 0.4f;
    [Tooltip("路面所在层（Ramp 层，路面和草地都在这一层，靠父子关系区分）。")]
    public LayerMask groundMask = 1 << 9;
    [Tooltip("路面根物体名。落点不属于它 = 已经开出路面。")]
    public string roadRootName = "Road";
    [Tooltip("探针高度（从车身位置往上）。")]
    public float probeHeight = 1.5f;
    [Tooltip("探针向下最多找多远（超过 = 掉出地图）。")]
    public float probeDepth = 8f;
    public bool playSfxOnRewind = true;

    struct Sample
    {
        public float t;
        public Vector3 position;
        public Quaternion rotation;
    }

    readonly List<Sample> _samples = new List<Sample>();

    CarController _car;
    TutorialDirector _tutorial;
    float _offTime;
    bool _rewinding;

    public int RewindCount { get; private set; }

    void Awake()
    {
        _car = GetComponent<CarController>();
        if (rewindSeconds < 0.5f) rewindSeconds = 0.5f;
    }

    void Start()
    {
        _tutorial = FindObjectOfType<TutorialDirector>();
    }

    void Update()
    {
        if (_car == null) return;
        float now = Time.time;

        Record(now);

        if (IsOffTrack())
        {
            _offTime += Time.deltaTime;
            if (_offTime >= offTrackGrace) Rewind(now);
        }
        else
        {
            _offTime = 0f;
        }
    }

    void Record(float now)
    {
        _samples.Add(new Sample
        {
            t = now,
            position = _car.transform.position,
            rotation = _car.transform.rotation
        });

        // 只保留 rewindSeconds + 2 秒，够找目标帧就行
        float keep = rewindSeconds + 2f;
        int drop = 0;
        while (drop < _samples.Count && _samples[drop].t < now - keep) drop++;
        if (drop > 0) _samples.RemoveRange(0, drop);
    }

    /// <summary>车身下方是不是路面。草地在 L1_Tutorial 下，路面在 Road 下，探针落空也算出界。</summary>
    bool IsOffTrack()
    {
        Vector3 p = _car.transform.position;
        if (p.y < -20f) return true;                       // 掉出地图
        if (Mathf.Abs(p.x) > 400f || Mathf.Abs(p.z) > 400f) return true;

        RaycastHit hit;
        if (!Physics.Raycast(p + Vector3.up * probeHeight, Vector3.down, out hit,
                probeHeight + probeDepth, groundMask, QueryTriggerInteraction.Ignore))
            return true;

        return hit.collider.transform.root.name != roadRootName;
    }

    void Rewind(float now)
    {
        if (_rewinding || _samples.Count == 0) return;
        _rewinding = true;

        // 找「出界前 rewindSeconds 秒」的那一帧：第一个时间 >= now - rewind 的样本
        float target = now - rewindSeconds;
        Sample pick = _samples[0];
        for (int i = 0; i < _samples.Count; i++)
        {
            if (_samples[i].t >= target) { pick = _samples[i]; break; }
            pick = _samples[i];
        }

        _car.WarpTo(pick.position, pick.rotation);
        _samples.Clear();          // 复位后重新攒历史，避免用旧点连环回退
        _offTime = 0f;
        RewindCount++;

        if (playSfxOnRewind) AudioDirector.PlaySfx("impact", 0.55f, 0.8f);
        if (_tutorial != null) _tutorial.Toast("跑出跑道 · 已退回 " + Mathf.RoundToInt(rewindSeconds) + " 秒前");
        Debug.Log("[出界] 已退回 " + rewindSeconds.ToString("F1") + " 秒前位置 " + pick.position.ToString("F2"));

        _rewinding = false;
    }
}
