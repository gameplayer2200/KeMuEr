using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 沿现有路网铺装饰：道路两侧自动撒楼房、行道树、灌木。
/// 做法：先用射线把整张地图扫成「路面/非路面」位图，再算距离变换得到每格离路面的距离，
/// 建筑放在 11~30m 的环带里、行道树放在 6.5~10.5m，保证「沿着路」而不是随机撒。
/// 只加不删原有内容；重复执行会先清掉上一次生成的 Roadside。
/// 菜单：科目二/装饰/沿途铺建筑与行道树
/// </summary>
public static class RoadsideDecorator
{
    const string PackRoot = "Assets/Models/Pandazole_Ultimate_Pack/Pandazole City Town Pack/Prefabs/";

    static readonly string[] BuildingNames =
    {
        "Env_CommercialBuilding_01", "Env_CommercialBuilding_02",
        "Env_CommercialBuilding_03", "Env_CommercialBuilding_04",
        "Env_CompanyBuilding_01", "Env_CompanyBuilding_02",
        "Env_CompanyBuilding_03", "Env_CompanyBuilding_04",
        "Env_Motel_01", "Env_Motel_02", "Env_Motel_03",
        "Env_Motel_04", "Env_Motel_05",
        "Env_ResidentBuilding_01", "Env_ResidentBuilding_02",
        "Env_ResidentBuilding_03", "Env_ResidentBuilding_04",
        "Env_ResidentBuilding_05", "Env_ResidentBuilding_06"
    };

    static readonly string[] TreeNames =
    {
        "Prop_Tree_01", "Prop_Tree_02", "Prop_Tree_03", "Prop_Tree_04",
        "Prop_Tree_05", "Prop_Tree_06", "Prop_Tree_07", "Prop_Tree_08"
    };

    static readonly string[] BushNames = { "Prop_Bush_01", "Prop_Bush_02", "Prop_Plant_01", "Prop_Plant_02", "Prop_Plant_03" };

    const float MinX = -138f, MaxX = 138f, MinZ = -152f, MaxZ = 56f;
    const float Cell = 2f;
    const float BuildingNear = 11f, BuildingFar = 30f;
    const float TreeNear = 6.5f, TreeFar = 10.5f;
    const int MaxBuildings = 150, MaxTrees = 170;

