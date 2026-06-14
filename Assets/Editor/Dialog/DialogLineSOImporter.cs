using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;   // xlsx
using NPOI.HSSF.UserModel;   // xls

namespace Core.Dialog.Timeline
{
    /// <summary>
    /// Excel → DialogLineSO 批量生成工具。
    /// 菜单入口：Tools / Dialog / Import DialogLineSO from Excel
    ///
    /// Excel 表格约定（每个 Tab 独立处理）：
    /// 第一行为列头，顺序不限，列名如下：
    ///   name          - asset 文件名（缺省时按 {Tab}_{序号} 命名，如 Scene3_01）
    ///   characterName - 角色名
    ///   dialogText    - 对话文本
    ///   useAudio      - 是否使用音频（TRUE/FALSE，缺省视为 true）
    ///   audioPath     - AudioClip 的 Asset 路径（缺省时使用默认路径规则）
    ///   fixedDuration - 固定时长（秒，缺省 3）
    ///   endPadding    - 末尾缓冲（秒，缺省 0.15）
    ///
    /// 音频默认路径规则（useAudio=true 且 audioPath 为空时）：
    ///   Assets/Resources/Sounds/{Tab名}/{line名}.mp3
    ///
    /// 输出路径：Assets/Prefabs/SO/Dialog/{Tab名}/{line名}.asset
    /// 已存在时覆盖。
    /// </summary>
    public class DialogLineSOImporter : EditorWindow
    {
        // ── 常量 ────────────────────────────────────────────────────────────
        private const string OutputRoot   = "Assets/Prefabs/SO/Dialog";
        private const string AudioRoot    = "Assets/Resources/Sounds";
        private const string DefaultAudioExt = ".mp3";
 
        // ── 状态 ────────────────────────────────────────────────────────────
        private string _excelPath = "";
        private Vector2 _scroll;
        private readonly List<string> _log = new();
 
        // ── 菜单 ────────────────────────────────────────────────────────────
        [MenuItem("Tools/Dialog/Import DialogLineSO from Excel")]
        public static void Open() =>
            GetWindow<DialogLineSOImporter>("DialogLineSO Importer");
 
