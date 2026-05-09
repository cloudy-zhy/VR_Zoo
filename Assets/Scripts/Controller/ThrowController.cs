using System.Collections;
using System.Collections.Generic;
using Manager;
using UnityEngine;
using Core.Event;
using Core.Pool;
using Entity.Pterosaur;

namespace Controller
{
    public enum ThrowStage
    {
        Tutorial,
        Advanced,
        Stable,
        Rainbow
    }

    public static class ThrowStageExtensions
    {
        public static int GetRequestCountByStage(this ThrowStage stage) => stage switch
        {
            ThrowStage.Tutorial => Random.Range(1, 2), // 教学期先一个一个来
            ThrowStage.Advanced => Random.Range(2, 4), // 2-3 个
            ThrowStage.Stable => 3,
            ThrowStage.Rainbow => ThrowController.RainbowGiftSpawned ? 0 : 1,
            _ => 1,
        };
    }
    
    public class ThrowController : MonoBehaviour
    {
        #region SerializedFieldVariables

        [Header("Pterosaur")] 
        [SerializeField] private Transform pterosaurParent;
        [SerializeField] private string pterosaurGiftPrefabKey = "PterosaurGift";

        [Header("Score")]
        [SerializeField] private int scoreToAdvanced = 100;
        [SerializeField] private int scoreToStable = 400;
        [SerializeField] private int scoreToRainbow = 800;

        [Header("Tutorial")]
        [SerializeField] private float tutorialThrowInterval = 3f;

        [Header("Advanced")]
        [SerializeField] private float advancedThrowInterval = 2.2f;

        [Header("Stable")]
        [SerializeField] private float stableThrowInterval = 1.6f;

        [Header("Dynamic Difficulty")]
        [SerializeField] private int missTriggerCount = 3;
        [SerializeField] private int recoverCatchCount = 3;
        [SerializeField] private float missCheckWindow = 10f;
        [SerializeField] private float comfortSlowMultiplier = 1.3f;
        [SerializeField] private float comfortFrequencyMultiplier = 1.3f;

        #endregion

        #region RuntimeState

        private ThrowStage _currentStage = ThrowStage.Tutorial;

        private int _score;
        private int _combo;
        private int _normalGiftCounter;

        private bool _isRunning;
        private bool _isComfortMode;
        public static bool RainbowGiftSpawned;

        private int _continuousCatchCount;
        private readonly Queue<float> _recentMissTimes = new();
        private Pterosaur[] _pterosaurs;

        private Coroutine _throwLoopCoroutine;

        #endregion

        #region Lifecycle

        private void Start()
        {
            GameManager.Event.Register<Vector3>("Pterosaur.Throw", OnPterosaurThrow);
            GameManager.Event.Register<PterosaurGiftType>("Gift.Caught", OnGiftCaught);
            GameManager.Event.Register<PterosaurGiftType>("Gift.Missed", OnGiftMissed);
            
            _pterosaurs = pterosaurParent.GetComponentsInChildren<Pterosaur>(true);
        }

        private void OnDestroy()
        {
            GameManager.Event.Unregister<Vector3>("Pterosaur.Throw", OnPterosaurThrow);
            GameManager.Event.Unregister<PterosaurGiftType>("Gift.Caught", OnGiftCaught);
            GameManager.Event.Unregister<PterosaurGiftType>("Gift.Missed", OnGiftMissed);
        }

        #endregion

        #region Public Methods

        [ContextMenu("StartThrowGame")]
        public void StartThrowGame()
        {
            if (_isRunning)
                return;

            _isRunning = true;
            _currentStage = ThrowStage.Tutorial;

            _score = 0;
            _combo = 0;
            _normalGiftCounter = 0;
            RainbowGiftSpawned = false;

            _throwLoopCoroutine = StartCoroutine(ThrowLoop());
        }

        [ContextMenu("StopThrowGame")]
        public void StopThrowGame()
        {
            _isRunning = false;

            if (_throwLoopCoroutine != null)
            {
                StopCoroutine(_throwLoopCoroutine);
                _throwLoopCoroutine = null;
            }
        }

        #endregion

        #region Throw Loop

