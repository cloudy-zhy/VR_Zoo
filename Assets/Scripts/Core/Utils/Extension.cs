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
    }
}