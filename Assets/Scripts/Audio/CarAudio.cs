using UnityEngine;

/// <summary>
/// 车辆音效：引擎、风噪、轮胎、刹车、换挡、手刹、撞击、锥桶、倒车提示音。
///
/// 由 <see cref="AudioDirector"/> 在运行期自动挂到车上（不动场景文件）。
/// 所有循环音都是常驻播放 + 只改音量和音高：Unity 里 Audiosource.Play/Stop
/// 每帧折腾会有爆音，改 volume 最干净。
/// </summary>
[RequireComponent(typeof(CarController))]
public class CarAudio : MonoBehaviour
{
    [Header("音量")]
    public float engineVolume = 0.22f;
    public float windVolume = 0.14f;
    public float skidVolume = 0.18f;
    public float brakeVolume = 0.14f;
    public float reverseBeepVolume = 0.45f;

    [Header("引擎音高")]
    public float idlePitch = 0.55f;
    public float maxPitch = 1.62f;
    [Tooltip("松油门滑行时，引擎声还留多少（0 = 完全跟着油门）")]
    [Range(0f, 1f)] public float coastRev = 0.55f;

    [Header("手感阈值")]
    [Tooltip("低于这个速度（m/s）不播倒车提示音")]
    public float reverseBeepSpeed = 0.12f;
    [Tooltip("倒车提示音间隔（秒）")]
    public float reverseBeepInterval = 0.85f;
    [Tooltip("轮胎打滑的速度阈值（m/s）")]
    public float skidSpeed = 3.0f;
    [Tooltip("这一帧速度从大于 impactSpeed 直接归零（m/s）就当作撞上了障碍物")]
    public float impactSpeed = 1.0f;

    CarController _car;
    CarInput _input;

    AudioSource _engine, _wind, _skid, _brake;

    float _prevSpeed;
    DriveGear _prevGear;
    bool _prevHandbrake;
    float _beepTimer;
    float _impactTimer;
    float _engineVol, _windVol, _skidVol, _brakeVol;
    bool _ready;

    void Awake()
    {
        _car = GetComponent<CarController>();
        _input = GetComponent<CarInput>();

        _engine = NewLoop("engine_loop");
        _wind = NewLoop("wind_loop");
        _skid = NewLoop("skid_loop");
        _brake = NewLoop("brake_loop");

        if (_car != null)
        {
            _prevSpeed = _car.Speed;
            _prevGear = _car.Gear;
        }
        if (_input != null) _prevHandbrake = _input.Handbrake;

        _ready = _engine != null;
    }

    void OnEnable()
    {
        ConeKnock.Knocked += OnConeKnocked;
    }

    void OnDisable()
    {
        ConeKnock.Knocked -= OnConeKnocked;
    }

    void OnDestroy()
    {
        // 音源挂在子物体上，随车一起销毁；这里只是避免悬挂事件
        ConeKnock.Knocked -= OnConeKnocked;
    }

    AudioSource NewLoop(string sfxKey)
    {
        var director = AudioDirector.Instance;
        var clip = director != null ? director.LoadSfx(sfxKey) : null;

        var go = new GameObject("Audio_" + sfxKey);
        go.transform.SetParent(transform, false);

        var src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = true;
        src.spatialBlend = 0f;
        src.priority = 160;      // 引擎属于背景，让语音/提示音优先
        src.clip = clip;
        src.volume = 0f;
        if (clip != null) src.Play();
        return src;
    }

    void OnConeKnocked(ConeKnock cone)
    {
        AudioDirector.PlaySfx("cone_hit", 0.85f, Random.Range(0.92f, 1.1f));
    }

