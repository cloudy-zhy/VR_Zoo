# FruitSlash 模块说明

## 模块定位

`FruitSlash` 是 VR 切水果小游戏的第一版功能模块，代码位于 `Assets/Scripts/FruitSlash/`。

当前版本用于 `Assets/Scenes/LanTest.unity` 中的功能测试，不接入 `Scene1.unity`，也不驱动正式剧情 Timeline。玩法表现采用“碰撞判定 + 预制半果/占位半果替换”，不做运行时 Mesh Slicing。

## 当前场景布置

`LanTest.unity` 中已直接布置 `FruitSlash Test Setup`，包含：

- `FruitSlash Director`
- `FruitSlash Spawn Point`
- `FruitSlash Target Center`
- `FruitSlash Score UI`
- 左右控制器下的 `FruitSlash Left Blade` / `FruitSlash Right Blade`

`FruitSlash Director` 已配置：

- `scoreController` 指向 `FruitSlash Score UI`
- `spawnPoint` 指向 `FruitSlash Spawn Point`
- `targetCenter` 指向 `FruitSlash Target Center`
- `blades` 包含左右两个 `FruitSlashBlade`
- `autoStart` 为开启状态

进入 `LanTest.unity` 的 Play Mode 后，小游戏会自动开始生成占位果实。

## 测试资源

FruitSlash 当前测试资源位于：

- `Assets/Prefabs/FruitSlash/Test/FruitSlash_TestFruit.prefab`
- `Assets/Prefabs/FruitSlash/Test/FruitSlash_TestHalf.prefab`
- `Assets/Prefabs/SO/PoolConfigData/FruitSlash/FruitSlash_TestFruitPool.asset`
- `Assets/Prefabs/SO/PoolConfigData/FruitSlash/FruitSlash_TestHalfPool.asset`
- `Assets/Prefabs/SO/FruitSlash/FruitConfig/FruitSlash_Test_FlameEggConfig.asset`
- `Assets/Prefabs/SO/FruitSlash/FruitConfig/FruitSlash_Test_GoldenFanConfig.asset`
- `Assets/Prefabs/SO/FruitSlash/FruitConfig/FruitSlash_Test_ConeFruitConfig.asset`
- `Assets/Prefabs/SO/FruitSlash/FruitConfig/FruitSlash_Test_RareConfig.asset`
- `Assets/Prefabs/SO/FruitSlash/FruitConfig/FruitSlash_Test_FastConfig.asset`
- `Assets/Prefabs/SO/FruitSlash/FruitConfig/FruitSlash_Test_RainbowBunchConfig.asset`

测试 Pool key：

- `FruitSlash.PlaceholderFruit`
- `FruitSlash.PlaceholderHalf`

## 主要脚本

### `FruitSlashDirector`

小游戏总控，负责：

- 启动/停止小游戏。
- 按切中数量切换教学、进阶、稳定阶段。
- 按阶段生成果实波次。
- 统计失误并触发动态放缓。
- 每 20 颗普通果实后安排珍稀果实。
- 达到阈值后生成七彩巨大果串。
- 广播预留事件。

常用公开方法：

- `StartGame()`
- `StopGame()`
- `SpawnNextWave()`
- `ForceSpawnRainbowBunch()`

果实斩切和落地失误不由果实直接调用 Director，而是通过内部事件进入 Director。

### `FruitSlashBlade`

挂在左右手光刃对象上，负责挥刀命中检测。

- 每帧采样上一帧到当前帧的移动线段。
- 使用 `Physics.OverlapCapsuleNonAlloc` 做无方向限制判定。
- 同一次挥刀中会记录已命中的果实，避免重复结算。
- 珍稀果实触发后可进入 5 秒刀光强化。

常用公开方法：

- `SetEmpowered(bool empowered, float duration)`
- `SetHitRadiusMultiplier(float multiplier)`
- `ConfigureVisuals(Renderer visualRenderer, LineRenderer line, TrailRenderer trailRenderer)`

### `FruitSlashFruit`

挂在果实根节点上，负责单颗果实运行时状态。

