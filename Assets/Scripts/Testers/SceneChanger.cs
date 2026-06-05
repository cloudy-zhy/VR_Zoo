using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneChanger : MonoBehaviour
{
    // Start is called before the first frame update
    [Header("跳转场景")]
    [SerializeField] private string targetSceneName = "Scene1-2";
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.K)) SceneManager.LoadScene(targetSceneName);
    }
}
