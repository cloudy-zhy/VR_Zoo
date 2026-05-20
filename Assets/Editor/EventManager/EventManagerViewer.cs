using System;
using System.Collections.Generic;
using System.Linq;
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
        private const string UssPath = "Assets/Editor/EventManager/EventManagerViewer.uss";
        private const string CategoryVisiblePrefsPrefix = "VRZoo.EventManagerViewer.CategoryVisible.";

        private readonly List<CategoryRowData> _categoryRows = new();
        private readonly List<EventDebugInfo> _eventRows = new();
        private readonly List<EventDebugLogRecord> _logRows = new();
        private readonly List<EventListenerInfo> _listenerRows = new();
        private readonly List<EventListenerInvokeInfo> _affectedRows = new();
        private readonly HashSet<string> _categoryVisibilityCache = new();

        // UI 元素缓存
        private MultiColumnListView _eventList;
        private MultiColumnListView _listenerList;
        private MultiColumnListView _logList;
        private MultiColumnListView _affectedList;

        private ToolbarButton _categoryFilterButton;
        private ToolbarButton _columnsButton;
        private ToolbarToggle _showRegisterToggle;
        private ToolbarToggle _showUnregisterToggle;
        private ToolbarToggle _showBroadcastToggle;

        private Label _noticeLabel;
        private Label _statusLabel;
        private Label _eventDetailLabel;

        private Label _listenersCountBadge;
        private Label _logsCountBadge;
        private Label _affectedCountBadge;

        private Button _listenersTabButton;
        private Button _historyTabButton;
        private VisualElement _listenersTabContent;
        private VisualElement _historyTabContent;

        private Toggle _selectedEventOnlyToggle;
        private Button _selectLastCallButton;

        private string _selectedEventName;
        private long _selectedLogId;

        // 列合并过滤状态
        private bool _filterShowNoPayload = true;
        private bool _filterShowPayload = true;
        private bool _activityShowReg = true;
        private bool _activityShowUnreg = true;
        private bool _activityShowBroadcast = true;
        private bool _activityCombinedView = true;

        [MenuItem("Tools/Event Manager Viewer")]
        public static void ShowWindow()
        {
            EventManagerViewer window = GetWindow<EventManagerViewer>();
            window.titleContent = new GUIContent("Event Manager Viewer");
            window.minSize = new Vector2(1260, 580);
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

            // 获取 UI 元素缓存
            _noticeLabel = rootVisualElement.Q<Label>("NoticeLabel");
            _statusLabel = rootVisualElement.Q<Label>("StatusLabel");
            _eventDetailLabel = rootVisualElement.Q<Label>("EventDetailLabel");

            _listenersCountBadge = rootVisualElement.Q<Label>("ListenersCountBadge");
            _logsCountBadge = rootVisualElement.Q<Label>("LogsCountBadge");
            _affectedCountBadge = rootVisualElement.Q<Label>("AffectedCountBadge");

            _categoryFilterButton = rootVisualElement.Q<ToolbarButton>("CategoryFilterButton");
            _columnsButton = rootVisualElement.Q<ToolbarButton>("ColumnsButton");
            _showRegisterToggle = rootVisualElement.Q<ToolbarToggle>("ShowRegisterToggle");
            _showUnregisterToggle = rootVisualElement.Q<ToolbarToggle>("ShowUnregisterToggle");
            _showBroadcastToggle = rootVisualElement.Q<ToolbarToggle>("ShowBroadcastToggle");

            _selectedEventOnlyToggle = rootVisualElement.Q<Toggle>("SelectedEventOnlyToggle");
            _selectLastCallButton = rootVisualElement.Q<Button>("SelectLastCallButton");

            // 工具栏交互绑定
            if (_categoryFilterButton != null) _categoryFilterButton.clicked += ShowCategoryMenuFromToolbar;
            if (_columnsButton != null) _columnsButton.clicked += ShowColumnsMenu;

            _showRegisterToggle?.SetValueWithoutNotify(false);
            _showUnregisterToggle?.SetValueWithoutNotify(false);
            _showBroadcastToggle?.SetValueWithoutNotify(true);

            _showRegisterToggle?.RegisterValueChangedCallback(_ => RefreshAll());
            _showUnregisterToggle?.RegisterValueChangedCallback(_ => RefreshAll());
            _showBroadcastToggle?.RegisterValueChangedCallback(_ => RefreshAll());

            ToolbarButton refreshButton = rootVisualElement.Q<ToolbarButton>("RefreshButton");
            if (refreshButton != null) refreshButton.clicked += RefreshAll;

            if (_selectLastCallButton != null) _selectLastCallButton.clicked += SelectCurrentEventLastCall;

            _selectedEventOnlyToggle?.RegisterValueChangedCallback(_ => {
                RebuildLogRows();
                BindItemsSource(_logList, _logRows);
                UpdateBadges();
            });

            SetupTabs();
            SetupLists();
            SetupHeaderDropdowns();
            RefreshAll();
        }

        private void SetupTabs()
        {
            _listenersTabButton = rootVisualElement.Q<Button>("ListenersTabButton");
            _historyTabButton = rootVisualElement.Q<Button>("HistoryTabButton");
            _listenersTabContent = rootVisualElement.Q<VisualElement>("ListenersTabContent");
            _historyTabContent = rootVisualElement.Q<VisualElement>("HistoryTabContent");

            if (_listenersTabButton != null && _historyTabButton != null &&
                _listenersTabContent != null && _historyTabContent != null)
            {
                _listenersTabButton.clicked += () => SwitchTab(true);
                _historyTabButton.clicked += () => SwitchTab(false);
            }
        }

        private void SwitchTab(bool showListeners)
        {
            if (showListeners)
            {
                _listenersTabButton.AddToClassList("tab-button--active");
                _historyTabButton.RemoveFromClassList("tab-button--active");
                _listenersTabContent.AddToClassList("tab-content--active");
                _historyTabContent.RemoveFromClassList("tab-content--active");
            }
            else
            {
                _historyTabButton.AddToClassList("tab-button--active");
                _listenersTabButton.RemoveFromClassList("tab-button--active");
                _historyTabContent.AddToClassList("tab-content--active");
                _listenersTabContent.RemoveFromClassList("tab-content--active");
            }
        }

        private void SetupLists()
        {
            // 1. 事件列表 MultiColumnListView
            _eventList = rootVisualElement.Q<MultiColumnListView>("EventList");
            if (_eventList != null)
            {
                _eventList.columns.Clear();
                
                AddEventListColumn("EventName", "Event Name", 190, (lbl, info) => lbl.text = info.EventName);
                AddEventListColumn("Category", "Category", 110, (lbl, info) => lbl.text = info.Category);
                AddEventListColumn("PayloadTypeName", "Payload Type", 120, (lbl, info) => lbl.text = info.PayloadTypeName);
                
                AddEventListColumn("Listeners", "Listeners", 90, (lbl, info) => {
                    lbl.text = GetListenersDisplay(info);
                }, true);
                
                AddEventListColumn("Activity", "Activity", 140, (lbl, info) => {
                    lbl.text = GetActivityDisplay(info);
                    lbl.AddToClassList("color-broadcast");
                }, true);
                
                AddEventListColumn("LastCall", "Last Log", 75, (lbl, info) => {
                    lbl.text = FormatLogId(info.LastCallLogId);
                    lbl.AddToClassList("color-muted");
                }, true);

                _eventList.fixedItemHeight = 24;
                _eventList.selectionType = SelectionType.Single;
                _eventList.selectionChanged += OnEventSelectionChanged;
                _eventList.sortingEnabled = true;
                _eventList.columnSortingChanged += OnEventListSortingChanged;

                ApplyColumnsVisibility();
            }

            // 2. 监听器列表 MultiColumnListView
            _listenerList = rootVisualElement.Q<MultiColumnListView>("ListenerList");
            if (_listenerList != null)
            {
                _listenerList.columns.Clear();
                
                AddListenerListColumn("Status", "Status", 75, (lbl, info) => {
                    lbl.text = info.Status;
                    if (info.Status == "Active" || info.Status == "Registered") lbl.AddToClassList("color-register");
                    else if (info.Status == "Unregistered") lbl.AddToClassList("color-unregister");
                    else lbl.AddToClassList("color-warning");
                });
                AddListenerListColumn("GameObject", "GameObject", 180, (lbl, info) => lbl.text = string.IsNullOrEmpty(info.TargetGameObjectPath) ? "-" : info.TargetGameObjectPath);
                AddListenerListColumn("Component", "Class / Component", 120, (lbl, info) => lbl.text = info.TargetTypeName);
                AddListenerListColumn("Method", "Method", 140, (lbl, info) => lbl.text = info.MethodName);
                AddListenerListColumn("RegLog", "Reg Log", 75, (lbl, info) => lbl.text = FormatLogId(info.RegisteredLogId), true);
                AddListenerListColumn("Invokes", "Invokes", 75, (lbl, info) => lbl.text = info.InvokeCount.ToString(), true);
                AddListenerListColumn("LastCall", "Last Log", 75, (lbl, info) => lbl.text = FormatLogId(info.LastCallLogId), true);

                _listenerList.fixedItemHeight = 24;
                _listenerList.selectionType = SelectionType.Single;
            }

            // 3. 日志历史列表 MultiColumnListView
            _logList = rootVisualElement.Q<MultiColumnListView>("LogList");
            if (_logList != null)
            {
                _logList.columns.Clear();
                
                AddLogListColumn("LogId", "Id", 65, (lbl, info) => lbl.text = FormatLogId(info.LogId), true);
                AddLogListColumn("Time", "Time (s)", 70, (lbl, info) => lbl.text = info.Time.ToString("F2"), true);
                AddLogListColumn("Type", "Type", 80, (lbl, info) => {
                    lbl.text = info.Type.ToString();
                    if (info.Type == EventDebugLogType.Broadcast) lbl.AddToClassList("color-broadcast");
                    else if (info.Type == EventDebugLogType.Register) lbl.AddToClassList("color-register");
                    else if (info.Type == EventDebugLogType.Unregister) lbl.AddToClassList("color-unregister");
                });
                AddLogListColumn("EventName", "Event Name", 160, (lbl, info) => lbl.text = info.EventName);
                AddLogListColumn("Source", "Source", 160, (lbl, info) => lbl.text = info.Source?.DisplayName ?? "-");
                AddLogListColumn("Caller", "Caller", 160, (lbl, info) => lbl.text = info.Caller?.DisplayName ?? "-");
                AddLogListColumn("Payload", "Payload", 120, (lbl, info) => lbl.text = info.PayloadPreview);
                AddLogListColumn("Affected", "Affected", 75, (lbl, info) => lbl.text = info.AffectedCount.ToString(), true);

                _logList.fixedItemHeight = 24;
                _logList.selectionType = SelectionType.Single;
                _logList.selectionChanged += OnLogSelectionChanged;
            }

            // 4. 受影响的监听器列表 MultiColumnListView
            _affectedList = rootVisualElement.Q<MultiColumnListView>("AffectedList");
            if (_affectedList != null)
            {
                _affectedList.columns.Clear();
                
                AddAffectedListColumn("Order", "Order", 60, (lbl, info) => lbl.text = info.Order.ToString(), true);
                AddAffectedListColumn("GameObject", "GameObject", 200, (lbl, info) => lbl.text = string.IsNullOrEmpty(info.TargetGameObjectPath) ? "-" : info.TargetGameObjectPath);
                AddAffectedListColumn("Component", "Class / Component", 130, (lbl, info) => lbl.text = info.TargetTypeName);
                AddAffectedListColumn("Method", "Method", 160, (lbl, info) => lbl.text = info.MethodName);

                _affectedList.fixedItemHeight = 24;
                _affectedList.selectionType = SelectionType.Single;
            }
        }

        #region 列定义助手函数
        private void AddEventListColumn(string name, string title, float width, Action<Label, EventDebugInfo> bindAction, bool alignRight = false)
        {
            var col = new Column {
                name = name,
                title = title,
                width = width,
                resizable = true,
                makeCell = () => {
                    var label = new Label();
                    label.AddToClassList("cell-text");
                    return label;
                },
                bindCell = (element, index) => {
                    var lbl = (Label)element;
                    ApplyZebraRow(lbl, index);
                    
                    // 清除可能残留的颜色类
                    lbl.RemoveFromClassList("color-broadcast");
                    lbl.RemoveFromClassList("color-register");
                    lbl.RemoveFromClassList("color-unregister");
                    lbl.RemoveFromClassList("color-muted");

                    if (alignRight) lbl.AddToClassList("cell-align-right");
                    else lbl.RemoveFromClassList("cell-align-right");

                    if (index >= 0 && index < _eventRows.Count)
                    {
                        bindAction(lbl, _eventRows[index]);
                        lbl.tooltip = lbl.text;
                    }
                }
            };
            _eventList.columns.Add(col);
        }

        private void AddListenerListColumn(string name, string title, float width, Action<Label, EventListenerInfo> bindAction, bool alignRight = false)
        {
            var col = new Column {
                name = name,
                title = title,
                width = width,
                resizable = true,
                makeCell = () => {
                    var label = new Label();
                    label.AddToClassList("cell-text");
                    return label;
                },
                bindCell = (element, index) => {
                    var lbl = (Label)element;
                    ApplyZebraRow(lbl, index);
                    
                    lbl.RemoveFromClassList("color-register");
                    lbl.RemoveFromClassList("color-unregister");
                    lbl.RemoveFromClassList("color-warning");

                    if (alignRight) lbl.AddToClassList("cell-align-right");
                    else lbl.RemoveFromClassList("cell-align-right");

                    if (index >= 0 && index < _listenerRows.Count)
                    {
                        bindAction(lbl, _listenerRows[index]);
                        lbl.tooltip = lbl.text;
                    }
                }
            };
            _listenerList.columns.Add(col);
        }

        private void AddLogListColumn(string name, string title, float width, Action<Label, EventDebugLogRecord> bindAction, bool alignRight = false)
        {
            var col = new Column {
                name = name,
                title = title,
                width = width,
                resizable = true,
                makeCell = () => {
                    var label = new Label();
                    label.AddToClassList("cell-text");
                    return label;
                },
                bindCell = (element, index) => {
                    var lbl = (Label)element;
                    ApplyZebraRow(lbl, index);
                    
                    lbl.RemoveFromClassList("color-broadcast");
                    lbl.RemoveFromClassList("color-register");
                    lbl.RemoveFromClassList("color-unregister");

                    if (alignRight) lbl.AddToClassList("cell-align-right");
                    else lbl.RemoveFromClassList("cell-align-right");

                    if (index >= 0 && index < _logRows.Count)
                    {
                        bindAction(lbl, _logRows[index]);
                        lbl.tooltip = lbl.text;
                    }
                }
            };
            _logList.columns.Add(col);
        }

        private void AddAffectedListColumn(string name, string title, float width, Action<Label, EventListenerInvokeInfo> bindAction, bool alignRight = false)
        {
            var col = new Column {
                name = name,
                title = title,
                width = width,
                resizable = true,
                makeCell = () => {
                    var label = new Label();
                    label.AddToClassList("cell-text");
                    return label;
                },
                bindCell = (element, index) => {
                    var lbl = (Label)element;
                    ApplyZebraRow(lbl, index);

                    if (alignRight) lbl.AddToClassList("cell-align-right");
                    else lbl.RemoveFromClassList("cell-align-right");

                    if (index >= 0 && index < _affectedRows.Count)
                    {
                        bindAction(lbl, _affectedRows[index]);
                        lbl.tooltip = lbl.text;
                    }
                }
            };
            _affectedList.columns.Add(col);
        }

        private static void ApplyZebraRow(VisualElement el, int index)
        {
            // 通过获取 item 的父容器，为整行设置斑马条纹背景
            if (el.parent != null)
            {
                el.parent.RemoveFromClassList("row-even");
                el.parent.RemoveFromClassList("row-odd");
                if (index % 2 == 0)
                {
                    el.parent.AddToClassList("row-even");
                }
                else
                {
                    el.parent.AddToClassList("row-odd");
                }
            }
        }
        #endregion

        #region 动态列显示 (Columns Configuration)
        private void ShowColumnsMenu()
        {
            if (_columnsButton == null) return;

            GenericMenu menu = new GenericMenu();
            AddColumnMenuItem(menu, "Category", "Category");
            AddColumnMenuItem(menu, "Payload Type", "PayloadTypeName");
            AddColumnMenuItem(menu, "Listeners", "Listeners");
            AddColumnMenuItem(menu, "Activity", "Activity");
            AddColumnMenuItem(menu, "Last Log", "LastCall");
            menu.DropDown(_columnsButton.worldBound);
        }

        private void AddColumnMenuItem(GenericMenu menu, string displayName, string colName)
        {
            string prefsKey = CategoryVisiblePrefsPrefix + "Col." + colName;
            bool visible = EditorPrefs.GetBool(prefsKey, true);
            menu.AddItem(new GUIContent(displayName), visible, () => {
                EditorPrefs.SetBool(prefsKey, !visible);
                ApplyColumnsVisibility();
            });
        }

        private void ApplyColumnsVisibility()
        {
            if (_eventList == null) return;

            ToggleColumnVisibility("Category", EditorPrefs.GetBool(CategoryVisiblePrefsPrefix + "Col.Category", true));
            ToggleColumnVisibility("PayloadTypeName", EditorPrefs.GetBool(CategoryVisiblePrefsPrefix + "Col.PayloadTypeName", true));
            ToggleColumnVisibility("Listeners", EditorPrefs.GetBool(CategoryVisiblePrefsPrefix + "Col.Listeners", true));
            ToggleColumnVisibility("Activity", EditorPrefs.GetBool(CategoryVisiblePrefsPrefix + "Col.Activity", true));
            ToggleColumnVisibility("LastCall", EditorPrefs.GetBool(CategoryVisiblePrefsPrefix + "Col.LastCall", true));
        }

        private void ToggleColumnVisibility(string colName, bool visible)
        {
            foreach (var col in _eventList.columns)
            {
                if (col.name == colName)
                {
                    col.visible = visible;
                    break;
                }
            }
        }
        #endregion

        #region 分类菜单与列合并菜单多选
        private void SetupHeaderDropdowns()
        {
            if (_eventList != null)
            {
                _eventList.RegisterCallback<GeometryChangedEvent>(OnEventListGeometryChanged);
            }
        }

        private void OnEventListGeometryChanged(GeometryChangedEvent evt)
        {
            RefreshHeaderDropdowns();
        }

        private void RefreshHeaderDropdowns()
        {
            if (this == null || _eventList == null) return;

            var header = FindElementByClass(_eventList, "unity-multi-column-header");
            if (header == null) return;

            var columns = new List<VisualElement>();
            FindAllElementsByClass(header, "unity-multi-column-header__column", columns);
            if (columns.Count == 0) return;

            for (int i = 0; i < columns.Count; i++)
            {
                var colHeader = columns[i];
                string colName = colHeader.name;

                if (colName == "EventName" && FindChildByName(colHeader, "EventNameDropdown") == null)
                {
                    AddDropdownToHeader(colHeader, "EventNameDropdown", () => ShowCategoryMenuInternal(false));
                }
                else if (colName == "Listeners" && FindChildByName(colHeader, "ListenersDropdown") == null)
                {
                    AddDropdownToHeader(colHeader, "ListenersDropdown", ShowListenersMenu);
                }
                else if (colName == "Activity" && FindChildByName(colHeader, "ActivityDropdown") == null)
                {
                    AddDropdownToHeader(colHeader, "ActivityDropdown", ShowActivityMenu);
                }
            }
        }

        private void AddDropdownToHeader(VisualElement colHeader, string name, Action clickAction)
        {
            colHeader.style.flexDirection = FlexDirection.Row;
            colHeader.style.alignItems = Align.Center;

            var btn = new Label(" ▾") {
                name = name
            };
            btn.AddToClassList("header-dropdown-btn");
            btn.style.color = new Color(0.22f, 0.74f, 0.97f);
            btn.style.unityFontStyleAndWeight = FontStyle.Bold;
            btn.style.marginLeft = 4;

            btn.RegisterCallback<PointerDownEvent>(e => {
                clickAction();
                e.StopPropagation();
            });
            btn.RegisterCallback<MouseDownEvent>(e => {
                e.StopPropagation();
            });

            colHeader.Add(btn);
        }

        private void ShowCategoryMenuFromToolbar()
        {
            ShowCategoryMenuInternal(true);
        }

        private void ShowCategoryMenuInternal(bool fromToolbar)
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Show All"), false, () => {
                foreach (var row in _categoryRows)
                {
                    SetCategoryVisible(row.Category, true);
                }
                RefreshAll();
            });
            menu.AddItem(new GUIContent("Hide All"), false, () => {
                foreach (var row in _categoryRows)
                {
                    SetCategoryVisible(row.Category, false);
                }
                RefreshAll();
            });
            menu.AddSeparator("");

            foreach (var row in _categoryRows)
            {
                bool visible = IsCategoryVisible(row.Category);
                menu.AddItem(new GUIContent(row.Category), visible, () => {
                    SetCategoryVisible(row.Category, !visible);
                    RefreshAll();
                });
            }

            if (fromToolbar && _categoryFilterButton != null)
            {
                menu.DropDown(_categoryFilterButton.worldBound);
            }
            else
            {
                menu.ShowAsContext();
            }
        }

        private void ShowListenersMenu()
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("No-Payload Listeners"), _filterShowNoPayload, () => {
                _filterShowNoPayload = !_filterShowNoPayload;
                RefreshAll();
            });
            menu.AddItem(new GUIContent("Payload Listeners"), _filterShowPayload, () => {
                _filterShowPayload = !_filterShowPayload;
                RefreshAll();
            });
            menu.ShowAsContext();
        }

        private void ShowActivityMenu()
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Combined Text (R:X | U:Y | B:Z)"), _activityCombinedView, () => {
                _activityCombinedView = !_activityCombinedView;
                RefreshAll();
            });
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Registers"), _activityShowReg, () => {
                _activityShowReg = !_activityShowReg;
                RefreshAll();
            });
            menu.AddItem(new GUIContent("Unregisters"), _activityShowUnreg, () => {
                _activityShowUnreg = !_activityShowUnreg;
                RefreshAll();
            });
            menu.AddItem(new GUIContent("Broadcasts"), _activityShowBroadcast, () => {
                _activityShowBroadcast = !_activityShowBroadcast;
                RefreshAll();
            });
            menu.ShowAsContext();
        }

        private string GetListenersDisplay(EventDebugInfo info)
        {
            int count = 0;
            if (_filterShowNoPayload) count += info.NoPayloadListenerCount;
            if (_filterShowPayload) count += info.PayloadListenerCount;
            return count.ToString();
        }

        private string GetActivityDisplay(EventDebugInfo info)
        {
            if (_activityCombinedView)
            {
                return $"R:{info.RegisterCount} | U:{info.UnregisterCount} | B:{info.BroadcastCount}";
            }

            int count = 0;
            if (_activityShowReg) count += info.RegisterCount;
            if (_activityShowUnreg) count += info.UnregisterCount;
            if (_activityShowBroadcast) count += info.BroadcastCount;
            return count.ToString();
        }

        private int GetListenersCount(EventDebugInfo info)
        {
            int count = 0;
            if (_filterShowNoPayload) count += info.NoPayloadListenerCount;
            if (_filterShowPayload) count += info.PayloadListenerCount;
            return count;
        }

        private int GetActivityCount(EventDebugInfo info)
        {
            int count = 0;
            if (_activityShowReg) count += info.RegisterCount;
            if (_activityShowUnreg) count += info.UnregisterCount;
            if (_activityShowBroadcast) count += info.BroadcastCount;
            return count;
        }

        private void UpdateCategoryButtonText()
        {
            if (_categoryFilterButton == null) return;

            int visibleCount = 0;
            foreach (CategoryRowData row in _categoryRows)
            {
                if (row.Visible) visibleCount++;
            }

            if (_categoryRows.Count > 0 && visibleCount < _categoryRows.Count)
            {
                _categoryFilterButton.text = $"Category ({visibleCount}/{_categoryRows.Count}) ▾";
            }
            else
            {
                _categoryFilterButton.text = "Category: All ▾";
            }
        }
        #endregion

        #region 数据重建与处理
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
            UpdateBadges();
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
            UpdateCategoryButtonText();
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
            
            bool filterByEvent = _selectedEventOnlyToggle != null && _selectedEventOnlyToggle.value && !string.IsNullOrEmpty(_selectedEventName);

            for (int i = logs.Count - 1; i >= 0; i--)
            {
                EventDebugLogRecord log = logs[i];
                if (!IsCategoryVisible(log.Category) || !ShouldShowLogType(log.Type))
                    continue;

                if (filterByEvent && log.EventName != _selectedEventName)
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
            // 保留事件列表排序
            if (_eventList != null && _eventList.sortedColumns != null && _eventList.sortedColumns.Any())
            {
                OnEventListSortingChanged();
            }
            else
            {
                BindItemsSource(_eventList, _eventRows);
            }
            
            BindItemsSource(_listenerList, _listenerRows);
            BindItemsSource(_logList, _logRows);
            BindItemsSource(_affectedList, _affectedRows);

            EditorApplication.delayCall -= RefreshHeaderDropdowns;
            EditorApplication.delayCall += RefreshHeaderDropdowns;
        }

        private static void BindItemsSource<T>(MultiColumnListView listView, List<T> source)
        {
            if (listView == null) return;
            listView.itemsSource = source;
            listView.RefreshItems();
        }
        #endregion

        #region 点击事件排序
        private void OnEventListSortingChanged()
        {
            var sortedCols = _eventList.sortedColumns;
            if (sortedCols == null || !sortedCols.Any()) return;

            var desc = sortedCols.First();
            string colName = desc.columnName;
            bool asc = desc.direction == SortDirection.Ascending;

            _eventRows.Sort((a, b) => {
                int cmp = 0;
                switch (colName)
                {
                    case "EventName": cmp = string.Compare(a.EventName, b.EventName, StringComparison.Ordinal); break;
                    case "Category": cmp = string.Compare(a.Category, b.Category, StringComparison.Ordinal); break;
                    case "PayloadTypeName": cmp = string.Compare(a.PayloadTypeName, b.PayloadTypeName, StringComparison.Ordinal); break;
                    case "Listeners": cmp = GetListenersCount(a).CompareTo(GetListenersCount(b)); break;
                    case "Activity": cmp = GetActivityCount(a).CompareTo(GetActivityCount(b)); break;
                    case "LastCall": cmp = a.LastCallLogId.CompareTo(b.LastCallLogId); break;
                }
                return asc ? cmp : -cmp;
            });

            _eventList.itemsSource = _eventRows;
            _eventList.RefreshItems();
        }
        #endregion

        #region 选项更改与元素选中绑定
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
                    UpdateBadges();

                    // 如果开启了单事件历史过滤，选中新事件时应刷新日志
                    if (_selectedEventOnlyToggle != null && _selectedEventOnlyToggle.value)
                    {
                        RebuildLogRows();
                        BindItemsSource(_logList, _logRows);
                        UpdateBadges();
                    }
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
                    UpdateBadges();
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
            
            // 切换到 History Tab
            SwitchTab(false);

            RebuildLogRows();
            RefreshLists();

            int index = _logRows.FindIndex(log => log.LogId == _selectedLogId);
            if (_logList != null && index >= 0)
            {
                _logList.selectedIndex = index;
                _logList.ScrollToItem(index);
            }
        }
        #endregion

        #region 状态与详情卡片更新
        private void UpdateStatus()
        {
            bool isPlaying = EditorApplication.isPlaying;
            if (_noticeLabel != null)
            {
                _noticeLabel.style.display = isPlaying ? DisplayStyle.None : DisplayStyle.Flex;
                _noticeLabel.text = " 进入 Play Mode 后可查阅实时运行时事件流。";
            }

            if (_statusLabel == null) return;

            int eventCount = _eventRows.Count;
            int categoryCount = _categoryRows.Count;

            _statusLabel.text = isPlaying
                ? $"【运行中】 分类数: {categoryCount}  |  当前事件数: {eventCount}  |  历史日志条数: {_logRows.Count}"
                : "【未运行】 分类数: " + categoryCount;
        }

        private void UpdateEventDetail()
        {
            if (_eventDetailLabel == null) return;

            EventDebugInfo info = FindEvent(_selectedEventName);
            if (info == null)
            {
                _eventDetailLabel.text = "当前未选择事件。双击或单击左侧的事件项以显示详细信息。";
                return;
            }

            _eventDetailLabel.text =
                $"<color=#38bdf8><b>Event Name</b></color> : {info.EventName}\n" +
                $"<color=#a78bfa><b>Category</b></color>   : {info.Category}\n" +
                $"<color=#f472b6><b>Payload</b></color>    : <color=#94a3b8>{info.PayloadTypeName}</color>\n" +
                $"------------------------------------------------------------\n" +
                $"<b>First Registered</b> : {FormatLogId(info.FirstRegisteredLogId)}  |  " +
                $"<b>Last Registered</b>  : {FormatLogId(info.LastRegisteredLogId)}\n" +
                $"<b>Last Unregistered</b>: {FormatLogId(info.LastUnregisteredLogId)}  |  " +
                $"<color=#38bdf8><b>Last Call</b></color> : <color=#38bdf8><b>{FormatLogId(info.LastCallLogId)}</b></color>";
        }

        private void UpdateBadges()
        {
            if (_listenersCountBadge != null)
            {
                _listenersCountBadge.text = _listenerRows.Count.ToString();
            }
            if (_logsCountBadge != null)
            {
                _logsCountBadge.text = _logRows.Count.ToString();
            }
            if (_affectedCountBadge != null)
            {
                _affectedCountBadge.text = _affectedRows.Count.ToString();
            }
        }
        #endregion

        #region 配置缓存帮助函数
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

            bool visible = EditorPrefs.GetBool(CategoryVisiblePrefsPrefix + category, true);
            if (visible) _categoryVisibilityCache.Add(category);
            return visible;
        }

        private void SetCategoryVisible(string category, bool visible)
        {
            EditorPrefs.SetBool(CategoryVisiblePrefsPrefix + category, visible);
            if (visible)
                _categoryVisibilityCache.Add(category);
            else
                _categoryVisibilityCache.Remove(category);
        }

        private EventDebugInfo FindEvent(string eventName)
        {
            if (string.IsNullOrEmpty(eventName)) return null;

            foreach (EventDebugInfo info in GameManager.Event.GetDebugEvents())
            {
                if (info.EventName == eventName) return info;
            }
            return null;
        }

        private EventDebugLogRecord FindLog(long logId)
        {
            if (logId == 0) return null;

            foreach (EventDebugLogRecord log in GameManager.Event.GetDebugLogs())
            {
                if (log.LogId == logId) return log;
            }
            return null;
        }

        private static string FormatLogId(long logId)
        {
            return logId > 0 ? $"#{logId}" : "-";
        }
        #endregion

        #region UIElements 树遍历辅助函数
        private static VisualElement FindElementByClass(VisualElement element, string className)
        {
            if (element == null) return null;
            if (element.ClassListContains(className)) return element;
            int count = element.hierarchy.childCount;
            for (int i = 0; i < count; i++)
            {
                var found = FindElementByClass(element.hierarchy.ElementAt(i), className);
                if (found != null) return found;
            }
            return null;
        }

        private static void FindAllElementsByClass(VisualElement element, string className, List<VisualElement> result)
        {
            if (element == null) return;
            if (element.ClassListContains(className)) result.Add(element);
            int count = element.hierarchy.childCount;
            for (int i = 0; i < count; i++)
            {
                FindAllElementsByClass(element.hierarchy.ElementAt(i), className, result);
            }
        }

        private static VisualElement FindChildByName(VisualElement element, string name)
        {
            if (element == null) return null;
            int count = element.hierarchy.childCount;
            for (int i = 0; i < count; i++)
            {
                var child = element.hierarchy.ElementAt(i);
                if (child.name == name) return child;
                var found = FindChildByName(child, name);
                if (found != null) return found;
            }
            return null;
        }
        #endregion

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
    }
}
