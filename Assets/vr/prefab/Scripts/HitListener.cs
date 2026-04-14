using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HitListener : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnEnable()
    {
        // 订阅事件
        Bird.BirdHitEvent += OnBirdHit;
    }

    private void OnDisable()
    {
        // 取消订阅
        Bird.BirdHitEvent -= OnBirdHit;
    }

    void OnBirdHit()
    {
        Debug.Log("收到击中事件！");
    }
}
