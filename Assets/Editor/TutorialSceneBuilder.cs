using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 一键生成第一关教学场景：简易城市路网（十字主路 + 街区）+ A 点到 B 点的教学路线。
/// 菜单：科目二 / 生成第一关教学场景
/// </summary>
public static class TutorialSceneBuilder
{
    public const string ScenePath = "Assets/Scenes/L1_Tutorial.unity";

    const string CarPrefabPath = "Assets/Models/Awbmecreations/Mobile Optimize-Free Low Poly Cars/Prefabs/Hatchback Car_15.prefab";
    const string ParkedCarPath = "Assets/Models/Awbmecreations/Mobile Optimize-Free Low Poly Cars/Prefabs/Classic Car_9.prefab";
    const string ConePrefabPath = "Assets/Models/Assets Storms/Cone/Prefabs/cone.prefab";
    const string PackRoot = "Assets/Models/Pandazole_Ultimate_Pack/Pandazole City Town Pack/Prefabs/";

    // 城市包道路是 10x10 的模块，路面居中。主路中心线 x = 0，右侧车道 x = LaneX。
    const float RoadTileX = 0f;
    const float LaneX = 1.6f;
    const float RoadY = 0f;           // 模块底面对齐 y=0
    const float RoadStep = 10f;

    // 路网范围
    const float MainZMin = -30f, MainZMax = 120f;
    const float CrossZ = 40f;
    const float CrossXMin = -60f, CrossXMax = 80f;

    // A / B
    static readonly Vector3 PointA = new Vector3(LaneX, 0f, -10f);
    static readonly Vector3 PointB = new Vector3(58f, 0f, CrossZ - LaneX);

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

