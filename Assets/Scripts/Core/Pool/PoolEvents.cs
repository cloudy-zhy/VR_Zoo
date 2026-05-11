namespace Core.Pool
{
    /// <summary>
    /// 对象池运行时事件名，供编辑器监控面板和玩法调试工具订阅。
    /// </summary>
    public static class PoolEvents
    {
        /// <summary>Payload 为 poolName，表示对象池完成注册。</summary>
        public const string Registered = "Pool.Registered";

        /// <summary>Payload 为 poolName，表示对象从池中租借。</summary>
        public const string Rented = "Pool.Rented";

        /// <summary>Payload 为 poolName，表示对象归还到池中。</summary>
        public const string Returned = "Pool.Returned";

        /// <summary>Payload 为 poolName，表示对象池已注销。</summary>
        public const string Unregistered = "Pool.Unregistered";

        /// <summary>无 Payload，表示所有对象池已清空。</summary>
        public const string Cleared = "Pool.Cleared";
    }
}