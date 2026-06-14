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

| 列名 | 说明 | 缺省行为 |
| :--- | :--- | :--- |
| `characterName` | 说话的角色显示名（在对话 UI 展现的名字，如 `DodoChief`） | 空字符串 |
| `characterState` | 表情或状态名称（如 `default`, `happy`, `sad`） | `"default"` |
| `assetName` | 美术资源文件使用名（用于寻路及大图匹配的文件名） | 留空时默认等于 `characterName` |
| `portraitPath` | 显式指定立绘资源文件的具体路径（**必须是 `.png` 格式**） | 留空时走“智能多模式查找”；特殊值 `"null"` 代表该表情无立绘且不报错 |

### 智能多模式查找规则（当 `portraitPath` 留空时）：
1.  **模式 1 (独立小图模式)**：优先检索 `Assets/Resources/Sprites/{assetName}/{characterState}.png`。
2.  **模式 2 (共享大图模式)**：若模式 1 未找到，检索 `Assets/Resources/Sprites/{assetName}.png`，并从中查找名字与 `characterState` 相同的子 Sprite。
3.  **表情回退**：若上述模式均找不到（且 `characterState` 不是 `"default"`），会自动回退去寻找该角色的 `"default"` 状态立绘（同样依序走模式 1 和模式 2，查找 `default` 状态）。

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
*   **回退规则**：若该角色没有配置对应的表情（且非显式设为 `"null"`），会自动尝试关联该角色的 `"default"` 立绘。
*   **报错中断**：若在 `Characters` 表中根本没有该角色的映射，或关联的立绘无法载入，**将立即报错并中断导入**。

---

## 3. 常见报错与排查

1.  **`[错误] Excel 中未找到名为 'Characters' 的专用 Sheet，导入终止！`**
    *   *排查*：检查 Excel 中是否有单独的 Tab 命名为 `Characters`。
2.  **`[错误] 行 X：角色「XXX」未在 'Characters' Sheet 中配置立绘映射！`**
    *   *排查*：对话表中的角色没有在 `Characters` Sheet 中声明 `default` 表情。
3.  **`[错误] 'Characters' 第 X 行角色「XXX」（资源名「YYY」）状态「ZZZ」的立绘资源未找到！`**
    *   *排查*：检查 `portraitPath` 填写的路径是否正确，或缺省位置（`Assets/Resources/Sprites/`）下是否存在对应的 `{assetName}` 文件夹、`.png` 独立文件或大图。