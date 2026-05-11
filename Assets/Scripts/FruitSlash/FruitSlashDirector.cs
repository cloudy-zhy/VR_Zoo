using System.Collections;
using System.Collections.Generic;
using Core.Event;
using Manager;
using UnityEngine;

namespace FruitSlash
{
    /// <summary>
    /// 切水果小游戏总控：阶段、波次、动态难度、特殊果实和完成事件。
    /// </summary>
    public class FruitSlashDirector : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private FruitSlashScoreController scoreController;
        [SerializeField] private List<FruitSlashBlade> blades = new();
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private Transform targetCenter;
        [SerializeField] private Animator longNeckAnimator;

        [Header("果实配置")]
        [SerializeField] private List<FruitSlashFruitConfigSO> fruitConfigs = new();
        [SerializeField] private string placeholderFruitPoolKey = "FruitSlash.PlaceholderFruit";
        [SerializeField] private string placeholderHalfPoolKey = "FruitSlash.PlaceholderHalf";

        [Header("节奏")]
        [Tooltip("果实飞行时间倍率。数值越大飞得越慢，推荐过快时调到 1.25-1.6。")]
        [SerializeField] private float flightTimeMultiplier = 1f;
        [SerializeField] private int tutorialEndCutCount = 5;
        [SerializeField] private int advancedEndCutCount = 18;
        [SerializeField] private int rainbowTriggerCutCount = 30;
        [SerializeField] private int rareInterval = 20;

        [Header("范围")]
        [SerializeField] private float tutorialHalfWidth = 0f;
        [SerializeField] private float advancedHalfWidth = 1.2f;
        [SerializeField] private float stableHalfWidth = 1.5f;
        [SerializeField] private float targetHeight = 1.25f;

        [Header("动态难度")]
        [SerializeField] private float missWindow = 10f;
        [SerializeField] private int missesToSlowDown = 3;
        [SerializeField] private int emptyWavesToSlowDown = 2;
        [SerializeField] private int successCutsToRecover = 3;

        [Header("调试")]
        [SerializeField] private bool debugLog;

        public FruitSlashStageType CurrentStage { get; private set; }
        public int CutFruitCount { get; private set; }
        public bool IsRunning { get; private set; }

        private readonly Dictionary<FruitSlashFruitType, FruitSlashFruitConfigSO> _fruitConfigMap = new();
        private readonly List<FruitSlashFruit> _activeFruits = new();
        private readonly Queue<float> _recentMissTimes = new();
        private Coroutine _spawnRoutine;
        private int _ordinaryCutsSinceRare;
        private int _consecutiveMisses;
        private int _consecutiveEmptyWaves;
        private int _consecutiveSuccessCuts;
        private int _currentWaveCuts;
        private bool _slowDownNextWave;
        private bool _pendingRareFruit;
        private bool _rainbowSpawned;
        private bool _completed;

        private void Awake()
        {
            if (scoreController == null)
                scoreController = GetComponentInChildren<FruitSlashScoreController>();
            BuildFruitConfigMap();
        }

        private void OnEnable()
        {
            GameManager.Event.Register<FruitSlashFruit>(FruitSlashEvents.InternalFruitCut, OnFruitCut);
            GameManager.Event.Register<FruitSlashFruit>(FruitSlashEvents.InternalFruitMissed, OnFruitMissed);
        }

        private void OnDisable()
        {
            GameManager.Event.Unregister<FruitSlashFruit>(FruitSlashEvents.InternalFruitCut, OnFruitCut);
            GameManager.Event.Unregister<FruitSlashFruit>(FruitSlashEvents.InternalFruitMissed, OnFruitMissed);
        }

        /// <summary>
        /// 开始小游戏。
        /// </summary>
        [ContextMenu("StartGame")]
        public void StartGame()
        {
            if (IsRunning)
                return;
            IsRunning = true;
            foreach (var blade in blades)
                blade.gameObject.SetActive(true);

            _completed = false;
            _rainbowSpawned = false;
            _pendingRareFruit = false;
            CutFruitCount = 0;
            _ordinaryCutsSinceRare = 0;
            _consecutiveMisses = 0;
            _consecutiveEmptyWaves = 0;
            _consecutiveSuccessCuts = 0;
            _slowDownNextWave = false;
            _recentMissTimes.Clear();
            CurrentStage = FruitSlashStageType.Tutorial;

            if (scoreController != null)
                scoreController.ResetScore();
            GameManager.Event.Broadcast(FruitSlashEvents.Started, this);
            if (debugLog)
                Debug.Log("[FruitSlashDirector] Started");
            BroadcastStageChanged(CurrentStage);

            if (_spawnRoutine != null)
                StopCoroutine(_spawnRoutine);
            _spawnRoutine = StartCoroutine(SpawnLoop());
        }

