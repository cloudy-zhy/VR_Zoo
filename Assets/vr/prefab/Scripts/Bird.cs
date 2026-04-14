using UnityEngine;

public class Bird : MonoBehaviour
{
    public static event System.Action BirdHitEvent;

    public LayerMask targetLayer;

    private bool isHit = false; // 碰撞一次后失效，避免重复计分

    private void OnCollisionEnter(Collision collision)
    {
        if (isHit)
            return;
        if ((targetLayer.value & (1 << collision.gameObject.layer)) != 0)
        {
            isHit = true;
            BirdHitEvent?.Invoke();
        }
    }
}