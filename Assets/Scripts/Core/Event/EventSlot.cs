using System;

namespace Core.Event
{
    internal interface IEventSlot
    {
        int ListenerCount { get; }

#if UNITY_EDITOR
        Delegate[] GetDebugInvocationList();
#endif
    }
    
    // 同一类事件，当然共享同一份EventContext
    internal sealed class EventSlot : IEventSlot
    {
        private Action<EventContext> _actions;
        private readonly EventContext _context;

        public int ListenerCount => _actions?.GetInvocationList().Length ?? 0;

        public EventSlot(string eventName)
        {
            _context = new EventContext(eventName);
        }

        public void Add(Action<EventContext> action)
        {
            _actions -= action;
            _actions += action;
        }

        public void Remove(Action<EventContext> action)
        {
            _actions -= action;
        }

        public void Invoke()
        {
            _actions?.Invoke(_context);
        }

#if UNITY_EDITOR
        public Delegate[] GetDebugInvocationList()
        {
            return _actions?.GetInvocationList() ?? Array.Empty<Delegate>();
        }
#endif
    }
    
    internal sealed class EventSlot<TPayload> : IEventSlot
    {
        private Action<EventContext<TPayload>> _actions;
        private EventContext<TPayload> _context;
        
        public int ListenerCount => _actions?.GetInvocationList().Length ?? 0;

        public EventSlot(string eventName)
        {
            _context = new EventContext<TPayload>(eventName, default);
        }

        public void Add(Action<EventContext<TPayload>> action)
        {
            _actions -= action;
            _actions += action;
        }

        public void Remove(Action<EventContext<TPayload>> action)
        {
            _actions -= action;
        }

        public void Invoke(TPayload payload)
        {
            _context.Payload = payload;
            _actions?.Invoke(_context);
        }

#if UNITY_EDITOR
        public Delegate[] GetDebugInvocationList()
        {
            return _actions?.GetInvocationList() ?? Array.Empty<Delegate>();
        }
#endif
    }
}