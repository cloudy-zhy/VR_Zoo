using UnityEngine;

[ExecuteAlways]
public class ChangeCubeColor : MonoBehaviour
{
    public Material normalMaterial;   // 初始材质
    public Material pressedMaterial;  // 按下 K 时的材质

    private Renderer rend;

    void Start()
    {
        rend = GetComponent<Renderer>();
        if (normalMaterial && rend)
            rend.material = normalMaterial;
    }

    void Update()
    {
        if (rend == null) return;

        if (Input.GetKeyDown(KeyCode.K))
        {
            if (pressedMaterial)
                rend.material = pressedMaterial;
        }

        if (Input.GetKeyUp(KeyCode.K))
        {
            if (normalMaterial)
                rend.material = normalMaterial;
        }
    }

    public void ChangeMaterial()
    {
        if (pressedMaterial)
            rend.material = pressedMaterial;
    }

    public void RestoreMaterial()
    {
        if (normalMaterial)
            rend.material = normalMaterial;
    }
}