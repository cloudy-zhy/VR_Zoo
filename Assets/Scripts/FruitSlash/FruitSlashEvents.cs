namespace FruitSlash
{
    /// <summary>
    /// Fruit Slash 对外预留事件名。
    /// </summary>
    public static class FruitSlashEvents
    {
        public const string Started = "FruitSlash.Started";
        public const string StageChanged = "FruitSlash.StageChanged";
        public const string FruitCut = "FruitSlash.FruitCut";
        public const string ComboChanged = "FruitSlash.ComboChanged";
        public const string Completed = "FruitSlash.Completed";
        public const string BladeEmpowered = "FruitSlash.BladeEmpowered";

        public const string InternalFruitCut = "FruitSlash.Internal.FruitCut";
        public const string InternalFruitMissed = "FruitSlash.Internal.FruitMissed";
    }
}
