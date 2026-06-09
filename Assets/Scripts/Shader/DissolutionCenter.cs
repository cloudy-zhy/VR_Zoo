using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class DissolutionCenter : MonoBehaviour
{
    public Transform target;

    public float distance = 2f;
    [SerializeField] private Material material;
    // Update is called once per frame
    void Update()
    {
        if (target && material)
        {
            material.SetVector("_Center", target.position);
            material.SetFloat("_Distance", distance);
        }
    }
}
