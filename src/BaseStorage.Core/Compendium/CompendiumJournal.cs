namespace BaseStorage.Core.Compendium;

using System;
using System.Collections.Generic;
using System.Globalization;

/// <summary>How much of what the player hasn't found the compendium shows.</summary>
public enum SpoilerLevel
{
    /// <summary>Nothing: only what the player has found.</summary>
    Hidden = 0,

    /// <summary>Silhouettes and hints for the lands the player has visited.</summary>
    Hints = 1,

    /// <summary>As <see cref="Hints"/>, plus silhouettes without hints for the next land.</summary>
    LookAhead = 2,
}

/// <summary>When a silhouette's second hint becomes available.</summary>
public enum SecondHintRule
{
    /// <summary>Once the player has found half of that land.</summary>
    HalfFound = 0,

    /// <summary>Straight away.</summary>
    RightAway = 1,

    /// <summary>Never.</summary>
    Never = 2,
}

/// <summary>
/// The player's own compendium state, saved on the character: hint progress, names revealed, hints tracked on screen, pages already read and
/// the spoiler settings. A damaged entry is skipped, so a bad save never loses the rest.
/// </summary>
public sealed class CompendiumJournal
{
    private const char Separator = ';';
    private const string StartedEntry = "started";
    private const string OptionPrefix = "o:";
    private const string ShownPrefix = "h:";
    private const string RevealedPrefix = "x:";
    private const string TrackedPrefix = "t:";
    private const string ReadPrefix = "r:";
    private const string SpoilerOption = "spoil";
    private const string SecondHintOption = "hint2";
    private const string CountsOption = "counts";
    private const string HudOption = "hud";
    private const string NearOption = "near";

    private readonly Dictionary<string, int> shown = new Dictionary<string, int>(StringComparer.Ordinal);
    private readonly HashSet<string> revealed = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> tracked = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> read = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>Initializes a new instance of the <see cref="CompendiumJournal"/> class.</summary>
    /// <param name="saved">The saved text, which may be null, empty or damaged.</param>
    public CompendiumJournal(string? saved)
    {
        foreach (string entry in (saved ?? string.Empty).Split(Separator))
        {
            Load(entry);
        }
    }

    /// <summary>Gets or sets how much of what the player hasn't found is shown.</summary>
    public SpoilerLevel Spoilers { get; set; } = SpoilerLevel.Hints;

    /// <summary>Gets or sets when second hints become available.</summary>
    public SecondHintRule SecondHint { get; set; } = SecondHintRule.HalfFound;

