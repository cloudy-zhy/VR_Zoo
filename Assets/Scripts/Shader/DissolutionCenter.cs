using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class DissolutionCenter : MonoBehaviour
{
    public Transform target;

    public float distance = 2f;
    [SerializeField] private Material material;

    [SerializeField] private Material[] materials;
    // Update is called once per frame
    void Update()
    {
        if (target && material)
        {
            material.SetVector("_Center", target.position);
            material.SetFloat("_Distance", distance);
        }

        if (target && materials.Length > 0)
        {
            for (int i = 0; i < materials.Length; i++)
            {
                materials[i].SetVector("_Center",target.position);
                materials[i].SetFloat("_Distance", distance);
            }
        }
    }
}
