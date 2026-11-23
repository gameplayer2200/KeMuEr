using UnityEngine;

/// <summary>
/// 第一关 · 按键教学。
///
/// 不再考"从 A 点开到 B 点"，而是把摇杆和四个按键逐个讲一遍：
/// 挂挡 → 手刹 → 油门 → 方向 → 切视角 → 倒挡。
/// 每一步等玩家真的做出这个动作才进入下一步，只提示、不判罚。
/// 讲完自动把车交给科目二。
/// </summary>
public class TutorialDirector : MonoBehaviour
{
    /// <summary>教学跑完时触发。科目二流程订阅它，把车 TP 到考试路线起点。</summary>
    public static event System.Action Completed;

    public CarController car;
    public Transform startPoint;

    /// <summary>教学是否已经跑完。</summary>
    public bool IsComplete { get { return _done; } }

    struct LessonStep
    {
        public readonly string title;
        public readonly string text;
        public LessonStep(string title, string text)
        {
            this.title = title;
            this.text = text;
        }
    }

    // 场景里没有任何路点数据：整套课程写死在代码里，改文案只改这里。
    static readonly LessonStep[] Steps =
    {
        new LessonStep("挂 1 档 · 绿键",
            "按手柄绿色按键（键 3）或键盘 1 = 挂前进挡。\n右上角仪表会从「N 空挡」变成「1 前进挡」。"),
        new LessonStep("松手刹 · 黄键",
            "上车时手刹是拉着的（右上角仪表显示「手刹」）。\n按黄键松开它，灯灭了车才会动。"),
        new LessonStep("油门 · 摇杆前推",
            "把摇杆向前推（键盘 W）= 给油。\n推多少给多少，起步轻推就行；往回拉是刹车（键盘 S）。"),
        new LessonStep("方向 · 摇杆左右",
            "摇杆向左/向右推（键盘 A / D）= 打方向。\n松手后车轮自动回正，方向盘不用自己回。"),
        new LessonStep("切视角 · 蓝键",
            "按蓝色按键（键 0）在「跟车视角 / 俯视视角」之间切换，键盘 Q 也行。"),
        new LessonStep("倒挡 · 红键",
            "先停稳，再按红色按键（键 9）或键盘 R = 挂 R 挡倒车。\n车速快的时候挂不进倒挡。"),
    };

    CarInput _input;
    int _step;
    float _hold;
    bool _done;
    int _conesHit;
    string _toast = "";
    float _toastUntil;
    float _lastConeVoice = -10f;

