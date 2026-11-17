using UnityEngine;

/// <summary>科目二的一项考试科目。</summary>
[System.Serializable]
public class Km2Stage
{
    [Tooltip("1=倒车入库 2=侧方停车 3=坡道定点停车和起步 4=直角转弯 5=曲线行驶")]
    public int number = 1;

    [Tooltip("科目名（HUD 显示用）")]
    public string title = "倒车入库";

    [Tooltip("这个科目的引导路点（按顺序开过去）。到达最后一个点表示本科目结束。")]
    public Transform[] points = new Transform[0];

    [Tooltip("到达判定半径（米）")]
    public float radius = 5f;

    [Tooltip("最后一个点是否要求停稳（速度 < stopSpeed）。")]
    public bool requireStopAtEnd;

    [TextArea]
    public string instruction = "";
}

/// <summary>
/// 科目二考试路线：第一关跑完后把车 TP 到考试起点，然后按
/// 1 倒车入库 → 2 侧方停车 → 3 坡道定点停车和起步 → 4 直角转弯 → 5 曲线行驶
/// 的顺序依次引导。只提示、不判罚，和第一关同一套 HUD 语言。
/// </summary>
public class Km2RouteDirector : MonoBehaviour
{
    [Header("依赖")]
    public CarController car;

    [Header("起点")]
    [Tooltip("第一关跑完后，车被 TP 到这里（位置 + 朝向）。留空则不 TP。")]
    public Transform examStart;

    [Tooltip("是否在第一关完成时自动 TP 到考试起点。")]
    public bool teleportOnTutorialComplete = true;

    [Header("考试科目（按编号顺序）")]
    public Km2Stage[] stages = new Km2Stage[0];

    [Header("判定")]
    [Tooltip("低于这个速度算停车（m/s），约 1.5 km/h。")]
    public float stopSpeed = 0.42f;

    [Tooltip("要求停稳时要连续保持多少秒。")]
    public float stopHoldSeconds = 0.6f;

    [Tooltip("到达一个点后要连续保持多少秒才算数（防止高速擦过）。")]
    public float arriveHoldSeconds = 0.15f;

    // ---- 运行时状态 ----
    int _stage;
    int _point;
    float _holdTimer;
    bool _active;
    bool _finished;
    string _toast = "";
    float _toastUntil;

    StopMarker[] _stopMarkers;
    CarInput _input;

    /// <summary>科目二是否已经接管画面（第一关据此决定要不要继续画自己的 HUD）。</summary>
    public static bool ExamActive { get; private set; }

    public bool IsActive { get { return _active; } }
    public bool IsFinished { get { return _finished; } }
    public int CurrentStage { get { return _stage; } }

    void OnEnable()
    {
        TutorialDirector.Completed += OnTutorialCompleted;
        ResolveCar();
        CacheStopMarkers();
        ResolveInput();
        ExamActive = false;
        HideAllStopMarkers();
    }

    void OnDisable()
    {
        TutorialDirector.Completed -= OnTutorialCompleted;
        if (_input != null) _input.OnAction -= OnCarAction;
        _input = null;
        ExamActive = false;
    }

    void HideAllStopMarkers()
    {
        if (_stopMarkers == null) CacheStopMarkers();
        if (_stopMarkers == null) return;
        for (int i = 0; i < _stopMarkers.Length; i++)
            if (_stopMarkers[i] != null) _stopMarkers[i].SetForStage(-1);
    }

    void ResolveCar()
    {
        if (car == null) car = FindObjectOfType<CarController>();
    }

    void ResolveInput()
    {
        if (_input == null && car != null) _input = car.GetComponent<CarInput>();
        if (_input == null) _input = FindObjectOfType<CarInput>();
        if (_input != null)
        {
            _input.OnAction -= OnCarAction;
            _input.OnAction += OnCarAction;
        }
    }

    void CacheStopMarkers()
    {
        _stopMarkers = FindObjectsOfType<StopMarker>();
    }

    void OnCarAction(CarAction action)
    {
        if (action == CarAction.Reset) ResetToStageStart();
    }

    void OnTutorialCompleted()
    {
        if (!teleportOnTutorialComplete) return;
        if (examStart == null) return;
        BeginExam();
    }

