namespace BaseStorage.Plugin.Compendium;

using BaseStorage.Core.Compendium;
using BaseStorage.Core.Persistence;

/// <summary>Loads the player's compendium journal from the character and saves it back. Main thread only.</summary>
internal static class CompendiumJournalStore
{
    private static Player? owner;
    private static CompendiumJournal? journal;

    /// <summary>Gets a player's journal, loading it when the character changed.</summary>
    /// <param name="player">The player.</param>
    /// <returns>The journal.</returns>
    internal static CompendiumJournal For(Player player)
    {
        if (journal == null || owner != player)
        {
            string? saved = null;
            player.m_customData?.TryGetValue(PersistenceKeys.CompendiumJournal, out saved);
            journal = new CompendiumJournal(saved);
            owner = player;
        }

        return journal;
    }

    /// <summary>Saves a player's journal on the character.</summary>
    /// <param name="player">The player.</param>
    internal static void Save(Player player)
    {
        if (player.m_customData != null && journal != null && owner == player)
        {
            player.m_customData[PersistenceKeys.CompendiumJournal] = journal.ToSaved();
        }
    }
}
