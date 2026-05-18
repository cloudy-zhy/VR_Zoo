using System.Collections.Generic;
using Core.Utils;
using Cysharp.Threading.Tasks;
using Manager;
using UnityEngine;

namespace Core.Pool
{
    public class PoolManager
    {
        private readonly Transform _poolRootTransform;
        public Dictionary<string, GameObjectPool> PoolDict { get; } = new();

        public PoolManager()
        {
            GameObject root = new GameObject("===Pools===");
            _poolRootTransform = root.transform;
        }

        #region Register
        
        public async UniTask Register(string poolName, 
            int step = 4, int capacity = -1, int prewarm = 0, GameObject prefab = null)
        {
            if (PoolDict.ContainsKey(poolName))
                return;
            var pool = new GameObjectPool();
            await pool.Initialize(poolName, _poolRootTransform, step, capacity, prewarm, prefab);
            PoolDict.Add(poolName, pool);
#if UNITY_EDITOR
            GameManager.Event.Broadcast(this, PoolEvents.Registered, poolName);
#endif
        }

        public async UniTask Register(PoolDataSO poolData)
        {
            await Register(poolData.poolName, poolData.step, poolData.capacity, poolData.prewarm, 
                poolData.useAddressable ? null : poolData.prefab);
        }
        
        #endregion

        #region Unregister

        public void Unregister(string poolName)
        {
            if (PoolDict.TryGetValue(poolName, out var pool))
            {
                pool.Destroy();
                PoolDict.Remove(poolName);
#if UNITY_EDITOR
                GameManager.Event.Broadcast(this, PoolEvents.Unregistered, poolName);
#endif
            }
        }

        public void Unregister()
        {
            foreach (var pool in PoolDict.Values)
            {
                pool.Destroy();
            }
            PoolDict.Clear();
#if UNITY_EDITOR
            GameManager.Event.Broadcast(this, PoolEvents.Cleared);
#endif
        }

        #endregion

        #region Rent

        private bool TryRent(string poolName, out GameObjectPool pool, out GameObject gameObject,
            Vector3? position = null, Quaternion? rotation = null, PoolParentOverride parent = default)
        {
            gameObject = null;
            if (!PoolDict.TryGetValue(poolName, out pool) || !pool.TryRent(out gameObject, parent))
                return false;
#if UNITY_EDITOR
                GameManager.Event.Broadcast(this, PoolEvents.Rented, poolName);
#endif
            if (position.HasValue) gameObject.transform.position = position.Value;
            if (rotation.HasValue) gameObject.transform.rotation = rotation.Value;
            return true;
        }

        public GameObject Rent(string poolName,
            Vector3? position = null, Quaternion? rotation = null, PoolParentOverride parent = default)
            => TryRent(poolName, out _, out var gameObject, position, rotation, parent) ? gameObject : null;

        public async UniTaskVoid Rent(string poolName, float duration,
            Vector3? position = null, Quaternion? rotation = null, PoolParentOverride parent = default)
        {
            if (TryRent(poolName, out var pool, out var gameObject, position, rotation, parent))
            {
                await UniTask.WaitForSeconds(duration);
                // 等待的这会，可能池/物体被销毁了，物体不会无端销毁，关心池因为场景切换注销的问题
                if (!pool.IsDestroyed && pool.Return(gameObject))
                {
#if UNITY_EDITOR
                    GameManager.Event.Broadcast(this, PoolEvents.Returned, poolName);
#endif
                }
            }
        }
        
        #region 语法糖
        public T Rent<T>(string poolName,
            Vector3? position = null, Quaternion? rotation = null, PoolParentOverride parent = default) where T : Component
        {
            if (TryRent(poolName, out _, out var gameObject, position, rotation, parent) && 
                gameObject.TryGetComponent(out T component))
            {
                return component;
            }
            return null;
        }
        public bool TryRent(string poolName, out GameObject gameObject,
            Vector3? position = null, Quaternion? rotation = null, PoolParentOverride parent = default) 
            => (gameObject = Rent(poolName, position, rotation, parent)).IsNotNull();

        public bool TryRent<T>(string poolName, out T component,
            Vector3? position = null, Quaternion? rotation = null, PoolParentOverride parent = default) where T : Component
            => (component = Rent<T>(poolName, position, rotation, parent)) != null;
        #endregion
        
        #endregion

        #region Return

        public void Return(GameObject gameObject, string poolName = null)
        {
            poolName ??= gameObject.name;
            if (gameObject.IsNotNull() && PoolDict.TryGetValue(poolName, out var pool) && pool.Return(gameObject))
            {
#if UNITY_EDITOR
                GameManager.Event.Broadcast(this, PoolEvents.Returned, poolName);
#endif
            }
        }

        public async UniTaskVoid Return(GameObject gameObject, float duration, string poolName = null)
        {
            poolName ??= gameObject.name;
            if (gameObject.IsNotNull() && PoolDict.TryGetValue(poolName, out var pool))
            {
                await UniTask.WaitForSeconds(duration);
                // 等待的这会，可能池/物体被销毁了，物体不会无端销毁，关心池因为场景切换注销的问题
                if (!pool.IsDestroyed && pool.Return(gameObject))
                {
#if UNITY_EDITOR
                    GameManager.Event.Broadcast(this, PoolEvents.Returned, poolName);
#endif
                }
            }
        }
        
        #region 语法糖
        public void Return<T>(T component, string poolName = null) where T : Component 
            => Return(component.gameObject, poolName ?? component.gameObject.name);
        public void Return<T>(T component, float duration, string poolName = null) where T : Component 
            => Return(component.gameObject, duration, poolName ?? component.gameObject.name).Forget();
        #endregion
        
        #endregion
    }
}
