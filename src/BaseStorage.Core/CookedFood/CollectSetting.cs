namespace BaseStorage.Core.CookedFood;

using System;

/// <summary>Reads and writes a station's stored collect setting. Pure.</summary>
public static class CollectSetting
{
    /// <summary>The stored value for <see cref="CollectMode.Off"/>.</summary>
    public const string Off = "off";

    /// <summary>The stored value for <see cref="CollectMode.ToChests"/>.</summary>
    public const string Chests = "chests";

    /// <summary>The stored value for <see cref="CollectMode.Drop"/>.</summary>
    public const string Drop = "drop";

    /// <summary>Reads a stored value. A station the player never set collects into chests while it is fed, and is left alone otherwise.</summary>
    /// <param name="stored">The stored value, which may be empty or unknown.</param>
    /// <param name="fed">Whether the station is kept fed.</param>
    /// <returns>The mode.</returns>
    public static CollectMode Decode(string? stored, bool fed)
    {
        if (string.Equals(stored, Off, StringComparison.Ordinal))
        {
            return CollectMode.Off;
        }

        if (string.Equals(stored, Chests, StringComparison.Ordinal))
        {
            return CollectMode.ToChests;
        }

        if (string.Equals(stored, Drop, StringComparison.Ordinal))
        {
            return CollectMode.Drop;
        }

        return fed ? CollectMode.ToChests : CollectMode.Off;
    }

    /// <summary>Gets the stored value of a mode.</summary>
    /// <param name="mode">The mode.</param>
    /// <returns>The stored value.</returns>
    public static string Encode(CollectMode mode) => mode switch
    {
        CollectMode.Off => Off,
        CollectMode.ToChests => Chests,
        CollectMode.Drop => Drop,
        _ => throw new ArgumentOutOfRangeException(nameof(mode)),
    };

    /// <summary>Gets the mode a click on the setting moves to: off, to chests, drop, then off again.</summary>
    /// <param name="mode">The current mode.</param>
    /// <returns>The next mode.</returns>
    public static CollectMode Next(CollectMode mode) => mode switch
    {
        CollectMode.Off => CollectMode.ToChests,
        CollectMode.ToChests => CollectMode.Drop,
        _ => CollectMode.Off,
    };
}
