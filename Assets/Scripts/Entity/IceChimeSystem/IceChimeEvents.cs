// IceChimeEvents.cs
// 冰凌事件系统定义
// 职责：定义所有事件参数类型与事件容器，供状态机发射、供外部系统订阅

using System;
using UnityEngine;
using UnityEngine.Events;

// 冰凌状态枚举

public enum IceChimeState
{
    Dormant,    // 沉寂：无光效，不可交互
    PrePlay,    // 预演：光效从底部缓慢升起
    Playable,   // 弹奏：光效到达演奏线，时间窗口开放
    Success,    // 成功：玩家在窗口内正确触发
    Missed,     // 错过：时间窗口结束，玩家未触发
    WrongHit    // 误触：在非 Playable 状态下被玩家触发
}


// 演奏准确度等级

public enum HitAccuracy
{
    Perfect,    // ±perfectWindow 内
    Great,      // ±greatWindow 内
    Good,       // ±goodWindow 内
    Okay,       // 窗口边缘但仍计为成功
    Miss        // 未命中（仅供扩展用，实际由 Missed 状态处理）
}


// 事件参数：所有事件共用一个参数类，按需填充字段

public class IceChimeEventArgs : EventArgs
{
    public int ChimeIndex;              // 冰凌编号
    public IceChimeState PreviousState; // 转换前状态
    public IceChimeState NewState;      // 转换后状态
    public float InteractionTime;       // 事件发生时的 Time.time
    public float TimingAccuracy;        // 准确度 0~1（仅 Success 时有意义）
    public HitAccuracy AccuracyGrade;   // 准确度等级（仅 Success 时有意义）
    public bool IsPerfect;              // 是否完美判定
    public Vector3 InteractionPosition; // 交互发生的世界坐标
    public GameObject Interactor;       // 触发交互的手部对象
}


// 冰凌事件容器：挂载在每个 IceChimeStateMachine 上

[Serializable]
public class IceChimeEvents
{
    // 通用：任意状态变更时触发
    public UnityEvent<IceChimeEventArgs> OnStateChanged = new UnityEvent<IceChimeEventArgs>();

    // 预演开始（Dormant → PrePlay）
    public UnityEvent<IceChimeEventArgs> OnPrePlayStarted = new UnityEvent<IceChimeEventArgs>();

    // 进入可演奏窗口（PrePlay → Playable）
    public UnityEvent<IceChimeEventArgs> OnBecamePlayable = new UnityEvent<IceChimeEventArgs>();

    // 演奏成功（Playable + 玩家触发 → Success）
    public UnityEvent<IceChimeEventArgs> OnSuccess = new UnityEvent<IceChimeEventArgs>();

    // 完美判定（Success 且 IsPerfect == true 时额外触发）
    public UnityEvent<IceChimeEventArgs> OnPerfect = new UnityEvent<IceChimeEventArgs>();

    // 错过时机（Playable → Missed，超时未触发）
    public UnityEvent<IceChimeEventArgs> OnMissed = new UnityEvent<IceChimeEventArgs>();

    // 误触（非 Playable 状态被触发 → WrongHit）
    public UnityEvent<IceChimeEventArgs> OnWrongHit = new UnityEvent<IceChimeEventArgs>();

    // 连击中断（Missed 或 WrongHit 时触发，方便外部重置连击）
    public UnityEvent<IceChimeEventArgs> OnComboBreak = new UnityEvent<IceChimeEventArgs>();

    // 回到沉寂（Success / Missed / WrongHit → Dormant，延迟后触发）
    public UnityEvent<IceChimeEventArgs> OnReturnToDormant = new UnityEvent<IceChimeEventArgs>();
}