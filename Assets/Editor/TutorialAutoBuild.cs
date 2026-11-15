using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 第一次把脚本交给 Unity 时，自动把第一关教学场景生成出来（只做一次）。
/// 场景文件已存在就不再自动重建，生成过也会写一个持久标记。
/// 想重新生成：菜单 科目二 / 生成第一关教学场景
/// </summary>
[InitializeOnLoad]
public static class TutorialAutoBuild
{
    // 放 ProjectSettings 里，Unity 不会清空（原来放 Temp/ 每次关 Unity 都被删，导致每次启动都重建覆盖手改）
    const string Marker = "ProjectSettings/KeMuEr_TutorialBuilt.marker";

    static TutorialAutoBuild()
    {
        EditorApplication.delayCall += TryBuildOnce;
    }

    static void TryBuildOnce()
    {
        if (Application.isPlaying) return;
        if (File.Exists(Marker)) return;
        // 场景已存在（包括手改过的版本）就绝不自动重建，想重建走菜单
        if (File.Exists(TutorialSceneBuilder.ScenePath)) return;

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
            File.WriteAllText(Marker, System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        }
    }
}
