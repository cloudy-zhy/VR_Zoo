using Manager;
using UnityEngine;

namespace Core.Event
{
    public static class EventBroadcastExtensions
    {
        public static void Broadcast(this Component source, string eventName)
        {
            GameManager.Event.Broadcast(source, eventName);
        }

        public static void Broadcast<TPayload>(this Component source, string eventName, TPayload payload)
        {
            GameManager.Event.Broadcast(source, eventName, payload);
        }

        public static void Broadcast(this GameObject source, string eventName)
        {
            GameManager.Event.Broadcast(source, eventName);
        }

        public static void Broadcast<TPayload>(this GameObject source, string eventName, TPayload payload)
        {
            GameManager.Event.Broadcast(source, eventName, payload);
        }
    }
}
