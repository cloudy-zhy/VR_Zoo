#if UNITY_EDITOR
using System;

namespace Core.Event
{
    /// <summary>
    /// Editor-only event debug channel. It is intentionally separate from EventManager's runtime event flow.
    /// </summary>
    public static class EventDebugHub
    {
        internal static EventDebugStore Store { get; } = new();

        public static event Action Changed;

        internal static void NotifyChanged()
        {
            Changed?.Invoke();
        }
    }
}
#endif
