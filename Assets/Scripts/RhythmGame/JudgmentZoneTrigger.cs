// JudgmentZoneTrigger.cs【新增文件】
// 挂载在每个 RhythmTrack 的 JudgmentPoint 子物体上
// 同时在该子物体上添加 SphereCollider，勾选 Is Trigger，半径与 JudgmentRadius 一致

using UnityEngine;

namespace RhythmGame
{
    [RequireComponent(typeof(Collider))]
    public class JudgmentZoneTrigger : MonoBehaviour
    {
        private int handsInZone = 0;

        public bool IsHandPresent => handsInZone > 0;

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponent<HandIdentifier>() != null)
                handsInZone++;
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.GetComponent<HandIdentifier>() != null)
                handsInZone = Mathf.Max(0, handsInZone - 1);
        }
    }
}
