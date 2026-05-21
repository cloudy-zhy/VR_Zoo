# 动物摸头系统实现方案

## 概要

- 新增一个通用的动物摸头交互系统，用于给部分动物添加近距离抚摸反馈。
- 第一版以 `PetZone` 预制体为核心：需要支持摸头的动物，在其原始 prefab 的头部子节点下添加 `PetZone`，并在动物本体或父级对象上实现 `IPettable` 接口。
- 交互检测优先使用 XR Interaction Toolkit 和 PICO XR 已接入的原生能力，减少自定义输入、手势识别和物理轮询代码。
- 摸头反馈由动物自身决定，例如播放 Animator Trigger、音效、粒子、表情、Timeline Signal 或广播事件。
- 不修改现有弹弓、抓取、剧情主流程；摸头作为独立的可选互动层挂接到动物 prefab 上。

## 设计目标

- 可复用：`PetZone` 做成 prefab，后续给新动物添加摸头能力时，只需要把 prefab 放到合适的头部位置并配置参数。
- 低侵入：不把摸头检测写进 `DodoBird`、`Pterosaur` 等动物主逻辑里，避免污染 FSM 和玩法状态。
- 原生优先：使用 `XRSimpleInteractable`、`HoverEnter/Exit`、`firstHoverEntered`、`InteractionLayerMask`、`XRDirectInteractor` 等 XRI 能力。
- 可控触发：只允许近距离直接交互触发，避免远距离 Ray Interactor 或控制器路过造成误触。
- 可扩展：同一套 `PetZone` 可以支持渡渡鸟、翼龙、长颈龙或后续其他动物。

## 模块结构

第一版建议新增 `Assets/Scripts/Pet/` 模块。

### `IPettable`

- 可被摸头对象实现的接口。
- 通常挂在动物根节点或能代表动物本体的组件上。
- `PetZone` 只依赖该接口，不依赖具体动物类型。
- 建议接口：

```csharp
public interface IPettable
{
    bool CanBePetted { get; }
    void OnPetted(PetContext context);
}
```

### `PetContext`

- 摸头触发时传递的轻量上下文。
- 建议字段：
  - `GameObject Interactor`：触发摸头的手或控制器对象。
  - `Transform PetZone`：被触发的摸头区域。
  - `Vector3 ContactPosition`：触发时的参考位置。
  - `float StrokeDistance`：本次在摸头区域内累计移动距离。
  - `float HoldDuration`：本次 hover 持续时间。

### `PetZone`

- 挂在 `PetZone.prefab` 上的核心检测组件。
- 只负责“是否构成一次摸头”的判断，不负责具体动物反馈。
- 组件依赖：
  - `Collider`：建议使用 `SphereCollider` 或 `CapsuleCollider`，默认不要开启 `isTrigger`。
  - `XRSimpleInteractable`：负责接入 XR Interaction Toolkit 的 hover 生命周期。
- 序列化配置：
  - `Transform pettableRoot`：可选，指定向父级查找 `IPettable` 的根。
  - `bool onlyDirectInteractor = true`：默认只允许 `XRDirectInteractor`。
  - `float minStrokeDistance = 0.12f`：手在区域内累计移动达到该距离后触发。
  - `float minHoverDuration = 0.15f`：最短停留时间，过滤瞬间擦过。
  - `float cooldown = 0.8f`：同一 `PetZone` 的触发冷却。
  - `bool triggerOncePerHover = false`：默认关闭，让 `cooldown` 控制同一次接触内的重复触发；只有剧情节点需要“一次进入只触发一次”时才开启。
  - `LayerMask` 或 `InteractionLayerMask`：限制只有玩家手/控制器可以触发。
- 工作方式：
  - 在 `hoverEntered` 中记录 interactor、初始位置和时间。
  - 在 `hoverExited` 中清理本次记录。
  - 在 `Update` 中读取 active interactor 的当前位置，累计移动距离。
  - 当 `IPettable.CanBePetted` 为 true，且距离、时间、冷却均满足时，调用 `IPettable.OnPetted(context)`。
  - 触发后重置本轮累计移动距离和停留计时；玩家不需要把手拿出 `PetZone`，冷却结束后重新摸动即可再次触发。

