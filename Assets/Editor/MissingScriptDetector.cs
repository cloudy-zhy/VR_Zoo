#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEditor;

public class MissingScriptDetector : EditorWindow
{
    private GameObject _targetPrefab;

    [MenuItem("Tools/Missing Script Detector")]
    public static void ShowWindow() => GetWindow<MissingScriptDetector>("Missing Script Detector");

    [MenuItem("Tools/Missing Scripts/🔍 扫描全部 Prefab")]
    static void ScanAllPrefabsMenu() => ScanAllPrefabs();

    [MenuItem("Tools/Missing Scripts/🔍 扫描当前场景")]
    static void ScanActiveSceneMenu() => ScanActiveScene();

    [MenuItem("Tools/Missing Scripts/🔍 扫描选中的 Prefab")]
    static void ScanSelectedPrefabMenu()
    {
        var selected = Selection.objects;
        if (selected == null || selected.Length == 0)
        {
            Debug.LogWarning("请在 Project 视图中选中至少一个 Prefab 资产");
            return;
        }

        int scanned = 0, totalMissing = 0;
        foreach (var obj in selected)
        {
            string path = AssetDatabase.GetAssetPath(obj);
            if (!path.EndsWith(".prefab")) continue;
            GameObject prefab = obj as GameObject;
            if (prefab == null) continue;

            int cnt = CountAndReportMissing(prefab, path);
            if (cnt > 0) totalMissing += cnt;
            scanned++;
        }

        if (scanned == 0)
            Debug.LogWarning("未找到任何 Prefab 资产");
        else
            Debug.Log($"扫描完成：共 {scanned} 个 Prefab，发现 {totalMissing} 处缺失脚本");
    }

    // ========== 核心扫描 ==========

    static void ScanAllPrefabs()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab");
        int totalMissing = 0, affected = 0;
        Debug.Log($"════ 开始扫描全部 Prefab，共 {guids.Length} 个 ────");

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            int cnt = CountAndReportMissing(prefab, path);
            if (cnt > 0) { affected++; totalMissing += cnt; }
        }

        if (totalMissing == 0)
            Debug.Log("✅ 全部 Prefab 扫描完毕 — 未发现缺失脚本！");
        else
            Debug.LogError($"❌ 扫描完毕：{affected} 个 Prefab 含共 {totalMissing} 处缺失脚本");
    }

    static void ScanActiveScene()
    {
        var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        int totalMissing = 0;
        foreach (var root in roots)
            totalMissing += CheckHierarchy(root);
        if (totalMissing == 0)
            Debug.Log("✅ 当前场景未发现缺失脚本！");
        else
            Debug.LogError($"❌ 当前场景中共有 {totalMissing} 处缺失脚本（见上方 Error 条目）");
    }

    // ---------- 整合输出方法 ----------

    /// <summary>
    /// 扫描一个 Prefab（含所有子物体），将所有缺失信息整合到一条 Error 中输出。
    /// </summary>
    static int CountAndReportMissing(GameObject prefab, string assetPath)
    {
        int totalMissing = 0;
        var allTransforms = prefab.GetComponentsInChildren<Transform>(true);
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"Prefab: {assetPath}");

        foreach (var t in allTransforms)
        {
            int cnt = CollectMissingInfo(t.gameObject, sb);
            if (cnt > 0) totalMissing += cnt;
        }

        if (totalMissing > 0)
        {
            Debug.LogError(sb.ToString(), prefab);
        }
        return totalMissing;
    }

    /// <summary>
    /// 扫描场景中的一个根对象（含所有子物体），将所有缺失信息整合到一条 Error 中输出。
    /// </summary>
    static int CheckHierarchy(GameObject root)
    {
        int totalMissing = 0;
        var allTransforms = root.GetComponentsInChildren<Transform>(true);
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"Scene Root: {root.name}");

        foreach (var t in allTransforms)
        {
            int cnt = CollectMissingInfo(t.gameObject, sb);
            if (cnt > 0) totalMissing += cnt;
        }

        if (totalMissing > 0)
        {
            Debug.LogError(sb.ToString(), root);
        }
        return totalMissing;
    }

    /// <summary>
    /// 收集单个 GameObject 的缺失信息，追加到 StringBuilder 中。
    /// </summary>
    static int CollectMissingInfo(GameObject go, StringBuilder sb)
    {
        int missingCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
        if (missingCount == 0) return 0;

        string path = GetPath(go);
        var monos = go.GetComponents<MonoBehaviour>();
        List<string> validScriptNames = new List<string>();
        foreach (var m in monos)
        {
            if (m != null)
                validScriptNames.Add(m.GetType().Name);
        }

        sb.AppendLine($"  ─ {path} (Missing ×{missingCount})");
        if (validScriptNames.Count > 0)
        {
            sb.AppendLine("    Existing scripts:");
            foreach (var name in validScriptNames)
                sb.AppendLine($"      - {name}");
        }
        else
        {
            sb.AppendLine("    No other scripts.");
        }

        return missingCount;
    }

    static string GetPath(GameObject go)
    {
        if (go.transform.parent == null) return go.name;
        return GetPath(go.transform.parent.gameObject) + "/" + go.name;
    }

    // ========== 窗口 GUI ==========

    void OnGUI()
    {
        GUILayout.Space(10);

        GUILayout.BeginHorizontal();
        _targetPrefab = (GameObject)EditorGUILayout.ObjectField("指定 Prefab", _targetPrefab, typeof(GameObject), false);
        if (GUILayout.Button("扫描此 Prefab", GUILayout.Width(110)))
        {
            if (_targetPrefab == null)
            {
                Debug.LogWarning("请先拖入一个 Prefab 资产");
            }
            else
            {
                string path = AssetDatabase.GetAssetPath(_targetPrefab);
                if (path.EndsWith(".prefab"))
                {
                    int cnt = CountAndReportMissing(_targetPrefab, path);
                    if (cnt == 0)
                        Debug.Log($"✅ {path} — 无缺失脚本", _targetPrefab);
                }
                else
                {
                    Debug.LogWarning("拖入的对象不是一个 Prefab 资产");
                }
            }
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(6);
        if (GUILayout.Button("🔍 扫描全部 Prefab → Console", GUILayout.Height(30))) ScanAllPrefabs();
        if (GUILayout.Button("🔍 扫描当前场景 → Console", GUILayout.Height(30))) ScanActiveScene();

        GUILayout.Space(10);
        EditorGUILayout.HelpBox(
            "本工具仅检测缺失脚本，不做任何修改。\n" +
            "每个 Prefab / 场景根对象只输出一条 Error，内含所有缺失位置及现有脚本列表。",
            MessageType.Info);
    }
}
#endif