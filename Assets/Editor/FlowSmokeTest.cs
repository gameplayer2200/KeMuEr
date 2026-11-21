using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 流程自检：跑完一圈之后的「结束」链路。
///
/// 检查三件事：
///   1) 结算（DebugFinish）真的进入结束状态，成绩单数据算得出来；
///   2) 结算时车辆被冻结（CarController.DrivingEnabled == false），车不会在成绩单背后继续跑；
///   3) 按蓝键（复位）能解冻并从第 1 项重新开一圈。
///
/// batchmode 用法：
///   Unity.exe -batchmode -nographics -projectPath &lt;项目&gt; -logFile Temp/flow_smoke.log
///             -executeMethod FlowSmokeTest.PlaySmoke
/// 结果落 Temp/flow_smoke_result.txt（第一行 PASS 0 / FAIL n），命令行退出码仅供参考。
///
/// 和 AudioSmokeTest 一样，状态放 SessionState：进 Play 会做一次域名重载，
/// 静态字段会被清空，靠 [InitializeOnLoadMethod] 重新挂回 update 接着跑。
/// </summary>
public static class FlowSmokeTest
{
    const int PhaseIdle = 0;
    const int PhaseWaitPlay = 1;
    const int PhaseRunning = 2;
    const int PhaseStop = 3;
    const int PhaseDone = 4;

    const string KeyPhase = "FlowSmoke.Phase";
    const string KeyT0 = "FlowSmoke.T0";
    const string KeyFrameMark = "FlowSmoke.FrameMark";
    const string KeyFrameStamp = "FlowSmoke.FrameStamp";
    const string KeyErrors = "FlowSmoke.Errors";
    const string KeyErrorText = "FlowSmoke.ErrorText";
    const string KeyCheckFail = "FlowSmoke.CheckFail";
    const string KeyFrozen = "FlowSmoke.Frozen";
    const string KeyRestoreBg = "FlowSmoke.RestoreBg";
    const string KeyStep = "FlowSmoke.Step";
    const string KeyStepStart = "FlowSmoke.StepStart";
    const string KeyNotes = "FlowSmoke.Notes";

    static int Phase
    {
        get { return SessionState.GetInt(KeyPhase, PhaseIdle); }
        set { SessionState.SetInt(KeyPhase, value); }
    }

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

    [MenuItem("科目二/流程自检（结束与重来）")]
    public static void PlaySmoke()
    {
        SessionState.SetBool(KeyRestoreBg, PlayerSettings.runInBackground);
        PlayerSettings.runInBackground = true;

        SessionState.SetInt(KeyErrors, 0);
        SessionState.SetInt(KeyCheckFail, 0);
        SessionState.SetBool(KeyFrozen, false);
        SessionState.SetString(KeyErrorText, "");
        SessionState.SetString(KeyNotes, "");
        SessionState.SetInt(KeyStep, 0);

        EditorSceneManager.OpenScene("Assets/Scenes/L1_Tutorial.unity");

        Application.logMessageReceived -= OnLog;
        Application.logMessageReceived += OnLog;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;

        Phase = PhaseWaitPlay;
        Debug.Log("[流程自检] 进入 Play 模式检查「结束 → 重来」");
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

    static void Note(string line)
    {
        SessionState.SetString(KeyNotes, SessionState.GetString(KeyNotes, "") + line + "\n");
    }

    static void Require(bool ok, string message)
    {
        if (ok) return;
        SessionState.SetInt(KeyCheckFail, SessionState.GetInt(KeyCheckFail, 0) + 1);
        Note("✗ " + message);
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
            SessionState.SetFloat(KeyStepStart, (float)now);
            SessionState.SetInt(KeyFrameMark, Time.frameCount);
            return;
        }

        if (phase == PhaseRunning)
        {
            if (!EditorApplication.isPlaying)
            {
                Phase = PhaseDone;
                return;
            }

            if (Time.frameCount != SessionState.GetInt(KeyFrameMark, -1))
            {
                SessionState.SetInt(KeyFrameMark, Time.frameCount);
                SessionState.SetFloat(KeyFrameStamp, (float)now);
            }

            if (now - SessionState.GetFloat(KeyFrameStamp, (float)now) > 3.0)
            {
                SessionState.SetBool(KeyFrozen, true);
                Note("✗ 玩家循环被挂起（Time.frameCount 3 秒没变化）");
                Stop();
                return;
            }

            RunSteps(now);
            return;
        }

        if (Phase == PhaseStop)
        {
            if (EditorApplication.isPlaying) return;
            Phase = PhaseDone;
        }

        if (Phase == PhaseDone) Finish();
    }

