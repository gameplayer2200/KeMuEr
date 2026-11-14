using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>临时：轮询标记文件控制 Play 模式（不用改脚本也能触发）。</summary>
[InitializeOnLoad]
public static class PlayTest
{
    const string PlayMarker = "Temp/KeMuEr_Play.txt";
    const string StopMarker = "Temp/KeMuEr_Stop.txt";

    static PlayTest()
    {
        EditorApplication.update += Tick;
    }

    static void Tick()
    {
        if (File.Exists(PlayMarker))
        {
            File.Delete(PlayMarker);
            if (!EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = true;
                Debug.Log("[PLAY] entering play mode");
            }
            else
            {
                Debug.Log("[PLAY] already playing");
            }
        }

        if (File.Exists(StopMarker))
        {
            File.Delete(StopMarker);
            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
                Debug.Log("[PLAY] leaving play mode");
            }
            else
            {
                Debug.Log("[PLAY] already stopped");
            }
        }
    }
}
