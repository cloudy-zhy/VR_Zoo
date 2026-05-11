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
            GameManager.Event.Broadcast(PoolEvents.Registered, poolName);
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
                GameManager.Event.Broadcast(PoolEvents.Unregistered, poolName);
            }
        }

        public void Unregister()
        {
            foreach (var pool in PoolDict.Values)
            {
                pool.Destroy();
            }
            PoolDict.Clear();
            GameManager.Event.Broadcast(PoolEvents.Cleared);
        }

        #endregion
        
        #region Rent
        
        public GameObject Rent(string poolName,
            Vector3? position = null, Quaternion? rotation = null, Transform parent = null)
        {
            if (PoolDict.TryGetValue(poolName, out var pool) && pool.TryRent(out var gameObject, parent))
            {
                if (position.HasValue) gameObject.transform.position = position.Value;
                if (rotation.HasValue) gameObject.transform.rotation = rotation.Value;
                GameManager.Event.Broadcast(PoolEvents.Rented, poolName);
                return gameObject;
            }
            return null;
        }

        public bool TryRent(string poolName, out GameObject gameObject,
            Vector3? position = null, Quaternion? rotation = null, Transform parent = null)
        {
            return (gameObject = Rent(poolName, position, rotation, parent)).IsNotNull();
        }

        public T Rent<T>(string poolName,
            Vector3? position = null, Quaternion? rotation = null, Transform parent = null) where T : Component
        {
            if (TryRent(poolName, out var gameObject, position, rotation, parent) && gameObject.TryGetComponent(out T component))
            {
                return component;
            }
            return null;
        }

        public bool TryRent<T>(string poolName, out T component,
            Vector3? position = null, Quaternion? rotation = null, Transform parent = null) where T : Component
        {
            return (component = Rent<T>(poolName, position, rotation, parent)) != null;
        }
        
        public async UniTaskVoid Rent(string poolName, float duration,
            Vector3? position = null, Quaternion? rotation = null, Transform parent = null)
        {
            if (PoolDict.TryGetValue(poolName, out var pool) && pool.TryRent(out var gameObject, parent))
            {
                if (position.HasValue) gameObject.transform.position = position.Value;
                if (rotation.HasValue) gameObject.transform.rotation = rotation.Value;
                GameManager.Event.Broadcast(PoolEvents.Rented, poolName);
                await UniTask.WaitForSeconds(duration);
                // 等待的这会，可能池/物体被销毁了，物体不会无端销毁，关心池因为场景切换注销的问题
                if (!pool.IsDestroyed)
                {
                    if (pool.Return(gameObject))
                        GameManager.Event.Broadcast(PoolEvents.Returned, poolName);
                }
            }
        }
        
        #endregion

        #region Return

        public void Return(string poolName, GameObject gameObject)
        {
            if (gameObject.IsNotNull() && PoolDict.TryGetValue(poolName, out var pool))
            {
                if (pool.Return(gameObject))
                    GameManager.Event.Broadcast(PoolEvents.Returned, poolName);
            }
        }

        public async UniTaskVoid Return(string poolName, GameObject gameObject, float duration)
        {
            if (gameObject.IsNotNull() && PoolDict.TryGetValue(poolName, out var pool))
            {
                await UniTask.WaitForSeconds(duration);
                // 等待的这会，可能池/物体被销毁了，物体不会无端销毁，关心池因为场景切换注销的问题
                if (!pool.IsDestroyed)
                {
                    if (pool.Return(gameObject))
                        GameManager.Event.Broadcast(PoolEvents.Returned, poolName);
                }
            }
        }
        
        public void Return<T>(string poolName, T component) where T : Component 
            => Return(poolName, component.gameObject);
        public void Return<T>(string poolName, T component, float duration) where T : Component 
            => Return(poolName, component.gameObject, duration).Forget();
        public void Return(GameObject gameObject) 
            => Return(gameObject.name, gameObject);
        public void Return(GameObject gameObject, float duration) 
            => Return(gameObject.name, gameObject, duration).Forget();
        public void Return<T>(T component) where T : Component 
            => Return(component.gameObject.name, component.gameObject);
        public void Return<T>(T component, float duration) where T : Component
            => Return(component.gameObject.name, component, duration);

        #endregion
    }
}