    static void RunSteps(double now)
    {
        float since = (float)now - SessionState.GetFloat(KeyStepStart, (float)now);
        int step = SessionState.GetInt(KeyStep, 0);

        var exam = UnityEngine.Object.FindObjectOfType<Km2RouteDirector>();
        var car = UnityEngine.Object.FindObjectOfType<CarController>();

        switch (step)
        {
            case 0: // 等教学和音频总线起来，再手动开场
                if (since < 1.5f) return;
                if (exam == null || car == null)
                {
                    Require(false, "场景里找不到 Km2RouteDirector / CarController");
                    Stop();
                    return;
                }
                exam.BeginExam();
                Require(exam.IsActive && !exam.IsEnded, "BeginExam 之后没有进入考试状态");
                Require(car.DrivingEnabled, "开场时车辆就是冻结的（DrivingEnabled=false）");
                Note("· 开场 CurrentStage=" + exam.CurrentStage + " 车辆可操作=" + car.DrivingEnabled);
                NextStep(1, now);
                break;

            case 1: // 复位计数 + 结算 + 冻结
                if (since < 0.3f) return;
                int before = exam.ResetCount;
                exam.ResetToStageStart();
                Require(exam.ResetCount == before + 1, "复位计数没有 +1（" + before + " → " + exam.ResetCount + "）");

                exam.DebugFinish();
                Require(exam.IsEnded, "DebugFinish 之后 IsEnded 仍是 false");
                Require(!car.DrivingEnabled, "结算时车辆没有冻结（DrivingEnabled 仍为 true）");
                Require(exam.ElapsedSeconds >= 0f, "用时是负数：" + exam.ElapsedSeconds);
                Note("· 结算 IsEnded=" + exam.IsEnded + " 用时=" + Km2RouteDirector.FormatTime(exam.ElapsedSeconds)
                     + " 锥桶=" + exam.ConesHit + " 复位=" + exam.ResetCount + " 车辆可操作=" + car.DrivingEnabled);
                NextStep(2, now);
                break;

            case 2: // 蓝键重来：解冻 + 从第 1 项开始
                if (since < 0.3f) return;
                exam.RestartAfterFinish();
                Require(!exam.IsEnded, "RestartAfterFinish 之后 IsEnded 仍是 true");
                Require(exam.IsActive, "RestartAfterFinish 之后没有重新开始（IsActive=false）");
                Require(car.DrivingEnabled, "重来之后车辆仍然冻结（DrivingEnabled=false）");
                Require(exam.CurrentStage == 0, "重来之后不是从第 1 项开始：" + exam.CurrentStage);
                Require(exam.ConesHit == 0, "重来之后锥桶计数没有清零：" + exam.ConesHit);
                Note("· 重来 IsActive=" + exam.IsActive + " CurrentStage=" + exam.CurrentStage
                     + " 车辆可操作=" + car.DrivingEnabled);
                Stop();
                break;
        }
    }

    static void NextStep(int step, double now)
    {
        SessionState.SetInt(KeyStep, step);
        SessionState.SetFloat(KeyStepStart, (float)now);
    }

    static void Stop()
    {
        Phase = PhaseStop;
        Debug.Log("[流程自检] 收尾，退出 Play 模式");
        EditorApplication.isPlaying = false;
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
        string notes = SessionState.GetString(KeyNotes, "");

        PlayerSettings.runInBackground = SessionState.GetBool(KeyRestoreBg, false);

        int fail = errors + checkFail + (frozen ? 1 : 0);

        var sb = new StringBuilder();
        sb.AppendLine("[流程自检] 运行期错误 " + errors + " 条");
        if (errText.Length > 0) sb.AppendLine(errText);
        if (notes.Length > 0) sb.Append(notes);
        sb.AppendLine(fail == 0 ? "[流程自检] 结束与重来通过" : "[流程自检] 结束与重来失败（" + fail + " 项）");
        Debug.Log(sb.ToString());

        if (Application.isBatchMode) QuitBatch((fail == 0 ? "PASS" : "FAIL") + " " + fail + "\n" + sb);
    }

    static void QuitBatch(string result)
    {
        try
        {
            File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "Temp/flow_smoke_result.txt"), result);
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
}