    /// <summary>Gets or sets a value indicating whether each land shows how much of it has been found.</summary>
    public bool ShowCounts { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether tracked hints are shown on screen while playing.</summary>
    public bool ShowTracked { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether the player is told when they reach the right place for a tracked hint.</summary>
    public bool TellWhenNear { get; set; } = true;

    /// <summary>Gets a value indicating whether the journal has recorded what was already known when it started.</summary>
    public bool Started { get; private set; }

    /// <summary>Gets the ids of the hints tracked on screen.</summary>
    public IReadOnlyCollection<string> Tracked => tracked;

    /// <summary>Gets how many hints of a silhouette the player has asked for; the first is always given.</summary>
    /// <param name="id">The silhouette's page id.</param>
    /// <returns>The number shown, at least one.</returns>
    public int ShownHints(string id) => shown.TryGetValue(id, out int count) && count > 1 ? count : 1;

    /// <summary>Records that one more hint is shown.</summary>
    /// <param name="id">The silhouette's page id.</param>
    /// <param name="most">The most hints the silhouette has.</param>
    /// <returns>True when it changed.</returns>
    public bool ShowNextHint(string id, int most)
    {
        int next = ShownHints(id) + 1;
        if (!Valid(id) || next > most)
        {
            return false;
        }

        shown[id] = next;
        return true;
    }

    /// <summary>Tells whether a silhouette's name has been revealed.</summary>
    /// <param name="id">The silhouette's page id.</param>
    /// <returns>True when revealed.</returns>
    public bool IsRevealed(string id) => revealed.Contains(id);

    /// <summary>Reveals a silhouette's name.</summary>
    /// <param name="id">The silhouette's page id.</param>
    /// <returns>True when it changed.</returns>
    public bool Reveal(string id) => Valid(id) && revealed.Add(id);

    /// <summary>Tells whether a hint is tracked on screen.</summary>
    /// <param name="id">The silhouette's page id.</param>
    /// <returns>True when tracked.</returns>
    public bool IsTracked(string id) => tracked.Contains(id);

    /// <summary>Starts or stops tracking a hint on screen.</summary>
    /// <param name="id">The silhouette's page id.</param>
    /// <returns>True when it is now tracked.</returns>
    public bool ToggleTracked(string id)
    {
        if (!Valid(id))
        {
            return false;
        }

        if (tracked.Remove(id))
        {
            return false;
        }

        tracked.Add(id);
        return true;
    }

    /// <summary>Stops tracking a hint, for example once its subject is found.</summary>
    /// <param name="id">The silhouette's page id.</param>
    /// <returns>True when it was tracked.</returns>
    public bool Untrack(string id) => tracked.Remove(id);

    /// <summary>Tells whether a page has been read since it was discovered.</summary>
    /// <param name="pageId">The page's id.</param>
    /// <returns>True when read.</returns>
    public bool IsRead(string pageId) => read.Contains(pageId);

    /// <summary>Records that a page has been read.</summary>
    /// <param name="pageId">The page's id.</param>
    /// <returns>True when it changed.</returns>
    public bool MarkRead(string pageId) => Valid(pageId) && read.Add(pageId);

    /// <summary>On the first use, marks everything already known as read, so only later discoveries count as new.</summary>
    /// <param name="knownPages">The ids of every page the player has.</param>
    /// <returns>True when it changed.</returns>
    public bool Start(IEnumerable<string> knownPages)
    {
        if (knownPages == null)
        {
            throw new ArgumentNullException(nameof(knownPages));
        }

        if (Started)
        {
            return false;
        }

        foreach (string page in knownPages)
        {
            MarkRead(page);
        }

        Started = true;
        return true;
    }

    /// <summary>Writes the text to save.</summary>
    /// <returns>The text, sorted so it doesn't change without cause.</returns>
    public string ToSaved()
    {
        var entries = new List<string>
        {
            OptionPrefix + SpoilerOption + "=" + ((int)Spoilers).ToString(CultureInfo.InvariantCulture),
            OptionPrefix + SecondHintOption + "=" + ((int)SecondHint).ToString(CultureInfo.InvariantCulture),
            OptionPrefix + CountsOption + "=" + Flag(ShowCounts),
            OptionPrefix + HudOption + "=" + Flag(ShowTracked),
            OptionPrefix + NearOption + "=" + Flag(TellWhenNear),
        };
        if (Started)
        {
            entries.Add(StartedEntry);
        }

        foreach (KeyValuePair<string, int> pair in shown)
        {
            entries.Add(ShownPrefix + pair.Key + "=" + pair.Value.ToString(CultureInfo.InvariantCulture));
        }

        Add(entries, RevealedPrefix, revealed);
        Add(entries, TrackedPrefix, tracked);
        Add(entries, ReadPrefix, read);
        entries.Sort(StringComparer.Ordinal);
        return string.Join(Separator.ToString(), entries);
    }

    private static bool Valid(string? id) => !string.IsNullOrEmpty(id) && id!.IndexOf(Separator) < 0 && id.IndexOf('=') < 0;

    private static string Flag(bool value) => value ? "1" : "0";

    private static void Add(List<string> entries, string prefix, IEnumerable<string> values)
    {
        foreach (string value in values)
        {
            entries.Add(prefix + value);
        }
    }

    private void Load(string entry)
    {
        if (entry.Length == 0)
        {
            return;
        }

        if (entry == StartedEntry)
        {
            Started = true;
        }
        else if (entry.StartsWith(OptionPrefix, StringComparison.Ordinal))
        {
            LoadOption(entry.Substring(OptionPrefix.Length));
        }
        else if (entry.StartsWith(ShownPrefix, StringComparison.Ordinal))
        {
            int equals = entry.LastIndexOf('=');
            if (equals > ShownPrefix.Length
                && int.TryParse(entry.Substring(equals + 1), NumberStyles.None, CultureInfo.InvariantCulture, out int count)
                && count > 1
                && count < 10)
            {
                shown[entry.Substring(ShownPrefix.Length, equals - ShownPrefix.Length)] = count;
            }
        }
        else if (entry.StartsWith(RevealedPrefix, StringComparison.Ordinal))
        {
            Reveal(entry.Substring(RevealedPrefix.Length));
        }
        else if (entry.StartsWith(TrackedPrefix, StringComparison.Ordinal))
        {
            string id = entry.Substring(TrackedPrefix.Length);
            if (Valid(id))
            {
                tracked.Add(id);
            }
        }
        else if (entry.StartsWith(ReadPrefix, StringComparison.Ordinal))
        {
            MarkRead(entry.Substring(ReadPrefix.Length));
        }
    }

    private void LoadOption(string option)
    {
        int equals = option.IndexOf('=');
        if (equals <= 0 || !int.TryParse(option.Substring(equals + 1), NumberStyles.None, CultureInfo.InvariantCulture, out int value))
        {
            return;
        }

        switch (option.Substring(0, equals))
        {
            case SpoilerOption when Enum.IsDefined(typeof(SpoilerLevel), value):
                Spoilers = (SpoilerLevel)value;
                break;
            case SecondHintOption when Enum.IsDefined(typeof(SecondHintRule), value):
                SecondHint = (SecondHintRule)value;
                break;
            case CountsOption when value <= 1:
                ShowCounts = value == 1;
                break;
            case HudOption when value <= 1:
                ShowTracked = value == 1;
                break;
            case NearOption when value <= 1:
                TellWhenNear = value == 1;
                break;
        }
    }
}
