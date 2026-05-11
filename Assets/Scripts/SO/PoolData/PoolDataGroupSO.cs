using System.Collections.Generic;
using UnityEngine;

namespace Core.Pool
{
    [CreateAssetMenu(fileName = "PoolDataGroupSO", menuName = "Data/Pool/PoolDataGroupSO", order = 0)]
    public class PoolDataGroupSO : ScriptableObject
    {
        public List<PoolDataSO> poolData;
    }
}
