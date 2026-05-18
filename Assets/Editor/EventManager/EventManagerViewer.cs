using System;
using System.Collections.Generic;
using Core.Event;
using Manager;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Core.Event.EditorTools
{
    public sealed class EventManagerViewer : EditorWindow
    {
        private const string WindowUxmlPath = "Assets/Editor/EventManager/EventManagerViewer.uxml";
        private const string ItemUxmlPath = "Assets/Editor/EventManager/EventManagerViewerItem.uxml";
        private const string UssPath = "Assets/Editor/EventManager/EventManagerViewer.uss";
        private const string CategoryVisiblePrefsPrefix = "VRZoo.EventManagerViewer.CategoryVisible.";

        private static readonly string[] CellLayoutClasses =
        {
            "category-name", "event-name", "target-cell", "method-cell", "payload-cell",
            "small-text-cell", "status-cell", "number-cell", "log-cell", "time-cell"
        };

        private readonly List<CategoryRowData> _categoryRows = new();
        private readonly List<EventDebugInfo> _eventRows = new();
        private readonly List<EventDebugLogRecord> _logRows = new();
        private readonly List<EventListenerInfo> _listenerRows = new();
        private readonly List<EventListenerInvokeInfo> _affectedRows = new();
        private readonly HashSet<string> _categoryVisibilityCache = new();

        private VisualTreeAsset _itemTemplate;
        private ListView _eventList;
        private ListView _listenerList;
        private ListView _logList;
        private ListView _affectedList;
        private Button _eventNameFilterButton;
        private Label _noticeLabel;
        private Label _statusLabel;
        private Label _eventDetailLabel;
        private ToolbarToggle _showRegisterToggle;
        private ToolbarToggle _showUnregisterToggle;
        private ToolbarToggle _showBroadcastToggle;
        private string _selectedEventName;
        private long _selectedLogId;

        [MenuItem("Tools/Event Manager Viewer")]
        public static void ShowWindow()
        {
            EventManagerViewer window = GetWindow<EventManagerViewer>();
            window.titleContent = new GUIContent("Event Manager Viewer");
            window.minSize = new Vector2(1260, 560);
        }

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EventDebugHub.Changed += OnDebugChanged;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EventDebugHub.Changed -= OnDebugChanged;
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();

            StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);
            if (styleSheet != null)
            {
                rootVisualElement.styleSheets.Add(styleSheet);
            }

            VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(WindowUxmlPath);
            if (visualTree == null)
            {
                rootVisualElement.Add(new Label($"未找到 UXML: {WindowUxmlPath}"));
                return;
            }

            visualTree.CloneTree(rootVisualElement);
            _itemTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(ItemUxmlPath);

            _noticeLabel = rootVisualElement.Q<Label>("NoticeLabel");
            _statusLabel = rootVisualElement.Q<Label>("StatusLabel");
            _eventDetailLabel = rootVisualElement.Q<Label>("EventDetailLabel");
            _eventNameFilterButton = rootVisualElement.Q<Button>("EventNameFilterButton");
            _showRegisterToggle = rootVisualElement.Q<ToolbarToggle>("ShowRegisterToggle");
            _showUnregisterToggle = rootVisualElement.Q<ToolbarToggle>("ShowUnregisterToggle");
            _showBroadcastToggle = rootVisualElement.Q<ToolbarToggle>("ShowBroadcastToggle");

            _showRegisterToggle?.SetValueWithoutNotify(false);
            _showUnregisterToggle?.SetValueWithoutNotify(false);
            _showBroadcastToggle?.SetValueWithoutNotify(true);
            _showRegisterToggle?.RegisterValueChangedCallback(_ => RefreshAll());
            _showUnregisterToggle?.RegisterValueChangedCallback(_ => RefreshAll());
            _showBroadcastToggle?.RegisterValueChangedCallback(_ => RefreshAll());

            ToolbarButton refreshButton = rootVisualElement.Q<ToolbarButton>("RefreshButton");
            if (refreshButton != null)
            {
                refreshButton.clicked += RefreshAll;
            }

            ToolbarButton selectLastCallButton = rootVisualElement.Q<ToolbarButton>("SelectLastCallButton");
            if (selectLastCallButton != null)
            {
                selectLastCallButton.clicked += SelectCurrentEventLastCall;
            }

            SetupLists();
            if (_eventNameFilterButton != null)
            {
                _eventNameFilterButton.clicked += ShowCategoryFilterPopup;
            }

            RefreshAll();
        }

        private void SetupLists()
        {
            _eventList = rootVisualElement.Q<ListView>("EventList");
            ConfigureList(_eventList, MakeGenericItem, BindEventItem, 26);
            if (_eventList != null)
            {
                _eventList.selectionChanged += OnEventSelectionChanged;
            }

            _listenerList = rootVisualElement.Q<ListView>("ListenerList");
            ConfigureList(_listenerList, MakeGenericItem, BindListenerItem, 26);

            _logList = rootVisualElement.Q<ListView>("LogList");
            ConfigureList(_logList, MakeGenericItem, BindLogItem, 26);
            if (_logList != null)
            {
                _logList.selectionChanged += OnLogSelectionChanged;
            }

            _affectedList = rootVisualElement.Q<ListView>("AffectedList");
            ConfigureList(_affectedList, MakeGenericItem, BindAffectedItem, 26);
        }

        private void ConfigureList(ListView listView, Func<VisualElement> makeItem, Action<VisualElement, int> bindItem, int itemHeight)
        {
            if (listView == null)
                return;

            listView.makeItem = makeItem;
            listView.bindItem = bindItem;
            listView.fixedItemHeight = itemHeight;
            listView.selectionType = SelectionType.Single;
        }

        private VisualElement MakeGenericItem()
        {
            return _itemTemplate != null ? _itemTemplate.Instantiate() : CreateFallbackItem();
        }

        private static VisualElement CreateFallbackItem()
        {
            VisualElement row = new();
            row.AddToClassList("row");
            Toggle toggle = new() { name = "VisibleToggle" };
            toggle.AddToClassList("cell");
            toggle.AddToClassList("category-visible");
            row.Add(toggle);
            for (int i = 0; i < 10; i++)
            {
                Label label = new() { name = $"Cell{i}" };
                label.AddToClassList("cell");
                row.Add(label);
            }

            return row;
        }

        private void OnDebugChanged()
        {
            RefreshAll();
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state is PlayModeStateChange.EnteredPlayMode or PlayModeStateChange.ExitingPlayMode)
            {
                RefreshAll();
            }
        }

        private void RefreshAll()
        {
            RebuildCategoryRows();
            RebuildEventRows();
            RebuildLogRows();
            RebuildListenerRows();
            RebuildAffectedRows();
            RefreshLists();
            UpdateStatus();
            UpdateEventDetail();
        }

        private void ShowCategoryFilterPopup()
        {
            if (_eventNameFilterButton == null)
                return;

            RebuildCategoryRows();
            UnityEditor.PopupWindow.Show(_eventNameFilterButton.worldBound, new CategoryFilterPopup(this));
        }

        private void RebuildCategoryRows()
        {
            Dictionary<string, CategoryRowData> rowsByCategory = new();
            foreach (EventDebugInfo info in GameManager.Event.GetDebugEvents())
            {
                if (!rowsByCategory.TryGetValue(info.Category, out CategoryRowData row))
                {
                    row = new CategoryRowData(info.Category, IsCategoryVisible(info.Category));
                    rowsByCategory.Add(info.Category, row);
                }
            }

            foreach (EventDebugLogRecord log in GameManager.Event.GetDebugLogs())
            {
                if (!rowsByCategory.ContainsKey(log.Category))
                {
                    rowsByCategory.Add(log.Category, new CategoryRowData(log.Category, IsCategoryVisible(log.Category)));
                }
            }

            _categoryRows.Clear();
            _categoryRows.AddRange(rowsByCategory.Values);
            _categoryRows.Sort((a, b) => string.Compare(a.Category, b.Category, StringComparison.Ordinal));
        }

        private void RebuildEventRows()
        {
            _eventRows.Clear();
            foreach (EventDebugInfo info in GameManager.Event.GetDebugEvents())
            {
                if (IsCategoryVisible(info.Category))
                {
                    _eventRows.Add(info);
                }
            }

            if (!string.IsNullOrEmpty(_selectedEventName) && _eventRows.Find(e => e.EventName == _selectedEventName) == null)
            {
                _selectedEventName = null;
            }
        }

        private void RebuildLogRows()
        {
            _logRows.Clear();
            IReadOnlyList<EventDebugLogRecord> logs = GameManager.Event.GetDebugLogs();
            for (int i = logs.Count - 1; i >= 0; i--)
            {
                EventDebugLogRecord log = logs[i];
                if (!IsCategoryVisible(log.Category) || !ShouldShowLogType(log.Type))
                    continue;

                _logRows.Add(log);
            }

            if (_selectedLogId != 0 && _logRows.FindIndex(log => log.LogId == _selectedLogId) < 0)
            {
                _selectedLogId = 0;
            }
        }

        private void RebuildListenerRows()
        {
            _listenerRows.Clear();
            if (!string.IsNullOrEmpty(_selectedEventName))
            {
                _listenerRows.AddRange(GameManager.Event.GetDebugListeners(_selectedEventName));
            }
        }

        private void RebuildAffectedRows()
        {
            _affectedRows.Clear();
            EventDebugLogRecord log = FindLog(_selectedLogId);
            if (log != null)
            {
                _affectedRows.AddRange(log.AffectedListeners);
            }
        }

        private void RefreshLists()
        {
            BindItemsSource(_eventList, _eventRows);
            BindItemsSource(_listenerList, _listenerRows);
            BindItemsSource(_logList, _logRows);
            BindItemsSource(_affectedList, _affectedRows);
            UpdateEventNameFilterButtonText();
        }

        private static void BindItemsSource<T>(ListView listView, List<T> source)
        {
            if (listView == null)
                return;

            listView.itemsSource = source;
            listView.RefreshItems();
        }

        private void BindEventItem(VisualElement element, int index)
        {
            EventDebugInfo row = _eventRows[index];
            HideToggle(element);
            SetCell(element, 0, row.EventName, "event-name");
            SetCell(element, 1, row.Category, "small-text-cell");
            SetCell(element, 2, row.PayloadTypeName, "small-text-cell");
            SetCell(element, 3, row.NoPayloadListenerCount.ToString(), "number-cell");
            SetCell(element, 4, row.PayloadListenerCount.ToString(), "number-cell");
            SetCell(element, 5, row.TotalListenerCount.ToString(), "number-cell");
            SetCell(element, 6, row.RegisterCount.ToString(), "number-cell");
            SetCell(element, 7, row.UnregisterCount.ToString(), "number-cell");
            SetCell(element, 8, row.BroadcastCount.ToString(), "number-cell");
            SetCell(element, 9, FormatLogId(row.LastCallLogId), "log-cell");
        }

        private void BindListenerItem(VisualElement element, int index)
        {
            EventListenerInfo row = _listenerRows[index];
            HideToggle(element);
            SetCell(element, 0, row.Status, "status-cell");
            SetCell(element, 1, string.IsNullOrEmpty(row.TargetGameObjectPath) ? "-" : row.TargetGameObjectPath, "target-cell");
            SetCell(element, 2, row.TargetTypeName, "small-text-cell");
            SetCell(element, 3, row.MethodName, "method-cell");
            SetCell(element, 4, FormatLogId(row.RegisteredLogId), "log-cell");
            SetCell(element, 5, row.InvokeCount.ToString(), "number-cell");
            SetCell(element, 6, FormatLogId(row.LastCallLogId), "log-cell");
            HideCells(element, 7, 10);
        }

        private void BindLogItem(VisualElement element, int index)
        {
            EventDebugLogRecord row = _logRows[index];
            HideToggle(element);
            SetCell(element, 0, FormatLogId(row.LogId), "log-cell");
            SetCell(element, 1, row.Time.ToString("F2"), "time-cell");
            SetCell(element, 2, row.Type.ToString(), "status-cell");
            SetCell(element, 3, row.EventName, "event-name");
            SetCell(element, 4, row.Source.DisplayName, "target-cell");
            SetCell(element, 5, row.Caller.DisplayName, "method-cell");
            SetCell(element, 6, row.PayloadPreview, "payload-cell");
            SetCell(element, 7, row.AffectedCount.ToString(), "number-cell");
            HideCells(element, 8, 10);
        }

        private void BindAffectedItem(VisualElement element, int index)
        {
            EventListenerInvokeInfo row = _affectedRows[index];
            HideToggle(element);
            SetCell(element, 0, row.Order.ToString(), "number-cell");
            SetCell(element, 1, string.IsNullOrEmpty(row.TargetGameObjectPath) ? "-" : row.TargetGameObjectPath, "target-cell");
            SetCell(element, 2, row.TargetTypeName, "small-text-cell");
            SetCell(element, 3, row.MethodName, "method-cell");
            HideCells(element, 4, 10);
        }

        private void OnEventSelectionChanged(IEnumerable<object> selectedItems)
        {
            foreach (object item in selectedItems)
            {
                if (item is EventDebugInfo info)
                {
                    _selectedEventName = info.EventName;
                    RebuildListenerRows();
                    BindItemsSource(_listenerList, _listenerRows);
                    UpdateEventDetail();
                    return;
                }
            }
        }

        private void OnLogSelectionChanged(IEnumerable<object> selectedItems)
        {
            foreach (object item in selectedItems)
            {
                if (item is EventDebugLogRecord log)
                {
                    _selectedLogId = log.LogId;
                    RebuildAffectedRows();
                    BindItemsSource(_affectedList, _affectedRows);
                    return;
                }
            }
        }

        private void SelectCurrentEventLastCall()
        {
            EventDebugInfo selectedEvent = FindEvent(_selectedEventName);
            if (selectedEvent == null || selectedEvent.LastCallLogId == 0)
                return;

            _selectedLogId = selectedEvent.LastCallLogId;
            RebuildAffectedRows();
            RebuildLogRows();
            RefreshLists();

            int index = _logRows.FindIndex(log => log.LogId == _selectedLogId);
            if (_logList != null && index >= 0)
            {
                _logList.selectedIndex = index;
                _logList.ScrollToItem(index);
            }
        }

        private void UpdateStatus()
        {
            bool isPlaying = EditorApplication.isPlaying;
            if (_noticeLabel != null)
            {
                _noticeLabel.style.display = isPlaying ? DisplayStyle.None : DisplayStyle.Flex;
                _noticeLabel.text = "进入 Play Mode 后显示运行时事件流。";
            }

            if (_statusLabel == null)
                return;

            int eventCount = _eventRows.Count;
            int categoryCount = _categoryRows.Count;

            _statusLabel.text = isPlaying
                ? $"分类: {categoryCount}    显示事件: {eventCount}    Log: {_logRows.Count}"
                : "当前未运行。";
        }

        private void UpdateEventDetail()
        {
            if (_eventDetailLabel == null)
                return;

            EventDebugInfo info = FindEvent(_selectedEventName);
            if (info == null)
            {
                _eventDetailLabel.text = "未选择事件。";
                return;
            }

            _eventDetailLabel.text =
                $"EventName: {info.EventName}\n" +
                $"Category: {info.Category}\n" +
                $"PayloadType: {info.PayloadTypeName}\n" +
                $"FirstRegisteredAt: {FormatLogId(info.FirstRegisteredLogId)}\n" +
                $"LastRegisteredAt: {FormatLogId(info.LastRegisteredLogId)}\n" +
                $"LastUnregisteredAt: {FormatLogId(info.LastUnregisteredLogId)}\n" +
                $"LastCall: {FormatLogId(info.LastCallLogId)}";
        }

        private bool ShouldShowLogType(EventDebugLogType type)
        {
            return type switch
            {
                EventDebugLogType.Register => _showRegisterToggle == null || _showRegisterToggle.value,
                EventDebugLogType.Unregister => _showUnregisterToggle == null || _showUnregisterToggle.value,
                EventDebugLogType.Broadcast => _showBroadcastToggle == null || _showBroadcastToggle.value,
                _ => true
            };
        }

        private bool IsCategoryVisible(string category)
        {
            if (_categoryVisibilityCache.Contains(category))
                return true;

            return EditorPrefs.GetBool(CategoryVisiblePrefsPrefix + category, true);
        }

        private void SetCategoryVisible(string category, bool visible)
        {
            EditorPrefs.SetBool(CategoryVisiblePrefsPrefix + category, visible);
            if (visible)
                _categoryVisibilityCache.Add(category);
            else
                _categoryVisibilityCache.Remove(category);
        }

        private void SetCategoryVisibleFromPopup(string category, bool visible)
        {
            SetCategoryVisible(category, visible);
            RefreshAll();
            Repaint();
        }

        private void UpdateEventNameFilterButtonText()
        {
            if (_eventNameFilterButton == null)
                return;

            int visibleCount = 0;
            foreach (CategoryRowData row in _categoryRows)
            {
                if (row.Visible)
                    visibleCount++;
            }

            _eventNameFilterButton.text = _categoryRows.Count > 0 && visibleCount < _categoryRows.Count
                ? $"EventName ({visibleCount}/{_categoryRows.Count}) ▾"
                : "EventName ▾";
        }

        private EventDebugInfo FindEvent(string eventName)
        {
            if (string.IsNullOrEmpty(eventName))
                return null;

            foreach (EventDebugInfo info in GameManager.Event.GetDebugEvents())
            {
                if (info.EventName == eventName)
                    return info;
            }

            return null;
        }

        private EventDebugLogRecord FindLog(long logId)
        {
            if (logId == 0)
                return null;

            foreach (EventDebugLogRecord log in GameManager.Event.GetDebugLogs())
            {
                if (log.LogId == logId)
                    return log;
            }

            return null;
        }

        private static void HideToggle(VisualElement element)
        {
            Toggle toggle = element.Q<Toggle>("VisibleToggle");
            if (toggle != null)
            {
                toggle.style.display = DisplayStyle.None;
            }
        }

        private static void HideCells(VisualElement element, int startInclusive, int endExclusive)
        {
            for (int i = startInclusive; i < endExclusive; i++)
            {
                Label label = element.Q<Label>($"Cell{i}");
                if (label != null)
                {
                    label.style.display = DisplayStyle.None;
                }
            }
        }

        private static void SetCell(VisualElement element, int cellIndex, string text, string layoutClass)
        {
            Label label = element.Q<Label>($"Cell{cellIndex}");
            if (label == null)
                return;

            label.style.display = DisplayStyle.Flex;
            label.text = text;
            label.tooltip = text;
            foreach (string className in CellLayoutClasses)
            {
                label.RemoveFromClassList(className);
            }
            label.AddToClassList(layoutClass);
        }

        private static string FormatLogId(long logId)
        {
            return logId > 0 ? $"#{logId}" : "-";
        }

        private sealed class CategoryRowData
        {
            public string Category { get; }
            public bool Visible { get; }

            public CategoryRowData(string category, bool visible)
            {
                Category = category;
                Visible = visible;
            }
        }

        private sealed class CategoryFilterPopup : PopupWindowContent
        {
            private readonly EventManagerViewer _viewer;
            private Vector2 _scrollPosition;

            public CategoryFilterPopup(EventManagerViewer viewer)
            {
                _viewer = viewer;
            }

            public override Vector2 GetWindowSize()
            {
                float height = _viewer._categoryRows.Count > 0
                    ? Mathf.Clamp(_viewer._categoryRows.Count * 22 + 10, 42, 320)
                    : 42;
                return new Vector2(260, height);
            }

            public override void OnGUI(Rect rect)
            {
                if (_viewer._categoryRows.Count == 0)
                {
                    EditorGUILayout.LabelField("No categories", EditorStyles.miniLabel);
                    return;
                }

                _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
                for (int i = 0; i < _viewer._categoryRows.Count; i++)
                {
                    CategoryRowData row = _viewer._categoryRows[i];
                    bool visible = EditorGUILayout.ToggleLeft(row.Category, row.Visible);
                    if (visible != row.Visible)
                    {
                        _viewer.SetCategoryVisibleFromPopup(row.Category, visible);
                        editorWindow?.Repaint();
                        break;
                    }
                }
                EditorGUILayout.EndScrollView();
            }
        }
    }
}
