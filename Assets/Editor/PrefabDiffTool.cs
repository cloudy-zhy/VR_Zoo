#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PrefabDiffTool : EditorWindow
{
    private string _targetName = "";
    private GameObject _targetPrefab;
    private Vector2 _scrollPos;
    private List<DiffEntry> _results = new List<DiffEntry>();
    private bool _showProps = true;
    private bool _showAdded = true;
    private bool _showRemoved = true;

    private class DiffEntry
    {
        public string scenePath;
        public string objectPath;
        public string prefabPath;
        public GameObject instanceRoot;
        public List<PropertyChange> propChanges;
        public List<string> addedChildren;
        public List<string> removedChildren;
        public bool hasAnyDiff => (propChanges?.Count ?? 0) > 0
                                   || (addedChildren?.Count ?? 0) > 0
                                   || (removedChildren?.Count ?? 0) > 0;
    }

    private struct PropertyChange
    {
        public string targetName;
        public string propertyPath;
        public string prefabValue;
        public string overrideValue;
    }

    [MenuItem("Tools/Prefab Diff Tool")]
    public static void ShowWindow() => GetWindow<PrefabDiffTool>("Prefab Diff Tool");

    private void OnGUI()
    {
        // ── 输入区 ──────────────────────────
        EditorGUILayout.LabelField("比对场景实例 ↔ 预制体差异", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        EditorGUILayout.BeginHorizontal();
        _targetName = EditorGUILayout.TextField("物体名称 (模糊匹配)", _targetName);
        if (GUILayout.Button("用选中物体", GUILayout.Width(80)))
        {
            if (Selection.activeGameObject != null)
                _targetName = Selection.activeGameObject.name;
        }
        EditorGUILayout.EndHorizontal();

        _targetPrefab = (GameObject)EditorGUILayout.ObjectField("限定预制体 (可选)", _targetPrefab, typeof(GameObject), false);
        EditorGUILayout.Space(4);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("扫描所有 Build Scenes", GUILayout.Height(35)))
        {
            ScanAllBuildScenes();
        }
        GUI.enabled = _results.Count > 0;
        if (GUILayout.Button("导出到 txt 文件", GUILayout.Height(35), GUILayout.Width(120)))
        {
            ExportToFile();
        }
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(8);

        // ── 结果区 ──────────────────────────
        EditorGUILayout.LabelField($"共发现 {_results.Count} 处实例", EditorStyles.boldLabel);

        _showProps = EditorGUILayout.ToggleLeft("显示 Inspector 属性改动", _showProps);
        _showAdded = EditorGUILayout.ToggleLeft("显示新添加的子物体", _showAdded);
        _showRemoved = EditorGUILayout.ToggleLeft("显示已删除的子物体 (来自预制体)", _showRemoved);

        EditorGUILayout.Space(4);

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
        foreach (var entry in _results)
        {
            DrawEntry(entry);
        }
        EditorGUILayout.EndScrollView();
    }

    // ── 绘制单个比对结果 ──────────────────

    private void DrawEntry(DiffEntry e)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField($"📁 {e.scenePath}", EditorStyles.miniBoldLabel);
        EditorGUILayout.LabelField($"  物体: {e.objectPath}", EditorStyles.miniLabel);
        EditorGUILayout.LabelField($"  预制体: {e.prefabPath}", EditorStyles.miniLabel);

        if (!e.hasAnyDiff)
        {
            EditorGUILayout.LabelField("  ✅ 无差异", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
            return;
        }

        // 属性改动
        if (_showProps && e.propChanges != null && e.propChanges.Count > 0)
        {
            EditorGUILayout.LabelField($"  ── Inspector 属性改动 ({e.propChanges.Count}) ──", EditorStyles.miniBoldLabel);
            foreach (var pc in e.propChanges)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"    {pc.targetName}.{pc.propertyPath}", GUILayout.MinWidth(160));
                EditorGUILayout.LabelField($"[预制体] {pc.prefabValue}", EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField($"  →  [场景] {pc.overrideValue}", EditorStyles.wordWrappedLabel);
                EditorGUILayout.EndHorizontal();
            }
        }

        // 新增子物体
        if (_showAdded && e.addedChildren != null && e.addedChildren.Count > 0)
        {
            EditorGUILayout.LabelField($"  ── 新增子物体 ({e.addedChildren.Count}) ──", EditorStyles.miniBoldLabel);
            foreach (var name in e.addedChildren)
            {
                EditorGUILayout.LabelField($"    + {name}", EditorStyles.miniLabel);
            }
        }

        // 删除的子物体
        if (_showRemoved && e.removedChildren != null && e.removedChildren.Count > 0)
        {
            EditorGUILayout.LabelField($"  ── 已删除子物体 ({e.removedChildren.Count}) ──", EditorStyles.miniBoldLabel);
            foreach (var name in e.removedChildren)
            {
                EditorGUILayout.LabelField($"    - {name}", EditorStyles.miniLabel);
            }
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(4);
    }

    // ── 导出到 txt 文件 ──────────────────

    private void ExportToFile()
    {
        string defaultName = $"PrefabDiff_{System.DateTime.Now:yyyyMMdd_HHmmss}.txt";
        string path = EditorUtility.SaveFilePanel("保存差异报告", "", defaultName, "txt");
        if (string.IsNullOrEmpty(path)) return;

        var sb = new StringBuilder();
        sb.AppendLine("══════════════════════════════════════");
        sb.AppendLine("  Prefab Diff 差异报告");
        sb.AppendLine($"  生成时间: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"  查找目标: \"{_targetName}\"");
        if (_targetPrefab != null)
            sb.AppendLine($"  限定预制体: {AssetDatabase.GetAssetPath(_targetPrefab)}");
        sb.AppendLine($"  匹配实例数: {_results.Count}");
        sb.AppendLine("══════════════════════════════════════");
        sb.AppendLine();

        for (int i = 0; i < _results.Count; i++)
        {
            var e = _results[i];
            sb.AppendLine($"── [{i + 1}] {e.scenePath} ──");
            sb.AppendLine($"  物体路径: {e.objectPath}");
            sb.AppendLine($"  预制体:   {e.prefabPath}");

            if (!e.hasAnyDiff)
            {
                sb.AppendLine("  ✅ 无差异");
                sb.AppendLine();
                continue;
            }

            if (e.propChanges != null && e.propChanges.Count > 0)
            {
                sb.AppendLine($"  ── Inspector 属性改动 ({e.propChanges.Count}) ──");
                foreach (var pc in e.propChanges)
                    sb.AppendLine($"    {pc.targetName}.{pc.propertyPath}\n      [预制体] {pc.prefabValue}\n      [场景]   {pc.overrideValue}");
            }

            if (e.addedChildren != null && e.addedChildren.Count > 0)
            {
                sb.AppendLine($"  ── 新增子物体 ({e.addedChildren.Count}) ──");
                foreach (var name in e.addedChildren)
                    sb.AppendLine($"    + {name}");
            }

            if (e.removedChildren != null && e.removedChildren.Count > 0)
            {
                sb.AppendLine($"  ── 已删除子物体 ({e.removedChildren.Count}) ──");
                foreach (var name in e.removedChildren)
                    sb.AppendLine($"    - {name}");
            }

            sb.AppendLine();
        }

        File.WriteAllText(path, sb.ToString());
        Debug.Log($"[PrefabDiff] 报告已导出: {path}");
        EditorUtility.RevealInFinder(path);
    }

    // ── 核心扫描逻辑 ──────────────────────

    private void ScanAllBuildScenes()
    {
        _results.Clear();
        var buildScenes = EditorBuildSettings.scenes;

        if (buildScenes.Length == 0)
        {
            Debug.LogWarning("[PrefabDiff] Build Settings 中没有场景");
            return;
        }

        if (string.IsNullOrEmpty(_targetName))
        {
            Debug.LogWarning("[PrefabDiff] 请输入要查找的物体名称");
            return;
        }

        Scene currentActive = SceneManager.GetActiveScene();
        string currentActivePath = currentActive.path;

        foreach (var bs in buildScenes)
        {
            if (!bs.enabled) continue;
            string path = bs.path;
            if (string.IsNullOrEmpty(path)) continue;

            Scene scene;
            if (path == currentActivePath)
            {
                scene = currentActive;
            }
            else
            {
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            }

            try
            {
                var roots = scene.GetRootGameObjects();
                foreach (var root in roots)
                {
                    ScanHierarchy(root, bs.path);
                }
            }
            finally
            {
                if (scene != currentActive)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        int totalWithDiff = _results.FindAll(r => r.hasAnyDiff).Count;
        Debug.Log($"[PrefabDiff] 扫描完成：共 {buildScenes.Length} 个场景，找到 {_results.Count} 个匹配实例，其中 {totalWithDiff} 个有差异");
        Repaint();
    }

    private void ScanHierarchy(GameObject go, string scenePath)
    {
        foreach (Transform child in go.transform)
        {
            ScanHierarchy(child.gameObject, scenePath);
        }

        // 名称模糊匹配
        if (!go.name.Contains(_targetName)) return;

        // 限定预制体
        if (_targetPrefab != null)
        {
            var goSource = PrefabUtility.GetCorrespondingObjectFromSource(go);
            if (goSource != _targetPrefab) return;
        }

        var instanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(go);
        if (instanceRoot == null) return;

        var source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(instanceRoot);
        if (source == null) return;

        string prefabPath = AssetDatabase.GetAssetPath(source);

        CollectChildDiffs(instanceRoot, out var added, out var removed);

        var entry = new DiffEntry
        {
            scenePath = scenePath,
            objectPath = BuildPath(go.transform),
            prefabPath = prefabPath,
            instanceRoot = instanceRoot,
            propChanges = CollectPropertyChanges(instanceRoot),
            addedChildren = added,
            removedChildren = removed,
        };

        _results.Add(entry);
    }

    // ── 收集属性改动 ──────────────────────

    private static List<PropertyChange> CollectPropertyChanges(GameObject instanceRoot)
    {
        var list = new List<PropertyChange>();
        var mods = PrefabUtility.GetPropertyModifications(instanceRoot);
        if (mods == null) return list;

        foreach (var mod in mods)
        {
            if (mod.target is RectTransform &&
                (mod.propertyPath == "m_AnchoredPosition" || mod.propertyPath == "m_AnchorMin"
                 || mod.propertyPath == "m_AnchorMax" || mod.propertyPath == "m_Pivot"
                 || mod.propertyPath == "m_SizeDelta"))
                continue;

            string targetName = "(Missing)";
            if (mod.target is Component c)      targetName = BuildPath(c.transform);
            else if (mod.target is GameObject g) targetName = BuildPath(g.transform);

            // mod.value 是场景中的覆盖值；SerializedObject 读到的是当前场景值
            string sceneVal = mod.value ?? "null";

            string prefabVal;
            if (mod.target is Component comp && comp != null)
            {
                using (var so = new SerializedObject(comp))
                {
                    var sp = so.FindProperty(mod.propertyPath);
                    prefabVal = sp != null ? GetPropertyValueString(sp) : "(找不到属性)";
                }
            }
            else
            {
                prefabVal = "(对象已丢失)";
            }

            list.Add(new PropertyChange
            {
                targetName = targetName,
                propertyPath = mod.propertyPath,
                prefabValue = prefabVal,
                overrideValue = sceneVal,
            });
        }

        return list;
    }

    // ── 层级差异对比（统一收集新增和删除） ──

    private static void CollectChildDiffs(GameObject instanceRoot,
        out List<string> added, out List<string> removed)
    {
        added = new List<string>();
        removed = new List<string>();

        var prefabSource = PrefabUtility.GetCorrespondingObjectFromOriginalSource(instanceRoot);
        if (prefabSource == null) return;

        // 预制体中所有子物体的相对路径（含嵌套）
        var prefabPaths = new HashSet<string>();
        foreach (Transform child in prefabSource.GetComponentsInChildren<Transform>(true))
        {
            if (child == prefabSource.transform) continue;
            prefabPaths.Add(RelativePath(child, prefabSource.transform));
        }

        // 实例中所有子物体的相对路径（含嵌套）
        var instancePaths = new HashSet<string>();
        foreach (Transform child in instanceRoot.GetComponentsInChildren<Transform>(true))
        {
            if (child == instanceRoot.transform) continue;
            instancePaths.Add(RelativePath(child, instanceRoot.transform));
        }

        // 实例中有但预制体中没有 → 新增
        foreach (var path in instancePaths)
        {
            if (!prefabPaths.Contains(path))
                added.Add(path);
        }

        // 预制体中有但实例中没有 → 删除
        foreach (var path in prefabPaths)
        {
            if (!instancePaths.Contains(path))
                removed.Add(path);
        }
    }

    // ── 工具方法 ─────────────────────────

    private static string BuildPath(Transform t)
    {
        string path = t.name;
        var parent = t.parent;
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }
        return path;
    }

    private static string RelativePath(Transform t, Transform root)
    {
        string path = t.name;
        var parent = t.parent;
        while (parent != null && parent != root)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }
        return path;
    }

    private static string GetPropertyValueString(SerializedProperty sp)
    {
        switch (sp.propertyType)
        {
            case SerializedPropertyType.Integer:     return sp.intValue.ToString();
            case SerializedPropertyType.Boolean:     return sp.boolValue.ToString();
            case SerializedPropertyType.Float:       return sp.floatValue.ToString("G");
            case SerializedPropertyType.String:      return sp.stringValue;
            case SerializedPropertyType.Color:       return sp.colorValue.ToString();
            case SerializedPropertyType.ObjectReference:
                return sp.objectReferenceValue != null ? sp.objectReferenceValue.name : "None";
            case SerializedPropertyType.Enum:        return $"{sp.enumDisplayNames[sp.enumValueIndex]} ({sp.enumValueIndex})";
            case SerializedPropertyType.Vector2:     return sp.vector2Value.ToString();
            case SerializedPropertyType.Vector3:     return sp.vector3Value.ToString();
            case SerializedPropertyType.Vector4:     return sp.vector4Value.ToString();
            case SerializedPropertyType.Vector2Int:  return sp.vector2IntValue.ToString();
            case SerializedPropertyType.Vector3Int:  return sp.vector3IntValue.ToString();
            case SerializedPropertyType.Quaternion:  return sp.quaternionValue.ToString();
            case SerializedPropertyType.Rect:        return sp.rectValue.ToString();
            case SerializedPropertyType.Bounds:      return sp.boundsValue.ToString();
            case SerializedPropertyType.BoundsInt:   return sp.boundsIntValue.ToString();
            case SerializedPropertyType.AnimationCurve: return "(AnimationCurve)";
            case SerializedPropertyType.Gradient:    return "(Gradient)";
            default:                                 return "(复合类型)";
        }
    }
}
#endif
