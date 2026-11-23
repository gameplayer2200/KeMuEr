using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 库位判定自检：侧方停车（科目 2）的「停进库才算过」链路。
///
/// 检查三件事：
///   1) 没挂倒挡时判不过（四条规则里的倒挡项生效）；
///   2) 挂过倒挡 + 完美入位 + 停稳，保持 stopHoldSeconds 后能推进到科目 3；
///   3) 库位状态灯的数据每帧都在更新（BayActive / BayState 不为默认值）。
///
/// 用 MenuItem「科目二/库位判定自检（侧方停车）」触发。状态走 SessionState，
/// 因为进 Play 会做域名重载，静态字段会被清空。
/// </summary>
public static class BayJudgeSmokeTest
{
    const int PhaseIdle = 0;
    const int PhaseWaitPlay = 1;
    const int PhaseRunning = 2;
    const int PhaseStop = 3;
    const int PhaseDone = 4;

    const string KeyPhase = "BaySmoke.Phase";
    const string KeyStep = "BaySmoke.Step";
    const string KeyStepStart = "BaySmoke.StepStart";
    const string KeyErrors = "BaySmoke.Errors";
    const string KeyErrorText = "BaySmoke.ErrorText";
    const string KeyCheckFail = "BaySmoke.CheckFail";
    const string KeyNotes = "BaySmoke.Notes";
    const string KeyRestoreBg = "BaySmoke.RestoreBg";

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

