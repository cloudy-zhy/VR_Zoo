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
        
        public static bool IsNull(this Component obj)
        {
            return ReferenceEquals(obj, null);
        }

        public static bool IsNotNull(this Component obj)
        {
            return !ReferenceEquals(obj, null);
        }

        public static void Return(this GameObject obj, string poolName = null)
        {
            if (obj.IsNull())
                return;
            GameManager.Pool.Return(obj, poolName ?? obj.name);
        }

        public static void Return<T>(this T component, string poolName = null) where T : Component
        {
            if (component.IsNull())
                return;
            GameManager.Pool.Return(component.gameObject, poolName ?? component.gameObject.name);
        }
        
        public static void Return(this GameObject obj, float duration, string poolName = null)
        {
            if (obj.IsNull())
                return;
            GameManager.Pool.Return(obj, duration, poolName ?? obj.name).Forget();
        }

        public static void Return<T>(this T component, float duration, string poolName = null) where T : Component
        {
            if (component.IsNull())
                return;
            GameManager.Pool.Return(component.gameObject, duration, poolName ?? component.gameObject.name).Forget();
        }
    }
}
