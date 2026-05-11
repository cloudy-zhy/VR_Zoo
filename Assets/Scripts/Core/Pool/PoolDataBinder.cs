using System.Collections.Generic;
using Manager;
using UnityEngine;

namespace Core.Pool
{
    public class PoolDataBinder : MonoBehaviour
    {
        [Tooltip("优先使用group")]
        [SerializeField] private PoolDataGroupSO group;
        [Tooltip("优先使用group")]
        [SerializeField] private List<PoolDataSO> poolData;

        private async void Start()
        {
            List<PoolDataSO> list = group != null ? group.poolData : poolData;
            foreach (var data in list)
            {
                await GameManager.Pool.Register(data);
            }
        }
    }
}