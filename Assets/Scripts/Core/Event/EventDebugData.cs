#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Core.Event
{
    public enum EventDebugLogType
    {
        Register,
        Unregister,
        Broadcast
    }

    public sealed class EventSourceInfo
    {
        public static readonly EventSourceInfo Null = new("<null source>", string.Empty, string.Empty, null);

        public string DisplayName { get; }
        public string GameObjectPath { get; }
        public string TypeName { get; }
        public UnityEngine.Object UnityObject { get; }

        public EventSourceInfo(string displayName, string gameObjectPath, string typeName, UnityEngine.Object unityObject)
        {
            DisplayName = string.IsNullOrEmpty(displayName) ? "-" : displayName;
            GameObjectPath = gameObjectPath ?? string.Empty;
            TypeName = typeName ?? string.Empty;
            UnityObject = unityObject;
        }

        public static EventSourceInfo Create(object source)
        {
            if (source == null)
                return Null;

            if (source is Component component)
            {
                string path = EventDebugUtility.GetGameObjectPath(component.gameObject);
                return new EventSourceInfo($"{path} / {component.GetType().Name}", path, component.GetType().Name, component);
            }

            if (source is GameObject gameObject)
            {
                string path = EventDebugUtility.GetGameObjectPath(gameObject);
                return new EventSourceInfo(path, path, nameof(GameObject), gameObject);
            }

            Type type = source.GetType();
            return new EventSourceInfo(type.Name, string.Empty, type.FullName, source as UnityEngine.Object);
        }
    }

    public sealed class EventCallSiteInfo
    {
        public static readonly EventCallSiteInfo Unknown = new("-", "-", string.Empty, 0);

        public string TypeName { get; }
        public string MethodName { get; }
        public string FilePath { get; }
        public int LineNumber { get; }
        public string DisplayName => LineNumber > 0
            ? $"{TypeName}.{MethodName}:{LineNumber}"
            : $"{TypeName}.{MethodName}";

        public EventCallSiteInfo(string typeName, string methodName, string filePath, int lineNumber)
        {
            TypeName = string.IsNullOrEmpty(typeName) ? "-" : typeName;
            MethodName = string.IsNullOrEmpty(methodName) ? "-" : methodName;
            FilePath = filePath ?? string.Empty;
            LineNumber = lineNumber;
        }
    }

    public sealed class EventListenerInfo
    {
        public long ListenerId { get; }
        public string EventName { get; }
        public string TargetGameObjectPath { get; }
        public string TargetTypeName { get; }
        public string MethodName { get; }
        public string DisplayTarget { get; }
        public UnityEngine.Object UnityObject { get; }
        public long RegisteredLogId { get; }
        public int InvokeCount { get; internal set; }
        public long LastCallLogId { get; internal set; }
        public string Status => "Active";

        public EventListenerInfo(
            long listenerId,
            string eventName,
            string targetGameObjectPath,
            string targetTypeName,
            string methodName,
            string displayTarget,
            UnityEngine.Object unityObject,
            long registeredLogId)
        {
            ListenerId = listenerId;
            EventName = eventName;
            TargetGameObjectPath = targetGameObjectPath ?? string.Empty;
            TargetTypeName = string.IsNullOrEmpty(targetTypeName) ? "-" : targetTypeName;
            MethodName = string.IsNullOrEmpty(methodName) ? "-" : methodName;
            DisplayTarget = string.IsNullOrEmpty(displayTarget) ? "-" : displayTarget;
            UnityObject = unityObject;
            RegisteredLogId = registeredLogId;
        }
    }

    public sealed class EventListenerInvokeInfo
    {
        public int Order { get; }
        public long ListenerId { get; }
        public string TargetGameObjectPath { get; }
        public string TargetTypeName { get; }
        public string MethodName { get; }
        public string DisplayTarget { get; }
        public UnityEngine.Object UnityObject { get; }

        public EventListenerInvokeInfo(int order, EventListenerInfo listener)
        {
            Order = order;
            ListenerId = listener.ListenerId;
            TargetGameObjectPath = listener.TargetGameObjectPath;
            TargetTypeName = listener.TargetTypeName;
            MethodName = listener.MethodName;
            DisplayTarget = listener.DisplayTarget;
            UnityObject = listener.UnityObject;
        }
    }

    public sealed class EventDebugLogRecord
    {
        public long LogId { get; }
        public float Time { get; }
        public EventDebugLogType Type { get; }
        public string EventName { get; }
        public string Category { get; }
        public EventSourceInfo Source { get; }
        public EventCallSiteInfo Caller { get; }
        public string PayloadPreview { get; }
        public IReadOnlyList<EventListenerInvokeInfo> AffectedListeners { get; }
        public int AffectedCount => AffectedListeners.Count;

        public EventDebugLogRecord(
            long logId,
            float time,
            EventDebugLogType type,
            string eventName,
            string category,
            EventSourceInfo source,
            EventCallSiteInfo caller,
            string payloadPreview,
            IReadOnlyList<EventListenerInvokeInfo> affectedListeners)
        {
            LogId = logId;
            Time = time;
            Type = type;
            EventName = eventName;
            Category = category;
            Source = source ?? EventSourceInfo.Null;
            Caller = caller ?? EventCallSiteInfo.Unknown;
            PayloadPreview = string.IsNullOrEmpty(payloadPreview) ? "-" : payloadPreview;
            AffectedListeners = affectedListeners ?? Array.Empty<EventListenerInvokeInfo>();
        }
    }

    public sealed class EventDebugInfo
    {
        public string EventName { get; }
        public string Category { get; }
        public Type PayloadType { get; internal set; }
        public string PayloadTypeName => PayloadType != null ? PayloadType.Name : "None";
        public int NoPayloadListenerCount { get; internal set; }
        public int PayloadListenerCount { get; internal set; }
        public int TotalListenerCount => NoPayloadListenerCount + PayloadListenerCount;
        public int RegisterCount { get; internal set; }
        public int UnregisterCount { get; internal set; }
        public int BroadcastCount { get; internal set; }
        public long FirstRegisteredLogId { get; internal set; }
        public long LastRegisteredLogId { get; internal set; }
        public long LastUnregisteredLogId { get; internal set; }
        public long LastCallLogId { get; internal set; }

        public EventDebugInfo(string eventName)
        {
            EventName = eventName;
            Category = EventDebugUtility.GetCategory(eventName);
        }
    }

    public static class EventDebugUtility
    {
        public static string GetCategory(string eventName)
        {
            if (string.IsNullOrEmpty(eventName))
                return "Uncategorized";

            int dotIndex = eventName.IndexOf('.');
            return dotIndex > 0 ? eventName[..dotIndex] : "Uncategorized";
        }

        public static string GetGameObjectPath(GameObject gameObject)
        {
            if (gameObject == null)
                return string.Empty;

            Transform current = gameObject.transform;
            Stack<string> names = new();
            while (current != null)
            {
                names.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", names);
        }

        public static string GetPayloadPreview(object payload)
        {
            if (payload == null)
                return "<null>";

            if (payload is UnityEngine.Object unityObject)
                return $"{unityObject.GetType().Name}: {unityObject.name}";

            string text = payload.ToString();
            if (string.IsNullOrEmpty(text))
                return payload.GetType().Name;

            return text.Length > 96 ? $"{text[..96]}..." : text;
        }
    }
}
#endif