        // ── UI ──────────────────────────────────────────────────────────────
        private void OnGUI()
        {
            GUILayout.Label("Excel → DialogLineSO 生成工具", EditorStyles.boldLabel);
            EditorGUILayout.Space();
 
            using (new EditorGUILayout.HorizontalScope())
            {
                _excelPath = EditorGUILayout.TextField("Excel 路径", _excelPath);
                if (GUILayout.Button("选择", GUILayout.Width(50)))
                {
                    string path = EditorUtility.OpenFilePanel(
                        "选择 Excel 文件", Application.dataPath, "xlsx,xls");
                    if (!string.IsNullOrEmpty(path))
                        _excelPath = path;
                }
            }
 
            EditorGUILayout.Space();
 
            GUI.enabled = !string.IsNullOrEmpty(_excelPath) && File.Exists(_excelPath);
            if (GUILayout.Button("开始生成", GUILayout.Height(32)))
            {
                _log.Clear();
                Import(_excelPath);
            }
            GUI.enabled = true;
 
            EditorGUILayout.Space();
            GUILayout.Label("日志", EditorStyles.boldLabel);
            _scroll = EditorGUILayout.BeginScrollView(_scroll,
                GUILayout.ExpandHeight(true));
            foreach (var line in _log)
                EditorGUILayout.LabelField(line, EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndScrollView();
        }
 
        // ── 核心逻辑 ────────────────────────────────────────────────────────
        private void Import(string excelPath)
        {
            IWorkbook workbook;
            try
            {
                using var fs = new FileStream(excelPath, FileMode.Open,
                    FileAccess.Read, FileShare.ReadWrite);
                workbook = excelPath.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)
                    ? new XSSFWorkbook(fs)
                    : new HSSFWorkbook(fs);
            }
            catch (Exception e)
            {
                Log($"[错误] 打开 Excel 失败：{e.Message}");
                return;
            }
 
            int totalCreated = 0;
            int totalError   = 0;

            // 1. 强制寻找并解析 Characters sheet
            ISheet charactersSheet = null;
            for (int si = 0; si < workbook.NumberOfSheets; si++)
            {
                var sheet = workbook.GetSheetAt(si);
                if (sheet.SheetName.Equals("Characters", StringComparison.OrdinalIgnoreCase))
                {
                    charactersSheet = sheet;
                    break;
                }
            }
            if (charactersSheet == null)
            {
                Log("[错误] Excel 中未找到名为 'Characters' 的专用 Sheet，导入终止！");
                return;
            }

            // 解析 Characters sheet 列头
            var charHeaderRow = charactersSheet.GetRow(0);
            if (charHeaderRow == null)
            {
                Log("[错误] 'Characters' Sheet 无列头行，导入终止！");
                return;
            }
            var charCols = ParseHeader(charHeaderRow);
            if (!charCols.ContainsKey("characterName"))
            {
                Log("[错误] 'Characters' Sheet 列头缺少必需列 'characterName'，导入终止！");
                return;
            }

            // 建立立绘映射字典
            var characterPortraits = new Dictionary<(string name, string state), Sprite>();
            for (int ri = 1; ri <= charactersSheet.LastRowNum; ri++)
            {
                var row = charactersSheet.GetRow(ri);
                if (IsRowEmpty(row)) continue;

                string characterName = GetCol(row, charCols, "characterName");
                if (string.IsNullOrWhiteSpace(characterName))
                {
                    Log($"[错误] 'Characters' 第 {ri + 1} 行 characterName 为空，导入终止！");
                    return;
                }

                string characterState = GetCol(row, charCols, "characterState");
                if (string.IsNullOrWhiteSpace(characterState))
                    characterState = "default";

                string assetName = GetCol(row, charCols, "assetName");
                if (string.IsNullOrWhiteSpace(assetName))
                    assetName = characterName;

                string portraitPath = GetCol(row, charCols, "portraitPath");

                Sprite portraitSprite = null;
                if (portraitPath.Trim().Equals("null", StringComparison.OrdinalIgnoreCase))
                {
                    portraitSprite = null;
                }
                else
                {
                    portraitSprite = FindPortraitSprite(assetName, characterState, portraitPath);
                    if (portraitSprite == null)
                    {
                        Log($"[错误] 'Characters' 第 {ri + 1} 行角色「{characterName}」（资源名「{assetName}」）状态「{characterState}」的立绘资源未找到！导入终止！");
                        return;
                    }
                }

                var key = (characterName.ToLowerInvariant().Trim(), characterState.ToLowerInvariant().Trim());
                characterPortraits[key] = portraitSprite;
            }

            // 2. 导入对话 Sheets
            for (int si = 0; si < workbook.NumberOfSheets; si++)
            {
                var sheet = workbook.GetSheetAt(si);
                string tabName = sheet.SheetName;
                if (tabName.Equals("Characters", StringComparison.OrdinalIgnoreCase))
                    continue;

                Log($"\n── Tab：{tabName} ──");

                // 解析列头（第 0 行）
                var headerRow = sheet.GetRow(0);
                if (headerRow == null)
                {
                    Log($"  [跳过] 无列头行");
                    continue;
                }

                var colIndex = ParseHeader(headerRow);
                int lineSeq = 0; // 序号计数器（用于缺省命名）

                for (int ri = 1; ri <= sheet.LastRowNum; ri++)
                {
                    var row = sheet.GetRow(ri);
                    if (IsRowEmpty(row)) continue;

                    lineSeq++;
                    bool ok = ProcessRow(row, colIndex, tabName, lineSeq, characterPortraits,
                        out string assetPath);

                    if (ok)
                    {
                        totalCreated++;
                    }
                    else
                    {
                        Log($"[错误] 导入在行 {row.RowNum + 1} 中断。");
                        return;
                    }
                }
            }
 
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
 
            Log($"\n完成：生成 {totalCreated} 个，错误 {totalError} 个。");
        }
 
        // ── 列头解析 ────────────────────────────────────────────────────────
        private static readonly string[] KnownColumns =
        {
            "name", "characterName", "characterState", "dialogText",
            "useAudio", "audioPath", "fixedDuration", "endPadding"
        };
 
        private Dictionary<string, int> ParseHeader(IRow headerRow)
        {
            var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int ci = 0; ci < headerRow.LastCellNum; ci++)
            {
                string val = GetCellString(headerRow.GetCell(ci));
                if (!string.IsNullOrWhiteSpace(val))
                    dict[val.Trim()] = ci;
            }
            return dict;
        }
 
