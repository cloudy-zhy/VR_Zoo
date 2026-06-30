using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CameraReferenceFinder : EditorWindow
{
    private Vector2 resultScrollPos, pathScrollPos;
    private List<Entry> results = new List<Entry>();
    private bool showComponentDetail = true;

    // 路径快速复制
    private GameObject targetObject;
    private string targetPath = "";
    private List<string> pathLines = new List<string>();

    private struct Entry
    {
        public string path;
        public string componentName;
        public string propertyName;
        public GameObject gameObject;
        public bool isAssigned;
        public string assignedCameraName;
    }

    [MenuItem("GameObject/Copy Scene Path", false, 0)]
    private static void CopyPathFromMenu()
    {
        var go = Selection.activeGameObject;
        if (go == null) return;
        string path = BuildPath(go.transform);
        GUIUtility.systemCopyBuffer = path;
        Debug.Log($"[ScenePath] 已复制: {path}");
    }

    [MenuItem("GameObject/Copy Scene Path", true)]
    private static bool CopyPathFromMenuValidate() => Selection.activeGameObject != null;

    [MenuItem("Tools/Camera Reference Finder")]
    private static void Open()
    {
        GetWindow<CameraReferenceFinder>("Camera 引用查找");
    }

    private void OnGUI()
    {
        // ========== 上半部分：快速获取路径 ==========
        EditorGUILayout.LabelField("快速获取路径", EditorStyles.boldLabel);
        EditorGUILayout.Space(3);

        EditorGUILayout.BeginHorizontal();
        targetObject = (GameObject)EditorGUILayout.ObjectField(
            "目标物体", targetObject, typeof(GameObject), true);

        if (GUILayout.Button("用选中物体", GUILayout.Width(80)))
        {
            targetObject = Selection.activeGameObject;
        }
        EditorGUILayout.EndHorizontal();

        if (targetObject != null)
        {
            targetPath = BuildPath(targetObject.transform);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("场景路径:", targetPath, EditorStyles.wordWrappedLabel);
            if (GUILayout.Button("复制", GUILayout.Width(40)))
            {
                GUIUtility.systemCopyBuffer = targetPath;
                Debug.Log($"[ScenePath] 已复制: {targetPath}");
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(3);

        // 批量显示子物体路径
        EditorGUILayout.LabelField("子物体路径列表", EditorStyles.miniBoldLabel);
        if (targetObject != null && GUILayout.Button("生成子物体路径列表", GUILayout.Height(20)))
        {
            BuildChildPathList();
        }

        if (pathLines.Count > 0)
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("复制全部", GUILayout.Width(60)))
            {
                GUIUtility.systemCopyBuffer = string.Join("\n", pathLines);
                Debug.Log($"[ScenePath] 已复制 {pathLines.Count} 条路径");
            }
            if (GUILayout.Button("清空", GUILayout.Width(40)))
            {
                pathLines.Clear();
            }
            EditorGUILayout.EndHorizontal();

            pathScrollPos = EditorGUILayout.BeginScrollView(pathScrollPos, GUILayout.Height(120));
            foreach (var line in pathLines)
            {
                EditorGUILayout.LabelField(line, EditorStyles.miniLabel);
            }
            EditorGUILayout.EndScrollView();
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        EditorGUILayout.Space(5);

        // ========== 下半部分：Camera 引用查找 ==========
        EditorGUILayout.LabelField("查找场景中所有引用 Camera 的组件", EditorStyles.boldLabel);
        EditorGUILayout.Space(3);

        if (GUILayout.Button("扫描场景", GUILayout.Height(30)))
        {
            Scan();
        }

        showComponentDetail = EditorGUILayout.Toggle("显示组件和属性名", showComponentDetail);

        EditorGUILayout.Space(3);
        EditorGUILayout.LabelField($"找到 {results.Count} 处引用:", EditorStyles.boldLabel);

        resultScrollPos = EditorGUILayout.BeginScrollView(resultScrollPos);
        foreach (var entry in results)
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("定位", GUILayout.Width(40)))
            {
                Selection.activeGameObject = entry.gameObject;
                EditorGUIUtility.PingObject(entry.gameObject);
            }

            string status = entry.isAssigned
                ? $" -> {entry.assignedCameraName}"
                : " (未赋值)";

            string display = showComponentDetail
                ? $"{entry.path}  [{entry.componentName}.{entry.propertyName}]{status}"
                : $"{entry.path}{status}";

            if (GUILayout.Button(display, EditorStyles.label))
            {
                GUIUtility.systemCopyBuffer = entry.path;
                Debug.Log($"[CameraRef] 已复制路径: {entry.path}");
            }

            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
    }

    private void OnSelectionChange()
    {
        if (Selection.activeGameObject != null)
        {
            targetObject = Selection.activeGameObject;
            targetPath = BuildPath(targetObject.transform);
        }
        Repaint();
    }

    private void BuildChildPathList()
    {
        pathLines.Clear();
        foreach (Transform child in targetObject.GetComponentsInChildren<Transform>(true))
        {
            pathLines.Add(BuildPath(child));
        }
    }

    private void Scan()
    {
        results.Clear();

        var cameraMap = new Dictionary<int, string>();
        foreach (var cam in FindObjectsOfType<Camera>(true))
        {
            cameraMap[cam.GetInstanceID()] = cam.name;
        }

        var seen = new HashSet<string>();
        int total = 0;

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;

            var roots = scene.GetRootGameObjects();
            foreach (var root in roots)
            {
                ScanTransform(root.transform, cameraMap, seen, ref total);
            }
        }

        Debug.Log($"[CameraRef] 扫描完成，共检查 {total} 个组件，找到 {results.Count} 处 Camera 引用");
        Repaint();
    }

    private void ScanTransform(Transform t, Dictionary<int, string> cameraMap,
        HashSet<string> seen, ref int checkedCount)
    {
        var go = t.gameObject;
        var components = go.GetComponents<Component>();

        foreach (var comp in components)
        {
            if (comp == null) continue;
            checkedCount++;

            using (var so = new SerializedObject(comp))
            {
                var prop = so.GetIterator();
                bool enterChildren = true;
                while (prop.Next(enterChildren))
                {
                    enterChildren = prop.propertyType == SerializedPropertyType.Generic;

                    if (prop.propertyType != SerializedPropertyType.ObjectReference)
                        continue;

                    if (prop.name == "m_Script" || prop.name == "m_GameObject")
                        continue;

                    bool isCameraType = prop.type.Contains("Camera>");
                    int refId = prop.objectReferenceInstanceIDValue;
                    bool isAssignedCamera = refId != 0 && cameraMap.ContainsKey(refId);

                    if (!isCameraType && !isAssignedCamera)
                        continue;

                    string key = $"{go.GetInstanceID()}|{comp.GetType().Name}|{prop.name}";
                    if (!seen.Add(key))
                        continue;

                    string assignedName = "";
                    if (isAssignedCamera)
                    {
                        cameraMap.TryGetValue(refId, out assignedName);
                    }

                    results.Add(new Entry
                    {
                        path = BuildPath(t),
                        componentName = comp.GetType().Name,
                        propertyName = prop.displayName,
                        gameObject = go,
                        isAssigned = isAssignedCamera,
                        assignedCameraName = assignedName
                    });
                }
            }
        }

        foreach (Transform child in t)
        {
            ScanTransform(child, cameraMap, seen, ref checkedCount);
        }
    }

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
}
