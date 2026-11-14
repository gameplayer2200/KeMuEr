using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 工程目录被移动或重命名后，Library/Bee 里的构建图缓存仍指向旧绝对路径，
/// Unity 下一次增量编译会报 "Internal build system error ... Could not find a part of the path"。
/// 这个类在编辑器启动时检查缓存里的路径与当前工程是否一致，不一致就删掉 Library/Bee 让它重建。
/// 代价是移动工程后第一次导入会慢一点，但不会再莫名其妙编译失败。
/// </summary>
[InitializeOnLoad]
public static class ProjectPathGuard
{
    const string SessionKey = "KeMuEr.ProjectPathGuard.Checked";

    // 切分缓存文本用的分隔符（避免在代码里写转义字符字面量）
    static readonly char[] TokenSeps = "\n\t \"'\\,=()[]{}<>;:".ToCharArray();

    static ProjectPathGuard()
    {
        EditorApplication.delayCall += Check;
    }

    static void Check()
    {
        // 每个编辑器会话只查一次（域重载后 SessionState 仍然保留）
        if (SessionState.GetBool(SessionKey, false)) return;
        SessionState.SetBool(SessionKey, true);

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string beeDir = Path.Combine(projectRoot, "Library", "Bee");
        if (!Directory.Exists(beeDir)) return;

        string stalePath = FindStalePath(beeDir, projectRoot);
        if (stalePath == null) return;

        Debug.LogWarning("[工程保护] Library/Bee 里残留旧工程路径 " + stalePath +
                         "，正在删除该缓存以便重建（不影响任何源文件）…");
        try
        {
            Directory.Delete(beeDir, true);
            Debug.Log("[工程保护] Library/Bee 已删除，Unity 会重新生成构建图。");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[工程保护] 删除 Library/Bee 失败：" + e.Message + "，请手动关闭 Unity 后删除该目录。");
        }
    }

    /// <summary>找形如 "X:/…/工程名/Library" 的旧路径，与当前路径比对。</summary>
    static string FindStalePath(string beeDir, string projectRoot)
    {
        string projectName = new DirectoryInfo(projectRoot).Name;
        string currentLibrary = Normalize(Path.Combine(projectRoot, "Library"));

        string[] probes =
        {
            Path.Combine(beeDir, "TundraBuildState.state"),
            Path.Combine(beeDir, "tundra.log.json")
        };

        for (int i = 0; i < probes.Length; i++)
        {
            if (!File.Exists(probes[i])) continue;

            string text;
            try { text = File.ReadAllText(probes[i]); }
            catch { continue; }

            string[] tokens = text.Split(TokenSeps, System.StringSplitOptions.RemoveEmptyEntries);

            for (int t = 0; t < tokens.Length; t++)
            {
                string token = tokens[t];
                if (token.Length < 12) continue;

                string norm = Normalize(token);

                // 只要 X:/…/工程名/Library 这个形状，且和当前路径不同
                if (norm.Length > 3 && norm[1] == ':' &&
                    norm.EndsWith("/" + projectName + "/Library", System.StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(norm, currentLibrary, System.StringComparison.OrdinalIgnoreCase))
                    return norm;
            }
        }
        return null;
    }

    static string Normalize(string path)
    {
        return path.Replace("\\\\", "/").Replace("\\", "/").TrimEnd('/');
    }
}
