using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 运行期音频总线：中文语音引导 + 车辆音效 + 底部字幕。
///
/// 设计原则（重要）：**不改场景**。
/// 场景里的对象全是脚本生成的 YAML，手工改容易和编辑器里的序列化对不上，
/// 所以这里用 <see cref="RuntimeInitializeOnLoadMethodAttribute"/> 在场景加载完成后
/// 自己建一个常驻的 AudioDirector，再按需把 <see cref="CarAudio"/> 挂到车上。
/// 音频资源走 Resources.Load，路径 Resources/Audio/Voice/*.mp3、Resources/Audio/Sfx/*.wav。
///
/// 语音走队列：一条播完（留一点间隙）再播下一条，跨流程的播报不会互相打断。
/// </summary>
[DefaultExecutionOrder(-200)]
public class AudioDirector : MonoBehaviour
{
    public static AudioDirector Instance { get; private set; }

    [Header("音量")]
    [Range(0f, 1f)] public float masterVolume = 1f;
    [Range(0f, 1f)] public float voiceVolume = 1f;
    [Range(0f, 1f)] public float sfxVolume = 0.9f;

    [Tooltip("语音播放期间，车辆音效压低到这个比例，保证听清人声")]
    [Range(0f, 1f)] public float duckWhileVoice = 0.45f;

    [Tooltip("两条语音之间的停顿（秒）")]
    public float voiceGap = 0.18f;

    [Header("字幕")]
    public bool showSubtitle = true;

    // ---- 内部 ----
    AudioSource _voice;
    AudioSource _sfx;
    readonly Queue<string> _queue = new Queue<string>();
    readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
    readonly HashSet<string> _missing = new HashSet<string>();
    string _currentKey = "";
    float _gapTimer;
    bool _wasPlaying;
    CarAudio _carAudio;
    float _carScanTimer;

    /// <summary>语音是否正在出声（字幕、音效压低都看它）。</summary>
    public bool VoiceBusy { get { return _voice != null && _voice.isPlaying; } }
    /// <summary>当前音效总衰减系数：语音播报时压低。</summary>
    public float SfxDuck { get { return VoiceBusy ? duckWhileVoice : 1f; } }
    public float MasterVolume { get { return masterVolume; } }
    /// <summary>排队等播的语音条数（自检面板 / 调试用）。</summary>
    public int QueuedCount { get { return _queue.Count; } }
    /// <summary>当前字幕文本（没有播报时返回空串）。</summary>
    public string CurrentSubtitle
    {
        get { return VoiceBusy ? VoiceLines.Text(_currentKey) : ""; }
    }

