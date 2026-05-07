using System;

namespace Core.Event
{
    public interface IEvent
    {
        
    }
    // 无参
    public class Event : IEvent
    {
        private Action _actions;
        public Event(Action action)
        {
            _actions += action;
        }
        public void Subscribe(Action action)
        {
            _actions += action;
        }
        public void Unsubscribe(Action action)
        {
            _actions -= action;
        }

        public void Invoke()
        {
            _actions?.Invoke();
        }
    }
    // 单参
    public class Event<T1> : IEvent
    {
        private Action<T1> _actions;
        public Event(Action<T1> action)
        {
            _actions += action;
        }
        public void Subscribe(Action<T1> action)
        {
            _actions += action;
        }
        public void Unsubscribe(Action<T1> action)
        {
            _actions -= action;
        }

        public void Invoke(T1 arg1)
        {
            _actions?.Invoke(arg1);
        }
    }
    // 双参
    public class Event<T1, T2> : IEvent
    {
        private Action<T1, T2> _actions;
        public Event(Action<T1, T2> action)
        {
            _actions += action;
        }
        public void Subscribe(Action<T1, T2> action)
        {
            _actions += action;
        }
        public void Unsubscribe(Action<T1, T2> action)
        {
            _actions -= action;
        }

        public void Invoke(T1 arg1, T2 arg2)
        {
            _actions?.Invoke(arg1, arg2);
        }
    }
    // 三参
    public class Event<T1, T2, T3> : IEvent
    {
        private Action<T1, T2, T3> _actions;
        public Event(Action<T1, T2, T3> action)
        {
            _actions += action;
        }
        public void Subscribe(Action<T1, T2, T3> action)
        {
            _actions += action;
        }
        public void Unsubscribe(Action<T1, T2, T3> action)
        {
            _actions -= action;
        }

        public void Invoke(T1 arg1, T2 arg2, T3 arg3)
        {
            _actions?.Invoke(arg1, arg2, arg3);
        }
    }
}