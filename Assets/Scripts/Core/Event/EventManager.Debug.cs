#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace Core.Event
{
    public partial class EventManager
    {
        public IReadOnlyList<EventDebugInfo> GetDebugEvents()
        {
            return EventDebugHub.Store.GetDebugEvents();
        }

        public IReadOnlyList<EventDebugLogRecord> GetDebugLogs()
        {
            return EventDebugHub.Store.GetDebugLogs();
        }

        public IReadOnlyList<EventListenerInfo> GetDebugListeners(string eventName)
        {
            return EventDebugHub.Store.GetDebugListeners(eventName, GetDebugInvocationList(eventName, true));
        }

        private void SetDebugPayloadType(string eventName, Type payloadType)
        {
            EventDebugHub.Store.SetPayloadType(
                eventName,
                payloadType,
                GetNoPayloadListenerCount(eventName),
                GetPayloadListenerCount(eventName));
        }

        private void RecordDebugRegister(string eventName, Delegate listener)
        {
            EventDebugHub.Store.RecordRegister(
                eventName,
                listener,
                GetNoPayloadListenerCount(eventName),
                GetPayloadListenerCount(eventName));
            EventDebugHub.NotifyChanged();
        }

        private void RecordDebugUnregister(string eventName, Delegate listener)
        {
            EventDebugHub.Store.RecordUnregister(
                eventName,
                listener,
                GetNoPayloadListenerCount(eventName),
                GetPayloadListenerCount(eventName));
            EventDebugHub.NotifyChanged();
        }

        private void RecordDebugUnregisterAll(string eventName)
        {
            EventDebugHub.Store.RecordUnregisterAll(eventName);
            EventDebugHub.NotifyChanged();
        }

        private void ClearDebug()
        {
            EventDebugHub.Store.Clear();
            EventDebugHub.NotifyChanged();
        }

        private void RecordDebugBroadcast(
            object source,
            string eventName,
            object payload,
            bool includePayloadListeners)
        {
            EventDebugHub.Store.RecordBroadcast(
                source,
                eventName,
                payload,
                GetDebugInvocationList(eventName, includePayloadListeners),
                GetNoPayloadListenerCount(eventName),
                GetPayloadListenerCount(eventName));
            EventDebugHub.NotifyChanged();
        }

        private Delegate[] GetDebugInvocationList(string eventName, bool includePayloadListeners)
        {
            List<Delegate> result = new();
            if (includePayloadListeners && _eventSlotsPayload.TryGetValue(eventName, out IEventSlot payloadSlot))
            {
                result.AddRange(payloadSlot.GetDebugInvocationList());
            }

            if (_eventSlots.TryGetValue(eventName, out IEventSlot slot))
            {
                result.AddRange(slot.GetDebugInvocationList());
            }

            return result.ToArray();
        }
    }
}
#endif