    /// <summary>把车放到考试起点并从头开始科目二。</summary>
    public void BeginExam()
    {
        ResolveCar();
        if (car == null) return;

        if (examStart != null)
            car.ResetToPose(examStart.position, examStart.rotation);

        _stage = 0;
        _point = 0;
        _holdTimer = 0f;
        _active = true;
        _finished = false;
        ExamActive = true;
        RefreshStopMarkers();
        ShowToast("科目二开始：1 倒车入库");
    }

    /// <summary>把车放回当前科目的第一个航点（复位功能）。</summary>
    public void ResetToStageStart()
    {
        if (!_active || _finished) return;
        ResolveCar();
        if (car == null) return;
        if (stages == null || stages.Length == 0) return;
        if (_stage >= stages.Length) return;

        var stage = stages[_stage];
        if (stage.points == null || stage.points.Length == 0) return;

        _point = 0;
        _holdTimer = 0f;
        var start = stage.points[0];
        car.ResetToPose(start.position, start.rotation);
        ShowToast("已回到「" + stage.title + "」起点");
    }

    /// <summary>
    /// 测试用：直接跳到指定科目（stage 下标，0 起），车被放到该科目第一个航点。
    /// 供 LevelManager 选关时调用。
    /// </summary>
    public void JumpToStage(int stageIndex)
    {
        ResolveCar();
        if (car == null) return;
        if (stages == null || stages.Length == 0) return;
        if (stageIndex < 0 || stageIndex >= stages.Length) return;

        var stage = stages[stageIndex];
        if (stage.points == null || stage.points.Length == 0) return;

        _stage = stageIndex;
        _point = 0;
        _holdTimer = 0f;
        _active = true;
        _finished = false;
        ExamActive = true;

        car.ResetToPose(stage.points[0].position, stage.points[0].rotation);
        RefreshStopMarkers();
        ShowToast("跳转到：" + stage.number + " " + stage.title);
    }

    void RefreshStopMarkers()
    {
        if (_stopMarkers == null) CacheStopMarkers();
        if (_stopMarkers == null) return;
        int current = _stage + 1;  // 科目编号 1 起
        for (int i = 0; i < _stopMarkers.Length; i++)
        {
            if (_stopMarkers[i] != null) _stopMarkers[i].SetForStage(current);
        }
    }

    void Update()
    {
        if (!_active || _finished) return;
        ResolveCar();
        if (car == null) return;
        if (stages == null || stages.Length == 0) return;
        if (_stage >= stages.Length) { Finish(); return; }

        var stage = stages[_stage];
        if (stage.points == null || stage.points.Length == 0) { AdvanceStage(); return; }

        var target = stage.points[Mathf.Min(_point, stage.points.Length - 1)];
        if (target == null) { AdvanceStage(); return; }

        Vector3 p = car.transform.position;
        float d = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(target.position.x, target.position.z));
        bool arrived = d <= Mathf.Max(0.1f, stage.radius);

        bool isLastPoint = _point >= stage.points.Length - 1;
        bool needStop = isLastPoint && stage.requireStopAtEnd;
        bool ok = arrived && (!needStop || car.SpeedKmh / 3.6f <= stopSpeed);

