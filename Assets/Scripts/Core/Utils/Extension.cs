using Manager;
using UnityEngine;

namespace Core.Utils
{
    public static class Extension
    {
        public static bool IsNull(this GameObject obj)
        {
            return ReferenceEquals(obj, null);
        }

        public static bool IsNotNull(this GameObject obj)
        {
            return !ReferenceEquals(obj, null);
        }

        public static void Return(this GameObject obj, string poolName = null)
        {
            GameManager.Pool.Return(poolName ?? obj.name, obj);
        }

        public static void Return<T>(this T component, string poolName = null) where T : Component
        {
            GameManager.Pool.Return(poolName ?? component.gameObject.name, component);
        }
        
        public static void Return(this GameObject obj, float duration, string poolName = null)
        {
            GameManager.Pool.Return(poolName ?? obj.name, obj, duration).Forget();
        }

        public static void Return<T>(this T component, float duration, string poolName = null) where T : Component
        {
            GameManager.Pool.Return(poolName ?? component.gameObject.name, component, duration);
        }
    }
}