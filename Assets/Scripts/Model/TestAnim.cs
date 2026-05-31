using UnityEngine;

public class TestAnim : MonoBehaviour
{
    private Animator anim;

    void Start()
    {
        anim = GetComponent<Animator>();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.M))
        {
            anim.SetTrigger("doMove");
        }else if(Input.GetKeyDown(KeyCode.S))
        {
            anim.SetTrigger("doStratch");
        }
        else if (Input.GetKeyDown(KeyCode.R))
        {
            anim.SetTrigger("doReaction");
        }
        else if (Input.GetKeyDown(KeyCode.I))
        {
            anim.SetTrigger("stopMove");
        }
    }
}