        /// <summary>
        /// 停止小游戏并停止继续生成果实。
        /// </summary>
        [ContextMenu("StopGame")]
        public void StopGame()
        {
            IsRunning = false;
            foreach (var blade in blades)
                blade.gameObject.SetActive(false);
            if (_spawnRoutine != null)
            {
                StopCoroutine(_spawnRoutine);
                _spawnRoutine = null;
            }
        }

        /// <summary>
        /// 立即生成下一波，供按钮或调试调用。
        /// </summary>
        public void SpawnNextWave()
        {
            if (!IsRunning || _completed)
                return;

            if (debugLog)
                Debug.Log("[FruitSlashDirector] SpawnNextWave requested");
            SpawnWave();
        }

        /// <summary>
        /// 强制生成七彩巨大果串。
        /// </summary>
        public void ForceSpawnRainbowBunch()
        {
            if (_completed || _rainbowSpawned)
                return;

            _rainbowSpawned = true;
            if (debugLog)
                Debug.Log("[FruitSlashDirector] Spawn rainbow bunch");
            SpawnFruit(FruitSlashFruitType.RainbowBunch, false, true);
        }

        /// <summary>
        /// 果实切中回调。
        /// </summary>
        private void OnFruitCut(EventContext<FruitSlashFruit> context)
        {
            FruitSlashFruit fruit = context.Payload;

            if (fruit == null || _completed)
                return;

            int sameSwingCutCount = fruit.LastSameSwingCutCount;

            _activeFruits.Remove(fruit);
            _currentWaveCuts += 1;
            _consecutiveMisses = 0;
            _consecutiveSuccessCuts += 1;

            if (fruit.IsRainbowBunch)
            {
                if (scoreController != null)
                    scoreController.CompleteRainbowBunch(fruit.RainbowReward);
                if (longNeckAnimator != null)
                    longNeckAnimator.SetTrigger("Cheer");
                if (debugLog)
                    Debug.Log($"[FruitSlashDirector] Rainbow completed, totalScore={(scoreController != null ? scoreController.TotalScore : 0)}");
                _completed = true;
                StopGame();
                GameManager.Event.Broadcast(FruitSlashEvents.Completed, scoreController != null ? scoreController.TotalScore : 0);
                return;
            }

            CutFruitCount += 1;
            if (scoreController != null)
                scoreController.AddFruitScore(fruit, sameSwingCutCount);
            GameManager.Event.Broadcast(FruitSlashEvents.FruitCut, fruit);
            if (debugLog)
                Debug.Log($"[FruitSlashDirector] Fruit cut: type={fruit.FruitType}, count={CutFruitCount}, sameSwing={sameSwingCutCount}, stage={CurrentStage}");

            if (fruit.IsRare)
            {
                EmpowerBlades(5f);
            }
            else
            {
                _ordinaryCutsSinceRare += 1;
                if (_ordinaryCutsSinceRare >= rareInterval)
                {
                    _ordinaryCutsSinceRare = 0;
                    _pendingRareFruit = true;
                }
            }

            if (_slowDownNextWave && _consecutiveSuccessCuts >= successCutsToRecover)
            {
                _slowDownNextWave = false;
                _consecutiveSuccessCuts = 0;
            }

            UpdateStage();
        }

        /// <summary>
        /// 果实完整落地回调。
        /// </summary>
        private void OnFruitMissed(EventContext<FruitSlashFruit> context)
        {
            FruitSlashFruit fruit = context.Payload;

            if (fruit != null)
                _activeFruits.Remove(fruit);

            float now = Time.time;
            _recentMissTimes.Enqueue(now);
            while (_recentMissTimes.Count > 0 && now - _recentMissTimes.Peek() > missWindow)
                _recentMissTimes.Dequeue();

            _consecutiveMisses += 1;
            _consecutiveSuccessCuts = 0;

            if (_consecutiveMisses >= missesToSlowDown || _recentMissTimes.Count >= missesToSlowDown)
                RequestSlowDown();

            if (debugLog)
                Debug.Log($"[FruitSlashDirector] Fruit missed: consecutiveMisses={_consecutiveMisses}, recentMisses={_recentMissTimes.Count}");
        }

