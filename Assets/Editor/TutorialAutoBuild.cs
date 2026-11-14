using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 第一次把脚本交给 Unity 时，自动把第一关教学场景生成出来（只做一次）。
/// 生成过就写一个标记，之后不会覆盖你手改过的场景。
/// 想重新生成：菜单 科目二 / 生成第一关教学场景
/// </summary>
[InitializeOnLoad]
public static class TutorialAutoBuild
{
    const string Marker = "Temp/KeMuEr_TutorialBuilt.txt";

    static TutorialAutoBuild()
    {
        EditorApplication.delayCall += TryBuildOnce;
    }

    static void TryBuildOnce()
    {
        if (Application.isPlaying) return;
        if (File.Exists(Marker)) return;

        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += TryBuildOnce;
            return;
        }

        var scene = EditorSceneManager.GetActiveScene();
        // 这个场景是脚本生成的，重建时覆盖掉未保存改动是预期行为
        if (scene.isDirty) Debug.Log("[科目二] 场景有未保存改动，仍然重新生成：" + scene.name);

        if (TutorialSceneBuilder.Build())
        {
            Directory.CreateDirectory("Temp");
            File.WriteAllText(Marker, System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        }
    }
}

// rebuild trigger 1794634051