    // ---------------- 启动 ----------------

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        Ensure();
    }

    /// <summary>拿到单例，没有就建一个。编辑器里非运行状态直接返回 null。</summary>
    public static AudioDirector Ensure()
    {
        if (Instance != null) return Instance;
        if (!Application.isPlaying) return null;

        var go = new GameObject("AudioDirector");
        DontDestroyOnLoad(go);
        go.AddComponent<AudioDirector>();
        return Instance;
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        EnsureListener();
        _voice = CreateSource("Voice", 0);
        _sfx = CreateSource("Sfx", 128);
        _gapTimer = 0f;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>场景里没有 AudioListener 时补一个（否则什么都没声）。</summary>
    static void EnsureListener()
    {
        if (FindObjectOfType<AudioListener>() != null) return;

        var cam = Camera.main;
        if (cam != null) cam.gameObject.AddComponent<AudioListener>();
        else
        {
            var go = new GameObject("AudioListener");
            go.AddComponent<AudioListener>();
        }
        Debug.Log("[音频] 场景里没有 AudioListener，已补到主相机上");
    }

    AudioSource CreateSource(string name, int priority)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = false;
        src.spatialBlend = 0f;          // 全 2D：车永远在镜头附近，3D 只会因为距离忽大忽小
        src.priority = priority;
        src.bypassEffects = true;
        src.bypassListenerEffects = true;
        src.volume = 1f;
        return src;
    }

    // ---------------- 语音 ----------------

    /// <summary>排队播一条语音（不会打断正在播的那条）。</summary>
    public static void Say(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        var d = Ensure();
        if (d != null) d.Enqueue(key);
    }

    /// <summary>立即打断并播这条语音（复位、碰撞提示这类需要马上听到的）。</summary>
    public static void SayNow(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        var d = Ensure();
        if (d != null)
        {
            d._queue.Clear();
            d.PlayKey(key);
        }
    }

    void Enqueue(string key)
    {
        if (key == _currentKey && VoiceBusy) return;      // 正在播同一条，不重复
        if (_queue.Contains(key)) return;                  // 队列里已经有了

        if (VocieMissing(key)) return;

        _queue.Enqueue(key);
        if (_queue.Count > 4)
        {
            // 队列堆积（比如反复复位）时丢弃最旧的，别让提示延迟太久
            _queue.Dequeue();
        }
    }

    /// <summary>资源存在性检查（避免往队列里塞一个播不出来的 key）。</summary>
    bool VocieMissing(string key)
    {
        return Load("Audio/Voice/" + key) == null;
    }

    void PlayKey(string key)
    {
        var clip = Load("Audio/Voice/" + key);
        if (clip == null) return;

        _currentKey = key;
        _voice.clip = clip;
        _voice.volume = muted ? 0f : masterVolume * voiceVolume;
        _voice.Play();
    }

    // ---------------- 音效 ----------------

    public static void PlaySfx(string key, float volume = 1f, float pitch = 1f)
    {
        if (string.IsNullOrEmpty(key)) return;
        var d = Ensure();
        if (d != null) d.PlayOne(key, volume, pitch);
    }

    void PlayOne(string key, float volume, float pitch)
    {
        var clip = Load("Audio/Sfx/" + key);
        if (clip == null) return;

        _sfx.pitch = Mathf.Clamp(pitch, 0.5f, 2f);
        _sfx.PlayOneShot(clip, Mathf.Clamp01(volume) * masterVolume * sfxVolume * SfxDuck);
    }

    /// <summary>给 CarAudio 之类的循环音源取资源。</summary>
    public AudioClip LoadSfx(string key)
    {
        return Load("Audio/Sfx/" + key);
    }

    public float SfxVolumeNow
    {
        get { return muted ? 0f : masterVolume * sfxVolume; }
    }

    // ---------------- 资源加载 ----------------

    AudioClip Load(string path)
    {
        AudioClip clip;
        if (_clips.TryGetValue(path, out clip)) return clip;

        clip = Resources.Load<AudioClip>(path);
        if (clip == null)
        {
            if (_missing.Add(path))
                Debug.LogWarning("[音频] 找不到 Resources/" + path + "（检查 Assets/Resources/Audio 下有没有这个文件）");
            return null;
        }
        _clips[path] = clip;
        return clip;
    }

    // ---------------- 每帧 ----------------

    void Update()
    {
        if (_voice == null) return;

#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.F9))
        {
            muted = !muted;
            if (_voice != null) _voice.volume = muted ? 0f : masterVolume * voiceVolume;
            Debug.Log("[音频] " + (muted ? "静音" : "恢复声音"));
        }
#endif

        // 语音播完 → 记一个间隙，避免两条贴在一起
        bool playing = _voice.isPlaying;
        if (_wasPlaying && !playing) _gapTimer = voiceGap;
        _wasPlaying = playing;

        if (playing) return;

        if (_gapTimer > 0f)
        {
            _gapTimer -= Time.unscaledDeltaTime;
            return;
        }

        if (_queue.Count > 0) PlayKey(_queue.Dequeue());
    }

    [System.NonSerialized] public bool muted;

    void LateUpdate()
    {
        EnsureCar();
    }

    /// <summary>车是场景里现成的对象，这里只负责给它补一个 CarAudio（不碰场景文件）。</summary>
    void EnsureCar()
    {
        if (_carAudio != null) return;

        _carScanTimer -= Time.unscaledDeltaTime;
        if (_carScanTimer > 0f) return;
        _carScanTimer = 0.5f;

        var controller = FindObjectOfType<CarController>();
        if (controller == null) return;

        _carAudio = controller.gameObject.AddComponent<CarAudio>();
        Debug.Log("[音频] 车辆音效已挂到 " + controller.name);
    }

    // ---------------- 字幕 ----------------

    GUIStyle _subStyle;

    void OnGUI()
    {
        if (!showSubtitle || !Application.isPlaying) return;

        string text = CurrentSubtitle;
        if (string.IsNullOrEmpty(text)) return;

        if (_subStyle == null)
        {
            _subStyle = new GUIStyle(GUI.skin.label);
            _subStyle.fontSize = 19;
            _subStyle.alignment = TextAnchor.MiddleCenter;
            _subStyle.wordWrap = false;
            _subStyle.normal.textColor = new Color(1f, 0.97f, 0.88f);
        }

        var content = new GUIContent(text);
        float w = Mathf.Min(Screen.width - 40f, _subStyle.CalcSize(content).x + 36f);
        var rect = new Rect((Screen.width - w) * 0.5f, Screen.height - 74f, w, 34f);

        var old = GUI.color;
        GUI.color = new Color(0.03f, 0.04f, 0.06f, 0.66f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = old;

        GUI.Label(rect, text, _subStyle);
    }
}
