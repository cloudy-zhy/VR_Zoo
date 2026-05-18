# EventManager 可视化面板实现方案

## 概要

- 新增 `EventManager` 调试可视化窗口，用于查看事件分类、注册监听者、广播历史，以及某次 `Broadcast` 实际影响到的监听者。
- 面板使用 UXML/USS 实现，菜单入口为 `Tools/Event Manager Viewer`。
- 事件按 `eventName` 第一个 `.` 前的词条分类，例如 `DodoBird.OnPulling` 归类为 `DodoBird`。
- 支持关闭显示某些分类；分类显示状态持久化到 `EditorPrefs`。
- 历史日志记录 `Register / Unregister / Broadcast`，默认只勾选显示 `Broadcast`，可手动显示其他类型。

## Editor-only 调试数据

- 正式事件分发仍由 `EventManager` 和 `EventSlot` 负责，`EventSlot` 只保存直接委托：
  - `Action<EventContext>`
  - `Action<EventContext<TPayload>>`
- 监听者数量使用 `action?.GetInvocationList().Length ?? 0` 计算。
- `EventDebugData`、`EventDebugStore`、`EventDebugHub` 均只在 `UNITY_EDITOR` 下编译，不进入正式 Player 构建。
- `EventDebugHub` 是专用调试通道，Viewer 订阅 `EventDebugHub.Changed` 刷新界面；调试通知不走普通 `EventManager` 事件流，避免递归和 `Editor.xx` 字符串特判。
- `EventDebugStore` 为每个事件维护调试信息：
  - `eventName`
  - `category`
  - `payloadType`
  - 无 Payload 监听者数量
  - 有 Payload 监听者数量
  - 累计 `Register / Unregister / Broadcast` 次数
  - `lastCallLogId`
- `EventManager` 主文件只保留正式事件逻辑和 `UNITY_EDITOR` 下的显式调试调用；Editor 调试方法实现放在 `EventManager.Debug.cs` partial 文件中。
- 不使用底部空的 `partial void` 声明，避免读代码时难以追踪。
- Player 构建下 `EventManager.Debug.cs` 不参与编译，主文件里的 `UNITY_EDITOR` 调用也会被移除。
- `Broadcast` 的 affected list 由调试层从 multicast delegate 的 `GetInvocationList()` 获取，不把 `EventListenerInfo` 绑定进正式分发结构。
- 监听者追踪来自委托本身：
  - `Action.Target` 是 `Component/MonoBehaviour`：记录 `GameObject` 路径、组件类型、方法名。
  - `Action.Target` 是普通类：记录类名、方法名。
  - 静态方法：记录声明类型和方法名。
- 调试日志使用环形缓存，默认保留最近 `2000` 条，避免长时间 Play Mode 无限增长。

## Broadcast API 方案

- 旧 `Broadcast(string eventName)` 和 `Broadcast<TPayload>(string eventName, TPayload payload)` 标记为禁用，让漏改调用点在编译期暴露。
- 新增强制传源 API：
  - `Broadcast(object source, string eventName)`
  - `Broadcast<TPayload>(object source, string eventName, TPayload payload)`
- 新增扩展方法作为推荐写法：
  - `this.Broadcast("DodoBird.Grabbed")`
  - `this.Broadcast("Gift.Caught", payload)`
  - `gameObject.Broadcast("DodoBird.FruitHit")`
- 扩展方法只提供给 `Component` 和 `GameObject`，不提供 `object.Broadcast(...)`，避免 IDE 补全污染和语义失控。
- 非 Unity 对象调用时显式传源：
  - 状态类传 `owner`
  - 服务类传 `this`
  - 确实没有明确源时传 `null`，面板显示 `<null source>`
- 代码定位使用 `StackTrace`，跳过 `EventManager` 和扩展方法层，记录真正业务调用处的类、方法、文件行号。

## 面板布局与字段

- 左栏：已注册事件列表
  - `EventName` 表头为可点击下拉入口，点击后弹出分类勾选菜单。
  - 分类勾选项只显示 `Visible` 和 `Category`，不显示无意义的分类 listener/count 汇总。
  - `EventName`
  - `Category`
  - `PayloadType`
  - `NoPayload Listener Count`
  - `Payload Listener Count`
  - `Total Listener Count`
  - `Register Count`
  - `Unregister Count`
  - `Broadcast Count`
  - `LastCall`
- 右栏：详情与历史
  - 事件详情：`EventName`、`Category`、`PayloadType`、`FirstRegisteredAt`、`LastRegisteredAt`、`LastUnregisteredAt`、`LastCall`
  - Listener 列表：`Status`、`Target GameObject`、`Target Class/Component`、`Method`、`Registered Log`、`Invoke Count`、`LastCall`
  - History Log：`LogId`、`Time`、`Type`、`EventName`、`Source`、`Caller`、`Payload Preview`、`Affected Count`
  - 选中某条 Broadcast log 时，显示本次 `Affected List`：调用顺序、目标对象、组件/类、方法名。
  - 右栏内容默认给 History Log 更大空间，并支持横向滚动查看完整字段。
  - 左栏事件表格拥有自己的横向滚动区域，列头不会越界覆盖右栏。

## 测试计划

- Unity 编译验证：刷新脚本后 Console 无 error。
- 打开 `Tools/Event Manager Viewer`，非 Play Mode 下显示提示，Play Mode 下显示运行时事件数据。
- 进入测试场景后确认分类包含 `DodoBird`、`FruitSlash`、`Gift`、`Pool`、`PokeBall` 等。
- 触发一次 Broadcast 后确认：
  - 对应事件 `Broadcast Count` 增加。
  - `LastCall` 指向最新 log。
  - History Log 显示 source、caller、payload preview。
  - Affected List 能显示本次实际调用到的监听者。
- 切换日志过滤：
  - 默认只显示 `Broadcast`。
  - 勾选后可显示 `Register` 和 `Unregister`。
- 隐藏某个分类后，事件列表和历史日志同步过滤；关闭重开窗口后过滤状态仍保留。
- 检查旧 `Broadcast(eventName...)` 调用已全部迁移，编译期不再出现禁用 API 报错。

## 假设

- 不处理“晚注册”约定；任何时机的 `Register` 都照常记录和显示。
- 调试数据主要服务 Editor/Play Mode，不作为正式 Android/PICO 运行时功能。
- Payload 只做简短预览，不深度序列化复杂对象，避免日志过重。
- 面板第一版只做观察和定位，不提供强制注销、清空事件、修改监听者等危险操作。