- 保存果实类型、基础分、珍稀/快速/七彩状态。
- 接收光刃切中通知。
- 普通果实切中后隐藏完整果实，生成两个半果占位物。
- 七彩巨大果串需要累计 3 次切中后才完成。
- 完整落地或低于 `failSafeY` 时通知失误。

当前已接入项目重构后的 `PoolManager`。占位果实和占位半果通过 `LanTest.unity` 中 `Controller/PoolDataBinder.poolData` 的独立 `PoolDataSO` 注册，不使用 `PoolDataGroupSO`。

### `FruitSlashScoreController`

独立计分组件。

- 基础分：火焰蛋 15、金扇子果 20、球果 18。
- 连斩窗口：1 秒。
- 连斩奖励：1-4 次 +8，5-8 次 +12，9 次及以上 +18。
- 多斩奖励：同刀每额外切中 1 颗 +10。
- 七彩巨大果串完成后加 150 分并锁定分数。

### 配置与枚举

- `FruitSlashFruitType`：果实类型。
- `FruitSlashStageType`：阶段类型。
- `FruitSlashFruitConfigSO`：果实类型配置资源，用于把果实类型映射到池 key、半果池 key、基础分、颜色、音效/VFX key 和飞行时间范围。
- `FruitSlashEvents`：预留事件名常量。

如果不配置 `FruitSlashFruitConfigSO`，`FruitSlashDirector` 会使用默认 key `FruitSlash.PlaceholderFruit` 从对象池获取测试果实。
`LanTest.unity` 当前已在 `FruitSlashDirector.fruitConfigs` 中配置 6 个测试 FruitConfig，覆盖 `FlameEgg`、`GoldenFan`、`ConeFruit`、`Rare`、`Fast`、`RainbowBunch`。

## 事件

事件沿用 `GameManager.Event.Broadcast`：

- `"FruitSlash.Started"`
- `"FruitSlash.StageChanged"`
- `"FruitSlash.FruitCut"`
- `"FruitSlash.ComboChanged"`
- `"FruitSlash.Completed"`

模块内部事件：

- `"FruitSlash.Internal.FruitCut"`：`FruitSlashFruit` 广播，`FruitSlashDirector` 独占监听并结算。
- `"FruitSlash.Internal.FruitMissed"`：`FruitSlashFruit` 广播，`FruitSlashDirector` 独占监听并统计失误。

注意：当前项目的 `EventManager` 对同一个事件名只保留一个注册监听器，后注册会覆盖先注册。后续如果多个系统都要监听同一个 FruitSlash 事件，需要先改造事件系统为多播，或由一个中转组件统一分发。

## 使用说明

### 在 `LanTest.unity` 测试

1. 打开 `Assets/Scenes/LanTest.unity`。
2. 确认层级中存在 `FruitSlash Test Setup`。
3. 确认 `FruitSlash Director` 的 `Auto Start` 开启。
4. 进入 Play Mode。
5. 用左右控制器上的绿色光刃切中飞来的占位果实。

### 调整生成位置

- 移动 `FruitSlash Spawn Point` 可以改变长颈龙抛果起点。
- 移动 `FruitSlash Target Center` 可以改变果实飞向玩家的位置。
- `FruitSlashDirector` 中的 `tutorialHalfWidth`、`advancedHalfWidth`、`stableHalfWidth` 控制不同阶段的水平落点范围。

### 调整节奏

在 `FruitSlashDirector` 中调整：

- `flightTimeMultiplier`：果实飞行时间倍率，数值越大飞得越慢。测试时默认设为 `1.35`，如果仍然过快可调到 `1.5` 或 `1.8`。
- `tutorialEndCutCount`
- `advancedEndCutCount`
- `rainbowTriggerCutCount`
- `rareInterval`
- `missWindow`
- `missesToSlowDown`
- `emptyWavesToSlowDown`
- `successCutsToRecover`

### 调试日志

`LanTest.unity` 中默认打开了测试日志：

- `FruitSlashDirector.debugLog`：输出开始、波次生成、阶段切换、斩切、失误、放缓和完成日志。
- `FruitSlashBlade.debugLogHits`：输出左右光刃命中果实的日志。

如果 Console 太吵，可以在 Inspector 中关闭这两个字段。

### 替换正式果实资源