        private IEnumerator SpawnLoop()
        {
            while (IsRunning && !_completed)
            {
                _currentWaveCuts = 0;
                SpawnWave();

                float interval = GetWaveInterval(CurrentStage);
                yield return new WaitForSeconds(interval);

                if (_currentWaveCuts == 0)
                {
                    _consecutiveEmptyWaves += 1;
                    if (_consecutiveEmptyWaves >= emptyWavesToSlowDown)
                        RequestSlowDown();
                }
                else
                {
                    _consecutiveEmptyWaves = 0;
                }
            }
        }

        private void SpawnWave()
        {
            if (_rainbowSpawned || CutFruitCount >= rainbowTriggerCutCount)
            {
                ForceSpawnRainbowBunch();
                return;
            }

            int fruitCount = GetWaveFruitCount(CurrentStage);
            bool slowWave = _slowDownNextWave;
            if (slowWave)
                fruitCount = 1;

            if (debugLog)
                Debug.Log($"[FruitSlashDirector] Spawn wave: stage={CurrentStage}, fruitCount={fruitCount}, slowWave={slowWave}");

            for (int i = 0; i < fruitCount; i++)
            {
                FruitSlashFruitType type = PickWaveFruitType(CurrentStage);
                bool fastTrajectory = type == FruitSlashFruitType.Fast;
                SpawnFruit(type, fastTrajectory, slowWave);
            }

            if (longNeckAnimator != null)
                longNeckAnimator.SetTrigger("Throw");
        }

        private void SpawnFruit(FruitSlashFruitType type, bool fastTrajectory, bool slowWave)
        {
            FruitSlashFruitConfigSO config = GetConfig(type);
            Vector3 start = spawnPoint != null ? spawnPoint.position : transform.position + Vector3.forward * 2f + Vector3.up * 1.4f;
            Vector3 target = GetTargetPosition(slowWave);
            float flightTime = GetFlightTime(CurrentStage, slowWave, fastTrajectory, config);
            Vector3 velocity = CalculateBallisticVelocity(start, target, flightTime);

            string poolName = config != null && !string.IsNullOrEmpty(config.fruitPoolKey)
                ? config.fruitPoolKey
                : placeholderFruitPoolKey;
            FruitSlashFruit fruit = GameManager.Pool.Rent<FruitSlashFruit>(poolName, start, Quaternion.identity);
            if (fruit == null)
                return;

            fruit.Initialize(
                config,
                type,
                type == FruitSlashFruitType.Rare,
                type == FruitSlashFruitType.Fast,
                type == FruitSlashFruitType.RainbowBunch,
                velocity,
                placeholderHalfPoolKey
            );
            _activeFruits.Add(fruit);
        }

        private Vector3 GetTargetPosition(bool slowWave)
        {
            Vector3 center = targetCenter != null ? targetCenter.position : transform.position + Vector3.forward * 0.8f + Vector3.up * targetHeight;
            float halfWidth = GetStageHalfWidth(CurrentStage);
            if (slowWave)
                halfWidth *= 0.3f;

            center.y = targetHeight;
            center.x += Random.Range(-halfWidth, halfWidth);
            center.z += Random.Range(-0.25f, 0.25f);
            return center;
        }

        private Vector3 CalculateBallisticVelocity(Vector3 start, Vector3 target, float flightTime)
        {
            flightTime = Mathf.Max(0.2f, flightTime);
            Vector3 gravity = Physics.gravity;
            return (target - start - 0.5f * gravity * flightTime * flightTime) / flightTime;
        }

        private FruitSlashFruitType PickWaveFruitType(FruitSlashStageType stage)
        {
            if (_pendingRareFruit)
            {
                _pendingRareFruit = false;
                return FruitSlashFruitType.Rare;
            }

            if (stage == FruitSlashStageType.Tutorial)
                return FruitSlashFruitType.FlameEgg;

            if (stage == FruitSlashStageType.Stable && Random.value < 0.12f)
                return FruitSlashFruitType.Fast;

            int index = Random.Range(0, 3);
            switch (index)
            {
                case 1:
                    return FruitSlashFruitType.GoldenFan;
                case 2:
                    return FruitSlashFruitType.ConeFruit;
                case 0:
                default:
                    return FruitSlashFruitType.FlameEgg;
            }
        }