        private IEnumerator ThrowLoop()
        {
            while (_isRunning)
            {
                UpdateStage();

                int requestCount = _currentStage.GetRequestCountByStage();

                for (int i = 0; i < requestCount; i++)
                {
                    RequestRandomPterosaurThrow();

                    // 同一轮多个包裹之间给一点点间隔，避免完全重叠
                    yield return new WaitForSeconds(Random.Range(0.1f, 0.35f));
                }

                yield return new WaitForSeconds(GetThrowInterval());
            }
        }

        private void RequestRandomPterosaurThrow()
        {
            if (_pterosaurs == null || _pterosaurs.Length == 0)
                return;

            Pterosaur pterosaur = _pterosaurs[Random.Range(0, _pterosaurs.Length)];

            pterosaur?.AddRequest();
        }

        #endregion

        #region Event Handlers

        private void OnPterosaurThrow(EventContext<Vector3> context)
        {
            if (!_isRunning)
                return;

            Vector3 throwPosition = context.Payload;
            PterosaurGiftType type = DecidePterosaurGiftType();

            PterosaurGift gift = PoolManager.I.Get<PterosaurGift>(
                pterosaurGiftPrefabKey,
                throwPosition
            );
            
            gift.Initialize(type, GetAirDrag(type), GetInitialVelocity(type));
        }

        private void OnGiftCaught(EventContext<PterosaurGiftType> context)
        {
            if (!_isRunning)
                return;

            PterosaurGiftType type = context.Payload;
            _continuousCatchCount++;
            _combo++;

            int gainedScore = CalculateScore(type);
            _score += gainedScore;

            // 漏接后的恢复机制
            if (_isComfortMode && _continuousCatchCount >= recoverCatchCount)
            {
                ExitComfortMode();
            }

            // TODO:
            // GameManager.Event.Broadcast("Score.Add", gainedScore);
            // GameManager.Event.Broadcast("Combo.Update", combo);
            // GameManager.Event.Broadcast("Scene.Atmosphere.Update", score);

            if (type == PterosaurGiftType.Rainbow)
            {
                StopThrowGame();

                // TODO:
                // GameManager.Event.Broadcast("ThrowGame.Complete");
                // GameManager.Event.Broadcast("NextMiniGame.Start", "FruitCut");
            }
        }

        private void OnGiftMissed(EventContext<PterosaurGiftType> context)
        {
            if (!_isRunning)
                return;

            _combo = 0;
            _continuousCatchCount = 0;

            _recentMissTimes.Enqueue(Time.time);

            while (_recentMissTimes.Count > 0 && Time.time - _recentMissTimes.Peek() > missCheckWindow)
            {
                _recentMissTimes.Dequeue();
            }

            if (!_isComfortMode && _recentMissTimes.Count >= missTriggerCount)
            {
                EnterComfortMode();
            }

            // TODO:
            // GameManager.Event.Broadcast("Combo.Break");
            // GameManager.Event.Broadcast("Gift.Pile.Add", gift.transform.position);
        }

        #endregion

        #region Stage

        private void UpdateStage()
        {
            if (RainbowGiftSpawned)
            {
                _currentStage = ThrowStage.Rainbow;
                return;
            }

            if (_score >= scoreToRainbow)
            {
                _currentStage = ThrowStage.Rainbow;
                return;
            }

            if (_score >= scoreToStable)
            {
                _currentStage = ThrowStage.Stable;
                return;
            }

            if (_score >= scoreToAdvanced)
            {
                _currentStage = ThrowStage.Advanced;
                return;
            }

            _currentStage = ThrowStage.Tutorial;
        }
        
        private float GetAirDrag(PterosaurGiftType type)
        {
            float airDrag = _currentStage switch
            {
                ThrowStage.Tutorial => 2.5f,
                ThrowStage.Advanced => 1.8f,
                ThrowStage.Stable => 1.3f,
                ThrowStage.Rainbow => 2.8f,
                _ => 0
            };

            if (type == PterosaurGiftType.Fast)
            {
                airDrag *= 0.8f;
            }

            if (_isComfortMode)
            {
                airDrag *= 1.25f;
            }

            return airDrag;
        }

