using UnityEngine;

namespace Core.Dialog
{
    [CreateAssetMenu(fileName = "DialogSO", menuName = "Data/Dialog/DialogSO", order = 0)]
    public class DialogSO : ScriptableObject
    {
        [Header("内容")]
        public string characterName;
 
        [TextArea(2, 6)]
        public string dialogText;
 
        public Sprite characterPortrait;
 
        [Tooltip("固定时长模式下的显示秒数")]
        [Min(0.1f)]
        public float fixedDuration = 3f;
    }
}