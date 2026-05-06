using UnityEngine;
using UnityEngine.Events;

public class Game2EventManager : MonoBehaviour
{
    public static Game2EventManager Instance { get; private set; }

    public UnityEvent OnGameStarted = new UnityEvent();
    public UnityEvent OnGameCompleted = new UnityEvent();

    public UnityEvent<int> OnComboChanged = new UnityEvent<int>();
    public UnityEvent<int> OnComboMilestone = new UnityEvent<int>();
    public UnityEvent<IceChimeEventArgs> OnComboBreak = new UnityEvent<IceChimeEventArgs>();

    private void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else Destroy(gameObject);
    }
}