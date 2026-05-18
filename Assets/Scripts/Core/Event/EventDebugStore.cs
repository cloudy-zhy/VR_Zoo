#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using UnityEngine;

namespace Core.Event
{
    internal sealed class EventDebugStore
    {
        private const int MaxDebugLogCount = 2000;

        private readonly Dictionary<string, EventDebugInfo> _debugInfos = new();
        private readonly Dictionary<string, Dictionary<Delegate, EventListenerInfo>> _listenersByEvent = new();
        private readonly List<EventDebugLogRecord> _debugLogs = new();

        private long _nextLogId = 1;
        private long _nextListenerId = 1;

        public IReadOnlyList<EventDebugInfo> GetDebugEvents()
        {
            List<EventDebugInfo> result = new(_debugInfos.Values);
            result.Sort((a, b) => string.Compare(a.EventName, b.EventName, StringComparison.Ordinal));
            return result;
        }

        public IReadOnlyList<EventDebugLogRecord> GetDebugLogs()
        {
            return _debugLogs;
        }

        public IReadOnlyList<EventListenerInfo> GetDebugListeners(string eventName, IReadOnlyList<Delegate> activeListeners)
        {
            List<EventListenerInfo> result = new();
            if (activeListeners == null)
                return result;

            foreach (Delegate listener in activeListeners)
            {
                result.Add(GetOrCreateTransientListenerInfo(eventName, listener));
            }

            return result;
        }

        public void SetPayloadType(string eventName, Type payloadType, int noPayloadListenerCount, int payloadListenerCount)
        {
            EventDebugInfo info = EnsureDebugInfo(eventName, noPayloadListenerCount, payloadListenerCount);
            info.PayloadType = payloadType;
        }

        public void RecordRegister(
            string eventName,
            Delegate listener,
            int noPayloadListenerCount,
            int payloadListenerCount)
        {
            if (listener == null)
                return;

            long logId = CreateLogId();
            EventDebugInfo info = EnsureDebugInfo(eventName, noPayloadListenerCount, payloadListenerCount);
            info.RegisterCount++;
            if (info.FirstRegisteredLogId == 0)
                info.FirstRegisteredLogId = logId;
            info.LastRegisteredLogId = logId;

            Dictionary<Delegate, EventListenerInfo> listenerMap = GetListenerMap(eventName);
            listenerMap.Remove(listener);
            listenerMap.Add(listener, CreateListenerInfo(eventName, listener, logId));

            AddLog(CreateLogRecord(
                logId,
                EventDebugLogType.Register,
                eventName,
                EventSourceInfo.Create(listener.Target),
                CaptureCallSite(),
                $"{listener.Method.DeclaringType?.Name}.{listener.Method.Name}",
                Array.Empty<EventListenerInvokeInfo>()));
        }

        public void RecordUnregister(
            string eventName,
            Delegate listener,
            int noPayloadListenerCount,
            int payloadListenerCount)
        {
            EventListenerInfo removedInfo = RemoveListenerInfo(eventName, listener);
            long logId = CreateLogId();

            EventDebugInfo info = EnsureDebugInfo(eventName, noPayloadListenerCount, payloadListenerCount);
            info.UnregisterCount++;
            info.LastUnregisteredLogId = logId;

            AddLog(CreateLogRecord(
                logId,
                EventDebugLogType.Unregister,
                eventName,
                listener != null ? EventSourceInfo.Create(listener.Target) : EventSourceInfo.Null,
                CaptureCallSite(),
                removedInfo != null ? $"{removedInfo.TargetTypeName}.{removedInfo.MethodName}" : "Listener not found",
                Array.Empty<EventListenerInvokeInfo>()));
        }

        public void RecordUnregisterAll(string eventName)
        {
            _listenersByEvent.Remove(eventName);

            long logId = CreateLogId();
            EventDebugInfo info = EnsureDebugInfo(eventName, 0, 0);
            info.NoPayloadListenerCount = 0;
            info.PayloadListenerCount = 0;
            info.UnregisterCount++;
            info.LastUnregisteredLogId = logId;

            AddLog(CreateLogRecord(
                logId,
                EventDebugLogType.Unregister,
                eventName,
                EventSourceInfo.Null,
                CaptureCallSite(),
                "Unregister all listeners",
                Array.Empty<EventListenerInvokeInfo>()));
        }

        public void RecordBroadcast(
            object source,
            string eventName,
            object payload,
            IReadOnlyList<Delegate> affectedListeners,
            int noPayloadListenerCount,
            int payloadListenerCount)
        {
            long logId = CreateLogId();
            EventDebugInfo info = EnsureDebugInfo(eventName, noPayloadListenerCount, payloadListenerCount);
            info.BroadcastCount++;
            info.LastCallLogId = logId;

            List<EventListenerInvokeInfo> affectedInfos = BuildAffectedInfos(eventName, logId, affectedListeners);
            AddLog(CreateLogRecord(
                logId,
                EventDebugLogType.Broadcast,
                eventName,
                EventSourceInfo.Create(source),
                CaptureCallSite(),
                EventDebugUtility.GetPayloadPreview(payload),
                affectedInfos));
        }

        public void Clear()
        {
            _debugInfos.Clear();
            _listenersByEvent.Clear();
            _debugLogs.Clear();
            _nextLogId = 1;
            _nextListenerId = 1;
        }

        private EventDebugInfo EnsureDebugInfo(string eventName, int noPayloadListenerCount, int payloadListenerCount)
        {
            if (!_debugInfos.TryGetValue(eventName, out EventDebugInfo info))
            {
                info = new EventDebugInfo(eventName);
                _debugInfos.Add(eventName, info);
            }

            info.NoPayloadListenerCount = noPayloadListenerCount;
            info.PayloadListenerCount = payloadListenerCount;
            return info;
        }

