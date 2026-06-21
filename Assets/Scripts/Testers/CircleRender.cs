using UnityEngine;

namespace Testers
{
    public class CircleRender : MonoBehaviour
    {
        [SerializeField] private LineRenderer line;
        [SerializeField] private int pointCount;
        [SerializeField] private float radius;

        [ContextMenu("DrawCircle")]
        private void DrawCircle()
        {
            line.loop = true;
            line.positionCount = pointCount;
            float angle = 360f / pointCount;
            for (int i = 0; i < pointCount; i++)
            {
                line.SetPosition(i, Quaternion.AngleAxis(angle * i, Vector3.up) * Vector3.forward * radius);
            }
        }
    }
}