using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 音频自检：确认 Resources 下的音效/语音都能加载、不是空文件，并且播放期不抛异常。
///
/// 菜单：科目二 / 音频自检（资源）
/// 命令：
///   Unity.exe -batchmode -nographics -quit -executeMethod AudioSmokeTest.RunResources
///   Unity.exe -batchmode -nographics -executeMethod AudioSmokeTest.PlaySmoke
///
/// PlaySmoke 进 Play 模式后编辑器会做一次域名重载，静态字段全被清空，
/// 所以整个监视状态都放在 SessionState 里，靠 [InitializeOnLoadMethod] 重载后接着跑。
/// </summary>
public static class AudioSmokeTest
{
    const string VoiceDir = "Assets/Resources/Audio/Voice";
    const string SfxDir = "Assets/Resources/Audio/Sfx";

    // 必须和 VoiceLines 里的 key 一一对应
    static readonly string[] VoiceKeys =
    {
        "tut_intro", "tut_s1", "tut_s2", "tut_s3", "tut_s4", "tut_s5", "tut_s6", "tut_s7",
        "tut_done", "tut_cone", "tut_reset",
        "km2_start", "km2_s1", "km2_s2", "km2_s3", "km2_s4", "km2_s5",
        "km2_done", "km2_reset", "reverse_warn",
    };

    static readonly string[] SfxKeys =
    {
        "engine_loop", "wind_loop", "skid_loop", "brake_loop", "impact",
        "cone_hit", "gear_shift", "handbrake", "reverse_beep", "horn",
    };

    [MenuItem("科目二/音频自检（资源）")]
    public static void RunResourcesMenu()
    {
        RunResources();
    }

    /// <summary>编辑器里检查所有资源能否加载、时长/响度是否正常。</summary>
    public static void RunResources()
    {
        var log = new StringBuilder();
        int fail = 0;

        log.AppendLine("[音频自检] ===== 语音 =====");
        fail += CheckFolder(VoiceDir, "Audio/Voice", VoiceKeys, log);

        log.AppendLine("[音频自检] ===== 音效 =====");
        fail += CheckFolder(SfxDir, "Audio/Sfx", SfxKeys, log);

        log.AppendLine("[音频自检] ===== 台词表 =====");
        foreach (var key in VoiceKeys)
        {
            string text = VoiceLines.Text(key);
            if (string.IsNullOrEmpty(text))
            {
                log.AppendLine("  ✗ " + key + " 在 VoiceLines 里没有台词");
                fail++;
            }
        }
        log.AppendLine("  台词条目 " + VoiceKeys.Length + " 条");

        log.AppendLine(fail == 0 ? "[音频自检] 全部通过" : "[音频自检] 失败 " + fail + " 项");
        Debug.Log(log.ToString());

        if (Application.isBatchMode) EditorApplication.Exit(fail == 0 ? 0 : 1);
    }

    static int CheckFolder(string dir, string resourcePrefix, string[] expected, StringBuilder log)
    {
        int fail = 0;

        if (!Directory.Exists(dir))
        {
            log.AppendLine("  ✗ 目录不存在：" + dir);
            return 1;
        }

        var files = new HashSet<string>();
        foreach (var path in Directory.GetFiles(dir))
        {
            if (path.EndsWith(".meta")) continue;
            files.Add(Path.GetFileNameWithoutExtension(path));
        }

        // 1) 清单里的 key 都要有文件
        foreach (var key in expected)
        {
            if (!files.Contains(key))
            {
                log.AppendLine("  ✗ 缺文件：" + key);
                fail++;
            }
        }

        // 2) 目录里的每个文件都要能真的加载出 AudioClip，且不能是全静音
        foreach (var key in files)
        {
            var clip = Resources.Load<AudioClip>(resourcePrefix + "/" + key);
            if (clip == null)
            {
                log.AppendLine("  ✗ 加载失败：" + resourcePrefix + "/" + key);
                fail++;
                continue;
            }

            float rms = Rms(clip);
            bool quiet = rms < 0.002f;
            log.AppendLine(string.Format("  {0} {1,-14} {2,5:F2}s  rms={3:F3}",
                quiet ? "✗" : "✓", key, clip.length, rms));
            if (quiet) fail++;
            if (clip.length < 0.1f)
            {
                log.AppendLine("    ✗ 时长过短：" + key);
                fail++;
            }
        }

        return fail;
    }

