using System;
using System.Collections.Generic;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Core.Event
{
    public partial class EventManager
    {
        private readonly Dictionary<string, IEventSlot> _eventSlots = new();
        private readonly Dictionary<string, IEventSlot> _eventSlotsPayload = new();
        private readonly Dictionary<string, Type> _payloadTypes = new();

        private bool CheckPayloadType<TPayload>(string eventName)
        {
            Type payloadType = typeof(TPayload);

            if (_payloadTypes.TryGetValue(eventName, out Type registeredType))
            {
                if (registeredType != payloadType)
                {
                    Debug.LogError(
                        $"事件 {eventName} 已注册 Payload 类型 {registeredType.Name}，不能再使用 {payloadType.Name}。");
                    return false;
                }

                return true;
            }

            _payloadTypes.Add(eventName, payloadType);

#if UNITY_EDITOR
            SetDebugPayloadType(eventName, payloadType);
#endif

            return true;
        }

        public void Register(string eventName, Action<EventContext> listener)
        {
            if (listener == null)
                return;

            if (!_eventSlots.TryGetValue(eventName, out IEventSlot slot))
            {
                slot = new EventSlot(eventName);
                _eventSlots.Add(eventName, slot);
            }

            (slot as EventSlot)?.Add(listener);

#if UNITY_EDITOR
            RecordDebugRegister(eventName, listener);
#endif
        }

        public void Register<TPayload>(string eventName, Action<EventContext<TPayload>> listener)
        {
            if (listener == null || !CheckPayloadType<TPayload>(eventName))
                return;

            if (!_eventSlotsPayload.TryGetValue(eventName, out IEventSlot slot))
            {
                slot = new EventSlot<TPayload>(eventName);
                _eventSlotsPayload.Add(eventName, slot);
            }

            (slot as EventSlot<TPayload>)?.Add(listener);

#if UNITY_EDITOR
            RecordDebugRegister(eventName, listener);
#endif
        }

        public void Unregister(string eventName, Action<EventContext> listener)
        {
            if (listener == null)
                return;

            if (_eventSlots.TryGetValue(eventName, out IEventSlot slot))
            {
                (slot as EventSlot)?.Remove(listener);
            }

#if UNITY_EDITOR
            RecordDebugUnregister(eventName, listener);
#endif
        }

        public void Unregister<TPayload>(string eventName, Action<EventContext<TPayload>> listener)
        {
            if (listener == null)
                return;

            if (_eventSlotsPayload.TryGetValue(eventName, out IEventSlot slot))
            {
                (slot as EventSlot<TPayload>)?.Remove(listener);
            }

#if UNITY_EDITOR
            RecordDebugUnregister(eventName, listener);
#endif
        }

        public void Unregister(string eventName)
        {
            _eventSlots.Remove(eventName);
            _eventSlotsPayload.Remove(eventName);
            _payloadTypes.Remove(eventName);

#if UNITY_EDITOR
            RecordDebugUnregisterAll(eventName);
#endif
        }

        public void Unregister()
        {
            _eventSlots.Clear();
            _eventSlotsPayload.Clear();
            _payloadTypes.Clear();

#if UNITY_EDITOR
            ClearDebug();
#endif
        }

        [Obsolete("请使用 Broadcast(object source, string eventName)，或 Component/GameObject 的 Broadcast 扩展方法。", true)]
        public void Broadcast(string eventName)
        {
            Broadcast(null, eventName);
        }

        [Obsolete("请使用 Broadcast<TPayload>(object source, string eventName, TPayload payload)，或 Component/GameObject 的 Broadcast 扩展方法。", true)]
        public void Broadcast<TPayload>(string eventName, TPayload payload)
        {
            Broadcast(null, eventName, payload);
        }

        public void Broadcast(object source, string eventName)
        {
#if UNITY_EDITOR
            RecordDebugBroadcast(source, eventName, "-", false);
#endif

            if (_eventSlots.TryGetValue(eventName, out IEventSlot slot))
            {
                (slot as EventSlot)?.Invoke();
            }
        }

        public void Broadcast<TPayload>(object source, string eventName, TPayload payload)
        {
            if (!CheckPayloadType<TPayload>(eventName))
                return;

#if UNITY_EDITOR
            RecordDebugBroadcast(source, eventName, payload, true);
#endif

            if (_eventSlotsPayload.TryGetValue(eventName, out IEventSlot payloadSlot))
            {
                (payloadSlot as EventSlot<TPayload>)?.Invoke(payload);
            }

            if (_eventSlots.TryGetValue(eventName, out IEventSlot slot))
            {
                (slot as EventSlot)?.Invoke();
            }
        }

        private int GetNoPayloadListenerCount(string eventName)
        {
            return _eventSlots.TryGetValue(eventName, out IEventSlot slot) ? slot.ListenerCount : 0;
        }

        private int GetPayloadListenerCount(string eventName)
        {
            return _eventSlotsPayload.TryGetValue(eventName, out IEventSlot slot) ? slot.ListenerCount : 0;
        }

    }
}
