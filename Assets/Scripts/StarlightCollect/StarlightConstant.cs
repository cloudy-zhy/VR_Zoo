namespace StarlightCollect
{
    /// <summary>
    /// 星光能量玩法事件名与对象池 key。
    /// </summary>
    public static class StarlightConstant
    {
        // ===== PoolKey =====
        public const string StarLightPoolKey = "StarLight";
        public const string StarLightCollectingPoolKey = "StarLightCollecting";
        public const string DisappearPoPoolKey = "StarLightDisappearPo";
        // ===== Variable =====
        public const float ArrivalDist = 0.2f;
        // ===== ThrowEvent =====
        public const string PterosaurThrow = "StarLight.PterosaurThrow";
        public const string StarlightMarked = "StarLight.StarLightMarked";    // 星光广播，CatchController接收
        // ===== CatchEvent =====
        public const string StarLightCollected = "StarLight.StarLightCollected";  // 回归星光广播，CatchController接收
        public const string PterosaurArrived = "StarLight.PterosaurArrived";    // 翼龙广播，提灯、CatchController接收
    }
}
