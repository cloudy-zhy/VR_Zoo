using System;
using System.Collections.Generic;

namespace Core.Event
{
    public class EventManager
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
                    UnityEngine.Debug.LogError(
                        $"事件 {eventName} 已注册 Payload 类型 {registeredType.Name}，不能再使用 {payloadType.Name}。");
                    return false;
                }

                return true;
            }

            _payloadTypes.Add(eventName, payloadType);
            return true;
        }
        
        public void Register(string eventName, Action<EventContext> listener)
        {
            if (!_eventSlots.TryGetValue(eventName, out var slot))
            {
                slot = new EventSlot(eventName);
                _eventSlots.Add(eventName, slot);
            }
            (slot as EventSlot)?.Add(listener);
        }
        
        public void Register<TPayload>(string eventName, Action<EventContext<TPayload>> listener)
        {
            if (!CheckPayloadType<TPayload>(eventName))
                return;
            if (!_eventSlotsPayload.TryGetValue(eventName, out var slot))
            {
                slot = new EventSlot<TPayload>(eventName);
                _eventSlotsPayload.Add(eventName, slot);
            }
            (slot as EventSlot<TPayload>)?.Add(listener);
        }

        public void Unregister(string eventName, Action<EventContext> listener)
        {
            if (_eventSlots.TryGetValue(eventName, out var slot))
            {
                (slot as EventSlot)?.Remove(listener);
            }
        }

        public void Unregister<TPayload>(string eventName, Action<EventContext<TPayload>> listener)
        {
            if (_eventSlotsPayload.TryGetValue(eventName, out var slot))
            {
                (slot as EventSlot<TPayload>)?.Remove(listener);
            }
        }

        public void Unregister(string eventName)
        {
            _eventSlots.Remove(eventName);
            _eventSlotsPayload.Remove(eventName);
        }

        public void Broadcast(string eventName)
        {
            if (_eventSlots.TryGetValue(eventName, out var slot))
            {
                (slot as EventSlot)?.Invoke();
            }
        }

        public void Broadcast<TPayload>(string eventName, TPayload payload)
        {
            if (!CheckPayloadType<TPayload>(eventName))
                return;
            if (_eventSlotsPayload.TryGetValue(eventName, out var slot))
            {
                (slot as EventSlot<TPayload>)?.Invoke(payload);
            }
            Broadcast(eventName);
        }
    }
}
