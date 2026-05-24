using UnityEngine;

/// <summary>
/// 持久化单例
/// </summary>
/// <typeparam name="T"></typeparam>
public class PersistentSingleton<T> : MonoBehaviour where T: MonoBehaviour
{
    public static T Instance { get; private set; } 

    protected virtual void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this as T;
        DontDestroyOnLoad(gameObject);
    }
}