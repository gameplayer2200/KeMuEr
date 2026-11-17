using UnityEngine;

/// <summary>教学路点：开到哪儿、要不要停稳。</summary>
[System.Serializable]
public class TutorialWaypoint
{
    public Vector3 position;
    public float radius = 6f;
    public bool requireStop;
    public string title = "行驶";
    [TextArea] public string instruction = "";
}

/// <summary>
/// 第一关 · 教学。A 点出发，沿主路直行 → 路口右转 → 到达 B 点停稳，第一关结束。
/// 只提示、不判罚。
/// </summary>
public class TutorialDirector : MonoBehaviour
{
    /// <summary>第一关跑完时触发。科目二流程订阅它，把车 TP 到考试路线起点。</summary>
    public static event System.Action Completed;

    public CarController car;
    public Transform startPoint;
    public TutorialWaypoint[] waypoints = new TutorialWaypoint[0];

    /// <summary>第一关是否已经跑完。</summary>
    public bool IsComplete { get { return _done; } }

    [Tooltip("已废弃：回起点改由 CarInput 统一处理。保留字段只为兼容旧场景。")]
    public KeyCode resetKey = KeyCode.Backspace;

    CarInput _input;
    int _stage;
    float _holdTimer;
    int _conesHit;
    bool _done;
    string _toast = "";
    float _toastUntil;

    void OnEnable()
    {
        ConeKnock.Knocked += OnConeKnocked;
        ResolveInput();
    }

    void OnDisable()
    {
        ConeKnock.Knocked -= OnConeKnocked;
        if (_input != null) _input.OnAction -= OnCarAction;
        _input = null;
    }

    void ResolveInput()
    {
        if (_input == null && car != null) _input = car.GetComponent<CarInput>();
        if (_input == null) _input = FindObjectOfType<CarInput>();
        if (_input != null)
        {
            _input.OnAction -= OnCarAction;   // 防重复订阅
            _input.OnAction += OnCarAction;
        }
    }

    void OnCarAction(CarAction action)
    {
        if (action == CarAction.Reset) Restart();
    }

    void OnConeKnocked(ConeKnock cone)
    {
        _conesHit++;
        ShowToast("碰倒了 1 个锥桶（教学关不扣分，正式考试扣 10 分）");
    }

    void Update()
    {
        if (car == null) return;
        if (_input == null) ResolveInput();

        // 编辑器调试：F8 直接把车挪到当前路点（打包后不存在）
#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.F8))
        {
            var dbgWp = waypoints[Mathf.Min(_stage, waypoints.Length - 1)];
            car.ResetToPose(new Vector3(dbgWp.position.x, 0f, dbgWp.position.z), Quaternion.Euler(0f, car.transform.eulerAngles.y, 0f));
        }
#endif
        if (_done || waypoints == null || waypoints.Length == 0) return;

        var wp = waypoints[Mathf.Min(_stage, waypoints.Length - 1)];
        Vector3 p = car.transform.position;
        float d = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(wp.position.x, wp.position.z));

        bool arrived = d < wp.radius;
        bool ok = arrived && (!wp.requireStop || car.SpeedKmh < 1.5f);