    /// <summary>归一化 RMS：用来抓「文件在但内容全静音」的情况。</summary>
    static float Rms(AudioClip clip)
    {
        try
        {
            int samples = clip.samples * clip.channels;
            if (samples <= 0) return 0f;
            if (samples > 44100 * 5) samples = 44100 * 5;   // 只测前 5 秒

            var data = new float[samples];
            if (!clip.GetData(data, 0)) return 1f;          // 取不到采样就不判失败

            double sum = 0;
            for (int i = 0; i < samples; i++) sum += data[i] * data[i];
            return Mathf.Sqrt((float)(sum / samples));
        }
        catch (System.Exception)
        {
            return 1f;
        }
    }

    // ---------------- 播放期冒烟测试 ----------------

    const int PhaseIdle = 0, PhaseWaitPlay = 1, PhaseRunning = 2, PhaseStop = 3, PhaseDone = 4;

    const string KeyPhase = "AudioSmoke.Phase";
    const string KeyT0 = "AudioSmoke.T0";
    const string KeyFrameMark = "AudioSmoke.FrameMark";
    const string KeyFrameStamp = "AudioSmoke.FrameStamp";
    const string KeyErrors = "AudioSmoke.Errors";
    const string KeyErrorText = "AudioSmoke.ErrorText";
    const string KeyCheckFail = "AudioSmoke.CheckFail";
    const string KeyFrozen = "AudioSmoke.Frozen";
    const string KeyRestoreBg = "AudioSmoke.RestoreBg";

    static int Phase
    {
        get { return SessionState.GetInt(KeyPhase, PhaseIdle); }
        set { SessionState.SetInt(KeyPhase, value); }
    }

    // 域名重载后自动重新挂回 update
    [InitializeOnLoadMethod]
    static void Boot()
    {
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;

        if (Phase != PhaseIdle)
        {
            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;
        }
    }

    /// <summary>进 Play 模式跑几秒，抓运行期异常 / 确认音频总线真的起来了。</summary>
    public static void PlaySmoke()
    {
        // batchmode 下编辑器不算「有焦点」，不打开 Run In Background 玩家循环会停住
        SessionState.SetBool(KeyRestoreBg, PlayerSettings.runInBackground);
        PlayerSettings.runInBackground = true;

        SessionState.SetInt(KeyErrors, 0);
        SessionState.SetInt(KeyCheckFail, 0);
        SessionState.SetBool(KeyFrozen, false);
        SessionState.SetString(KeyErrorText, "");

        EditorSceneManager.OpenScene("Assets/Scenes/L1_Tutorial.unity");

        Application.logMessageReceived -= OnLog;
        Application.logMessageReceived += OnLog;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;

        Phase = PhaseWaitPlay;
        Debug.Log("[音频自检] 进入 Play 模式做运行期检查");
        EditorApplication.isPlaying = true;
    }

    static void OnLog(string condition, string stack, LogType type)
    {
        if (Phase == PhaseIdle) return;
        if (type != LogType.Exception && type != LogType.Error) return;

        SessionState.SetInt(KeyErrors, SessionState.GetInt(KeyErrors, 0) + 1);
        string text = SessionState.GetString(KeyErrorText, "");
        if (text.Length < 4000) SessionState.SetString(KeyErrorText, text + type + ": " + condition + "\n");
    }

