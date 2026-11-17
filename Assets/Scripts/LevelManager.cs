using UnityEngine;

/// <summary>
/// 测试用选关管理器：让测试时不用从头跑，直接跳到想测的关卡。
///
/// 按键：
///   数字 0         → 第一关（教学 A→B）
///   数字 1..5      → 科目二对应科目（1倒车入库 2侧方 3坡道 4直角 5曲线）
///   退格/复位键     → 回到当前科目起点（已由 Km2RouteDirector 处理）
///   按住 ` 键(反引号) → 显示/隐藏选关菜单
///
/// 纯测试工具，正式打包时可在场景里禁用本组件（或由构建脚本剔除）。
/// </summary>
public class LevelManager : MonoBehaviour
{
    [Header("依赖")]
    public TutorialDirector tutorial;
    public Km2RouteDirector exam;

    [Tooltip("显示选关菜单的按键（反引号，键盘左上角 ~ 键）")]
    public KeyCode menuKey = KeyCode.BackQuote;

    [Tooltip("是否启用数字键快捷跳关。")]
    public bool enableQuickSelect = true;

    bool _menuVisible;

    void OnEnable()
    {
        Resolve();
    }

    void Resolve()
    {
        if (tutorial == null) tutorial = FindObjectOfType<TutorialDirector>();
        if (exam == null) exam = FindObjectOfType<Km2RouteDirector>();
    }

    void Update()
    {
        if (Input.GetKeyDown(menuKey))
        {
            _menuVisible = !_menuVisible;
        }

        if (!enableQuickSelect) return;

        Resolve();

        // 数字键 1..5 直接跳科目二对应科目
        if (Input.GetKeyDown(KeyCode.Alpha1)) Jump(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) Jump(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) Jump(2);
        if (Input.GetKeyDown(KeyCode.Alpha4)) Jump(3);
        if (Input.GetKeyDown(KeyCode.Alpha5)) Jump(4);

        // 数字键 0 回第一关（教学）
        if (Input.GetKeyDown(KeyCode.Alpha0)) JumpToTutorial();
    }

    /// <summary>跳到科目二第 stageIndex 个科目（0 起）。</summary>
    void Jump(int stageIndex)
    {
        if (exam == null) { Resolve(); if (exam == null) return; }
        exam.JumpToStage(stageIndex);
        _menuVisible = false;
    }

    void JumpToTutorial()
    {
        if (tutorial == null) { Resolve(); if (tutorial == null) return; }
        if (exam != null && exam.IsActive) exam.FinishExamForMenu();
        tutorial.RestartFromMenu();
        _menuVisible = false;
    }

    // ---------------- 菜单 HUD ----------------

    GUIStyle _title, _row, _key;

    void EnsureStyles()
    {
        if (_title != null) return;
        _title = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold };
        _title.normal.textColor = new Color(1f, 0.93f, 0.82f);
        _row = new GUIStyle(GUI.skin.label) { fontSize = 18 };
        _row.normal.textColor = Color.white;
        _key = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
        _key.normal.textColor = new Color(0.55f, 1f, 0.55f);
    }

    void OnGUI()
    {
        if (!_menuVisible) return;
        EnsureStyles();

        float w = 360f, h = 300f;
        Rect panel = new Rect(24f, 24f, w, h);
        var old = GUI.color;
        GUI.color = new Color(0.03f, 0.04f, 0.06f, 0.90f);
        GUI.DrawTexture(panel, Texture2D.whiteTexture);
        GUI.color = old;

        float x = panel.x + 18f, y = panel.y + 14f;
        GUI.Label(new Rect(x, y, w - 36f, 30f), "选关（测试用）", _title);
        y += 40f;

        GUI.Label(new Rect(x, y, w - 36f, 22f), "[0]  第一关 · 教学（A→B）", _key); y += 26f;
        GUI.Label(new Rect(x, y, w - 36f, 22f), "[1]  倒车入库", _key); y += 26f;
        GUI.Label(new Rect(x, y, w - 36f, 22f), "[2]  侧方停车", _key); y += 26f;
        GUI.Label(new Rect(x, y, w - 36f, 22f), "[3]  坡道定点停车和起步", _key); y += 26f;
        GUI.Label(new Rect(x, y, w - 36f, 22f), "[4]  直角转弯", _key); y += 26f;
        GUI.Label(new Rect(x, y, w - 36f, 22f), "[5]  曲线行驶", _key); y += 26f;

        y += 8f;
        GUI.Label(new Rect(x, y, w - 36f, 22f), "按 ` 关闭菜单", _row);
    }
}