        if (ok)
        {
            if (Hold(true, wp.requireStop ? 0.6f : 0.15f)) Advance();
        }
        else
        {
            Hold(false, 0f);
        }
    }

    bool Hold(bool condition, float seconds)
    {
        if (condition) _holdTimer += Time.deltaTime;
        else _holdTimer = 0f;
        return _holdTimer >= seconds;
    }

    void Advance()
    {
        _holdTimer = 0f;
        _stage++;
        Debug.Log(string.Format("[TUT] stage->{0} carPos={1} speed={2:F2}", _stage, car.transform.position, car.SpeedKmh));

        if (_stage >= waypoints.Length)
        {
            _done = true;
            ShowToast("第一关完成！你已经学会起步、直行、转向和停车了。");
            if (Completed != null) Completed();
        }
    }

    void Restart()
    {
        _stage = 0;
        _holdTimer = 0f;
        _conesHit = 0;
        _done = false;
        if (startPoint != null && car != null)
            car.ResetToPose(startPoint.position, startPoint.rotation);
        ShowToast("已回到 A 点");
    }

    /// <summary>选关菜单跳回第一关时调用：重置教学流程并把车放回起点。</summary>
    public void RestartFromMenu()
    {
        Restart();
    }

    void ShowToast(string message)
    {
        _toast = message;
        _toastUntil = Time.time + 2.5f;
    }

    // ---------------- HUD ----------------

    GUIStyle _titleStyle;
    GUIStyle _bodyStyle;
    GUIStyle _smallStyle;
    GUIStyle _bigStyle;

    void EnsureStyles()
    {
        if (_titleStyle != null) return;

        _titleStyle = new GUIStyle(GUI.skin.label);
        _titleStyle.fontSize = 24;
        _titleStyle.fontStyle = FontStyle.Bold;
        _titleStyle.normal.textColor = new Color(1f, 0.93f, 0.82f);

        _bodyStyle = new GUIStyle(GUI.skin.label);
        _bodyStyle.fontSize = 18;
        _bodyStyle.wordWrap = true;
        _bodyStyle.normal.textColor = Color.white;

        _smallStyle = new GUIStyle(GUI.skin.label);
        _smallStyle.fontSize = 15;
        _smallStyle.normal.textColor = new Color(0.85f, 0.88f, 0.92f);

        _bigStyle = new GUIStyle(GUI.skin.label);
        _bigStyle.fontSize = 30;
        _bigStyle.fontStyle = FontStyle.Bold;
        _bigStyle.normal.textColor = new Color(0.55f, 1f, 0.55f);
    }

    /// <summary>第一关跑完、科目二接管后，第一关的 HUD 就不再画，避免两块卡片叠在一起。</summary>
    bool HandOverToExam()
    {
        return Km2RouteDirector.ExamActive;
    }

    void OnGUI()
    {
        EnsureStyles();

        // 科目二接管后，第一关的 HUD 全部让位，避免两块面板重叠
        if (HandOverToExam()) return;

        // ---- 顶部任务卡 ----
        float cardW = Mathf.Min(620f, Screen.width - 48f);
        float cardH = 132f;
        Rect card = new Rect(24f, 24f, cardW, cardH);
        DrawPanel(card, new Color(0.04f, 0.05f, 0.07f, 0.80f));

        string header;
        string body;

        if (_done)
        {
            header = "第一关 · 完成";
            body = "恭喜通过第一关。按 退格 再开一遍，或者去挑战科目二场地考试。";
        }
        else if (waypoints != null && waypoints.Length > 0)
        {
            var wp = waypoints[Mathf.Min(_stage, waypoints.Length - 1)];
            header = string.Format("第一关 · 从 A 到 B   {0}/{1}   {2}", _stage + 1, waypoints.Length, wp.title);
            body = wp.instruction;
        }
        else
        {
            header = "第一关";
            body = "场景尚未配置路点。";
        }

        GUI.Label(new Rect(card.x + 18f, card.y + 10f, card.width - 36f, 32f), header, _titleStyle);
        GUI.Label(new Rect(card.x + 18f, card.y + 48f, card.width - 36f, 72f), body, _bodyStyle);

        // ---- 左下操作说明 ----
        float helpH = 172f;
        float helpY = Screen.height - helpH - 24f;
        if (helpY < card.y + cardH + 12f) helpY = Mathf.Max(0f, Screen.height - helpH);
        Rect help = new Rect(24f, helpY, Mathf.Min(494f, Screen.width - 48f), helpH);
        DrawPanel(help, new Color(0.04f, 0.05f, 0.07f, 0.72f));
        GUI.Label(new Rect(help.x + 16f, help.y + 8f, 440f, 26f), "操作", _titleStyle);
        var helpStyle = new GUIStyle(_smallStyle) { fontSize = 12 };
        GUI.Label(new Rect(help.x + 16f, help.y + 36f, help.width - 24f, 132f),
            CarInputMap.HelpLines(_input), helpStyle);

        // ---- 右上仪表 ----
        if (car != null)
        {
            Rect dash = new Rect(Screen.width - 250f, 24f, 226f, 96f);
            DrawPanel(dash, new Color(0.04f, 0.05f, 0.07f, 0.75f));

            var speedStyle = new GUIStyle(_titleStyle);
            speedStyle.fontSize = 30;
            speedStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(dash.x + 16f, dash.y + 6f, 200f, 36f),
                Mathf.RoundToInt(car.SpeedKmh) + " km/h", speedStyle);

            string gear = car.Gear == DriveGear.Reverse ? "R 倒挡"
                        : car.Gear == DriveGear.Neutral ? "N 空挡" : "1 前进挡";
            GUI.Label(new Rect(dash.x + 16f, dash.y + 46f, 200f, 24f),
                gear + (car.GetComponent<CarInput>() != null && car.GetComponent<CarInput>().Handbrake ? "   手刹" : ""), _smallStyle);
        }

        // ---- 目标距离 ----
        if (!_done && car != null && waypoints != null && waypoints.Length > 0)
        {
            var wp = waypoints[Mathf.Min(_stage, waypoints.Length - 1)];
            Vector3 p = car.transform.position;
            float d = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(wp.position.x, wp.position.z));

            Rect dist = new Rect(Screen.width * 0.5f - 110f, 24f, 220f, 52f);
            DrawPanel(dist, new Color(0.04f, 0.05f, 0.07f, 0.62f));
            GUI.Label(new Rect(dist.x + 12f, dist.y + 8f, 200f, 34f),
                (wp.requireStop ? "目标 B 点  " : "目标  ") + Mathf.RoundToInt(d) + " m", _bodyStyle);
        }

        // ---- Toast ----
        if (Time.time < _toastUntil && !string.IsNullOrEmpty(_toast))
        {
            var s = new GUIStyle(_bodyStyle);
            s.fontSize = 20;
            s.alignment = TextAnchor.MiddleCenter;
            GUI.Label(new Rect(0f, Screen.height * 0.72f, Screen.width, 40f), _toast, s);
        }

        if (_done)
        {
            var s = new GUIStyle(_bigStyle);
            s.alignment = TextAnchor.MiddleCenter;
            GUI.Label(new Rect(0f, Screen.height * 0.30f, Screen.width, 60f), "第一关完成", s);
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
