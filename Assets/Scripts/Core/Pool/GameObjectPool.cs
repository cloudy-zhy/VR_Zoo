using System.Collections.Generic;
using System.Linq;
using Core.Utils;
using Cysharp.Threading.Tasks;
using Manager;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Core.Pool
{
    public class GameObjectPool
    {
        public bool IsDestroyed { get; private set; }
        public string PoolName { get; private set; }
        public Transform RootTransform { get; private set; }
        public Queue<GameObject> PoolQueue { get; } = new();
        public HashSet<GameObject> RentSet { get; } = new();
        private readonly Queue<GameObject> _rentQueue = new();
        // 对应的prefab，初始化后不再变化
        public GameObject Prefab { get; private set; }
        // 池创建出的对象总数，仅在 Expand 增加，Destroy 后失效
        public int Count { get; private set; }
        // 最大上限，初始化后不再变化
        public int Capacity { get; private set; }
        // 单次扩容/缩容步数，初始化后不再变化
        public int Step { get; private set; }

        public async UniTask Initialize(string poolName, Transform upperRootTransform, 
            int step = 8, int capacity = -1, int prewarm = 0, GameObject prefab = null)
        {
            // 检查输入合法性
            if (prefab.IsNull())
                Prefab = await GameManager.AssetLoader.LoadPrefab(poolName);
            else
                Prefab = prefab;
            if (capacity != -1 )
            {
                // 扩张步长最大为容量上限
                if (step > capacity)
                    step = capacity;
                // 预热个数最大为容量上限
                if (prewarm > capacity)
                    prewarm = capacity;
            }
            // 创建存放父级
            GameObject gameObject = new GameObject();
            gameObject.transform.SetParent(upperRootTransform);
            // 重置
            PoolName = poolName;
            RootTransform = gameObject.transform;
            RootTransform.name = $"[Pool] {PoolName}";
            Step = step;
            Capacity = capacity;
            // 预热
            Expand(prewarm);
        }

        private void Expand(int target)
        {
            // 不能超过最大上限
            if (Capacity != -1 && Capacity <= Count)
                return;
            // 需要增加的个数，最小为0，最大为剩余可增加的个数
            int count = Capacity == -1
                ? Mathf.Max(0, target)
                : Mathf.Clamp(target, 0, Capacity - Count);
            for (int i = 0; i < count; i++)
            {
                GameObject gameObject = Object.Instantiate(Prefab, RootTransform);
                gameObject.name = PoolName;
                Push(gameObject);
                Count++;
            }
        }

        private void Expand()
        {
            Expand(Step);
        }

        private void Push(GameObject gameObject)
        {
            PoolQueue.Enqueue(gameObject);
            gameObject.transform.SetParent(RootTransform);
            gameObject.SetActive(false);
        }

        public GameObject Rent(PoolParentOverride parent = default)
        {
            // 如果超限了，还在借，便尝试回收一些
            if (PoolQueue.Count == 0 && Capacity != -1 && Count >= Capacity)
                Recycle();
            if (PoolQueue.Count == 0)
                Expand();
            if (PoolQueue.Count == 0)
                return null;
            var gameObject = PoolQueue.Dequeue();
            gameObject.SetActive(true);
            // 未指定 parent 时保留池默认层级；显式传 null 时才脱离父级。
            if (parent.IsSpecified)
            {
                gameObject.transform.SetParent(parent.Value);
                if (parent.Value == null)
                {
                    SceneManager.MoveGameObjectToScene(gameObject, SceneManager.GetActiveScene());
                }
            }
            RentSet.Add(gameObject);
            _rentQueue.Enqueue(gameObject);
            return gameObject;
        }

        public bool Return(GameObject gameObject)
        {
            if (gameObject.IsNull() || !RentSet.Remove(gameObject))
                return false;
            Push(gameObject);
            return true;
        }

        public void Recycle()
        {
            int count = 0;
            while (count < Step && _rentQueue.Count > 0)
            {
                var gameObject = _rentQueue.Dequeue();
                if (RentSet.Contains(gameObject))
                {
                    Return(gameObject);
                    count++;
                }
            }
        }

        public bool TryRent(out GameObject gameObject, PoolParentOverride parent = default)
        {
            return (gameObject = Rent(parent)).IsNotNull();
        }
        
        public void Destroy()
        {
            IsDestroyed = true;
            foreach (var gameObject in RentSet.Where(gameObject => gameObject != null))
            {
                Object.Destroy(gameObject);
            }
            // if (RootTransform.IsNotNull())
            //     Object.Destroy(RootTransform.gameObject);
            PoolQueue.Clear();
            RentSet.Clear();
            _rentQueue.Clear();
            RootTransform = null;
            Prefab = null;
        }
    }
}