        // ── 单行处理 ────────────────────────────────────────────────────────
        private bool ProcessRow(IRow row, Dictionary<string, int> col,
            string tabName, int seq, Dictionary<(string, string), Sprite> portraitsDict, out string assetPath)
        {
            assetPath = "";

            // ── 读取字段 ──
            string lineName      = GetCol(row, col, "name");
            string characterName = GetCol(row, col, "characterName");
            string characterState = GetCol(row, col, "characterState");
            if (string.IsNullOrWhiteSpace(characterState))
                characterState = "default";

            string dialogText    = GetCol(row, col, "dialogText");
            string useAudioStr   = GetCol(row, col, "useAudio");
            string audioPath     = GetCol(row, col, "audioPath");
            string fixedDurStr   = GetCol(row, col, "fixedDuration");
            string endPadStr     = GetCol(row, col, "endPadding");

            // ── 缺省命名 ──
            bool nameDefault = string.IsNullOrWhiteSpace(lineName);
            if (nameDefault)
                lineName = $"{tabName}_{seq:D2}";

            // ── useAudio 解析（缺省 true）──
            bool useAudio = string.IsNullOrWhiteSpace(useAudioStr)
                || useAudioStr.Trim().Equals("true", StringComparison.OrdinalIgnoreCase)
                || useAudioStr.Trim() == "1";

            // ── 音频路径解析 ──
            string resolvedAudioAssetPath = "";
            if (useAudio)
            {
                if (string.IsNullOrWhiteSpace(audioPath))
                {
                    // 缺省路径规则
                    resolvedAudioAssetPath =
                        $"{AudioRoot}/{tabName}/{lineName}{DefaultAudioExt}";
                }
                else
                {
                    resolvedAudioAssetPath = audioPath.Trim();
                }

                // 立即校验是否能找到
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(resolvedAudioAssetPath);
                if (clip == null)
                {
                    Log($"  [错误] 行 {row.RowNum + 1}「{lineName}」：useAudio=true " +
                        $"但找不到音频：{resolvedAudioAssetPath}");
                    return false;
                }
            }

            // 立绘解析
            Sprite portraitSprite = null;
            if (!string.IsNullOrWhiteSpace(characterName))
            {
                var key = (characterName.ToLowerInvariant().Trim(), characterState.ToLowerInvariant().Trim());
                if (portraitsDict.TryGetValue(key, out var sprite))
                {
                    portraitSprite = sprite;
                }
                else
                {
                    // 回退到默认表情
                    var defKey = (characterName.ToLowerInvariant().Trim(), "default");
                    if (portraitsDict.TryGetValue(defKey, out var defSprite))
                    {
                        portraitSprite = defSprite;
                    }
                    else
                    {
                        Log($"  [错误] 行 {row.RowNum + 1}：角色「{characterName}」未在 'Characters' Sheet 中配置立绘映射！");
                        return false;
                    }
                }
            }

            // ── 数值字段 ──
            float fixedDuration = 3f;
            if (!string.IsNullOrWhiteSpace(fixedDurStr) &&
                float.TryParse(fixedDurStr, out float fd))
                fixedDuration = fd;
 
            float endPadding = 0.15f;
            if (!string.IsNullOrWhiteSpace(endPadStr) &&
                float.TryParse(endPadStr, out float ep))
                endPadding = ep;
 
            // ── 确保目录存在 ──
            string dirPath = $"{OutputRoot}/{tabName}";
            if (!AssetDatabase.IsValidFolder(dirPath))
                CreateFolderRecursive(dirPath);
 
            // ── 创建或覆盖 SO ──
            assetPath = $"{dirPath}/{lineName}.asset";
            var so = AssetDatabase.LoadAssetAtPath<DialogLineSO>(assetPath);
            bool isNew = so == null;
            if (isNew)
                so = CreateInstance<DialogLineSO>();
 
            so.characterName  = characterName;
            so.characterState = characterState;
            so.dialogText     = dialogText;
            so.characterPortrait = portraitSprite;
            so.useAudioDuration = useAudio;
            so.fixedDuration  = fixedDuration;
            so.endPadding     = endPadding;
 
            // 音频
            so.voiceClip = useAudio
                ? AssetDatabase.LoadAssetAtPath<AudioClip>(resolvedAudioAssetPath)
                : null;
 
            if (isNew)
            {
                AssetDatabase.CreateAsset(so, assetPath);
                Log($"  [新建] {assetPath}");
            }
            else
            {
                EditorUtility.SetDirty(so);
                Log($"  [覆盖] {assetPath}");
            }
 
            return true;
        }

