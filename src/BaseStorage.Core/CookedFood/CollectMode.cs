namespace BaseStorage.Core.CookedFood;

/// <summary>What a cooking station does with food once it is cooked.</summary>
public enum CollectMode
{
    /// <summary>Food stays on the station until a player takes it, as in the game.</summary>
    Off = 0,

    /// <summary>Food goes into a base chest for it, or is dropped by the station when no chest has room.</summary>
    ToChests = 1,

    /// <summary>Food is dropped by the station.</summary>
    Drop = 2,
}
