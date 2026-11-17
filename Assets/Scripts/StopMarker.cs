using UnityEngine;

/// <summary>
/// 停车提示浮动箭头：金色向下箭头，在停车点上空上下浮动。
/// 配合 <see cref="Billboard"/> 让箭头始终面向相机，无论从哪个角度看都醒目。
/// </summary>
public class StopMarker : MonoBehaviour
{
    [Tooltip("上下浮动幅度（米）")]
    public float amplitude = 0.4f;

    [Tooltip("浮动速度（弧度/秒）")]
    public float speed = 1.8f;

    [Tooltip("可选：初始相位偏移，让多个箭头错开浮动节奏。")]
    public float phaseOffset = 0f;

    [Tooltip("这个箭头属于第几个科目（1=倒车入库 2=侧方 3=坡道 4=直角 5=曲线）。0 = 常驻。")]
    public int stageIndex = 0;

    Vector3 _base;
    Renderer _renderer;
    bool _baseSet;

    void OnEnable()
    {
        _renderer = GetComponent<Renderer>();
        if (!_baseSet)
        {
            _base = transform.position;
            _baseSet = true;
        }
    }

    void Update()
    {
        Vector3 p = _base;
        p.y += Mathf.Sin(Time.time * speed + phaseOffset) * amplitude;
        transform.position = p;
    }

    /// <summary>重新锚定基准位置（场景里移动箭头后调用，或运行时动态摆放）。</summary>
    public void Rebase()
    {
        _base = transform.position;
        _baseSet = true;
    }

    /// <summary>按当前科目设置显隐。传 0 表示常驻显示。</summary>
    public void SetForStage(int currentStage)
    {
        bool show = stageIndex == 0 || stageIndex == currentStage;
        SetVisible(show);
    }

    void SetVisible(bool v)
    {
        if (_renderer != null) _renderer.enabled = v;
        // TextMesh 的显示也走 Renderer，所以这里就够了
        gameObject.SetActive(v);
    }
}