    [MenuItem("科目二/装饰/沿途铺建筑与行道树", false, 20)]
    public static void Decorate()
    {
        var road = GameObject.Find("Road");
        if (road == null) { Debug.LogError("[装饰] 场景里找不到 Road"); return; }

        // ---- 1) 扫路面位图 ----
        int nx = Mathf.CeilToInt((MaxX - MinX) / Cell) + 1;
        int nz = Mathf.CeilToInt((MaxZ - MinZ) / Cell) + 1;
        bool[] isRoad = new bool[nx * nz];
        int roadCells = 0;
        for (int j = 0; j < nz; j++)
        {
            for (int i = 0; i < nx; i++)
            {
                float x = MinX + i * Cell, z = MinZ + j * Cell;
                RaycastHit hit;
                bool r = Physics.Raycast(new Vector3(x, 80f, z), Vector3.down, out hit, 300f, 1 << 9, QueryTriggerInteraction.Ignore)
                         && hit.collider.transform.root.name == "Road";
                if (r) { isRoad[j * nx + i] = true; roadCells++; }
            }
        }
        if (roadCells == 0) { Debug.LogError("[装饰] 扫不到路面，检查路面层是否为 Ramp(9)"); return; }

        // ---- 2) 距离变换（近似欧氏距离，单位：格）----
        float[] dist = new float[nx * nz];
        float inf = 1e9f;
        for (int k = 0; k < dist.Length; k++) dist[k] = isRoad[k] ? 0f : inf;
        const float S = 1.41421356f;
        for (int j = 0; j < nz; j++)
            for (int i = 0; i < nx; i++)
            {
                int k = j * nx + i;
                if (dist[k] <= 0f) continue;
                float d = dist[k];
                if (i > 0) d = Mathf.Min(d, dist[k - 1] + 1f);
                if (j > 0)
                {
                    d = Mathf.Min(d, dist[k - nx] + 1f);
                    if (i > 0) d = Mathf.Min(d, dist[k - nx - 1] + S);
                    if (i < nx - 1) d = Mathf.Min(d, dist[k - nx + 1] + S);
                }
                dist[k] = d;
            }
        for (int j = nz - 1; j >= 0; j--)
            for (int i = nx - 1; i >= 0; i--)
            {
                int k = j * nx + i;
                if (dist[k] <= 0f) continue;
                float d = dist[k];
                if (i < nx - 1) d = Mathf.Min(d, dist[k + 1] + 1f);
                if (j < nz - 1)
                {
                    d = Mathf.Min(d, dist[k + nx] + 1f);
                    if (i < nx - 1) d = Mathf.Min(d, dist[k + nx + 1] + S);
                    if (i > 0) d = Mathf.Min(d, dist[k + nx - 1] + S);
                }
                dist[k] = d;
            }
        for (int k = 0; k < dist.Length; k++) if (dist[k] < inf) dist[k] *= Cell;

        // ---- 3) 准备父物体 ----
        var level = GameObject.Find("L1_Tutorial");
        Transform parent = level != null ? level.transform : null;
        var old = parent != null ? parent.Find("Roadside") : null;
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var rootGo = new GameObject("Roadside");
        rootGo.transform.SetParent(parent, false);
        Undo.RegisterCreatedObjectUndo(rootGo, "铺装饰建筑");
        var root = rootGo.transform;

        var treeSpots = new List<Vector2>();
        foreach (var t in Object.FindObjectsOfType<Transform>())
            if (t.name.ToLowerInvariant().Contains("tree") && t.parent != parent)
                treeSpots.Add(new Vector2(t.position.x, t.position.z));

        var rng = new System.Random(20261121);
        int buildings = 0, trees = 0, bushes = 0;
        int stride = 2;   // 4m 一采样

        // ---- 4) 楼房：11~30m 环带 ----
        for (int j = 0; j < nz && buildings < MaxBuildings; j += stride)
        {
            for (int i = 0; i < nx && buildings < MaxBuildings; i += stride)
            {
                float d = dist[j * nx + i];
                if (d < BuildingNear || d > BuildingFar) continue;
                if ((float)rng.NextDouble() > 0.5f) continue;

                float x = MinX + i * Cell + ((float)rng.NextDouble() - 0.5f) * 2.6f;
                float z = MinZ + j * Cell + ((float)rng.NextDouble() - 0.5f) * 2.6f;
                if (NearExistingTree(treeSpots, x, z)) continue;
                if (TryPlaceBuilding(root, rng, x, z)) buildings++;
            }
        }

        // ---- 5) 行道树：6.5~10.5m 窄带 ----
        for (int j = 0; j < nz && trees < MaxTrees; j += stride)
        {
            for (int i = 0; i < nx && trees < MaxTrees; i += stride)
            {
                float d = dist[j * nx + i];
                if (d < TreeNear || d > TreeFar) continue;
                if ((float)rng.NextDouble() > 0.42f) continue;

                float x = MinX + i * Cell + ((float)rng.NextDouble() - 0.5f) * 2.2f;
                float z = MinZ + j * Cell + ((float)rng.NextDouble() - 0.5f) * 2.2f;
                if (NearExistingTree(treeSpots, x, z)) continue;
                if (TryPlaceTree(root, rng, x, z)) trees++;
            }
        }

        // ---- 6) 灌木：贴着路缘 ----
        for (int j = 0; j < nz && bushes < 80; j += stride)
        {
            for (int i = 0; i < nx && bushes < 80; i += stride)
            {
                float d = dist[j * nx + i];
                if (d < 4.2f || d > 6.4f) continue;
                if ((float)rng.NextDouble() > 0.3f) continue;
                float x = MinX + i * Cell + ((float)rng.NextDouble() - 0.5f) * 1.6f;
                float z = MinZ + j * Cell + ((float)rng.NextDouble() - 0.5f) * 1.6f;
                if (NearExistingTree(treeSpots, x, z)) continue;
                if (TryPlaceBush(root, rng, x, z)) bushes++;
            }
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[装饰] 路面格 " + roadCells + " 个；楼房 " + buildings + " 幢 / 行道树 " + trees + " 棵 / 灌木 " + bushes + " 丛");
    }

    static bool NearExistingTree(List<Vector2> spots, float x, float z)
    {
        var p = new Vector2(x, z);
        for (int i = 0; i < spots.Count; i++)
            if ((spots[i] - p).sqrMagnitude < 36f) return true;
        return false;
    }

    // ---------------- 摆放 ----------------

    static bool TryPlaceBuilding(Transform parent, System.Random rng, float x, float z)
    {
        string name = BuildingNames[rng.Next(BuildingNames.Length)];
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PackRoot + name + ".prefab");
        if (prefab == null) return false;

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;

        Bounds b = GetBounds(go);
        float foot = Mathf.Max(b.size.x, b.size.z);
        float targetFoot = 9.5f + (float)rng.NextDouble() * 5f;
        float s = foot > 0.01f ? targetFoot / foot : 1f;
        float maxH = 8f + (float)rng.NextDouble() * 7f;
        if (b.size.y * s > maxH && b.size.y > 0.01f) s = maxH / b.size.y;
        go.transform.localScale = Vector3.one * s;

        go.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);

