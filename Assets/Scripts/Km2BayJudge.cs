using UnityEngine;

/// <summary>
/// 库位判定结果。侧方停车和倒车入库共用同一套规则：
/// 四角线内、车身摆正、已停稳、用过倒挡 —— 四条全中才算入位。
/// </summary>
public struct BayJudgeResult
{
    /// <summary>车身四个角都在库位矩形内（含边距容差）。</summary>
    public bool cornersInside;

    /// <summary>车身与库位轴线的夹角在容差内。</summary>
    public bool aligned;

    /// <summary>当前速度低于 stopSpeed。</summary>
    public bool stopped;

    /// <summary>本次入位过程中挂过倒挡。</summary>
    public bool reversed;

    /// <summary>四角到库位边界的最小余量（米）。负数表示已越界。</summary>
    public float minMargin;

    /// <summary>车身与库位轴线的夹角（度，0 = 完全平行）。</summary>
    public float alignErrorDeg;

    /// <summary>四条规则是否全部满足。</summary>
    public bool AllOk
    {
        get { return cornersInside && aligned && stopped && reversed; }
    }
}

/// <summary>
/// 库位几何判定：把车的车身矩形投影到库位的局部坐标系里，检查是否整体落在库内。
/// 纯函数，不改任何状态，HUD 每帧调也不会有副作用。
/// </summary>
public static class Km2BayJudge
{
    /// <summary>
    /// 判定一次。bayYawDeg 定义库位朝向：0 表示库长沿世界 Z 轴，
    /// baySize.x 是横向宽度（跨车道方向），baySize.y 是纵向长度（车头进出方向）。
    /// </summary>
    public static BayJudgeResult Evaluate(
        CarController car,
        Transform bayCenter,
        Vector2 baySize,
        float bayYawDeg,
        float alignToleranceDeg,
        float edgeTolerance,
        bool requireReverse,
        float stopSpeed,
        bool reverseSeen)
    {
        var result = new BayJudgeResult();
        result.reversed = !requireReverse || reverseSeen;

        if (car == null || bayCenter == null)
        {
            result.cornersInside = false;
            result.aligned = false;
            result.stopped = false;
            result.minMargin = -999f;
            result.alignErrorDeg = 180f;
            return result;
        }

        float halfW = car.bodyHalfWidth > 0.01f ? car.bodyHalfWidth : 0.82f;
        float halfL = car.bodyHalfLength > 0.01f ? car.bodyHalfLength : 2.2f;

        Vector3 center = car.transform.position;
        Quaternion carYaw = Quaternion.Euler(0f, car.transform.eulerAngles.y, 0f);

        // 库位坐标系：把世界点转到「库位局部」，局部 x 是横宽、z 是纵深
        Quaternion bayRot = Quaternion.Euler(0f, bayYawDeg, 0f);
        Quaternion bayInverse = Quaternion.Inverse(bayRot);
        Vector3 bayOrigin = bayCenter.position;

        float halfSizeX = Mathf.Abs(baySize.x) * 0.5f;
        float halfSizeZ = Mathf.Abs(baySize.y) * 0.5f;

        bool allInside = true;
        float margin = float.MaxValue;

        for (int sx = -1; sx <= 1; sx += 2)
        {
            for (int sz = -1; sz <= 1; sz += 2)
            {
                Vector3 outer = center + carYaw * new Vector3(sx * halfW, 0f, sz * halfL);
                Vector3 local = bayInverse * (outer - bayOrigin);

                float mx = halfSizeX - Mathf.Abs(local.x);
                float mz = halfSizeZ - Mathf.Abs(local.z);
                if (mx < margin) margin = mx;
                if (mz < margin) margin = mz;

                if (mx < -edgeTolerance || mz < -edgeTolerance) allInside = false;
            }
        }

        result.cornersInside = allInside;
        result.minMargin = margin;

        // 对齐：车身朝向与库轴平行即可，车头朝里朝外都算（侧方停车是倒进去的）
        Vector3 carForward = carYaw * Vector3.forward;
        Vector3 bayAxis = bayRot * Vector3.forward;
        float angle = Vector3.Angle(carForward, bayAxis);
        if (angle > 90f) angle = 180f - angle;   // 折到 0..90，不区分正反
        result.alignErrorDeg = angle;
        result.aligned = angle <= alignToleranceDeg;

        result.stopped = car.SpeedKmh / 3.6f <= stopSpeed;

        return result;
    }
}
