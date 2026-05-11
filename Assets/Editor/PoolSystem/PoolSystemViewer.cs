using System.Collections.Generic;
using Core.Event;
using Core.Pool;
using Manager;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Core.Pool.EditorTools
{
    public sealed class PoolSystemViewer : EditorWindow
    {
        private const string WindowUxmlPath = "Assets/Editor/PoolSystem/PoolSystemViewer.uxml";
        private const string ItemUxmlPath = "Assets/Editor/PoolSystem/PoolSystemViewerItem.uxml";
        private const string UssPath = "Assets/Editor/PoolSystem/PoolSystemViewer.uss";

        private readonly List<PoolRowData> _rows = new();
        private readonly Dictionary<string, int> _rowIndexByPoolName = new();

        private VisualTreeAsset _itemTemplate;
        private ListView _poolList;
        private Label _noticeLabel;
        private Label _statusLabel;
        private bool _eventsRegistered;

        [MenuItem("Tools/Pool System Viewer")]
        public static void ShowWindow()
        {
            PoolSystemViewer window = GetWindow<PoolSystemViewer>();
            window.titleContent = new GUIContent("Pool System Viewer");
            window.minSize = new Vector2(760, 320);
        }

        private void OnEnable()
        {
            RegisterPoolEvents();
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private void OnDisable()
        {
            UnregisterPoolEvents();
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
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

            ToolbarButton refreshButton = rootVisualElement.Q<ToolbarButton>("RefreshButton");
            if (refreshButton != null)
            {
                refreshButton.clicked += RebuildFromCurrentPools;
            }

            _poolList = rootVisualElement.Q<ListView>("PoolList");
            if (_poolList != null)
            {
                _poolList.itemsSource = _rows;
                _poolList.makeItem = MakePoolItem;
                _poolList.bindItem = BindPoolItem;
                _poolList.fixedItemHeight = 28;
                _poolList.selectionType = SelectionType.Single;
            }

            RebuildFromCurrentPools();
        }

        private VisualElement MakePoolItem()
        {
            if (_itemTemplate != null)
            {
                return _itemTemplate.Instantiate();
            }

            VisualElement row = new();
            row.AddToClassList("pool-row");
            row.AddToClassList("pool-item");
            AddFallbackCell(row, "PoolName", "pool-name");
            AddFallbackCell(row, "Prefab", "pool-prefab");
            AddFallbackCell(row, "Idle", "pool-number");
            AddFallbackCell(row, "Rented", "pool-number");
            AddFallbackCell(row, "Total", "pool-number");
            AddFallbackCell(row, "Capacity", "pool-capacity");
            AddFallbackCell(row, "Step", "pool-number");
            AddFallbackCell(row, "Root", "pool-root");
            return row;
        }

        private static void AddFallbackCell(VisualElement row, string name, string className)
        {
            Label label = new() { name = name };
            label.AddToClassList("pool-cell");
            label.AddToClassList(className);
            row.Add(label);
        }

        private void BindPoolItem(VisualElement element, int index)
        {
            if (index < 0 || index >= _rows.Count)
            {
                return;
            }

            PoolRowData data = _rows[index];
            SetLabelText(element, "PoolName", data.PoolName);
            SetLabelText(element, "Prefab", data.PrefabName);
            SetLabelText(element, "Idle", data.IdleCount.ToString());
            SetLabelText(element, "Rented", data.RentedCount.ToString());
            SetLabelText(element, "Total", data.TotalCount.ToString());
            SetLabelText(element, "Capacity", data.CapacityText);
            SetLabelText(element, "Step", data.Step.ToString());
            SetLabelText(element, "Root", data.RootName);
        }

        private static void SetLabelText(VisualElement element, string labelName, string text)
        {
            Label label = element.Q<Label>(labelName);
            if (label != null)
            {
                label.text = text;
                label.tooltip = text;
            }
        }

        private void RegisterPoolEvents()
        {
            if (_eventsRegistered)
            {
                return;
            }

            GameManager.Event.Register<string>(PoolEvents.Registered, OnPoolRegistered);
            GameManager.Event.Register<string>(PoolEvents.Rented, OnPoolChanged);
            GameManager.Event.Register<string>(PoolEvents.Returned, OnPoolChanged);
            GameManager.Event.Register<string>(PoolEvents.Unregistered, OnPoolUnregistered);
            GameManager.Event.Register(PoolEvents.Cleared, OnPoolsCleared);
            _eventsRegistered = true;
        }

        private void UnregisterPoolEvents()
        {
            if (!_eventsRegistered)
            {
                return;
            }

            GameManager.Event.Unregister<string>(PoolEvents.Registered, OnPoolRegistered);
            GameManager.Event.Unregister<string>(PoolEvents.Rented, OnPoolChanged);
            GameManager.Event.Unregister<string>(PoolEvents.Returned, OnPoolChanged);
            GameManager.Event.Unregister<string>(PoolEvents.Unregistered, OnPoolUnregistered);
            GameManager.Event.Unregister(PoolEvents.Cleared, OnPoolsCleared);
            _eventsRegistered = false;
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state is PlayModeStateChange.EnteredPlayMode or PlayModeStateChange.ExitingPlayMode)
            {
                RebuildFromCurrentPools();
            }
        }

        private void OnPoolRegistered(EventContext<string> context)
        {
            UpsertPool(context.Payload);
        }

        private void OnPoolChanged(EventContext<string> context)
        {
            UpsertPool(context.Payload);
        }

        private void OnPoolUnregistered(EventContext<string> context)
        {
            RemovePool(context.Payload);
        }

        private void OnPoolsCleared(EventContext context)
        {
            ClearRows();
        }

        private void RebuildFromCurrentPools()
        {
            _rows.Clear();
            _rowIndexByPoolName.Clear();

            if (EditorApplication.isPlaying)
            {
                foreach (KeyValuePair<string, GameObjectPool> pair in GameManager.Pool.PoolDict)
                {
                    AddPoolRow(pair.Key, pair.Value);
                }
            }

            RefreshList();
            UpdateStatus();
        }

        private void UpsertPool(string poolName)
        {
            if (!TryGetPool(poolName, out GameObjectPool pool))
            {
                RemovePool(poolName);
                return;
            }

            PoolRowData data = BuildRowData(poolName, pool);
            if (_rowIndexByPoolName.TryGetValue(poolName, out int index))
            {
                _rows[index] = data;
            }
            else
            {
                _rowIndexByPoolName.Add(poolName, _rows.Count);
                _rows.Add(data);
            }

            RefreshList();
            UpdateStatus();
        }

        private void AddPoolRow(string poolName, GameObjectPool pool)
        {
            _rowIndexByPoolName[poolName] = _rows.Count;
            _rows.Add(BuildRowData(poolName, pool));
        }

        private void RemovePool(string poolName)
        {
            if (string.IsNullOrEmpty(poolName) || !_rowIndexByPoolName.TryGetValue(poolName, out int index))
            {
                return;
            }

            _rows.RemoveAt(index);
            RebuildRowIndex();
            RefreshList();
            UpdateStatus();
        }

        private void ClearRows()
        {
            _rows.Clear();
            _rowIndexByPoolName.Clear();
            RefreshList();
            UpdateStatus();
        }

        private void RebuildRowIndex()
        {
            _rowIndexByPoolName.Clear();
            for (int i = 0; i < _rows.Count; i++)
            {
                _rowIndexByPoolName[_rows[i].PoolName] = i;
            }
        }

        private static bool TryGetPool(string poolName, out GameObjectPool pool)
        {
            pool = null;
            return EditorApplication.isPlaying
                   && !string.IsNullOrEmpty(poolName)
                   && GameManager.Pool.PoolDict.TryGetValue(poolName, out pool);
        }

        private static PoolRowData BuildRowData(string poolName, GameObjectPool pool)
        {
            int idleCount = pool.PoolQueue?.Count ?? 0;
            int rentedCount = pool.RentSet?.Count ?? 0;

            return new PoolRowData
            {
                PoolName = poolName,
                PrefabName = pool.Prefab != null ? pool.Prefab.name : "-",
                IdleCount = idleCount,
                RentedCount = rentedCount,
                TotalCount = pool.Count,
                CapacityText = pool.Capacity < 0 ? "无限" : pool.Capacity.ToString(),
                Step = pool.Step,
                RootName = pool.RootTransform != null ? pool.RootTransform.name : "-"
            };
        }

        private void RefreshList()
        {
            _poolList?.RefreshItems();
        }

        private void UpdateStatus()
        {
            bool isPlaying = EditorApplication.isPlaying;

            if (_noticeLabel != null)
            {
                _noticeLabel.style.display = isPlaying ? DisplayStyle.None : DisplayStyle.Flex;
                _noticeLabel.text = "进入 Play Mode 后显示运行时对象池。";
            }

            if (_poolList != null)
            {
                _poolList.SetEnabled(isPlaying);
            }

            if (_statusLabel == null)
            {
                return;
            }

            if (!isPlaying)
            {
                _statusLabel.text = "当前未运行。";
                return;
            }

            int idleTotal = 0;
            int rentedTotal = 0;
            int objectTotal = 0;
            foreach (PoolRowData row in _rows)
            {
                idleTotal += row.IdleCount;
                rentedTotal += row.RentedCount;
                objectTotal += row.TotalCount;
            }

            _statusLabel.text = $"池数量: {_rows.Count}    总对象: {objectTotal}    空闲: {idleTotal}    借出: {rentedTotal}";
        }

        private sealed class PoolRowData
        {
            public string PoolName;
            public string PrefabName;
            public int IdleCount;
            public int RentedCount;
            public int TotalCount;
            public string CapacityText;
            public int Step;
            public string RootName;
        }
    }
}