        b = GetBounds(go);
        go.transform.position = new Vector3(x, -b.min.y, z);

        // 占地检查：和已有建筑/树/锥桶重叠就放弃
        b = GetBounds(go);
        Vector3 half = new Vector3(Mathf.Max(0.6f, b.extents.x - 0.5f), 3f, Mathf.Max(0.6f, b.extents.z - 0.5f));
        if (Physics.CheckBox(new Vector3(b.center.x, 3.2f, b.center.z), half, Quaternion.identity, 1 << 8, QueryTriggerInteraction.Ignore))
        {
            Object.DestroyImmediate(go);
            return false;
        }

        AddBoxObstacle(go);
        go.name = "RoadsideBld_" + Mathf.RoundToInt(x) + "_" + Mathf.RoundToInt(z);
        return true;
    }

    static bool TryPlaceTree(Transform parent, System.Random rng, float x, float z)
    {
        string name = TreeNames[rng.Next(TreeNames.Length)];
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PackRoot + name + ".prefab");
        if (prefab == null) return false;

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;

        Bounds b = GetBounds(go);
        float height = 4.4f + (float)rng.NextDouble() * 2.4f;
        float s = b.size.y > 0.01f ? height / b.size.y : 1f;
        go.transform.localScale = Vector3.one * s;
        go.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);

        b = GetBounds(go);
        go.transform.position = new Vector3(x, -b.min.y, z);

        AddCapsuleObstacle(go, 0.35f);
        go.name = "RoadsideTree_" + Mathf.RoundToInt(x) + "_" + Mathf.RoundToInt(z);
        return true;
    }

    static bool TryPlaceBush(Transform parent, System.Random rng, float x, float z)
    {
        string name = BushNames[rng.Next(BushNames.Length)];
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PackRoot + name + ".prefab");
        if (prefab == null) return false;

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.transform.localPosition = Vector3.zero;
        Bounds b = GetBounds(go);
        float s = b.size.y > 0.01f ? (0.9f + (float)rng.NextDouble() * 0.6f) / b.size.y : 1f;
        go.transform.localScale = Vector3.one * s;
        go.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
        b = GetBounds(go);
        go.transform.position = new Vector3(x, -b.min.y, z);
        go.name = "RoadsideBush_" + Mathf.RoundToInt(x) + "_" + Mathf.RoundToInt(z);
        return true;
    }

    // ---------------- 组件 ----------------

    static void AddBoxObstacle(GameObject go)
    {
        int layer = LayerMask.NameToLayer("Obstacle");
        if (layer < 0) layer = 8;
        SetLayerRecursive(go, layer);

        Bounds b = GetBounds(go);
        var bc = go.GetComponent<BoxCollider>();
        if (bc == null) bc = go.AddComponent<BoxCollider>();
        Vector3 ls = go.transform.lossyScale;
        bc.center = go.transform.InverseTransformPoint(b.center);
        bc.size = new Vector3(
            b.size.x / Mathf.Max(0.0001f, Mathf.Abs(ls.x)),
            b.size.y / Mathf.Max(0.0001f, Mathf.Abs(ls.y)),
            b.size.z / Mathf.Max(0.0001f, Mathf.Abs(ls.z)));
    }

    static void AddCapsuleObstacle(GameObject go, float worldRadius)
    {
        int layer = LayerMask.NameToLayer("Obstacle");
        if (layer < 0) layer = 8;
        SetLayerRecursive(go, layer);

        Bounds b = GetBounds(go);
        Vector3 ls = go.transform.lossyScale;
        float sy = Mathf.Max(0.0001f, Mathf.Abs(ls.y));
        float sxz = Mathf.Max(0.0001f, Mathf.Max(Mathf.Abs(ls.x), Mathf.Abs(ls.z)));

        var cap = go.AddComponent<CapsuleCollider>();
        cap.direction = 1;
        cap.radius = worldRadius / sxz;
        cap.height = Mathf.Max(cap.radius * 2f + 0.01f, (b.size.y * 0.6f) / sy);
        cap.center = new Vector3(0f, (b.center.y - go.transform.position.y) / sy, 0f);
    }

    static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform t in go.transform) SetLayerRecursive(t.gameObject, layer);
    }

    static Bounds GetBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        return b;
    }
}
