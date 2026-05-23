using Core.Event;
// using Cysharp.Threading.Tasks;
// using DG.Tweening;
using UnityEngine;
using Manager;
using System.Collections;

namespace Slingshot
{
    /// <summary>
    /// 酋长鸟。不可交互，负责监听命中事件、计算分数并驱动头顶 UI。
    ///
    /// 事件：
    ///   监听 "DodoBird.HitFruit"  携带参数 SlingshotFruitType
    ///   监听 "DodoBird.HitCombo"  携带参数 int（连击数，≥3 时触发）
    ///
    /// 分值规则（对应设计文档）：
    ///   直接命中基础分   = SlingshotFruitType.GetScore()
    ///   连锁碰撞         = 基础分 × 0.5（调用方传入时已折半，此处直接累加）
    ///   完美摘取连击奖励 = 100分（由 "DodoBird.HitCombo" 事件携带）（没有，乱说的）
    /// </summary>
    public class SlingshotBird : MonoBehaviour
    {
        // ─── 序列化字段 ──────────────────────────────────────────────────────

        [Header("UI 组件")]
        [SerializeField] private SlingshotBirdUI ui;

        // [Header("连击配置")]
        // [Tooltip("多少秒内无命中则重置连击计数。")]
        // [SerializeField] private float comboResetDelay = 3f;
        //
        // [Tooltip("触发完美摘取连击奖励所需的最低连击数。")]
        // [SerializeField] private int   comboThreshold  = 3;
        //
        // [Tooltip("完美摘取连击奖励分值。")]
        // [SerializeField] private int   comboBonus      = 100;

        // ─── 私有状态 ────────────────────────────────────────────────────────

        private int   _totalScore;
        // private int   _comboCount;

        // 用于取消连击重置的 UniTask token
        private System.Threading.CancellationTokenSource _comboCts;

        // ─── 生命周期 ────────────────────────────────────────────────────────

        private void Awake()
        {
            ui = GetComponentInChildren<SlingshotBirdUI>();
        }

        private void OnEnable()
        {
            GameManager.Event.Register<SlingshotFruitType>("DodoBird.HitFruit", OnFruitHit);

            _totalScore = 0;
            // _comboCount = 0;
            ui.ShowScore(0);
        }

        private void OnDisable()
        {
            GameManager.Event.Unregister<SlingshotFruitType>("DodoBird.HitFruit", OnFruitHit);

            // CancelComboReset();
        }

        // ─── 事件回调（EventManager 注册的处理函数）─────────────────────────

        /// <summary>
        /// 命中果实事件回调。
        /// 参数 fruitType 由广播方传入，连锁命中时 score 由广播方折半后传入。
        /// </summary>
        private void OnFruitHit(EventContext<SlingshotFruitType> context)
        {
            SlingshotFruitType fruitType = context.Payload;
            int delta = fruitType.GetScore();
            AddScore(delta, fruitType == SlingshotFruitType.GoldenFruit);
        }

        /// <summary>
        /// 连锁碰撞命中（分值已折半），由广播方直接传入最终分值。
        /// </summary>
        public void OnFruitHitChain(int halfScore)
        {
            AddScore(halfScore, isGolden: false);
        }

        // ─── 私有计分逻辑 ────────────────────────────────────────────────────

        private void AddScore(int delta, bool isGolden)
        {
            _totalScore += delta;

            // 更新连击
            // _comboCount++;
            // RestartComboResetTimer().Forget();

            // 连击达标：触发额外奖励
            // bool isCombo = false;
            // if (_comboCount >= comboThreshold)
            // {
            //     _totalScore += comboBonus;
            //     isCombo      = true;
            //     // 连击后重置，避免每次都触发
            //     _comboCount  = 0;
            // }

            // 驱动 UI
            ui.ShowScore(_totalScore, isGolden);
            ui.ShowDelta(delta, isGolden/*, isCombo*/);

            // 判断是否达到目标分数
            FruitManager fruitManager = FruitManager.Instance;
            if (fruitManager.targetScore <= _totalScore)
            {
                StartCoroutine(SwitchToNextLevel(isGolden,fruitManager));
            }
            // if (isCombo)
            //     ShowComboEffectAsync().Forget();
        }

        IEnumerator SwitchToNextLevel(bool isGolden, FruitManager fruitManager)
        {
            yield return new WaitForSeconds(3f);
            _totalScore = 0;
            //ui.ShowScore(_totalScore, isGolden);
            fruitManager.NextLevel();
        }

        // ─── UniTask：连击重置计时 ───────────────────────────────────────────

        // /// <summary>
        // /// 重启连击重置计时器。
        // /// 每次命中时取消上一个计时，重新等待 comboResetDelay 秒后归零连击数。
        // /// </summary>
        // private async UniTaskVoid RestartComboResetTimer()
        // {
        //     CancelComboReset();
        //     _comboCts = new System.Threading.CancellationTokenSource();
        //
        //     try
        //     {
        //         await UniTask.Delay(
        //             System.TimeSpan.FromSeconds(comboResetDelay),
        //             cancellationToken: _comboCts.Token);
        //
        //         // _comboCount = 0;
        //     }
        //     catch (System.OperationCanceledException)
        //     {
        //         // 被新一次命中取消，属于正常流程，忽略
        //     }
        // }

        // private void CancelComboReset()
        // {
        //     _comboCts?.Cancel();
        //     _comboCts?.Dispose();
        //     _comboCts = null;
        // }

        // // ─── UniTask + DOTween：连击特效 ─────────────────────────────────────
        //
        // /// <summary>
        // /// 连击达标时，酋长鸟自身进行一次欢快的弹跳动画。
        // /// </summary>
        // private async UniTaskVoid ShowComboEffectAsync()
        // {
        //     // 等待本帧 UI 动画开始后再播放鸟自身的动画，避免帧内冲突
        //     await UniTask.Yield(PlayerLoopTiming.LastUpdate);
        //
        //     if (!this) return; // 已被销毁则退出
        //
        //     transform
        //         .DOPunchPosition(Vector3.up * 0.08f, 0.4f, vibrato: 8, elasticity: 0.6f)
        //         .SetLink(gameObject);
        //
        //     transform
        //         .DOPunchRotation(new Vector3(0f, 25f, 0f), 0.4f, vibrato: 8)
        //         .SetLink(gameObject);
        // }
    }
}
