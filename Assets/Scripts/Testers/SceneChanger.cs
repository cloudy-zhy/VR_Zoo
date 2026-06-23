using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem; 

public class SceneChanger : MonoBehaviour
{
    [Header("跳转场景")]
    [SerializeField] private string targetSceneName = "Scene1-2";

    void Update()
    {
        // 用 Keyboard.current 代替 Input.GetKeyDown
        if (Keyboard.current.kKey.wasPressedThisFrame)
        {
            SceneManager.LoadScene(targetSceneName);
        }
    }
}