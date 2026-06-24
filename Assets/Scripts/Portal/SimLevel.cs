using UnityEngine;

public class SimLevel : MonoBehaviour
{
    public Material skybox;
    // Start is called before the first frame update
    void Start()
    {
        Portal[] portals = GetComponentsInChildren<Portal>();
        foreach (Portal portal in portals)
        {
            Camera camera = portal.GetComponentInChildren<Camera>();
            Skybox skybox=camera.gameObject.AddComponent<Skybox>();
            skybox.material = this.skybox;
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
