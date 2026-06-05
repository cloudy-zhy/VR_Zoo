// BeatmapImporter.cs
// 放在 Assets/Editor/ 文件夹下
// 使用方式：菜单栏 → RhythmGame → Import Beatmap JSON

using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using RhythmGame;

public class BeatmapImporter : EditorWindow
{
    private string jsonPath = "";

    [MenuItem("RhythmGame/Import Beatmap JSON")]
    public static void ShowWindow()
    {
        GetWindow<BeatmapImporter>("Beatmap Importer");
    }

    private void OnGUI()
    {
        GUILayout.Label("导入谱面 JSON", EditorStyles.boldLabel);
        GUILayout.Space(8);

        EditorGUILayout.BeginHorizontal();
        jsonPath = EditorGUILayout.TextField("JSON 路径", jsonPath);
        if (GUILayout.Button("浏览", GUILayout.Width(50)))
        {
            jsonPath = EditorUtility.OpenFilePanel("选择谱面 JSON", Application.dataPath, "json");
        }
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(12);

        if (GUILayout.Button("导入为 BeatmapData"))
        {
            ImportBeatmap(jsonPath);
        }
    }

    private void ImportBeatmap(string path)
    {
        if (!File.Exists(path))
        {
            EditorUtility.DisplayDialog("错误", "文件不存在：" + path, "OK");
            return;
        }

        string json = File.ReadAllText(path);
        BeatmapJson data = JsonUtility.FromJson<BeatmapJson>(json);

        // 创建 ScriptableObject
        BeatmapData asset = ScriptableObject.CreateInstance<BeatmapData>();
        asset.bpm = data.bpm;
        asset.notes = new List<NoteData>();

        foreach (var n in data.notes)
        {
            TrackType track;
            switch (n.track)
            {
                case "LeftHigh": track = TrackType.LeftHigh; break;
                case "RightHigh": track = TrackType.RightHigh; break;
                case "LeftLow": track = TrackType.LeftLow; break;
                default: track = TrackType.RightLow; break;
            }

            asset.notes.Add(new NoteData
            {
                track        = track,
                hitTime      = n.hitTime,
                isHold       = n.isHold,        // ← 新增
                holdDuration = n.holdDuration   // ← 新增
            });
        }

        // 保存到 Assets/Resources/Beatmaps/
        string dir = "Assets/Resources/Beatmaps";
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        string assetName = Path.GetFileNameWithoutExtension(path);
        string assetPath = $"{dir}/{assetName}.asset";

        AssetDatabase.CreateAsset(asset, assetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "导入成功",
            $"已生成：{assetPath}\n共 {asset.notes.Count} 个音符\nBPM: {asset.bpm}",
            "OK");

        Selection.activeObject = asset;
    }

    // JSON 反序列化用的临时类
    [System.Serializable]
    private class BeatmapJson
    {
        public float bpm;
        public List<NoteJsonEntry> notes;
    }

    [System.Serializable]
    private class NoteJsonEntry
    {
        public string track;
        public float hitTime;
        public bool   isHold       = false;   // ← 新增
        public float  holdDuration = 0f;      // ← 新增
    }
}