    // 玩家是否真的按过对应按键（用来区分"初始状态"和"做出动作"）
    bool _sawDrive;
    bool _sawReverse;
    bool _sawCamera;
    bool _handbrakeSeen;

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
        switch (action)
        {
            case CarAction.Drive: _sawDrive = true; break;
            case CarAction.Reverse: _sawReverse = true; break;
            case CarAction.Camera: _sawCamera = true; break;
        }
    }

    void Start()
    {
        _step = 0;
        ShowToast("跟着左上角的提示，把每个按键都按一遍");
        AudioDirector.Say(VoiceLines.TutIntro);
        AudioDirector.Say(VoiceLines.TutorialStepKey(0));
    }

    void OnConeKnocked(ConeKnock cone)
    {
        _conesHit++;
        ShowToast("碰倒了 1 个锥桶（教学关不扣分，正式考试扣 10 分）");
        if (Time.time - _lastConeVoice > 2f)
        {
            _lastConeVoice = Time.time;
            AudioDirector.Say(VoiceLines.TutCone);
        }
    }

    void Update()
    {
        if (car == null) return;
        if (_input == null) ResolveInput();
        if (_done) return;

        switch (_step)
        {
            case 0: // 挂 1 档
                if (_sawDrive && car.Gear == DriveGear.Drive) Next();
                break;

            case 1: // 手刹：先按下去锁住，再按一次松开
                if (_input != null)
                {
                    if (_input.Handbrake) _handbrakeSeen = true;
                    else if (_handbrakeSeen) Next();
                }
                break;

            case 2: // 起步
                if (car.SpeedKmh > 3f) Next();
                break;

            case 3: // 方向
                if (_input != null)
                {
                    if (Mathf.Abs(_input.Steer) > 0.55f)
                    {
                        _hold += Time.deltaTime;
                        if (_hold > 0.2f) Next();
                    }
                    else
                    {
                        _hold = 0f;
                    }
                }
                break;

            case 4: // 切视角
                if (_sawCamera) Next();
                break;

            case 5: // 倒挡
                if (_sawReverse && car.Gear == DriveGear.Reverse) Next();
                break;
        }
    }

    void Next()
    {
        _hold = 0f;
        _step++;
        Debug.Log(string.Format("[TUT] 教学进度 {0}/{1} carPos={2} speed={3:F2}",
            _step, Steps.Length, car != null ? car.transform.position.ToString() : "-",
            car != null ? car.SpeedKmh : 0f));

        if (_step >= Steps.Length)
        {
            _done = true;
            ShowToast("教学完成！按键都认识了，接下来跑一圈：1 倒车入库");
            AudioDirector.Say(VoiceLines.TutDone);
            if (Completed != null) Completed();
        }
        else
        {
            ShowToast("✓ 这一步过了：" + Steps[_step - 1].title);
            AudioDirector.Say(VoiceLines.TutorialStepKey(_step));
        }
    }

    void Restart()
    {
        _step = 0;
        _hold = 0f;
        _conesHit = 0;
        _done = false;
        _sawDrive = false;
        _sawReverse = false;
        _sawCamera = false;
        _handbrakeSeen = false;
        if (startPoint != null && car != null)
            car.ResetToPose(startPoint.position, startPoint.rotation);
        ShowToast("已回到起点，按键教学从头开始");
        AudioDirector.SayNow(VoiceLines.TutIntro);
    }

    /// <summary>选关菜单跳回第一关时调用：重置教学流程并把车放回起点。</summary>
    public void RestartFromMenu()
    {
        Restart();
    }

    /// <summary>给外部（比如跑出跑道回退）用的短提示。</summary>
    public void Toast(string message)
    {
        ShowToast(message);
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

    /// <summary>科目二接管后，第一关的 HUD 就不再画，避免两块卡片叠在一起。</summary>
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
        float cardW = Mathf.Min(660f, Screen.width - 48f);
        float cardH = 148f;
        Rect card = new Rect(24f, 24f, cardW, cardH);
        DrawPanel(card, new Color(0.04f, 0.05f, 0.07f, 0.80f));

        string header;
        string body;

        if (_done)
        {
            header = "按键教学 · 完成";
            body = "摇杆和四个按键都认识了。\n直接上场考试吧。";
        }
        else
        {
            var step = Steps[Mathf.Min(_step, Steps.Length - 1)];
            header = string.Format("按键教学  {0}/{1}  ·  {2}", _step + 1, Steps.Length, step.title);
            body = step.text;
        }

        GUI.Label(new Rect(card.x + 18f, card.y + 10f, card.width - 36f, 32f), header, _titleStyle);
        GUI.Label(new Rect(card.x + 18f, card.y + 48f, card.width - 36f, 92f), body, _bodyStyle);

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
            Rect dash = new Rect(Screen.width - 250f, 24f, 226f, 118f);
            DrawPanel(dash, new Color(0.04f, 0.05f, 0.07f, 0.75f));

            var speedStyle = new GUIStyle(_titleStyle);
            speedStyle.fontSize = 30;
            speedStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(dash.x + 16f, dash.y + 6f, 200f, 36f),
                Mathf.RoundToInt(car.SpeedKmh) + " km/h", speedStyle);

            string gear = car.Gear == DriveGear.Reverse ? "R 倒挡"
                        : car.Gear == DriveGear.Neutral ? "N 空挡" : "1 前进挡";
            GUI.Label(new Rect(dash.x + 16f, dash.y + 46f, 200f, 24f),
                gear + (_input != null && _input.Handbrake ? "   手刹" : ""), _smallStyle);
            if (_input != null)
                GUI.Label(new Rect(dash.x + 16f, dash.y + 76f, 210f, 22f), _input.CurrentProfileName, _smallStyle);
        }

        // ---- 步骤进度条 ----
        if (!_done)
        {
            Rect bar = new Rect(Screen.width * 0.5f - 160f, 24f, 320f, 40f);
            DrawPanel(bar, new Color(0.04f, 0.05f, 0.07f, 0.62f));
            var s = new GUIStyle(_smallStyle) { alignment = TextAnchor.MiddleCenter };
            GUI.Label(bar, string.Format("第 {0} / {1} 步", _step + 1, Steps.Length), s);
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
            GUI.Label(new Rect(0f, Screen.height * 0.30f, Screen.width, 60f), "按键教学完成", s);
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