        private Vector3 GetInitialVelocity(PterosaurGiftType type)
        {
            Vector3 velocity = Vector3.zero;

            switch (_currentStage)
            {
                case ThrowStage.Tutorial:
                    // 教学阶段：几乎垂直慢慢掉
                    velocity = Vector3.down * 0.2f;
                    break;

                case ThrowStage.Advanced:
                    // 进阶阶段：轻微横向漂移
                    velocity = new Vector3(
                        Random.Range(-0.35f, 0.35f),
                        Random.Range(-0.2f, 0f),
                        Random.Range(-0.25f, 0.25f)
                    );
                    break;

                case ThrowStage.Stable:
                    // 稳定阶段：有一点抛投感，但仍然舒适
                    velocity = new Vector3(
                        Random.Range(-0.6f, 0.6f),
                        Random.Range(-0.35f, 0.05f),
                        Random.Range(-0.45f, 0.45f)
                    );
                    break;

                case ThrowStage.Rainbow:
                    // 彩虹包裹：慢一点，更容易被看到和接住
                    velocity = Vector3.down * 0.15f;
                    break;
            }

            if (type == PterosaurGiftType.Fast)
            {
                velocity += Vector3.down * 0.6f;
            }

            if (_isComfortMode)
            {
                // 安慰机制：降低横向漂移，让礼物更集中、更好接
                velocity.x *= 0.4f;
                velocity.z *= 0.4f;
                velocity.y *= 0.75f;
            }

            return velocity;
        }

        private float GetThrowInterval()
        {
            float interval;

            switch (_currentStage)
            {
                case ThrowStage.Tutorial:
                    interval = tutorialThrowInterval;
                    break;

                case ThrowStage.Advanced:
                    interval = advancedThrowInterval;
                    break;

                case ThrowStage.Stable:
                    interval = stableThrowInterval;
                    break;

                case ThrowStage.Rainbow:
                    interval = 999f;
                    break;

                default:
                    interval = 2f;
                    break;
            }

            if (_isComfortMode)
            {
                interval *= comfortFrequencyMultiplier;
            }

            return interval;
        }

        #endregion

        private PterosaurGiftType DecidePterosaurGiftType()
        {
            if (_currentStage == ThrowStage.Rainbow && !RainbowGiftSpawned)
            {
                RainbowGiftSpawned = true;
                return PterosaurGiftType.Rainbow;
            }

            if (_currentStage == ThrowStage.Tutorial)
            {
                return PterosaurGiftType.Tutorial;
            }

            _normalGiftCounter++;

            // 每 15 个普通包裹附近刷 1 个幸运包裹
            if (_normalGiftCounter >= 15)
            {
                _normalGiftCounter = 0;
                return PterosaurGiftType.Lucky;
            }

            // 稳定期偶尔出现快速包裹
            if (_currentStage == ThrowStage.Stable && Random.value < 0.12f)
            {
                return PterosaurGiftType.Fast;
            }

            return PterosaurGiftType.Normal;
        }

        #region Score

        private int CalculateScore(PterosaurGiftType type)
        {
            int baseScore;

            switch (type)
            {
                case PterosaurGiftType.Lucky:
                    baseScore = 30;
                    break;

                case PterosaurGiftType.Rainbow:
                    baseScore = 100;
                    break;

                default:
                    baseScore = 10;
                    break;
            }

            float multiplier = GetComboMultiplier();

            return Mathf.RoundToInt(baseScore * multiplier);
        }

        private float GetComboMultiplier()
        {
            if (_combo >= 10)
                return 2f;

            if (_combo >= 5)
                return 1.5f;

            return 1f;
        }

        #endregion

        #region Difficulty

        private void EnterComfortMode()
        {
            _isComfortMode = true;
            _recentMissTimes.Clear();

            // TODO:
            // GameManager.Event.Broadcast("Difficulty.ComfortMode.Enter");
            // 小翼龙安慰动画、声音、投递向前方收束等
        }

        private void ExitComfortMode()
        {
            _isComfortMode = false;
            _continuousCatchCount = 0;

            // TODO:
            // GameManager.Event.Broadcast("Difficulty.ComfortMode.Exit");
        }

        #endregion
    }
}
