using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

public class RightBJumpScene : MonoBehaviour
{
    [Header("场景跳转设置")]
    public bool useNextSceneIndex = true;
    public string targetSceneName = "YourSceneName";

    private InputDevice rightHand;
    private bool lastBPressed;

    void Update()
    {
        // 1. 获取右手柄设备（每次检查有效性，防止设备断开）
        if (!rightHand.isValid)
        {
            var devices = new System.Collections.Generic.List<InputDevice>();
            InputDevices.GetDevicesAtXRNode(XRNode.RightHand, devices);
            if (devices.Count > 0)
                rightHand = devices[0];
            else
                return; // 未找到右手柄，跳过本次更新
        }

        // 2. 读取 B 键（secondaryButton）
        if (rightHand.TryGetFeatureValue(CommonUsages.secondaryButton, out bool bNow))
        {
            if (bNow && !lastBPressed) // 上升沿触发
            {
                JumpToScene();
            }
            lastBPressed = bNow;
        }
    }

    private void JumpToScene()
    {
        if (useNextSceneIndex)
        {
            int cur = SceneManager.GetActiveScene().buildIndex;
            int next = (cur + 1) % SceneManager.sceneCountInBuildSettings;
            SceneManager.LoadScene(next);
        }
        else if (!string.IsNullOrEmpty(targetSceneName))
        {
            SceneManager.LoadScene(targetSceneName);
        }
        else
        {
            Debug.LogWarning("未设置目标场景，请在Inspector中配置。");
        }
    }
}