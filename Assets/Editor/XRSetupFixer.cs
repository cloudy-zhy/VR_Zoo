using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public class XRSetupFixer : EditorWindow
{
    [MenuItem("Tools/XR Setup/Fix (Simulator OFF, Y=-0.5)")]
    static void FixXRSetup()
    {
        ProcessScenes(false, -0.5f, "Fix");
    }

    [MenuItem("Tools/XR Setup/Restore (Simulator ON, Y=1.3)")]
    static void RestoreXRSetup()
    {
        ProcessScenes(true, 1.3f, "Restore");
    }

    static void ProcessScenes(bool enableSimulator, float cameraY, string operationName)
    {
        int processedCount = 0;
        int modifiedCount = 0;

        // 获取构建设置中的所有场景
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;

        if (scenes.Length == 0)
        {
            Debug.LogWarning("No scenes found in Build Settings. Please add scenes first.");
            return;
        }

        // 强制停止播放模式，避免在运行时修改场景出错
        if (EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = false;
            Debug.Log("Exited Play Mode to safely modify scenes.");
        }

        // 遍历所有场景
        for (int i = 0; i < scenes.Length; i++)
        {
            EditorBuildSettingsScene scene = scenes[i];
            if (!scene.enabled) continue; // 只处理已勾选的场景

            string scenePath = scene.path;
            processedCount++;
            bool sceneModified = false;

            Debug.Log($"Processing scene [{processedCount}/{scenes.Length}]: {scenePath}");

            // 打开场景
            Scene currentScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            // 1. 处理 XR Device Simulator
            GameObject simulatorObj = FindInactiveObjectByName("XR Device Simulator");
            if (simulatorObj != null)
            {
                if (simulatorObj.activeSelf != enableSimulator)
                {
                    simulatorObj.SetActive(enableSimulator);
                    sceneModified = true;
                    Debug.Log($"  > Set 'XR Device Simulator' to {enableSimulator}");
                }
            }
            else
            {
                Debug.LogWarning("  > 'XR Device Simulator' not found.");
            }

            // 2. 处理 Camera Offset Y
            // 查找基础对象：优先找 XR Origin (XR Rig)，如果没有则找 XR Origin
            GameObject originObj = FindInactiveObjectByName("XR Origin (XR Rig)");
            if (originObj == null) originObj = FindInactiveObjectByName("XR Origin");

            if (originObj != null)
            {
                Transform cameraOffset = FindChildRecursive(originObj.transform, "Camera Offset");
                if (cameraOffset != null)
                {
                    Vector3 localPos = cameraOffset.localPosition;
                    if (!Mathf.Approximately(localPos.y, cameraY))
                    {
                        localPos.y = cameraY;
                        cameraOffset.localPosition = localPos;
                        sceneModified = true;
                        Debug.Log($"  > Set 'Camera Offset' Y to {cameraY}");
                    }
                }
                else
                {
                    Debug.LogWarning("  > 'Camera Offset' not found under XR Origin.");
                }
            }
            else
            {
                Debug.LogWarning("  > Base 'XR Origin' object not found.");
            }

            // 3. 保存
            if (sceneModified)
            {
                EditorSceneManager.SaveScene(currentScene);
                modifiedCount++;
                Debug.Log($"  > Scene Saved.");
            }
            else
            {
                Debug.Log($"  > No changes needed.");
            }
        }

        Debug.Log($"=== Finished: {modifiedCount} out of {processedCount} scenes were modified. ===");
    }

    // 递归查找子物体（即使子物体是未激活的也能找到）
    private static Transform FindChildRecursive(Transform parent, string name)
    {
        if (parent.name == name) return parent;

        foreach (Transform child in parent)
        {
            Transform result = FindChildRecursive(child, name);
            if (result != null) return result;
        }
        return null;
    }

    // 通过名称查找场景中所有物体（包括未激活的）
    private static GameObject FindInactiveObjectByName(string name)
    {
        Scene activeScene = EditorSceneManager.GetActiveScene();

        if (!activeScene.isLoaded)
        {
            return null;
        }

        // 获取场景中所有根物体
        GameObject[] rootObjects = activeScene.GetRootGameObjects();

        foreach (GameObject root in rootObjects)
        {
            Transform result = FindChildRecursive(root.transform, name);
            if (result != null) return result.gameObject;
        }
        return null;
    }
}