    void Update()
    {
        if (!_ready || _car == null) return;

        var director = AudioDirector.Instance;
        float sfx = director != null ? director.SfxVolumeNow * director.SfxDuck : 1f;

        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        float speed = _car.Speed;
        float absSpeed = Mathf.Abs(speed);
        float maxSpeed = Mathf.Max(0.1f, _car.maxForwardSpeed);
        float load = Mathf.Clamp01(absSpeed / maxSpeed);

        float throttle = _input != null ? _input.Throttle : 0f;
        float brake = _input != null ? _input.Brake : 0f;
        float steer = _input != null ? Mathf.Abs(_input.Steer) : 0f;
        bool handbrake = _input != null && _input.Handbrake;

        // ---- 引擎：怠速低鸣，油门/车速拉高音高 ----
        // 踩油门时转速立刻跟上；松开油门滑行只留 coastRev 那份
        float rev = Mathf.Clamp01(Mathf.Max(load, throttle) * (throttle > 0.05f ? 1f : coastRev));
        float targetEnginePitch = Mathf.Lerp(idlePitch, maxPitch, rev);

        // 手刹拉住还踩油门：原地轰车（熄火抖动的听觉版）
        if (handbrake && throttle > 0.1f && absSpeed < 0.3f) targetEnginePitch = Mathf.Lerp(idlePitch, maxPitch, 0.75f);

        // 空挡滑行时怠速音低一点
        if (_car.Gear == DriveGear.Neutral) targetEnginePitch *= 0.92f;

        if (_engine != null)
        {
            _engine.pitch = Mathf.Lerp(_engine.pitch, targetEnginePitch, 1f - Mathf.Exp(-dt * 6f));

            float target = engineVolume * (0.55f + 0.75f * rev);
            _engineVol = Mathf.MoveTowards(_engineVol, target, dt * 0.8f);
            _engine.volume = _engineVol * sfx;
        }

        // ---- 风噪：只跟车速 ----
        if (_wind != null)
        {
            float target = windVolume * load * load;
            _windVol = Mathf.MoveTowards(_windVol, target, dt * 0.6f);
            _wind.volume = _windVol * sfx;
            _wind.pitch = 0.85f + 0.3f * load;
        }

        // ---- 轮胎：手刹锁轮，或者高速大方向 ----
        if (_skid != null)
        {
            bool handbrakeSkid = handbrake && absSpeed > 1.6f;
            bool cornerSkid = steer > 0.72f && absSpeed > skidSpeed;
            float target = (handbrakeSkid || cornerSkid) ? skidVolume * Mathf.Clamp01(absSpeed / maxSpeed + 0.35f) : 0f;
            _skidVol = Mathf.MoveTowards(_skidVol, target, dt * 3f);
            _skid.volume = _skidVol * sfx;
        }

        // ---- 刹车 ----
        if (_brake != null)
        {
            float target = (brake > 0.12f && absSpeed > 0.5f) ? brakeVolume * brake : 0f;
            _brakeVol = Mathf.MoveTowards(_brakeVol, target, dt * 4f);
            _brake.volume = _brakeVol * sfx;
        }

        // ---- 换挡 / 手刹：一次性"咔哒" ----
        if (_car.Gear != _prevGear)
        {
            AudioDirector.PlaySfx("gear_shift", 0.7f, Random.Range(0.95f, 1.05f));
            _prevGear = _car.Gear;
        }
        if (_input != null && _input.Handbrake != _prevHandbrake)
        {
            if (_input.Handbrake) AudioDirector.PlaySfx("handbrake", 0.75f);
            _prevHandbrake = _input.Handbrake;
        }

        // ---- 倒车提示音：挂在 R 挡且车在动 ----
        if (_car.Gear == DriveGear.Reverse && absSpeed > reverseBeepSpeed)
        {
            _beepTimer -= dt;
            if (_beepTimer <= 0f)
            {
                AudioDirector.PlaySfx("reverse_beep", reverseBeepVolume);
                _beepTimer = reverseBeepInterval;
            }
        }
        else if (_beepTimer < 0f)
        {
            _beepTimer = 0f;
        }

        // ---- 撞击：CarController 撞障碍物时会把 Speed 直接归零 ----
        _impactTimer -= dt;
        if (_impactTimer <= 0f && Mathf.Abs(_prevSpeed) > impactSpeed && absSpeed < 0.05f)
        {
            AudioDirector.PlaySfx("impact", 0.9f, Random.Range(0.9f, 1.08f));
            _impactTimer = 0.35f;
        }
        _prevSpeed = speed;

#if UNITY_EDITOR
        // 编辑器里用 H 听喇叭（自制板上四键已经排满，喇叭没有独立键位）
        if (Input.GetKeyDown(KeyCode.H)) AudioDirector.PlaySfx("horn", 0.7f);
#endif
    }
}