可以创建 `FruitSlashFruitConfigSO`，并在 `FruitSlashDirector.fruitConfigs` 中配置。

每个配置建议填写：

- `fruitType`
- `baseScore`
- `fruitPoolKey`
- `halfFruitPoolKey`
- `juiceVfxPoolKey`
- `sparkVfxPoolKey`
- `cutAudio`
- `placeholderColor`
- `flightTimeRange`

正式果实池对应的预制体根节点需要有：

- `FruitSlashFruit`
- `Rigidbody`
- `Collider`

正式半果/VFX 池对应的预制体建议挂 `FruitSlashPooledObject`，用于回池时重置 Rigidbody 和粒子状态。延迟回池直接使用项目已有 `GameManager.Pool.Return(obj, delay)`，不要重复实现新的延迟回池逻辑。

## 注意事项

- 当前版本不是真正的 Mesh Slicing，只是切中后生成半果替代物。
- 当前版本只面向 `LanTest.unity`，不要直接接入 `Scene1.unity` 或正式 Timeline。
- `FruitSlashDirector.autoStart` 会在 Play Mode 自动开始生成果实；如果要手动触发，关闭 `autoStart` 后调用 `StartGame()`。
- 光刃命中依赖物理查询，果实必须有有效 `Collider`。
- 光刃本身不需要 Collider，当前使用上一帧到当前帧的 capsule 查询。
- 光刃和拖影会在运行时创建兜底材质，避免 TrailRenderer 未分配材质时显示紫色。
- `FruitSlashBlade.fruitMask` 默认是 `~0`，会查询所有 Layer；后续正式化时建议建立专用 Fruit layer 降低误判和查询成本。
- 当前占位果实和占位半果已使用 `PoolManager`，默认 key 为 `FruitSlash.PlaceholderFruit` 和 `FruitSlash.PlaceholderHalf`。
- 正式果实、正式半果和 VFX 如果使用自定义 key，需要先通过 `PoolDataSO` 注册对象池。
- `LanTest.unity` 中 FruitSlash 测试池走 `PoolDataBinder.poolData` 直接列表，不使用 `PoolDataGroupSO`。
- `FruitSlashScoreController` 当前使用 `TextMeshPro` 世界空间文本，未做复杂动画和 Billboard，正式 UI 需要补充朝向玩家和动效。
- `LanTest.unity` 中已有一个与本模块无关的 `Prefab Indexer` 缺失脚本问题，当前不处理。

## 后续开发建议

- 完善对象池配置：
  - 将正式果实、半果、果汁 VFX、火花 VFX、飘分 UI 加入独立 `PoolDataSO`。
  - 保持延迟回池使用 `GameManager.Pool.Return(obj, delay)`。
- 完善正式资源：
  - 替换基础 3D Object 占位果实。
  - 为每种果实制作完整体、半果、断面材质、粒子和音效。
- 强化 VR 手感：
  - 根据控制器速度设置最小有效挥刀速度。
  - 为刀光增加更稳定的 Trail、音效和轻微 haptic feedback。
  - 调整 `baseHitRadius`，避免过难或过宽。
- 优化动态难度：
  - 区分“玩家没碰到”和“果实飞出舒适范围”。
  - 对慢速保护波次增加更明显的长颈龙动画/提示。
- 正式剧情接入：
  - 保留 `StartGame()` 和 `"FruitSlash.Completed"` 作为剧情入口和出口。
  - 后续通过 Timeline Signal 或剧情控制器调用，不要让 FruitSlash 直接依赖具体 Timeline。
- 事件系统升级：
  - 如果多个系统需要监听同一事件，建议将 `EventManager` 改为同名事件多监听器列表。
- 性能整理：
  - 给果实设置专用 Layer。
  - 减少运行时 `material` 实例化。
  - 半果、VFX、文本提示接对象池。

## 编译验证

最近一次实现后已通过 Unity 脚本刷新/编译检查，Console 中没有本模块导致的 error。

后续修改该模块后，交付前至少确认：

- Unity Console 无新增编译错误。
- `LanTest.unity` 中 `FruitSlashDirector` 的核心引用未丢失。
- 不误改 `Scene1.unity`、正式剧情 Timeline 或无关 ProjectSettings。
