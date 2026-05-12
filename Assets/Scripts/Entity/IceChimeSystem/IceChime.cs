using UnityEngine;

[RequireComponent(typeof(IceChimeStateMachine))]
public class IceChime : MonoBehaviour
{
    [Header("基础信息")]
    public int chimeIndex = 0;          // 冰凌编号，由 IceChimeArray 赋值
    public string noteName = "C4";        // 音阶名（调试/UI 用）
    public AudioClip chimeSound;             // 马林巴琴音效，注入给状态机

    // 状态机引用（只读，由本类在 Awake 获取）
    public IceChimeStateMachine StateMachine { get; private set; }

    // ── 简化的交互代理，让外部代码不必关心状态机 ──
    public void Interact(GameObject interactor = null)
        => StateMachine.OnPlayerInteraction(interactor);

    public void StartPrePlay()
        => StateMachine.StartPrePlay();

    public IceChimeState CurrentState
        => StateMachine.CurrentState;

    public IceChimeEvents Events
        => StateMachine.Events;

    private void Awake()
    {
        StateMachine = GetComponent<IceChimeStateMachine>();
    }
}