    [MenuItem("科目二/库位判定自检（侧方停车）")]
    public static void Run()
    {
        SessionState.SetBool(KeyRestoreBg, PlayerSettings.runInBackground);
        PlayerSettings.runInBackground = true;

        SessionState.SetInt(KeyErrors, 0);
        SessionState.SetInt(KeyCheckFail, 0);
        SessionState.SetString(KeyErrorText, "");
        SessionState.SetString(KeyNotes, "");
        SessionState.SetString("BaySmoke.Report", "");
        SessionState.SetInt(KeyStep, 0);

        EditorSceneManager.OpenScene("Assets/Scenes/L1_Tutorial.unity");

        Application.logMessageReceived -= OnLog;
        Application.logMessageReceived += OnLog;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;

        Phase = PhaseWaitPlay;
        Debug.Log("[库位自检] 进入 Play 模式检查「侧方停车 库位判定」");
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

    /// <summary>把车精确摆进库位：位置=库心，朝向=库轴。</summary>
    static void PlaceInBay(CarController car, Km2Stage stage)
    {
        Vector3 p = stage.bayCenter != null ? stage.bayCenter.position : stage.points[stage.bayPointIndex].position;
        car.ResetToPose(new Vector3(p.x, car.transform.position.y, p.z),
                        Quaternion.Euler(0f, stage.bayYawDeg, 0f));
    }

    /// <summary>把当前航点下标推到库位那一格（_point 是私有字段，自检里用反射改）。</summary>
    static void JumpPointToBay(Km2RouteDirector exam)
    {
        var f = typeof(Km2RouteDirector).GetField("_point",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (f == null) { Require(false, "找不到私有字段 _point"); return; }
        f.SetValue(exam, exam.stages[exam.CurrentStage].bayPointIndex);
    }

    /// <summary>读当前航点下标。</summary>
    static int GetPoint(Km2RouteDirector exam)
    {
        var f = typeof(Km2RouteDirector).GetField("_point",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (f == null) return -1;
        return (int)f.GetValue(exam);
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
            SessionState.SetFloat(KeyStepStart, (float)now);
            return;
        }

        if (phase == PhaseRunning)
        {
            if (!EditorApplication.isPlaying) { Phase = PhaseDone; return; }
            RunSteps(now);
            return;
        }

        if (phase == PhaseStop)
        {
            if (EditorApplication.isPlaying) return;
            Phase = PhaseDone;
        }

        if (phase == PhaseDone) Finish();
    }

    static void RunSteps(double now)
    {
        float since = (float)now - SessionState.GetFloat(KeyStepStart, (float)now);
        int step = SessionState.GetInt(KeyStep, 0);

        var exam = UnityEngine.Object.FindObjectOfType<Km2RouteDirector>();
        var car = UnityEngine.Object.FindObjectOfType<CarController>();

        switch (step)
        {
            case 0: // 等启动，跳到侧方停车（stage 下标 1）
                if (since < 1.5f) return;
                if (exam == null || car == null)
                {
                    Require(false, "场景里找不到 Km2RouteDirector / CarController");
                    Stop();
                    return;
                }
                exam.JumpToStage(1);
                PlaceInBay(car, exam.stages[1]);
                JumpPointToBay(exam);
                Require(exam.CurrentStage == 1, "JumpToStage(1) 之后 CurrentStage=" + exam.CurrentStage);
                Note("· 开场 CurrentStage=" + exam.CurrentStage + " bayPointIndex=" + exam.stages[1].bayPointIndex
                     + " BayActive=" + exam.BayActive);
                NextStep(1, now);
                break;

            case 1: // 前进挡 + 完美入位：位置摆对，但没挂过倒挡
                if (since < 0.35f) return;
                PlaceInBay(car, exam.stages[1]);   // ResetToPose 会把挡位写回 initialGear
                car.Gear = DriveGear.Drive;        // 所以挡位必须放在复位之后
                NextStep(2, now);
                break;

            case 2: // 上一帧是「前进挡 + 完美入位」：应因没挂倒挡而判不过
                if (since < 0.2f) return;
                Note("· 未挂倒挡入位 reversed=" + exam.BayState.reversed + " AllOk=" + exam.BayState.AllOk
                     + "（四角=" + exam.BayState.cornersInside + " 摆正=" + exam.BayState.aligned
                     + " 停稳=" + exam.BayState.stopped + "）");
                Require(!exam.BayState.reversed, "没挂倒挡却判定 reversed=true");
                Require(!exam.BayState.AllOk, "没挂倒挡却判定全部通过");
                PlaceInBay(car, exam.stages[1]);
                car.Gear = DriveGear.Reverse;
                NextStep(3, now);
                break;

            case 3: // 保持 stopHoldSeconds 后应推进到「开出点」
                if (since < (exam.stopHoldSeconds + 0.8f)) return;
                int pnt = GetPoint(exam);
                int bayIdx = exam.stages[1].bayPointIndex;
                Note("· 挂倒挡入位并保持后 _point=" + pnt + "（库位航点=" + bayIdx + "，期望 > " + bayIdx + "）"
                     + " CurrentStage=" + exam.CurrentStage);
                Require(pnt > bayIdx, "入位并保持后航点没推进，_point 仍是 " + pnt);
                Require(exam.BayState.AllOk, "库位四条规则没有全部满足："
                       + "四角=" + exam.BayState.cornersInside + " 摆正=" + exam.BayState.aligned
                       + " 停稳=" + exam.BayState.stopped + " 倒挡=" + exam.BayState.reversed);
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
        Debug.Log("[库位自检] 收尾，退出 Play 模式");
        EditorApplication.isPlaying = false;
    }

    static void Finish()
    {
        Phase = PhaseIdle;
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;

        int errors = SessionState.GetInt(KeyErrors, 0);
        int checkFail = SessionState.GetInt(KeyCheckFail, 0);
        string errText = SessionState.GetString(KeyErrorText, "");
        string notes = SessionState.GetString(KeyNotes, "");

        PlayerSettings.runInBackground = SessionState.GetBool(KeyRestoreBg, false);

        int fail = errors + checkFail;

        var sb = new StringBuilder();
        sb.AppendLine("[库位自检] 运行期错误 " + errors + " 条");
        if (errText.Length > 0) sb.AppendLine(errText);
        if (notes.Length > 0) sb.Append(notes);
        sb.AppendLine(fail == 0 ? "[库位自检] 侧方停车判定通过" : "[库位自检] 侧方停车判定失败（" + fail + " 项）");
        Debug.Log(sb.ToString());

        SessionState.SetString("BaySmoke.Report", sb.ToString());
        if (Application.isBatchMode) QuitBatch((fail == 0 ? "PASS" : "FAIL") + " " + fail + "\n" + sb);
    }

    static void QuitBatch(string result)
    {
        try
        {
            File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "Temp/bay_smoke_result.txt"), result);
        }
        catch (System.Exception) { }

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
