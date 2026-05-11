using UnityEngine;

namespace Core.Pool
{
    [CreateAssetMenu(fileName = "PoolDataSO", menuName = "Data/Pool/PoolDataSO", order = 0)]
    public class PoolDataSO : ScriptableObject
    {
        [Tooltip("唯一标识符，对应 Rent/Return 调用中的 poolName 参数。")]
        public string poolName;

        [Tooltip("扩容步长")] 
        public int step = 8;
        
        [Tooltip("最大上限，-1表示无上限")]
        public int capacity = -1;
        
        [Tooltip("初始化预热数目，默认为0不预热")]
        public int prewarm = 0;
        
        [Tooltip("是否使用Addressable加载预制体，启用时prefab项无效，其中assetPath将使用poolName")]
        public bool useAddressable = true;

        [Tooltip("预制体")]
        public GameObject prefab;
    }
}