        private Dictionary<Delegate, EventListenerInfo> GetListenerMap(string eventName)
        {
            if (!_listenersByEvent.TryGetValue(eventName, out Dictionary<Delegate, EventListenerInfo> listenerMap))
            {
                listenerMap = new Dictionary<Delegate, EventListenerInfo>();
                _listenersByEvent.Add(eventName, listenerMap);
            }

            return listenerMap;
        }

        private EventListenerInfo RemoveListenerInfo(string eventName, Delegate listener)
        {
            if (listener == null)
                return null;

            if (!_listenersByEvent.TryGetValue(eventName, out Dictionary<Delegate, EventListenerInfo> listenerMap))
                return null;

            if (!listenerMap.TryGetValue(listener, out EventListenerInfo listenerInfo))
                return null;

            listenerMap.Remove(listener);
            return listenerInfo;
        }

        private EventListenerInfo GetOrCreateTransientListenerInfo(string eventName, Delegate listener)
        {
            if (listener == null)
                return CreateUnknownListenerInfo(eventName);

            if (_listenersByEvent.TryGetValue(eventName, out Dictionary<Delegate, EventListenerInfo> listenerMap)
                && listenerMap.TryGetValue(listener, out EventListenerInfo listenerInfo))
            {
                return listenerInfo;
            }

            return CreateListenerInfo(eventName, listener, 0);
        }

        private List<EventListenerInvokeInfo> BuildAffectedInfos(
            string eventName,
            long logId,
            IReadOnlyList<Delegate> affectedListeners)
        {
            List<EventListenerInvokeInfo> affectedInfos = new();
            if (affectedListeners == null)
                return affectedInfos;

            foreach (Delegate listener in affectedListeners)
            {
                EventListenerInfo listenerInfo = GetOrCreateTransientListenerInfo(eventName, listener);
                listenerInfo.InvokeCount++;
                listenerInfo.LastCallLogId = logId;
                affectedInfos.Add(new EventListenerInvokeInfo(affectedInfos.Count + 1, listenerInfo));
            }

            return affectedInfos;
        }

        private EventListenerInfo CreateUnknownListenerInfo(string eventName)
        {
            return new EventListenerInfo(
                0,
                eventName,
                string.Empty,
                "Unknown",
                "Unknown",
                "Unknown",
                null,
                0);
        }

        private EventListenerInfo CreateListenerInfo(string eventName, Delegate listener, long registeredLogId)
        {
            object target = listener.Target;
            MethodInfo method = listener.Method;
            string methodName = method.Name;
            string targetTypeName;
            string gameObjectPath = string.Empty;
            string displayTarget;
            UnityEngine.Object unityObject = null;

            if (target is Component component)
            {
                gameObjectPath = EventDebugUtility.GetGameObjectPath(component.gameObject);
                targetTypeName = component.GetType().Name;
                displayTarget = $"{gameObjectPath} / {targetTypeName}";
                unityObject = component;
            }
            else if (target is GameObject gameObject)
            {
                gameObjectPath = EventDebugUtility.GetGameObjectPath(gameObject);
                targetTypeName = nameof(GameObject);
                displayTarget = gameObjectPath;
                unityObject = gameObject;
            }
            else if (target != null)
            {
                targetTypeName = target.GetType().Name;
                displayTarget = targetTypeName;
                unityObject = target as UnityEngine.Object;
            }
            else
            {
                targetTypeName = method.DeclaringType != null ? method.DeclaringType.Name : "Static";
                displayTarget = $"{targetTypeName}.static";
            }

            return new EventListenerInfo(
                _nextListenerId++,
                eventName,
                gameObjectPath,
                targetTypeName,
                methodName,
                displayTarget,
                unityObject,
                registeredLogId);
        }

        private EventDebugLogRecord CreateLogRecord(
            long logId,
            EventDebugLogType type,
            string eventName,
            EventSourceInfo source,
            EventCallSiteInfo caller,
            string payloadPreview,
            IReadOnlyList<EventListenerInvokeInfo> affectedListeners)
        {
            return new EventDebugLogRecord(
                logId,
                Time.realtimeSinceStartup,
                type,
                eventName,
                EventDebugUtility.GetCategory(eventName),
                source,
                caller,
                payloadPreview,
                affectedListeners);
        }

        private void AddLog(EventDebugLogRecord record)
        {
            _debugLogs.Add(record);
            while (_debugLogs.Count > MaxDebugLogCount)
            {
                _debugLogs.RemoveAt(0);
            }
        }

        private long CreateLogId()
        {
            return _nextLogId++;
        }

        private static EventCallSiteInfo CaptureCallSite()
        {
            StackTrace stackTrace = new(true);
            for (int i = 0; i < stackTrace.FrameCount; i++)
            {
                StackFrame frame = stackTrace.GetFrame(i);
                MethodBase method = frame?.GetMethod();
                Type declaringType = method?.DeclaringType;
                if (declaringType == null)
                    continue;

                string namespaceName = declaringType.Namespace;
                if (!string.IsNullOrEmpty(namespaceName)
                    && namespaceName.StartsWith("Core.Event", StringComparison.Ordinal))
                {
                    continue;
                }

                return new EventCallSiteInfo(
                    declaringType.Name,
                    method.Name,
                    frame.GetFileName(),
                    frame.GetFileLineNumber());
            }

            return EventCallSiteInfo.Unknown;
        }
    }
}
#endif
