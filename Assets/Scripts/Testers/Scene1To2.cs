using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Scene1To2 : MonoBehaviour
{
    // Start is called before the first frame update
    
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        string targetSceneName = "Scene1-2";
        if (Input.GetKeyDown(KeyCode.K)) SceneManager.LoadScene(targetSceneName);
    }
}
