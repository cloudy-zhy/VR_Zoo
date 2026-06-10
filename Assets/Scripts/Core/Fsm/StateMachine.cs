using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Core.Fsm
{
    public class StateMachine<TStateKey>
    {
        private readonly Dictionary<TStateKey, IState> _states = new();
        public IState CurrentState { get; private set; }
        public TStateKey CurrentKey { get; set; }

        /// <summary>
        /// 每次状态切换后触发。参数：(前一个 key, 新的 key)。
        /// 用于简单通知
        /// </summary>
        public Action<TStateKey, TStateKey> OnStateChanged;
        
        /// <summary>
        /// 注册一个状态。重复注册同一 key 会覆盖旧状态并打印警告。
        /// </summary>
        public void AddState(TStateKey key, IState state)
        {
            _states[key] = state;
        }

        /// <summary>
        /// 启动状态机，进入初始状态。必须在所有 AddState() 之后调用。
        /// </summary>
        public void Initialize(TStateKey key)
        {
            CurrentKey = key;
            CurrentState = _states[CurrentKey];
            CurrentState.OnEnter();
        }
        
        /// <summary>
        /// 切换到指定状态。
        /// </summary>
        /// <param name="key">目标状态的 key。</param>
        /// <param name="allowReEnter">
        /// 是否允许重入同一状态。
        /// false（默认）：目标已是当前状态时忽略；
        /// true：强制执行 OnExit + OnEnter，适用于需要重置状态内部数据的场景。
        /// </param>
        public async void ChangeState(TStateKey key, bool allowReEnter = false)
        {
            
            if (!allowReEnter && EqualityComparer<TStateKey>.Default.Equals(CurrentKey, key))
            {
                return;
            }
            CurrentState.OnExit();
            // 状态间切换先等待一帧，防止一帧内切换多个状态
            await UniTask.Yield();
            TStateKey prevKey = CurrentKey;
            CurrentKey = key;
            CurrentState = _states[CurrentKey];
            CurrentState.OnEnter();
            
            OnStateChanged?.Invoke(prevKey, CurrentKey);
        }
        
        /// <summary>驱动当前状态的逻辑帧更新。在宿主的 Update() 中调用。</summary>
        public void OnUpdate()
        {
            CurrentState.OnUpdate();
        }
        
        // ─── 工具方法 ────────────────────────────────────────────────────────
 
        /// <summary>查询当前是否处于指定状态。</summary>
        public bool IsInState(TStateKey key) => EqualityComparer<TStateKey>.Default.Equals(CurrentKey, key);
 
        /// <summary>查询指定 key 是否已注册。</summary>
        public bool HasState(TStateKey key) => _states.ContainsKey(key);
    }
}