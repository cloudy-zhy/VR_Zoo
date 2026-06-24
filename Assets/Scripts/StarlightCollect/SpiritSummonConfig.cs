using UnityEngine;

namespace StarlightCollect
{
    /// <summary>
    /// 每种动物剪影的召唤配置。
    /// 在 Project 窗口右键 → Create → VR Zoo → Spirit Summon Config 创建。
    /// </summary>
    [CreateAssetMenu(fileName = "SpiritSummonConfig", menuName = "VR Zoo/Spirit Summon Config")]
    public class SpiritSummonConfig : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("动物名称")]
        public string animalName = "???";
        [Tooltip("出现时的介绍文本")]
        [TextArea(2, 5)]
        public string introductionText = "一只神秘的动物出现了……";
        [Tooltip("剪影纹理（用在星空中显示）")]
        public Texture2D silhouetteTexture;

        [Header("Summon Effect")]
        [Tooltip("从星空坠落阶段的持续时间")]
        public float fallDuration = 1.5f;
        [Tooltip("流星坠落阶段的持续时间")]
        public float meteorDuration = 1.0f;
        [Tooltip("灵魂实体聚合阶段的持续时间")]
        public float assembleDuration = 1.0f;
        [Tooltip("坠落曲线弧高")]
        public float arcHeight = 5f;
        [Tooltip("落地后距离玩家的偏移")]
        public Vector3 landOffset = new Vector3(0f, 0f, 2f);

        [Header("Spirit Entity")]
        [Tooltip("灵魂实体预制体（暂时可用一个半透明球/模型代替）")]
        public GameObject spiritPrefab;
        [Tooltip("实体初始缩放")]
        public float spiritScale = 1f;
        [Tooltip("挥手动画时长")]
        public float waveDuration = 1.5f;
        [Tooltip("实体存在时间（-1 为永久，等待手动关闭）")]
        public float spiritLifetime = 15f;

        [Header("Audio (optional)")]
        [Tooltip("召唤音效")]
        public AudioClip summonSound;
        [Tooltip("出现音效")]
        public AudioClip appearSound;
    }
}