## `PetZone` Prefab 方案

建议新增 `Assets/Prefabs/Pet/PetZone.prefab`。

Prefab 结构：

```text
PetZone
├── SphereCollider / CapsuleCollider
├── XRSimpleInteractable
└── PetZone
```

推荐配置：

- `Collider.isTrigger = false`。`XRDirectInteractor` 在启用 Sphere Collider 精度优化时会使用物理 overlap/sphere cast，默认忽略 trigger collider；如果 `PetZone` 设为 trigger，Direct hover 可能完全不会触发。
- Collider 尺寸按常见动物头部设置一个较小默认值，例如半径 `0.18m`，具体动物 prefab 内可覆盖缩放。
- `XRSimpleInteractable` 不需要 grab，仅用于 hover 检测。
- `Interaction Layer Mask` 只包含玩家手部/控制器所在交互层，避免环境物件参与检测。
- `PetZone` prefab 自身不包含动物动画、音频或粒子引用，保持纯检测职责。

给动物添加摸头能力的流程：

1. 打开动物原始 prefab。
2. 在头顶、额头或适合抚摸的位置添加 `PetZone.prefab`。
3. 调整 `PetZone` 的本地位置、旋转和 Collider 大小。
4. 在动物根节点或父级控制组件上实现 `IPettable`。
5. 在 `OnPetted` 中播放该动物自己的反馈。
6. Play Mode 中用 XR Device Simulator、PICO Live Preview 或真机验证触发范围和误触情况。

## XR Toolkit 与 PICO 原生能力使用原则

- 近距离检测优先使用 `XRSimpleInteractable` 的 hover 事件，而不是直接手写 `OnTriggerEnter` 作为主入口。
- 只接受 `XRDirectInteractor`，沿用项目中 `PterosaurGift` 对直接交互的判断方式，避免 `XRRayInteractor` 远距离触发摸头。
- 输入和手部来源交给 XRI/PICO XR 管线处理，`PetZone` 不直接读取 PICO Controller API，也不自行判断按键。
- 如果启用 PICO 手部追踪或 XR Hands，优先让手部模型/手部交互对象通过 XRI interactor 参与 hover，不额外写一套手势识别。
- 如果某些 PICO 手部对象没有稳定的 `XRDirectInteractor`，再考虑加一个很薄的适配层，例如 `PetHandProxy`，把 PICO 手部碰撞体桥接成 XRI 可识别的 direct interactor；不在 `PetZone` 内写 PICO 专属分支。
- 使用 `InteractionLayerMask` 做粗过滤，使用 `XRDirectInteractor` 类型检查做精过滤，使用移动距离和冷却做防抖。

## 动物接入建议

### 渡渡鸟

- 渡渡鸟根节点已经使用 `XRGrabInteractable` 和 FSM 处理抓取、装填、瞄准、发射。
- 摸头不应复用根节点 `XRGrabInteractable`，避免和弹弓玩法抢交互。
- 建议在渡渡鸟头部子节点添加 `PetZone`。
- `DodoBird` 或新增 `DodoBirdPetResponder` 实现 `IPettable`。
- 只允许以下状态响应摸头：
  - `Idle`
  - `Wait`
  - 必要时允许 `Return` 到达后响应
- 以下状态不响应：
  - `Grabbed`
  - `Loaded`
  - `Aim`
  - `Shot`
- 反馈建议：
  - 播放 `Happy` / `Pet` Animator Trigger。
  - 播放轻量开心音效。
  - 播放心形、星星或已有粒子。
  - 广播 `"DodoBird.Petted"`，方便教程或剧情监听。

### 翼龙

- 翼龙本体已经使用 `XRSimpleInteractable` 和 hover 事件，可自然接入同一套思路。
- 如果翼龙当前 hover 已承担“靠近/召唤/投礼物”等玩法含义，摸头建议仍放在独立头部 `PetZone`，避免一个 interactable 同时表达多个行为。
- `Pterosaur` 或新增 `PterosaurPetResponder` 实现 `IPettable`。
- 可根据飞行/降落状态控制 `CanBePetted`，例如只在落地、停靠、等待玩家互动时允许。

