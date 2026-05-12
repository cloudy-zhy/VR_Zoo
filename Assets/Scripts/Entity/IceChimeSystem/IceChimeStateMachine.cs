// IceChimeStateMachine.cs
// 冰凌状态机
// 职责：状态流转、时间窗口判定、事件发射、视觉/音效驱动
// 注意：视觉材质、粒子等引用通过 Inspector 赋值，或由 IceChime 初始化时注入

using System.Collections;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class IceChimeStateMachine : MonoBehaviour
{
    
    [Header("冰凌基本信息")]
    [SerializeField] private int chimeIndex = 0;
    [SerializeField] private AudioClip chimeSound;           // 马林巴琴音阶音效

    [Header("时间窗口参数")]
    [SerializeField] private float prePlayDuration = 1.5f;       // 光效从底部升到演奏线的时长
    [SerializeField] private float playableWindowDuration = 0.3f;// 可演奏时间窗口总时长
    [SerializeField] private float perfectWindow = 0.05f;        // 完美判定半径
    [SerializeField] private float greatWindow = 0.10f;        // 优秀判定半径
    [SerializeField] private float goodWindow = 0.15f;        // 良好判定半径

    [Header("视觉组件")]
    [SerializeField] private MeshRenderer iceRenderer;
    [SerializeField] private Light pointLight;
    [SerializeField] private float lightRiseHeight = 0.8f;       // 预演光效升起高度（传给 Shader）

    [Header("粒子特效")]
    [SerializeField] private ParticleSystem successParticles;
    [SerializeField] private ParticleSystem missParticles;

    [Header("材质（各状态对应材质）")]
    [SerializeField] private Material dormantMaterial;
    [SerializeField] private Material prePlayMaterial;
    [SerializeField] private Material playableMaterial;
    [SerializeField] private Material successMaterial;
    [SerializeField] private Material missedMaterial;
    [SerializeField] private Material wrongMaterial;

    // ─────────────────────────────────────────
    // 公开事件容器
    // ─────────────────────────────────────────
    public IceChimeEvents Events = new IceChimeEvents();

    // ─────────────────────────────────────────
    // 内部状态
    // ─────────────────────────────────────────
    private IceChimeState _currentState = IceChimeState.Dormant;
    public IceChimeState CurrentState => _currentState;

    private float prePlayProgress = 0f;
    private float playableTimer = 0f;
    private float optimalHitTime = 0f;   // 窗口中间时刻 = 最佳演奏时机
    private Coroutine activeRoutine = null;

    private AudioSource audioSource;
    private MaterialPropertyBlock propBlock;

    // Shader 属性 ID（避免每帧字符串查找）
    private static readonly int LightHeightID = Shader.PropertyToID("_LightHeight");
    private static readonly int FlickerIntensityID = Shader.PropertyToID("_FlickerIntensity");
    private static readonly int GlowColorID = Shader.PropertyToID("_GlowColor");
    private static readonly int GlowPowerID = Shader.PropertyToID("_GlowPower");

    // ─────────────────────────────────────────
    // 生命周期
    // ─────────────────────────────────────────
    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.spatialBlend = 1f;
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.maxDistance = 5f;

        if (iceRenderer == null)
            iceRenderer = GetComponent<MeshRenderer>();

        propBlock = new MaterialPropertyBlock();
        iceRenderer.GetPropertyBlock(propBlock);

        if (successParticles != null) successParticles.Stop();
        if (missParticles != null) missParticles.Stop();
    }

    private void Start()
    {
        TransitionTo(IceChimeState.Dormant, null);
    }

    // ─────────────────────────────────────────
    // 公开接口
    // ─────────────────────────────────────────

    /// <summary>
    /// 由 RhythmController 调用，启动预演倒计时。
    /// </summary>
    public void StartPrePlay()
    {
        if (_currentState == IceChimeState.Dormant)
            TransitionTo(IceChimeState.PrePlay, null);
    }

    /// <summary>
    /// 由手势/碰撞系统调用，传入触发者 GameObject（可为 null）。
    /// </summary>
    public void OnPlayerInteraction(GameObject interactor = null)
    {
        if (_currentState == IceChimeState.Playable)
        {
            HandleSuccessInteraction(interactor);
        }
        else if (_currentState == IceChimeState.Dormant ||
                 _currentState == IceChimeState.PrePlay)
        {
            HandleWrongInteraction(interactor);
        }
        // Success / Missed / WrongHit 状态下触发一律忽略
    }

    // ─────────────────────────────────────────
    // 核心状态转换
    // ─────────────────────────────────────────

    /// <summary>
    /// 统一状态转换入口。所有状态切换都经过此方法。
    /// </summary>
    private void TransitionTo(IceChimeState newState, IceChimeEventArgs incomingArgs)
    {
        if (_currentState == newState) return;

        // 构建事件参数
        var args = incomingArgs ?? new IceChimeEventArgs();
        args.ChimeIndex = chimeIndex;
        args.PreviousState = _currentState;
        args.NewState = newState;
        args.InteractionTime = Time.time;

        // 退出当前状态协程
        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
            activeRoutine = null;
        }

        ExitCurrentState();
        _currentState = newState;
        EnterNewState(newState, args);

        // 发射通用变更事件
        Events.OnStateChanged.Invoke(args);
    }

    private void ExitCurrentState()
    {
        switch (_currentState)
        {
            case IceChimeState.PrePlay:
                prePlayProgress = 0f;
                break;
            case IceChimeState.Playable:
                playableTimer = 0f;
                break;
        }
    }

    private void EnterNewState(IceChimeState state, IceChimeEventArgs args)
    {
        switch (state)
        {
            // ── 沉寂 ──────────────────────────────
            case IceChimeState.Dormant:
                ApplyVisualDormant();
                Events.OnReturnToDormant.Invoke(args);
                break;

            // ── 预演 ──────────────────────────────
            case IceChimeState.PrePlay:
                prePlayProgress = 0f;
                ApplyVisualPrePlay(0f);
                Events.OnPrePlayStarted.Invoke(args);
                activeRoutine = StartCoroutine(PrePlayRoutine());
                break;

            // ── 弹奏 ──────────────────────────────
            case IceChimeState.Playable:
                playableTimer = playableWindowDuration;
                optimalHitTime = Time.time + playableWindowDuration * 0.5f;
                ApplyVisualPlayable(1f);
                Events.OnBecamePlayable.Invoke(args);
                activeRoutine = StartCoroutine(PlayableRoutine());
                break;

            // ── 成功 ──────────────────────────────
            case IceChimeState.Success:
                ApplyVisualSuccess();
                PlaySuccessEffects();
                Events.OnSuccess.Invoke(args);
                if (args.IsPerfect)
                    Events.OnPerfect.Invoke(args);
                activeRoutine = StartCoroutine(AutoReturnToDormant(0.8f));
                break;

            // ── 错过 ──────────────────────────────
            case IceChimeState.Missed:
                ApplyVisualMissed();
                PlayMissEffects();
                Events.OnMissed.Invoke(args);
                Events.OnComboBreak.Invoke(args);
                activeRoutine = StartCoroutine(AutoReturnToDormant(1.2f));
                break;

            // ── 误触 ──────────────────────────────
            case IceChimeState.WrongHit:
                ApplyVisualWrong();
                PlayWrongEffects();
                Events.OnWrongHit.Invoke(args);
                Events.OnComboBreak.Invoke(args);
                activeRoutine = StartCoroutine(AutoReturnToDormant(0.5f));
                break;
        }
    }

    // ─────────────────────────────────────────
    // 交互处理
    // ─────────────────────────────────────────

    private void HandleSuccessInteraction(GameObject interactor)
    {
        float timeDiff = Mathf.Abs(Time.time - optimalHitTime);

        var args = new IceChimeEventArgs
        {
            InteractionPosition = interactor != null
                ? interactor.transform.position
                : transform.position,
            Interactor = interactor,
            TimingAccuracy = ComputeAccuracyValue(timeDiff),
            AccuracyGrade = ComputeAccuracyGrade(timeDiff),
            IsPerfect = timeDiff <= perfectWindow
        };

        TransitionTo(IceChimeState.Success, args);
    }

    private void HandleWrongInteraction(GameObject interactor)
    {
        var args = new IceChimeEventArgs
        {
            InteractionPosition = interactor != null
                ? interactor.transform.position
                : transform.position,
            Interactor = interactor
        };

        TransitionTo(IceChimeState.WrongHit, args);
    }

    // ─────────────────────────────────────────
    // 判定辅助
    // ─────────────────────────────────────────

    private HitAccuracy ComputeAccuracyGrade(float timeDiff)
    {
        if (timeDiff <= perfectWindow) return HitAccuracy.Perfect;
        if (timeDiff <= greatWindow) return HitAccuracy.Great;
        if (timeDiff <= goodWindow) return HitAccuracy.Good;
        return HitAccuracy.Okay;
    }

    private float ComputeAccuracyValue(float timeDiff)
    {
        if (timeDiff <= perfectWindow) return 1.00f;
        if (timeDiff <= greatWindow) return 0.85f;
        if (timeDiff <= goodWindow) return 0.65f;
        return Mathf.Max(0f, 1f - timeDiff / playableWindowDuration);
    }

    // ─────────────────────────────────────────
    // 协程
    // ─────────────────────────────────────────

    /// <summary>光效从底部升起，到达演奏线后切换 Playable。</summary>
    private IEnumerator PrePlayRoutine()
    {
        float startTime = Time.time;

        while (prePlayProgress < 1f && _currentState == IceChimeState.PrePlay)
        {
            prePlayProgress = (Time.time - startTime) / prePlayDuration;
            ApplyVisualPrePlay(Mathf.Clamp01(prePlayProgress));
            yield return null;
        }

        if (_currentState == IceChimeState.PrePlay)
            TransitionTo(IceChimeState.Playable, null);
    }

    /// <summary>维持时间窗口，超时后切换 Missed。</summary>
    private IEnumerator PlayableRoutine()
    {
        while (playableTimer > 0f && _currentState == IceChimeState.Playable)
        {
            playableTimer -= Time.deltaTime;

            // 窗口剩余不足 30% 时加快闪烁频率，给玩家紧迫感
            float freq = playableTimer < playableWindowDuration * 0.3f ? 20f : 10f;
            float flicker = Mathf.Sin(Time.time * freq) * 0.5f + 0.5f;
            ApplyVisualPlayable(flicker);

            yield return null;
        }

        if (_currentState == IceChimeState.Playable)
            TransitionTo(IceChimeState.Missed, null);
    }

    /// <summary>延迟后自动回到沉寂。</summary>
    private IEnumerator AutoReturnToDormant(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (_currentState == IceChimeState.Success ||
            _currentState == IceChimeState.Missed ||
            _currentState == IceChimeState.WrongHit)
        {
            TransitionTo(IceChimeState.Dormant, null);
        }
    }

    // ─────────────────────────────────────────
    // 视觉更新
    // ─────────────────────────────────────────

    private void ApplyVisualDormant()
    {
        SetMaterial(dormantMaterial);
        SetLight(false, 0f, Color.white);
        propBlock.SetFloat(LightHeightID, 0f);
        propBlock.SetFloat(FlickerIntensityID, 0f);
        propBlock.SetFloat(GlowPowerID, 0f);
        iceRenderer.SetPropertyBlock(propBlock);
    }

    private void ApplyVisualPrePlay(float progress)
    {
        if (progress == 0f) SetMaterial(prePlayMaterial);
        SetLight(true, 0.3f * progress, Color.cyan);
        propBlock.SetFloat(LightHeightID, progress * lightRiseHeight);
        propBlock.SetFloat(GlowPowerID, progress * 0.5f);
        iceRenderer.SetPropertyBlock(propBlock);
    }

    private void ApplyVisualPlayable(float flicker)
    {
        SetMaterial(playableMaterial);
        SetLight(true, 1f, Color.cyan);
        Color glowColor = Color.Lerp(Color.cyan, Color.white, flicker);
        propBlock.SetFloat(FlickerIntensityID, flicker);
        propBlock.SetFloat(GlowPowerID, 1f + flicker * 0.5f);
        propBlock.SetColor(GlowColorID, glowColor);
        iceRenderer.SetPropertyBlock(propBlock);
    }

    private void ApplyVisualSuccess()
    {
        SetMaterial(successMaterial);
        SetLight(true, 2f, Color.yellow);
        propBlock.SetFloat(GlowPowerID, 3f);
        propBlock.SetColor(GlowColorID, Color.yellow);
        iceRenderer.SetPropertyBlock(propBlock);
    }

    private void ApplyVisualMissed()
    {
        SetMaterial(missedMaterial);
        SetLight(true, 0.4f, Color.gray);
        propBlock.SetFloat(GlowPowerID, 0.3f);
        iceRenderer.SetPropertyBlock(propBlock);
    }

    private void ApplyVisualWrong()
    {
        SetMaterial(wrongMaterial);
        SetLight(true, 1.5f, Color.red);
        propBlock.SetFloat(GlowPowerID, 1f);
        propBlock.SetColor(GlowColorID, Color.red);
        iceRenderer.SetPropertyBlock(propBlock);
        StartCoroutine(WrongFlicker());
    }

    private IEnumerator WrongFlicker()
    {
        for (int i = 0; i < 3; i++)
        {
            if (pointLight) pointLight.enabled = !pointLight.enabled;
            yield return new WaitForSeconds(0.08f);
        }
        if (pointLight) pointLight.enabled = false;
    }

    private void SetMaterial(Material mat)
    {
        if (iceRenderer != null && mat != null)
            iceRenderer.material = mat;
    }

    private void SetLight(bool enabled, float intensity, Color color)
    {
        if (pointLight == null) return;
        pointLight.enabled = enabled;
        pointLight.intensity = intensity;
        pointLight.color = color;
    }

    // ─────────────────────────────────────────
    // 音效 / 粒子
    // ─────────────────────────────────────────

    private void PlaySuccessEffects()
    {
        if (chimeSound != null) audioSource.PlayOneShot(chimeSound);
        successParticles?.Play();
    }

    private void PlayMissEffects()
    {
        if (chimeSound != null)
        {
            audioSource.pitch = 0.8f;
            audioSource.PlayOneShot(chimeSound);
            StartCoroutine(ResetPitch(0.5f));
        }
        missParticles?.Play();
    }

    private void PlayWrongEffects()
    {
        // 误触不播放任何声音（规则：按错冰凌无反应）
        // 若希望有微弱音效，可在此添加
    }

    private IEnumerator ResetPitch(float delay)
    {
        yield return new WaitForSeconds(delay);
        audioSource.pitch = 1f;
    }

    // ─────────────────────────────────────────
    // 只读属性（供外部查询）
    // ─────────────────────────────────────────
    public int ChimeIndex => chimeIndex;
    public float PrePlayProgress => prePlayProgress;
    public float PlayableTimer => playableTimer;
}