        private int GetWaveFruitCount(FruitSlashStageType stage)
        {
            switch (stage)
            {
                case FruitSlashStageType.Advanced:
                    return Random.value < 0.35f ? 2 : 1;
                case FruitSlashStageType.Stable:
                    return 2;
                case FruitSlashStageType.Tutorial:
                default:
                    return 1;
            }
        }

        private float GetWaveInterval(FruitSlashStageType stage)
        {
            switch (stage)
            {
                case FruitSlashStageType.Advanced:
                    return 1f;
                case FruitSlashStageType.Stable:
                    return 0.8f;
                case FruitSlashStageType.Tutorial:
                default:
                    return 1.25f;
            }
        }

        private float GetFlightTime(FruitSlashStageType stage, bool slowWave, bool fast, FruitSlashFruitConfigSO config)
        {
            float time;
            if (config != null)
            {
                time = Random.Range(config.flightTimeRange.x, config.flightTimeRange.y);
            }
            else
            {
                switch (stage)
                {
                    case FruitSlashStageType.Advanced:
                        time = Random.Range(1.55f, 1.95f);
                        break;
                    case FruitSlashStageType.Stable:
                        time = Random.Range(1.35f, 1.75f);
                        break;
                    case FruitSlashStageType.Tutorial:
                    default:
                        time = Random.Range(2.2f, 2.7f);
                        break;
                }
            }

            if (fast)
                time *= 0.82f;
            if (slowWave)
                time *= 1.35f;

            return time * Mathf.Max(0.2f, flightTimeMultiplier);
        }

        private float GetStageHalfWidth(FruitSlashStageType stage)
        {
            switch (stage)
            {
                case FruitSlashStageType.Advanced:
                    return advancedHalfWidth;
                case FruitSlashStageType.Stable:
                    return stableHalfWidth;
                case FruitSlashStageType.Tutorial:
                default:
                    return tutorialHalfWidth;
            }
        }

        private void BuildFruitConfigMap()
        {
            _fruitConfigMap.Clear();
            for (int i = 0; i < fruitConfigs.Count; i++)
            {
                FruitSlashFruitConfigSO config = fruitConfigs[i];
                if (config == null)
                    continue;

                if (_fruitConfigMap.ContainsKey(config.fruitType))
                {
                    Debug.LogWarning($"[FruitSlashDirector] Duplicate FruitConfig for {config.fruitType}; later entry is ignored.");
                    continue;
                }

                _fruitConfigMap.Add(config.fruitType, config);
            }
        }

        private FruitSlashFruitConfigSO GetConfig(FruitSlashFruitType type)
        {
            if (_fruitConfigMap.Count != fruitConfigs.Count)
                BuildFruitConfigMap();

            return _fruitConfigMap.TryGetValue(type, out FruitSlashFruitConfigSO config)
                ? config
                : null;
        }

        private void UpdateStage()
        {
            FruitSlashStageType nextStage;
            if (CutFruitCount <= tutorialEndCutCount)
                nextStage = FruitSlashStageType.Tutorial;
            else if (CutFruitCount <= advancedEndCutCount)
                nextStage = FruitSlashStageType.Advanced;
            else
                nextStage = FruitSlashStageType.Stable;

            if (nextStage == CurrentStage)
                return;

            CurrentStage = nextStage;
            BroadcastStageChanged(CurrentStage);
        }

        private void BroadcastStageChanged(FruitSlashStageType stage)
        {
            if (debugLog)
                Debug.Log($"[FruitSlashDirector] Stage changed: {stage}");
            GameManager.Event.Broadcast(FruitSlashEvents.StageChanged, stage);
        }

        private void RequestSlowDown()
        {
            _slowDownNextWave = true;
            if (longNeckAnimator != null)
                longNeckAnimator.SetTrigger("ScratchHead");
            if (debugLog)
                Debug.Log("[FruitSlashDirector] Slow down next wave");
        }

        private void EmpowerBlades(float duration)
        {
            for (int i = 0; i < blades.Count; i++)
            {
                if (blades[i] != null)
                    blades[i].SetEmpowered(true, duration);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Vector3 spawn = spawnPoint != null ? spawnPoint.position : transform.position + Vector3.forward * 2f + Vector3.up * 1.4f;
            Gizmos.DrawWireSphere(spawn, 0.15f);

            Gizmos.color = Color.yellow;
            Vector3 target = targetCenter != null ? targetCenter.position : transform.position + Vector3.forward * 0.8f + Vector3.up * targetHeight;
            Gizmos.DrawWireCube(target, new Vector3(stableHalfWidth * 2f, 0.2f, 0.5f));
        }
#endif
    }
}
