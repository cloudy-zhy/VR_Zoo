using System;
using System.Collections.Generic;

namespace Core.Event
{
    public class EventManager
    {
        private readonly Dictionary<string, IEvent> _eventDict = new();

        // ══════════════════════════════════════════════════════════════════
        // 无参数版本
        // ══════════════════════════════════════════════════════════════════

        public void Register(string eventName, Action action)
        {
            if (_eventDict.TryGetValue(eventName, out var @event))
            {
                ((Event)@event).Subscribe(action);
            }
            else
            {
                _eventDict[eventName] = new Event(action);
            }
        }

        public void Unregister(string eventName, Action action)
        {
            if (_eventDict.TryGetValue(eventName, out var @event))
            {
                ((Event)@event).Unsubscribe(action);
            }
        }

        public void Broadcast(string eventName)
        {
            if (_eventDict.TryGetValue(eventName, out var @event))
            {
                ((Event)@event).Invoke();
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // 单参数版本 <T1>
        // ══════════════════════════════════════════════════════════════════

        public void Register<T1>(string eventName, Action<T1> action)
        {
            if (_eventDict.TryGetValue(eventName, out var @event))
            {
                ((Event<T1>)@event).Subscribe(action);
            }
            else
            {
                _eventDict[eventName] = new Event<T1>(action);
            }
        }

        public void Unregister<T1>(string eventName, Action<T1> action)
        {
            if (_eventDict.TryGetValue(eventName, out var @event))
            {
                ((Event<T1>)@event).Unsubscribe(action);
            }
        }

        public void Broadcast<T1>(string eventName, T1 arg1)
        {
            if (_eventDict.TryGetValue(eventName, out var @event))
            {
                ((Event<T1>)@event).Invoke(arg1);
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // 双参数版本 <T1, T2>
        // ══════════════════════════════════════════════════════════════════

        public void Register<T1, T2>(string eventName, Action<T1, T2> action)
        {
            if (_eventDict.TryGetValue(eventName, out var @event))
            {
                ((Event<T1, T2>)@event).Subscribe(action);
            }
            else
            {
                _eventDict[eventName] = new Event<T1, T2>(action);
            }
        }

        public void Unregister<T1, T2>(string eventName, Action<T1, T2> action)
        {
            if (_eventDict.TryGetValue(eventName, out var @event))
            {
                ((Event<T1, T2>)@event).Unsubscribe(action);
            }
        }

        public void Broadcast<T1, T2>(string eventName, T1 arg1, T2 arg2)
        {
            if (_eventDict.TryGetValue(eventName, out var @event))
            {
                ((Event<T1, T2>)@event).Invoke(arg1, arg2);
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // 三参数版本 <T1, T2, T3>（按需扩展）
        // ══════════════════════════════════════════════════════════════════

        public void Register<T1, T2, T3>(string eventName, Action<T1, T2, T3> action)
        {
            if (_eventDict.TryGetValue(eventName, out var @event))
            {
                ((Event<T1, T2, T3>)@event).Subscribe(action);
            }
            else
            {
                _eventDict[eventName] = new Event<T1, T2, T3>(action);
            }
        }

        public void Unregister<T1, T2, T3>(string eventName, Action<T1, T2, T3> action)
        {
            if (_eventDict.TryGetValue(eventName, out var @event))
            {
                ((Event<T1, T2, T3>)@event).Unsubscribe(action);
            }
        }

        public void Broadcast<T1, T2, T3>(string eventName, T1 arg1, T2 arg2, T3 arg3)
        {
            if (_eventDict.TryGetValue(eventName, out var @event))
            {
                ((Event<T1, T2, T3>)@event).Invoke(arg1, arg2, arg3);
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // 工具方法
        // ══════════════════════════════════════════════════════════════════

        /// <summary>清除指定事件的所有监听</summary>
        public void Unregister(string eventName)
        {
            _eventDict.Remove(eventName);
        }

        /// <summary>清除所有事件（场景切换时调用）</summary>
        public void UnregisterAll()
        {
            _eventDict.Clear();
        }
    }
}