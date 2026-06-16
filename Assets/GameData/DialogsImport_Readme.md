# DialogsImport_Readme.md

本文件为 `DialogLineSOImporter`（Excel → DialogLineSO 批量生成工具）的使用与配表说明。

---

## 菜单入口
在 Unity 菜单栏中点击：
**`Tools > Dialog > Import DialogLineSO from Excel`**

---

## 配表规范

每次导入的 Excel 文件必须包含一个专用 Tab 页面 **`Characters`** 用于建立角色立绘映射，同时可包含多个其他的**对话内容 Tab 页面**（如 `Scene1`, `Scene2` 等）。

---

## 1. Characters 专用 Sheet 规范（必须包含）

此 Sheet 用于声明各角色在不同表情或状态下的立绘映射。名称必须固定为 **`Characters`**（不区分大小写）。

### **Characters 表头约定**（列名顺序任意，不区分大小写）：

| 列名 | 说明 | 缺省行为 / 校验规则 |
| :--- | :--- | :--- |
| `characterName` | 说话的角色显示名（在对话 UI 展现的名字，如 `DodoChief`） | 空字符串 |
| `characterState` | 表情或状态名称（如 `default`, `happy`, `sad`） | `"default"` |
| `assetName` | 美术资源文件使用名（用于拼接默认子 Sprite 名称） | 留空时默认等于 `characterName` |
| `portraitPath` | 指定立绘大图资源文件的路径或文件名（**必须是 `.png` 格式**） | **必须填写**，若仅填文件名（如 `UI.png`）则去 `Assets/Resources/Sprites/` 下寻找；若包含路径分隔符则按完全路径寻找。特殊值 `"null"` 代表该表情无立绘且不报错。 |
| `spriteName` | 精确指定大图中的子 Sprite 资源名称 | **表头必须包含此列**。若 `characterState` 为 `"default"`（或为空）时允许为空，自动拼为 `{assetName}_default`；若为其他非 default 状态，则**必须填写**，为空将立即报错中断。 |

### 精确查找规则：
1.  **大图定位**：
    *   若 `portraitPath` 包含 `/` 或 `\`，工具将直接按该路径加载大图。
    *   若 `portraitPath` 为单文件名（如 `UI.png`），则默认前往 `Assets/Resources/Sprites/` 目录下查找。
    *   若找不到大图文件，将立即报错并中断导入。
2.  **Sprite 名称匹配**：
    *   若 `spriteName` 不为空，则直接使用 `spriteName`。
    *   若 `spriteName` 为空（要求 `characterState` 必须为 `"default"` 或为空），则使用 `{assetName}_default` 作为目标 Sprite 名字。
3.  **加载子 Sprite**：
    *   通过 `AssetDatabase.LoadAllAssetRepresentationsAtPath` 加载该大图下的所有子 Sprite，遍历比对名字。如果大图中找不到对应名字的子 Sprite，将立即报错并中断导入。

---

## 2. 对话内容 Sheet 规范

每个 Tab（如 `Scene1`）会被独立导出成一个同名文件夹，里面的每一行对话导出一个 `DialogLineSO`。

### **对话表头约定**（列名顺序任意，不区分大小写）：

| 列名 | 说明 | 缺省行为 |
| :--- | :--- | :--- |
| `name` | asset 文件名 | `{Tab}_{序号}` 如 `Scene3_01` |
| `characterName` | 说话的角色显示名 | 空字符串（须与 `Characters` 页中的 `characterName` 一致） |
| `characterState` | 角色当前的表情/状态 | `"default"`（自动关联对应立绘） |
| `dialogText` | 对话文本内容 | 空字符串 |
| `useAudio` | 是否用音频（TRUE/FALSE） | `TRUE` |
| `audioPath` | AudioClip 的 Asset 路径 | `Assets/Resources/Sounds/{Tab}/{name}.mp3` |
| `fixedDuration` | 固定时长（秒，仅在 `useAudio=false` 时生效） | `3` |
| `endPadding` | 末尾缓冲时长（秒） | `0.15` |

### 立绘自动装填规则：
*   对话行解析时，会根据 `characterName` 和 `characterState` 从 `Characters` 字典中加载对应立绘。
*   **无隐式回退**：若在 `Characters` 表中没有配置该角色的映射，或在对话行中填写的 `characterState` 对应表情在 `Characters` 表中不存在，**将立即报错并中断导入**。

---

## 3. 常见报错与排查

1.  **`[错误] Excel 中未找到名为 'Characters' 的专用 Sheet，导入终止！`**
    *   *排查*：检查 Excel 中是否有单独的 Tab 命名为 `Characters`。
2.  **`[错误] 'Characters' Sheet 列头缺少必需列 'spriteName'，导入终止！`**
    *   *排查*：检查 `Characters` Sheet 列头是否遗漏了 `spriteName` 列。
3.  **`[错误] 'Characters' 第 X 行角色「XXX」的状态「YYY」非 default，但其 'spriteName' 为空！`**
    *   *排查*：检查对于非 default 的表情状态，是否在 `spriteName` 列中填写了对应的子 Sprite 名字。
4.  **`[错误] 无法在大图「XXX」中找到名为「YYY」的子 Sprite！`**
    *   *排查*：确认图片资产中是否包含切片出的同名子 Sprite，大图格式是否设置为 Multiple。
5.  **`[错误] 行 X：角色「XXX」状态「YYY」未在 'Characters' Sheet 中配置立绘映射！`**
    *   *排查*：检查对话内容表里填写的角色名和状态，在 `Characters` 页中是否有匹配的行声明。