    static void Tick()
    {
        int phase = Phase;
        if (phase == PhaseIdle) return;

        double now = EditorApplication.timeSinceStartup;

        if (phase == PhaseWaitPlay)
        {
            if (!EditorApplication.isPlaying) return;
            Phase = PhaseRunning;
            SessionState.SetFloat(KeyT0, (float)now);
            SessionState.SetFloat(KeyFrameStamp, (float)now);
            SessionState.SetInt(KeyFrameMark, Time.frameCount);
            return;
        }

        if (phase == PhaseRunning)
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.Log("[音频自检] Play 模式被提前退出，直接收尾");
                Phase = PhaseDone;
            }
            else
            {
                if (Time.frameCount != SessionState.GetInt(KeyFrameMark, -1))
                {
                    SessionState.SetInt(KeyFrameMark, Time.frameCount);
                    SessionState.SetFloat(KeyFrameStamp, (float)now);
                }

                bool frozen = now - SessionState.GetFloat(KeyFrameStamp, (float)now) > 3.0;
                bool timeUp = now - SessionState.GetFloat(KeyT0, (float)now) > 6.0;   // 6 秒：够建总线、挂 CarAudio、TutIntro 播起来
                if (!frozen && !timeUp) return;

                if (frozen)
                {
                    SessionState.SetBool(KeyFrozen, true);
                    Debug.Log("[音频自检] 玩家循环被挂起（Time.frameCount 3 秒没变化）");
                }

                Check();
                EditorApplication.isPlaying = false;
                Phase = PhaseStop;
                SessionState.SetFloat(KeyT0, (float)now);
                return;
            }
        }

        if (Phase == PhaseStop)
        {
            if (EditorApplication.isPlaying) return;
            Phase = PhaseDone;
        }

        if (Phase == PhaseDone) Finish();
    }

    static void Finish()
    {
        Phase = PhaseIdle;
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;

        int errors = SessionState.GetInt(KeyErrors, 0);
        int checkFail = SessionState.GetInt(KeyCheckFail, 0);
        bool frozen = SessionState.GetBool(KeyFrozen, false);
        string errText = SessionState.GetString(KeyErrorText, "");

        PlayerSettings.runInBackground = SessionState.GetBool(KeyRestoreBg, false);

        int fail = errors + checkFail + (frozen ? 1 : 0);

        var sb = new StringBuilder();
        sb.AppendLine("[音频自检] 运行期错误 " + errors + " 条");
        if (errText.Length > 0) sb.AppendLine(errText);
        sb.AppendLine(fail == 0 ? "[音频自检] 播放期通过" : "[音频自检] 播放期失败（" + fail + " 项）");
        Debug.Log(sb.ToString());

        if (Application.isBatchMode) QuitBatch((fail == 0 ? "PASS" : "FAIL") + " " + fail + "\n" + sb);
    }

    /// <summary>
    /// batchmode 里 EditorApplication.Exit 有时候退不干净（进程挂着还占着项目锁），
    /// 所以先落一份结果文件，再起一个后台线程兜底强杀。
    /// </summary>
    static void QuitBatch(string result)
    {
        try
        {
            File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "Temp/audio_smoke_result.txt"), result);
        }
        catch (System.Exception)
        {
            // 写不了结果文件不影响退出
        }

        var killer = new System.Threading.Thread(() =>
        {
            System.Threading.Thread.Sleep(8000);
            try { System.Diagnostics.Process.GetCurrentProcess().Kill(); } catch (System.Exception) { }
        });
        killer.IsBackground = true;
        killer.Start();

        EditorApplication.Exit(result.StartsWith("PASS") ? 0 : 1);
    }

    static void Check()
    {
        var sb = new StringBuilder();
        sb.AppendLine("[音频自检] ===== 运行期状态 =====");
        int bad = 0;

        var director = AudioDirector.Instance;
        if (director == null)
        {
            sb.AppendLine("  ✗ AudioDirector 没有自动创建");
            bad++;
        }
        else
        {
            sb.AppendLine("  ✓ AudioDirector 已创建");
            if (director.VoiceBusy) sb.AppendLine("  ✓ 语音正在播放：" + director.CurrentSubtitle);
            else if (director.QueuedCount > 0) sb.AppendLine("  ✓ 语音已入队 " + director.QueuedCount + " 条");
            else
            {
                sb.AppendLine("  ✗ 语音既没在播也没有排队（tut_intro 没触发？）");
                bad++;
            }
        }

        var carAudio = Object.FindObjectOfType<CarAudio>();
        if (carAudio == null) { sb.AppendLine("  ✗ 车上没有 CarAudio"); bad++; }
        else sb.AppendLine("  ✓ CarAudio 已挂到 " + carAudio.name);

        if (Object.FindObjectOfType<AudioListener>() == null)
        {
            sb.AppendLine("  ✗ 场景里没有 AudioListener");
            bad++;
        }
        else sb.AppendLine("  ✓ AudioListener 存在");

        var car = Object.FindObjectOfType<CarController>();
        if (car == null) { sb.AppendLine("  ✗ 没找到 CarController"); bad++; }
        else sb.AppendLine("  ✓ 找到车辆 " + car.name);

        var input = Object.FindObjectOfType<CarInput>();
        if (input != null)
            sb.AppendLine("  · 输入布局 " + input.CurrentProfileName + "，手柄轴 " + (input.JoystickAxesOk ? "可用" : "不可用（走键盘）"));

        SessionState.SetInt(KeyCheckFail, SessionState.GetInt(KeyCheckFail, 0) + bad);
        Debug.Log(sb.ToString());
    }
}