### 长颈龙和其他动物

- 如果动物没有复杂 FSM，可以直接在动物根节点新增一个 responder 组件实现 `IPettable`。
- 如果动物只有 Animator 表演，`OnPetted` 可以只触发 Animator、AudioSource 和事件。
- 如果动物参与小游戏流程，需要由对应 Director 或状态机暴露 `CanBePetted`，避免摸头打断玩法节奏。

## 事件与反馈

- 推荐广播通用事件：
  - `"Animal.Petted"`：payload 为 `IPettable` 或动物 `Component`。
- 对需要剧情精确监听的动物，可以额外广播具体事件：
  - `"DodoBird.Petted"`
  - `"Pterosaur.Petted"`
- 第一版不强制新增 UI 提示，优先通过动物动作、声音和粒子反馈让玩家理解交互成功。
- 如果后续要做任务目标，例如“摸摸三只动物”，由任务系统监听事件，不反向依赖 `PetZone`。

## 实现步骤

1. 新增 `Assets/Scripts/Pet/IPettable.cs`、`PetContext.cs`、`PetZone.cs`。
2. 新增 `Assets/Prefabs/Pet/PetZone.prefab`，包含 `Collider`、`XRSimpleInteractable`、`PetZone`。
3. 为第一批目标动物添加 responder：
   - 渡渡鸟：建议新增 `DodoBirdPetResponder`，避免直接扩大 `DodoBird` 主类职责。
   - 翼龙：建议新增 `PterosaurPetResponder` 或在现有类中暴露状态后由 responder 读取。
4. 在目标动物原始 prefab 的头部添加 `PetZone.prefab` 实例。
5. 配置 Interaction Layer、Collider 范围、移动距离阈值和冷却时间。
6. 在 Play Mode 中验证直接交互、误触、状态禁用和反馈表现。

## 测试计划

- Unity 编译验证：脚本刷新后 Console 无 error。
- XR Device Simulator 验证：
  - 直接手/控制器靠近头部，轻微经过不会触发。
  - 在头部区域内短距离移动后触发一次摸头。
  - 同一次 hover 不会连续刷触发。
  - 冷却结束后重新摸头可以再次触发。
- PICO Live Preview 或真机验证：
  - PICO 控制器 direct interactor 可以触发。
  - Ray interactor 不能远距离触发。
  - 如果启用手部追踪，确认手部 direct interaction 对象能进入 hover。
- 渡渡鸟验证：
  - `Idle/Wait` 下可以摸头。
  - 抓起、装入弹弓、拉弓、发射中不会触发摸头。
  - 摸头不会影响 `XRGrabInteractable` 的抓取流程。
- 翼龙验证：
  - 摸头区域和原有 hover/礼物交互不冲突。
  - 飞行或不可互动状态下不会触发。
- 性能和稳定性：
  - 多个动物同时带 `PetZone` 时无明显 GC spike。
  - 禁用或销毁动物时，hover 监听正常注销，无 MissingReference 报错。

## 注意事项

- `PetZone` prefab 应该保持轻量，不绑定具体动物资源。
- 不建议第一版做复杂手势识别，例如手掌朝向、手指弯曲、连续抚摸曲线识别；这些可在手部追踪稳定后作为增强层加入。
- 不建议用动物根节点的大 Collider 判断摸头，否则容易把碰身体、抓取、路过都误判为摸头。
- 不建议在 `PetZone` 内直接调用具体动物类，例如 `DodoBird` 或 `Pterosaur`；应通过 `IPettable` 解耦。
- 不随意修改 PICO SDK、XR Interaction Toolkit 配置或项目输入资产；优先通过 prefab 局部配置完成。

## 假设

- 第一版只支持近距离摸头，不支持远距离指向互动。
- 项目当前的 XR Rig 已正确配置 direct interactor，PICO 控制器和模拟器能通过 XRI hover 事件进入 `XRSimpleInteractable`。
- 目标动物的头部骨骼或模型子节点位置稳定，适合挂载 `PetZone`。
- 动物反馈资源可能尚未齐全，因此 responder 允许先用 Debug.Log、已有粒子或临时 Animator Trigger 占位。