        if (ok)
        {
            _holdTimer += Time.deltaTime;
            if (_holdTimer >= (needStop ? stopHoldSeconds : arriveHoldSeconds))
            {
                _holdTimer = 0f;
                _point++;
                if (_point >= stage.points.Length) AdvanceStage();
            }
        }
        else
        {
            _holdTimer = 0f;
        }
    }

    void AdvanceStage()
    {
        _stage++;
        _point = 0;
        _holdTimer = 0f;

        if (stages == null || _stage >= stages.Length) { Finish(); return; }
        RefreshStopMarkers();
        ShowToast("下一项：" + stages[_stage].number + " " + stages[_stage].title);
    }

    void Finish()
    {
        _finished = true;
        _active = false;
        if (_stopMarkers != null)
        {
            for (int i = 0; i < _stopMarkers.Length; i++)
                if (_stopMarkers[i] != null) _stopMarkers[i].SetForStage(-1);  // 全部隐藏
        }
        ShowToast("科目二路线走完！");
    }

    /// <summary>选关菜单切回第一关时调用：结束科目二流程并清空接管状态。</summary>
    public void FinishExamForMenu()
    {
        _active = false;
        _finished = true;
        ExamActive = false;
        if (_stopMarkers != null)
        {
            for (int i = 0; i < _stopMarkers.Length; i++)
                if (_stopMarkers[i] != null) _stopMarkers[i].SetForStage(-1);
        }
    }

    void ShowToast(string m)
    {
        _toast = m;
        _toastUntil = Time.time + 2.5f;
    }

    // ---------------- HUD ----------------

    GUIStyle _title, _body, _small, _big;

    void EnsureStyles()
    {
        if (_title != null) return;
        _title = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold };
        _title.normal.textColor = new Color(1f, 0.93f, 0.82f);
        _body = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true };
        _body.normal.textColor = Color.white;
        _small = new GUIStyle(GUI.skin.label) { fontSize = 15 };
        _small.normal.textColor = new Color(0.85f, 0.88f, 0.92f);
        _big = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold };
        _big.normal.textColor = new Color(0.55f, 1f, 0.55f);
    }

    void OnGUI()
    {
        if (!_active && !_finished) return;
        EnsureStyles();

        if (_finished)
        {
            var s = new GUIStyle(_big) { alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(0f, Screen.height * 0.3f, Screen.width, 60f), "科目二路线完成", s);
            return;
        }

        var stage = stages[Mathf.Min(_stage, stages.Length - 1)];

        // 顶部任务卡：和第一关同一位置、同一尺寸（第一关此时已经不画了）
        float cardW = Mathf.Min(620f, Screen.width - 48f);
        float cardH = 152f;
        Rect card = new Rect(24f, 24f, cardW, cardH);
        DrawPanel(card, new Color(0.04f, 0.05f, 0.07f, 0.80f));

        string header = string.Format("科目二 · {0}/{1}   {2}   {3}",
            stage.number, stages.Length, stage.number, stage.title);
        if (stage.points != null && stage.points.Length > 0)
            header += string.Format("   {0}/{1}", Mathf.Min(_point + 1, stage.points.Length), stage.points.Length);

        GUI.Label(new Rect(card.x + 18f, card.y + 10f, card.width - 36f, 32f), header, _title);
        GUI.Label(new Rect(card.x + 18f, card.y + 50f, card.width - 36f, 92f), stage.instruction, _body);

        // 目标距离
        if (stage.points != null && stage.points.Length > 0)
        {
            var target = stage.points[Mathf.Min(_point, stage.points.Length - 1)];
            if (target != null)
            {
                Vector3 p = car.transform.position;
                float d = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(target.position.x, target.position.z));
                Rect dist = new Rect(Screen.width * 0.5f - 120f, 24f, 240f, 52f);                DrawPanel(dist, new Color(0.04f, 0.05f, 0.07f, 0.62f));
                bool needStop = _point >= stage.points.Length - 1 && stage.requireStopAtEnd;
                GUI.Label(new Rect(dist.x + 12f, dist.y + 8f, 220f, 34f),
                    (needStop ? "停车点  " : "目标  ") + Mathf.RoundToInt(d) + " m", _body);
            }
        }

        // ---- 右上仪表（速度 + 档位 + 手刹）----
        // 科目二接管后第一关的 HUD 让位了，这段仪表盘得在这里补上，否则档位显示会消失。
        if (car != null)
        {
            Rect dash = new Rect(Screen.width - 250f, 24f, 226f, 96f);
            DrawPanel(dash, new Color(0.04f, 0.05f, 0.07f, 0.75f));

            var speedStyle = new GUIStyle(_title);
            speedStyle.fontSize = 30;
            speedStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(dash.x + 16f, dash.y + 6f, 200f, 36f),
                Mathf.RoundToInt(car.SpeedKmh) + " km/h", speedStyle);

            string gear = car.Gear == DriveGear.Reverse ? "R 倒挡"
                        : car.Gear == DriveGear.Neutral ? "N 空挡" : "1 前进挡";
            var carInput = car.GetComponent<CarInput>();
            GUI.Label(new Rect(dash.x + 16f, dash.y + 46f, 200f, 24f),
                gear + (carInput != null && carInput.Handbrake ? "   手刹" : ""), _small);
        }

        if (Time.time < _toastUntil && !string.IsNullOrEmpty(_toast))
        {
            var s = new GUIStyle(_body) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(0f, Screen.height * 0.72f, Screen.width, 40f), _toast, s);
        }
    }

    static void DrawPanel(Rect r, Color c)
    {
        var old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = old;
    }
}
