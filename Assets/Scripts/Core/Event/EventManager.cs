using System;
using System.Collections.Generic;

namespace Core.Event
{
    public class EventManager
    {
        private readonly Dictionary<string, IEventSlot> _eventSlots = new();
        
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
            if (!_eventSlots.TryGetValue(eventName, out var slot))
            {
                slot = new EventSlot<TPayload>(eventName);
                _eventSlots.Add(eventName, slot);
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
            if (_eventSlots.TryGetValue(eventName, out var slot))
            {
                (slot as EventSlot<TPayload>)?.Remove(listener);
            }
        }

        public void Unregister(string eventName)
        {
            _eventSlots.Remove(eventName);
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
            if (_eventSlots.TryGetValue(eventName, out var slot))
            {
                (slot as EventSlot<TPayload>)?.Invoke(payload);
            }
        }
    }
}