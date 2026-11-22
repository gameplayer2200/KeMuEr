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

    [Header("库位判定")]
    [Tooltip("哪一段航点用库位判定。-1 = 不用（纯到达判定）。\n" +
             "填 2 表示走到 points[2] 时，不是「靠近」就算过，而是必须停进库才算过。\n" +
             "侧方停车 / 倒车入库都填 2。")]
    public int bayPointIndex = -1;

    [Tooltip("库位中心锚点。留空则用 points[bayPointIndex] 的位置。")]
    public Transform bayCenter;

    [Tooltip("库位尺寸：(横向宽度, 纵向长度)，单位米。bayYawDeg 为 0 时 x 是横宽、y 是纵深。")]
    public Vector2 baySize = new Vector2(2.62f, 6.4f);

    [Tooltip("库位朝向（度）。0 = 库长沿世界 Z 轴；90 = 库长沿世界 X 轴。")]
    public float bayYawDeg;

    [Tooltip("车身与库轴允许的最大夹角（度）。超过就判没摆正。")]
    public float bayAlignToleranceDeg = 8f;

    [Tooltip("四角允许溢出库边的量（米）。0 表示必须严格在线内。")]
    public float bayEdgeTolerance = 0.05f;

    [Tooltip("是否要求入位过程中挂过倒挡。")]
    public bool bayRequireReverse = true;
}

