using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RhythmGame;

public class Scene2Manager : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        FindObjectOfType<RhythmGameManager>().StartGame();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