    [MenuItem("科目二/生成第一关教学场景", false, 1)]
    public static void BuildFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Build();
    }

    public static bool Build()
    {
        EnsureFolder("Assets/Scenes");
        EnsureFolder("Assets/Materials");

        var carPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CarPrefabPath);
        var conePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ConePrefabPath);
        if (carPrefab == null)
        {
            Debug.LogError("[科目二] 找不到车模型：" + CarPrefabPath);
            return false;
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var rng = new System.Random(20261114);

        var grass = MakeMaterial("M_Grass", new Color(0.33f, 0.42f, 0.26f), 0.05f);
        var white = MakeMaterial("M_LineWhite", new Color(0.93f, 0.93f, 0.90f), 0.25f);
        var yellow = MakeMaterial("M_LineYellow", new Color(0.92f, 0.76f, 0.12f), 0.25f);
        var coneMat = MakeMaterial("M_Cone", new Color(0.86f, 0.24f, 0.08f), 0.22f);

        var root = new GameObject("L1_Tutorial").transform;

        // ---------------- 地面 ----------------
        var grassPlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        grassPlane.name = "Ground_Grass";
        grassPlane.transform.SetParent(root, false);
        grassPlane.transform.localPosition = new Vector3(10f, -0.05f, 40f);
        grassPlane.transform.localScale = new Vector3(40f, 1f, 40f);      // 400m
        grassPlane.GetComponent<Renderer>().sharedMaterial = grass;
        Object.DestroyImmediate(grassPlane.GetComponent<Collider>());

        // ---------------- 路网 ----------------
        var roads = new GameObject("Roads").transform;
        roads.SetParent(root, false);
        BuildRoads(roads);

        // ---------------- 街区 ----------------
        var town = new GameObject("Town").transform;
        town.SetParent(root, false);
        var blocks = new[]
        {
            new Rect(-42f, -26f, 33f, 58f),   // xMin, zMin, w, h  —— 西南
            new Rect(-42f, 46f, 33f, 60f),    // 西北
            new Rect(9f, -26f, 52f, 58f),     // 东南
            new Rect(9f, 46f, 52f, 60f)       // 东北
        };

        foreach (var block in blocks)
            FillBlock(town, rng, block);

        BuildTreesAlongRoad(town, rng);

        // ---------------- 道具 / 指示牌 ----------------
        var props = new GameObject("Props").transform;
        props.SetParent(root, false);
        BuildProps(props);

        // ---------------- 起点标记（A/B 点已废弃：第一关改成纯按键教学） ----------------
        var markers = new GameObject("Markers").transform;
        markers.SetParent(root, false);

        // ---------------- 锥桶（施工区装饰） ----------------
        if (conePrefab != null)
        {
            var cones = new GameObject("Cones").transform;
            cones.SetParent(root, false);

            for (int i = 0; i < 4; i++)
                PlaceCone(conePrefab, new Vector3(-8f, 0f, 30f + i * 2.2f), cones, coneMat);
        }
        else
        {
            Debug.LogWarning("[科目二] 没找到锥桶 prefab：" + ConePrefabPath);
        }

        // ---------------- 教练车 ----------------
        var car = BuildPlayerCar(carPrefab, root);

        if (AssetDatabase.LoadAssetAtPath<GameObject>(ParkedCarPath) != null)
        {
            PlaceParkedCar(ParkedCarPath, new Vector3(8.4f, 0f, 2f), 0f, props, "ParkedCar_1");
            PlaceParkedCar(ParkedCarPath, new Vector3(8.4f, 0f, 66f), 0f, props, "ParkedCar_2");
        }

        // ---------------- 相机 ----------------
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        camGO.transform.SetParent(root, false);
        camGO.transform.position = new Vector3(LaneX, 4f, PointA.z - 7f);
        var cam = camGO.AddComponent<Camera>();
        cam.fieldOfView = 60f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 900f;
        camGO.AddComponent<AudioListener>();
        var chase = camGO.AddComponent<ChaseCamera>();
        chase.target = car.transform;
        camGO.transform.LookAt(car.transform.position + Vector3.up * 1.2f);

        // ---------------- 灯光 / 环境 ----------------
        var lightGO = new GameObject("Sun_Directional");
        lightGO.transform.SetParent(root, false);
        lightGO.transform.rotation = Quaternion.Euler(46f, 38f, 0f);
        var sun = lightGO.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(1f, 0.95f, 0.87f);
        sun.intensity = 1.2f;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.8f;

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.46f, 0.50f, 0.56f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.0018f;
        RenderSettings.fogColor = new Color(0.74f, 0.77f, 0.79f);
        var sky = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
        if (sky != null) RenderSettings.skybox = sky;

        // ---------------- 起点 + 教学导演 ----------------
        var startPoint = new GameObject("StartPoint");
        startPoint.transform.SetParent(root, false);
        startPoint.transform.position = PointA;
        startPoint.transform.rotation = Quaternion.identity;

        car.transform.position = PointA;
        car.transform.rotation = Quaternion.Euler(0f, 0f, 0f);

        var tutorialGO = new GameObject("TutorialDirector");
        tutorialGO.transform.SetParent(root, false);
        var director = tutorialGO.AddComponent<TutorialDirector>();
        director.car = car.GetComponent<CarController>();
        director.startPoint = startPoint.transform;
        // 按键教学的步骤写在 TutorialDirector.Steps 里，不再需要场景路点数据。

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuildSettings(ScenePath);

        Selection.activeGameObject = car;
        Debug.Log("[科目二] 第一关教学场景已生成（城市 A→B 版）：" + ScenePath);
        return true;
    }

    // ---------------- 路网 ----------------

    static void BuildRoads(Transform roads)
    {
        // 南北主路
        for (float z = MainZMin; z <= MainZMax; z += RoadStep)
        {
            string name = Mathf.Approximately(z, CrossZ) ? "Env_Road_Cross_01"
                        : Mathf.Approximately(z, MainZMax) ? "Env_Road_End_01"
                        : "Env_Road_Straight_01";
            LayRoad(roads, name, new Vector3(RoadTileX, 0f, z), 90f);
        }

        // 东西横路（避开路口那一块）
        for (float x = CrossXMin; x <= CrossXMax; x += RoadStep)
        {
            if (Mathf.Approximately(x, 0f)) continue;
            LayRoad(roads, "Env_Road_Straight_01", new Vector3(x, 0f, CrossZ), 0f);
        }
    }

    static void LayRoad(Transform parent, string prefabName, Vector3 pos, float yaw)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PackRoot + prefabName + ".prefab");
        if (prefab == null)
        {
            Debug.LogWarning("[科目二] 缺道路 prefab：" + prefabName);
            return;
        }

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.name = prefabName;
        go.transform.position = new Vector3(pos.x, RoadY, pos.z);
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    // ---------------- 街区 ----------------

    static void FillBlock(Transform town, System.Random rng, Rect block)
    {
        int count = Mathf.Clamp(Mathf.RoundToInt(block.width * block.height / 260f), 3, 6);

        for (int i = 0; i < count; i++)
        {
            string name = BuildingNames[rng.Next(BuildingNames.Length)];
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PackRoot + name + ".prefab");
            if (prefab == null) continue;

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.Euler(0f, (rng.Next(2) == 0) ? 0f : 90f, 0f);

            Bounds b = GetBounds(go);
            float foot = Mathf.Max(b.size.x, b.size.z);
            float targetFoot = 12f + (float)rng.NextDouble() * 7f;
            float s = foot > 0.01f ? targetFoot / foot : 1f;
            float maxH = 10f + (float)rng.NextDouble() * 14f;
            if (b.size.y * s > maxH && b.size.y > 0.01f) s = maxH / b.size.y;
            go.transform.localScale *= s;

            b = GetBounds(go);
            float ex = b.extents.x, ez = b.extents.z;
            if (block.width < ex * 2f || block.height < ez * 2f) { Object.DestroyImmediate(go); continue; }

            float x = block.xMin + ex + (float)rng.NextDouble() * (block.width - ex * 2f);
            float z = block.yMin + ez + (float)rng.NextDouble() * (block.height - ez * 2f);

            go.transform.position = new Vector3(x, 0f, z);
            go.transform.position += new Vector3(0f, -b.min.y, 0f);
            go.transform.SetParent(town, true);
            go.name = "Bld_" + i;
            AddObstacle(go);
        }
    }

    static void BuildTreesAlongRoad(Transform town, System.Random rng)
    {
        // 主路两侧行道树
        for (int side = -1; side <= 1; side += 2)
        {
            for (float z = -24f; z <= 112f; z += 14f)
            {
                if (Mathf.Abs(z - CrossZ) < 9f) continue;
                if (Mathf.Abs(z - PointA.z) < 7f) continue;

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PackRoot + TreeNames[rng.Next(TreeNames.Length)] + ".prefab");
                if (prefab == null) continue;

                float height = 4.6f + (float)rng.NextDouble() * 1.6f;
                var go = PlaceProp(prefab, new Vector3(side * 7.8f, 0f, z), (float)rng.NextDouble() * 360f, height, town);
                go.name = "Tree_Main_" + Mathf.RoundToInt(z);
            }
        }

        // 横路两侧行道树
        for (int side = -1; side <= 1; side += 2)
        {
            for (float x = -52f; x <= 74f; x += 14f)
            {
                if (Mathf.Abs(x) < 9f) continue;

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PackRoot + TreeNames[rng.Next(TreeNames.Length)] + ".prefab");
                if (prefab == null) continue;

                float height = 4.6f + (float)rng.NextDouble() * 1.6f;
                var go = PlaceProp(prefab, new Vector3(x, 0f, CrossZ + side * 7.8f), (float)rng.NextDouble() * 360f, height, town);
                go.name = "Tree_Cross_" + Mathf.RoundToInt(x);
            }
        }
    }

    static void BuildProps(Transform props)
    {
        // 沿主路右侧放：限速、右转指示、B 点停车
        PlaceSign("Prop_StreetSign_Speed_15", new Vector3(5.6f, 0f, -2f), 180f, props);
        PlaceSign("Prop_StreetSign_TurnRight", new Vector3(5.8f, 0f, 26f), 180f, props);
        PlaceSign("Prop_StreetSign_TurnRight", new Vector3(8.5f, 0f, 34.5f), 180f, props);
        PlaceSign("Prop_StreetSign_Stright", new Vector3(5.6f, 0f, 14f), 180f, props);
        PlaceSign("Prop_StreetSign_Parking", new Vector3(54f, 0f, CrossZ - 6.5f), 180f, props);
        PlaceSign("Prop_StreetSign_TrafficSignals", new Vector3(-6.5f, 0f, 33f), 180f, props);
        PlaceSign("Prop_StreetSign_TrafficSignals", new Vector3(6.5f, 0f, 47f), 0f, props);

        var barrel = AssetDatabase.LoadAssetAtPath<GameObject>(PackRoot + "Prop_OilBerrel_01.prefab");
        if (barrel != null)
        {
            PlaceProp(barrel, new Vector3(-7.4f, 0f, 24f), 0f, 0.95f, props).name = "Barrel_1";
            PlaceProp(barrel, new Vector3(-8.0f, 0f, 25.4f), 40f, 0.95f, props).name = "Barrel_2";
        }

        var can = AssetDatabase.LoadAssetAtPath<GameObject>(PackRoot + "Prop_CTPTrashCan_01.prefab");
        if (can != null)
        {
            PlaceProp(can, new Vector3(6.4f, 0f, -6f), 20f, 1.1f, props).name = "TrashCan_1";
            PlaceProp(can, new Vector3(62f, 0f, CrossZ - 6.4f), 70f, 1.1f, props).name = "TrashCan_2";
        }

        var bush = AssetDatabase.LoadAssetAtPath<GameObject>(PackRoot + "Prop_Bush_01.prefab");
        if (bush != null)
        {
            PlaceProp(bush, new Vector3(-6.6f, 0f, 8f), 0f, 1.2f, props);
            PlaceProp(bush, new Vector3(6.6f, 0f, 52f), 90f, 1.1f, props);
            PlaceProp(bush, new Vector3(-6.6f, 0f, 76f), 200f, 1.3f, props);
        }
    }

    static void PlaceSign(string name, Vector3 position, float yaw, Transform parent)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PackRoot + name + ".prefab");
        if (prefab == null)
        {
            Debug.LogWarning("[科目二] 缺指示牌：" + name);
            return;
        }
        var go = PlaceProp(prefab, position, yaw, 2.3f, parent);
        go.name = name;
    }

    static void CreateLabel(string text, Vector3 position, float yaw, Color color)
    {
        var go = new GameObject("Label_" + text);
        go.transform.position = position;
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        var tm = go.AddComponent<TextMesh>();
        tm.text = text;
        tm.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (tm.font == null) tm.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        tm.fontSize = 90;
        tm.characterSize = 0.12f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = color;
        go.name = "Label_" + text;
        go.AddComponent<Billboard>();
    }

    // ---------------- 车辆 ----------------

    static GameObject BuildPlayerCar(GameObject prefab, Transform parent)
    {
        var root = new GameObject("PlayerCar");
        root.transform.SetParent(parent, false);
        root.transform.position = Vector3.zero;
        root.transform.rotation = Quaternion.identity;

        var visual = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        visual.name = "Visual";
        visual.transform.SetParent(root.transform, false);
        FitVehicle(visual.transform, Vector3.zero, 0f, 4.5f);

        var carInput = root.AddComponent<CarInput>();
        carInput.profile = ControlProfileKind.Auto;
        carInput.steerAxisCandidates = new[] { "Stick X", "Stick X2" };
        carInput.throttleAxisCandidates = new[] { "Stick Y", "Stick Y2" };
        root.AddComponent<CarController>();

        var rb = root.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.mass = 1200f;

        var box = root.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, 0.75f, 0.15f);
        box.size = new Vector3(1.7f, 1.5f, 4.4f);

        return root;
    }

    static void PlaceParkedCar(string path, Vector3 position, float yaw, Transform parent, string name)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) return;

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.name = name;
        FitVehicle(go.transform, position, yaw, 4.5f);
        go.transform.SetParent(parent, true);
        AddObstacle(go);
    }

    /// <summary>把任意车模型缩放到指定车长，底部贴地，车头朝 +Z。</summary>
    static void FitVehicle(Transform t, Vector3 position, float yaw, float targetLength)
    {
        t.position = position;
        t.rotation = Quaternion.Euler(0f, yaw, 0f);

        Bounds b = GetBounds(t.gameObject);
        float length = Mathf.Max(b.size.x, b.size.z);
        if (length > 0.01f)
            t.localScale *= targetLength / length;

        if (b.size.x > b.size.z)
            t.rotation = Quaternion.Euler(0f, yaw + 90f, 0f);

        b = GetBounds(t.gameObject);
        t.position += new Vector3(0f, position.y - b.min.y, 0f);
    }

    // ---------------- 通用 ----------------

    static GameObject PlaceProp(GameObject prefab, Vector3 position, float yaw, float targetHeight, Transform parent)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.transform.position = position;
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        Bounds b = GetBounds(go);
        if (b.size.y > 0.01f && targetHeight > 0.01f)
            go.transform.localScale *= targetHeight / b.size.y;

        b = GetBounds(go);
        go.transform.position += new Vector3(0f, position.y - b.min.y, 0f);
        go.transform.SetParent(parent, true);
        AddObstacle(go);
        return go;
    }

    static void PlaceCone(GameObject prefab, Vector3 position, Transform parent, Material mat)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.transform.position = position;
        go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        Bounds b = GetBounds(go);
        if (b.size.y > 0.01f)
            go.transform.localScale = go.transform.localScale * (0.7f / b.size.y);

        b = GetBounds(go);
        go.transform.position += new Vector3(0f, position.y - b.min.y, 0f);

        if (mat != null)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>())
                r.sharedMaterial = mat;
        }

        if (go.GetComponentInChildren<Collider>() == null)
        {
            var bc = go.AddComponent<BoxCollider>();
            bc.center = new Vector3(0f, 0.35f, 0f);
            bc.size = new Vector3(0.4f, 0.7f, 0.4f);
        }
        AddObstacle(go);

        var rb = go.GetComponent<Rigidbody>();
        if (rb == null) rb = go.AddComponent<Rigidbody>();
        rb.mass = 3f;
        rb.drag = 0.4f;
        rb.angularDrag = 0.8f;

        if (go.GetComponent<ConeKnock>() == null)
            go.AddComponent<ConeKnock>();
    }

    static Bounds GetBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return new Bounds(go.transform.position, Vector3.one);

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            b.Encapsulate(renderers[i].bounds);
        return b;
    }

    /// <summary>给场景道具挂一个贴合包围盒的碰撞体，并放到 Obstacle 层，让车撞不穿。</summary>
    static void AddObstacle(GameObject go, float padding = 0f)
    {
        if (go == null) return;

        int layer = LayerMask.NameToLayer("Obstacle");
        if (layer < 0) layer = 8;
        SetLayerRecursive(go, layer);

        Bounds b = GetBounds(go);
        if (b.size.sqrMagnitude < 0.0001f) return;

        Vector3 min = Vector3.one * float.MaxValue;
        Vector3 max = Vector3.one * float.MinValue;
        Transform t = go.transform;
        for (int c = 0; c < 8; c++)
        {
            Vector3 corner = new Vector3(
                (c & 1) == 0 ? b.min.x : b.max.x,
                (c & 2) == 0 ? b.min.y : b.max.y,
                (c & 4) == 0 ? b.min.z : b.max.z);
            Vector3 local = t.InverseTransformPoint(corner);
            min = Vector3.Min(min, local);
            max = Vector3.Max(max, local);
        }

        var box = go.GetComponent<BoxCollider>();
        if (box == null) box = go.AddComponent<BoxCollider>();
        box.center = (min + max) * 0.5f;
        box.size = (max - min) + Vector3.one * (padding * 2f);
    }

    static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursive(child.gameObject, layer);
    }

    static GameObject Box(string name, Vector3 pos, Vector3 size, Material mat, Transform parent, bool removeCollider)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat;

        if (removeCollider)
        {
            var col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
        }

        return go;
    }

    static Material MakeMaterial(string name, Color color, float smoothness)
    {
        string path = "Assets/Materials/" + name + ".mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        var shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Diffuse");

        var mat = new Material(shader);
        mat.color = color;
        mat.SetFloat("_Glossiness", smoothness);
        mat.SetFloat("_Metallic", 0f);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    static void AddToBuildSettings(string path)
    {
        var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (list.Exists(x => x.path == path)) return;
        list.Insert(0, new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = list.ToArray();
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        string leaf = System.IO.Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}
// rebuild 1794634988
// v3.1 1794635273
// v3.2 1794635410
// v3.3 1794635474
// v3.4 1794635877
// v3.5 1794636081
// v3.6 1794636336
// v3.7 1794636491
// v4 1794636889
