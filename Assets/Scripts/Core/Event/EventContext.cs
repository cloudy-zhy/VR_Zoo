namespace Core.Event
{
    public readonly struct EventContext
    {
        public string EventName { get; }

        public EventContext(string eventName)
        {
            EventName = eventName;
        }
    }

    public struct EventContext<TPayload>
    {
        public string EventName { get; }
        public TPayload Payload { get; set; }

        public EventContext(string eventName, TPayload payload)
        {
            EventName = eventName;
            Payload = payload;
        }
    }
}