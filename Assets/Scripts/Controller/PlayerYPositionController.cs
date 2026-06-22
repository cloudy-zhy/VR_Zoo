using UnityEngine;

public class PlayerYPositionController : MonoBehaviour
{
    private Transform _playerTrans;
    private Transform _ladderTrans;
    private Vector3 climbVelocity;
    private float dis;
    private float speed;
    private float minDis=0.1f;
    
    private void Awake()
    {
        _playerTrans = Camera.main?.transform;
        _ladderTrans = gameObject.transform;
    }

    public void Climb()
    {
        climbVelocity = new Vector3(0, speed, 0);
        Vector3 posA = _playerTrans.position;
        Vector3 posB = _ladderTrans.position;

        dis = Vector2.Distance(new Vector2(posA.x, posB.x), new Vector2(posA.z, posB.z));
        if (minDis >= dis)
        {
            _playerTrans.Translate(climbVelocity * Time.deltaTime);
        }
    }
}