/// <summary>
/// 科目二考试路线：第一关跑完后把车 TP 到考试起点，然后按
/// 1 倒车入库 → 2 侧方停车 → 3 坡道定点停车和起步 → 4 直角转弯 → 5 曲线行驶
/// 的顺序依次引导。
///
/// 判罚分两档：3/4/5 项只做到点判定，不判罚；1/2 项（bayPointIndex 指向库位航点）用库位判定，
/// 要求四角线内 + 车身摆正 + 已停稳 + 挂过倒挡，四条全中才算过。
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

    [Header("结束")]
    [Tooltip("跑完一圈后冻结车辆操作：成绩单背后车不能还在乱跑。")]
    public bool freezeCarWhenFinished = true;

    // ---- 运行时状态 ----
    int _stage;
    int _point;
    float _holdTimer;
    bool _active;
    bool _finished;
    string _toast = "";
    float _toastUntil;

    // 库位判定：当前科目里是否挂过倒挡（挂一次就锁定，直到本科目结束）
    bool _reverseSeen;

    // 当前帧的库位判定结果，HUD 读它画状态灯
    BayJudgeResult _bay;

    /// <summary>当前科目是否正在做库位判定（HUD 据此决定要不要画状态灯）。</summary>
    public bool BayActive
    {
        get
        {
            if (!_active || _finished || stages == null) return false;
            if (_stage < 0 || _stage >= stages.Length) return false;
            var s = stages[_stage];
            if (s.bayPointIndex < 0) return false;
            return _point >= s.bayPointIndex;
        }
    }

    /// <summary>当前帧的库位判定结果。</summary>
    public BayJudgeResult BayState { get { return _bay; } }

    // 结束状态：跑完一圈后进入，画成绩单、冻结车辆
    bool _ended;
    float _examStartTime;
    float _elapsedSeconds;
    int _conesHit;
    IDrivingInput _drivingCache;   // 冻结前缓存的输入源

    StopMarker[] _stopMarkers;

    /// <summary>科目二是否已经接管画面（第一关据此决定要不要继续画自己的 HUD）。</summary>
    public static bool ExamActive { get; private set; }

    public bool IsActive { get { return _active; } }
    public bool IsFinished { get { return _finished; } }

    /// <summary>一圈跑完了，正在显示成绩单。</summary>
    public bool IsEnded { get { return _ended; } }

    /// <summary>这一圈用了多少秒（从 BeginExam 开始算）。</summary>
    public float ElapsedSeconds { get { return _ended ? _elapsedSeconds : Mathf.Max(0f, Time.time - _examStartTime); } }

    /// <summary>这一圈碰倒的锥桶数量。</summary>
    public int ConesHit { get { return _conesHit; } }

    public int CurrentStage { get { return _stage; } }

    void OnEnable()
    {
        TutorialDirector.Completed += OnTutorialCompleted;
        ConeKnock.Knocked += OnConeKnocked;
        ResolveCar();
        CacheStopMarkers();
        ExamActive = false;
        HideAllStopMarkers();
    }

    void OnDisable()
    {
        TutorialDirector.Completed -= OnTutorialCompleted;
        ConeKnock.Knocked -= OnConeKnocked;
        ExamActive = false;
    }

    void OnConeKnocked(ConeKnock cone)
    {
        if (_active && !_ended) _conesHit++;
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

    void CacheStopMarkers()
    {
        _stopMarkers = FindObjectsOfType<StopMarker>();
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
        _ended = false;
        _elapsedSeconds = 0f;
        _examStartTime = Time.time;
        _conesHit = 0;
        _reverseSeen = false;
        _bay = new BayJudgeResult();
        RestoreDriving();
        ExamActive = true;
        RefreshStopMarkers();
        ShowToast("科目二开始：1 倒车入库");
        AudioDirector.Say(VoiceLines.Km2Start);
        if (stages != null && stages.Length > 0) AudioDirector.Say(VoiceLines.StageKey(stages[0].number));
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
        _ended = false;
        _elapsedSeconds = 0f;
        _examStartTime = Time.time;
        _conesHit = 0;
        _reverseSeen = false;
        _bay = new BayJudgeResult();
        RestoreDriving();
        ExamActive = true;

        car.ResetToPose(stage.points[0].position, stage.points[0].rotation);
        RefreshStopMarkers();
        ShowToast("跳转到：" + stage.number + " " + stage.title);
        AudioDirector.Say(VoiceLines.StageKey(stage.number));
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

        // 库位科目：只要本项还没结束，挂过一次倒挡就记下来（退出倒挡也保留）
        if (stage.bayPointIndex >= 0 && car.Gear == DriveGear.Reverse) _reverseSeen = true;

        if (stage.points == null || stage.points.Length == 0) { AdvanceStage(); return; }

        // ---- 库位航点：目标不是「靠近」，而是「四条规则全满足并保持」----
        if (stage.bayPointIndex >= 0 && _point == stage.bayPointIndex)
        {
            Transform anchor = stage.bayCenter != null ? stage.bayCenter : stage.points[stage.bayPointIndex];

            _bay = Km2BayJudge.Evaluate(
                car, anchor,
                stage.baySize, stage.bayYawDeg,
                stage.bayAlignToleranceDeg, stage.bayEdgeTolerance,
                stage.bayRequireReverse, stopSpeed,
                _reverseSeen);

            if (_bay.AllOk)
            {
                _holdTimer += Time.deltaTime;
                if (_holdTimer >= stopHoldSeconds)
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
            return;
        }

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
        _reverseSeen = false;
        _bay = new BayJudgeResult();

        if (stages == null || _stage >= stages.Length) { Finish(); return; }
        RefreshStopMarkers();
        ShowToast("下一项：" + stages[_stage].number + " " + stages[_stage].title);
        AudioDirector.Say(VoiceLines.StageKey(stages[_stage].number));
    }

    void Finish()
    {
        _finished = true;
        _active = false;
        _ended = true;
        _elapsedSeconds = Mathf.Max(0f, Time.time - _examStartTime);
        if (_stopMarkers != null)
        {
            for (int i = 0; i < _stopMarkers.Length; i++)
                if (_stopMarkers[i] != null) _stopMarkers[i].SetForStage(-1);  // 全部隐藏
        }
        if (freezeCarWhenFinished) FreezeCar();
        ShowToast("科目二完成！这一圈用时 " + FormatTime(_elapsedSeconds));
        AudioDirector.Say(VoiceLines.Km2Done);
    }

    /// <summary>
    /// 冻结车辆：成绩单期间车不能还跟着摇杆跑。
    /// CarController.Update 开头就是 `if (_driving == null) return;`，把输入源摘掉即可。
    /// </summary>
    void FreezeCar()
    {
        if (car == null) return;
        var input = car.GetComponent<CarInput>();
        if (input != null) _drivingCache = input;
        car.SetDrivingInput(null);
    }

    /// <summary>把冻结前缓存的输入源装回去。</summary>
    void RestoreDriving()
    {
        if (car == null) return;
        if (_drivingCache == null)
        {
            var input = car.GetComponent<CarInput>();
            if (input != null) _drivingCache = input;
        }
        if (_drivingCache != null) car.SetDrivingInput(_drivingCache);
    }

    /// <summary>测试用：不跑路线，直接进入结束状态（验证成绩单 / 冻结 / 重来链路）。</summary>
    public void DebugFinish()
    {
        ResolveCar();
        if (!_active && !_ended) BeginExam();
        Finish();
    }

    /// <summary>秒 → "分:秒"（成绩单和 HUD 共用）。</summary>
    public static string FormatTime(float seconds)
    {
        if (seconds < 0f) seconds = 0f;
        int m = Mathf.FloorToInt(seconds / 60f);
        float s = seconds - m * 60f;
        return string.Format("{0}:{1:00.0}", m, s);
    }

    /// <summary>选关菜单切回第一关时调用：结束科目二流程并清空接管状态。</summary>
    public void FinishExamForMenu()
    {
        _active = false;
        _finished = true;
        _ended = false;
        ExamActive = false;
        RestoreDriving();
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

        if (_ended)
        {
            DrawFinishScreen();
            return;
        }

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

        // 目标距离（库位科目在最后一点改成显示「离库边还有多少余量」）
        if (stage.points != null && stage.points.Length > 0)
        {
            var target = stage.points[Mathf.Min(_point, stage.points.Length - 1)];
            if (target != null)
            {
                Vector3 p = car.transform.position;
                float d = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(target.position.x, target.position.z));
                Rect dist = new Rect(Screen.width * 0.5f - 120f, 24f, 240f, 52f);
                DrawPanel(dist, new Color(0.04f, 0.05f, 0.07f, 0.62f));

                string distText;
                if (stage.bayPointIndex >= 0 && _point == stage.bayPointIndex)
                {
                    float m = _bay.minMargin;
                    distText = m >= 0f
                        ? "离库边  " + m.ToString("0.00") + " m"
                        : "已越界  " + Mathf.Abs(m).ToString("0.00") + " m";
                }
                else
                {
                    bool needStop = _point >= stage.points.Length - 1 && stage.requireStopAtEnd;
                    distText = (needStop ? "停车点  " : "目标  ") + Mathf.RoundToInt(d) + " m";
                }

                GUI.Label(new Rect(dist.x + 12f, dist.y + 8f, 220f, 34f), distText, _body);
            }
        }

        // ---- 库位状态灯：倒车入库 / 侧方停车专用 ----
        // 四条规则逐条亮灯，绿的表示已满足。玩家靠它知道差在哪一条，而不是到点了莫名其妙不过。
        if (stage.bayPointIndex >= 0 && _point == stage.bayPointIndex)
        {
            Rect bay = new Rect(card.x, card.y + cardH + 10f, cardW, 44f);
            DrawPanel(bay, new Color(0.04f, 0.05f, 0.07f, 0.72f));

            var okStyle = new GUIStyle(_small) { fontSize = 17 };
            okStyle.normal.textColor = new Color(0.5f, 1f, 0.55f);
            var badStyle = new GUIStyle(_small) { fontSize = 17 };
            badStyle.normal.textColor = new Color(1f, 0.55f, 0.45f);

            float x = bay.x + 14f;
            float y = bay.y + 10f;

            string[] labels = new string[]
            {
                _bay.cornersInside ? "四角在线内 ✓" : "四角溢出 ✗",
                _bay.aligned ? "车身摆正 ✓" : ("车身斜 " + Mathf.RoundToInt(_bay.alignErrorDeg) + "° ✗"),
                _bay.stopped ? "已停稳 ✓" : "还在动 ✗",
                _bay.reversed ? "倒挡 ✓" : "未挂倒挡 ✗",
            };
            bool[] state = new bool[]
            {
                _bay.cornersInside, _bay.aligned, _bay.stopped, _bay.reversed
            };

            for (int i = 0; i < labels.Length; i++)
            {
                GUI.Label(new Rect(x, y, 130f, 24f), labels[i], state[i] ? okStyle : badStyle);
                x += 142f;
            }
        }

        // ---- 右上仪表（速度 + 档位 + 手刹）----
        // 科目二接管后第一关的 HUD 让位了，这段仪表盘得在这里补上，否则档位显示会消失。
        if (car != null)
        {
            Rect dash = new Rect(Screen.width - 250f, 24f, 226f, 122f);
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

            GUI.Label(new Rect(dash.x + 16f, dash.y + 76f, 200f, 24f),
                "用时 " + FormatTime(ElapsedSeconds), _small);
        }

        if (Time.time < _toastUntil && !string.IsNullOrEmpty(_toast))
        {
            var s = new GUIStyle(_body) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(0f, Screen.height * 0.72f, Screen.width, 40f), _toast, s);
        }
    }

    /// <summary>
    /// 结束成绩单：跑完一圈后常驻显示，替代原来的任务卡。
    /// 场景到这里就算走完了 —— 车已冻结，本关没有重来入口。
    /// </summary>
    void DrawFinishScreen()
    {
        DrawPanel(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0.02f, 0.03f, 0.04f, 0.70f));

        float w = Mathf.Min(680f, Screen.width - 56f);
        float h = 300f;
        Rect card = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.42f, w, h);
        DrawPanel(card, new Color(0.06f, 0.08f, 0.11f, 0.96f));

        var title = new GUIStyle(_big) { fontSize = 34, alignment = TextAnchor.MiddleCenter };
        GUI.Label(new Rect(card.x + 20f, card.y + 18f, card.width - 40f, 48f), "科目二完成 · 跑完一圈", title);

        var line = new GUIStyle(_body) { fontSize = 21, alignment = TextAnchor.MiddleCenter };
        float y = card.y + 84f;
        GUI.Label(new Rect(card.x + 20f, y, card.width - 40f, 30f), "用时  " + FormatTime(_elapsedSeconds), line); y += 36f;
        GUI.Label(new Rect(card.x + 20f, y, card.width - 40f, 30f), "碰倒锥桶  " + _conesHit + " 个", line); y += 48f;

        var end = new GUIStyle(_small) { fontSize = 16, alignment = TextAnchor.MiddleCenter };
        end.normal.textColor = new Color(0.72f, 0.76f, 0.82f);
        GUI.Label(new Rect(card.x + 20f, y, card.width - 40f, 26f), "本关到此结束", end);
    }

    static void DrawPanel(Rect r, Color c)
    {
        var old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = old;
    }
}