        /// <summary>
        /// 寻找并提取立绘 Sprite，仅寻找 .png 格式。
        /// </summary>
        private Sprite FindPortraitSprite(string assetName, string characterState, string portraitPath)
        {
            // 1. 若填了具体路径 (非 "null")
            if (!string.IsNullOrWhiteSpace(portraitPath))
            {
                if (!portraitPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                {
                    Log($"  [错误] 指定的立绘路径必须是 .png 文件：{portraitPath}");
                    return null;
                }

                // 尝试直接加载单图 Sprite
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(portraitPath);
                if (sprite != null) return sprite;

                // 尝试加载大图的子 Sprite
                var allAssets = AssetDatabase.LoadAllAssetsAtPath(portraitPath);
                foreach (var asset in allAssets)
                {
                    if (asset is Sprite subSprite && subSprite.name.Equals(characterState, StringComparison.OrdinalIgnoreCase))
                    {
                        return subSprite;
                    }
                }
                return null;
            }

            // 2. 留空，走缺省规则 (仅限 .png)
            // 模式 1：独立图片模式
            string path1 = $"Assets/Resources/Sprites/{assetName}/{characterState}.png";
            var s1 = AssetDatabase.LoadAssetAtPath<Sprite>(path1);
            if (s1 != null) return s1;

            // 模式 2：共享大图模式 (Assets/Resources/Sprites/{assetName}.png 里找同名子 Sprite)
            string path2 = $"Assets/Resources/Sprites/{assetName}.png";
            var assets2 = AssetDatabase.LoadAllAssetsAtPath(path2);
            if (assets2 != null && assets2.Length > 0)
            {
                foreach (var asset in assets2)
                {
                    if (asset is Sprite subSprite && subSprite.name.Equals(characterState, StringComparison.OrdinalIgnoreCase))
                    {
                        return subSprite;
                    }
                }
            }

            // 3. 回退规则：若特定 state 未找到，尝试回退寻找默认（default）表情
            if (!characterState.Equals("default", StringComparison.OrdinalIgnoreCase))
            {
                Log($"  [提示] 角色「{assetName}」的状态「{characterState}」未找到立绘，尝试寻找其「default」状态...");

                // 回退到模式 1 默认立绘
                string defPath1 = $"Assets/Resources/Sprites/{assetName}/default.png";
                var defS1 = AssetDatabase.LoadAssetAtPath<Sprite>(defPath1);
                if (defS1 != null) return defS1;

                // 回退到模式 2 默认子 Sprite
                if (assets2 != null && assets2.Length > 0)
                {
                    foreach (var asset in assets2)
                    {
                        if (asset is Sprite subSprite && subSprite.name.Equals("default", StringComparison.OrdinalIgnoreCase))
                        {
                            return subSprite;
                        }
                    }
                }
            }

            return null;
        }
 
        // ── 工具方法 ────────────────────────────────────────────────────────
 
        private string GetCol(IRow row, Dictionary<string, int> col, string key)
        {
            if (!col.TryGetValue(key, out int ci)) return "";
            return GetCellString(row.GetCell(ci));
        }
 
        private static string GetCellString(ICell cell)
        {
            if (cell == null) return "";
            return cell.CellType switch
            {
                CellType.String  => cell.StringCellValue?.Trim() ?? "",
                CellType.Numeric => cell.NumericCellValue.ToString(),
                CellType.Boolean => cell.BooleanCellValue.ToString(),
                CellType.Formula => cell.ToString()?.Trim() ?? "",
                _                => ""
            };
        }
 
        private static bool IsRowEmpty(IRow row)
        {
            if (row == null) return true;
            for (int ci = 0; ci < row.LastCellNum; ci++)
            {
                var c = row.GetCell(ci);
                if (c != null && c.CellType != CellType.Blank &&
                    !string.IsNullOrWhiteSpace(c.ToString()))
                    return false;
            }
            return true;
        }
 
        /// <summary>递归创建 Assets 下的多级目录。</summary>
        private static void CreateFolderRecursive(string path)
        {
            // path 形如 "Assets/Prefabs/SO/Dialog/Scene3"
            var parts = path.Split('/');
            string current = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
 
        private void Log(string msg)
        {
            _log.Add(msg);
            Debug.Log($"[DialogImporter] {msg}");
            Repaint();
        }
    }
}