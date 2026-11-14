namespace BaseStorage.Core.Compendium;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BaseStorage.Core.Ui;

/// <summary>What the player carries and what the base they stand in holds, by item id.</summary>
public sealed class CompendiumStock
{
    /// <summary>Initializes a new instance of the <see cref="CompendiumStock"/> class.</summary>
    /// <param name="inBase">True when the player stands in a base, so its chests were counted.</param>
    public CompendiumStock(bool inBase)
    {
        InBase = inBase;
    }

    /// <summary>Gets an empty stock, for when nothing could be counted.</summary>
    public static CompendiumStock None => new CompendiumStock(false);

    /// <summary>Gets a value indicating whether the player stands in a base, so its chests were counted.</summary>
    public bool InBase { get; }

    /// <summary>Gets what the player carries.</summary>
    public Dictionary<string, int> Carried { get; } = new Dictionary<string, int>(StringComparer.Ordinal);

    /// <summary>Gets what the base's chests hold.</summary>
    public Dictionary<string, int> Base { get; } = new Dictionary<string, int>(StringComparer.Ordinal);

    /// <summary>Gets how many the player carries.</summary>
    /// <param name="itemId">The item's id.</param>
    /// <returns>The count.</returns>
    public int CarriedOf(string itemId) => Carried.TryGetValue(itemId, out int count) ? count : 0;

    /// <summary>Gets how many the base's chests hold.</summary>
    /// <param name="itemId">The item's id.</param>
    /// <returns>The count.</returns>
    public int BaseOf(string itemId) => Base.TryGetValue(itemId, out int count) ? count : 0;

    /// <summary>Works out how many of a recipe's result can be made from what's carried and in the base.</summary>
    /// <param name="recipe">The recipe.</param>
    /// <returns>The number of items, zero when an ingredient is short.</returns>
    public int CanMake(RecipeRecord recipe)
    {
        if (recipe == null)
        {
            throw new ArgumentNullException(nameof(recipe));
        }

        long crafts = long.MaxValue;
        foreach (IngredientRecord ingredient in recipe.Ingredients)
        {
            if (ingredient.Amount > 0)
            {
                crafts = Math.Min(crafts, ((long)CarriedOf(ingredient.ItemId) + BaseOf(ingredient.ItemId)) / ingredient.Amount);
            }
        }

        if (crafts == long.MaxValue)
        {
            return 0;
        }

        long made = crafts * Math.Max(1, recipe.Amount);
        return made > int.MaxValue ? int.MaxValue : (int)made;
    }
}

/// <summary>
/// Builds the whole book: the discovered pages, with what the base holds and the best weapons added, plus the home page, the silhouettes
/// of what's not yet found and the settings page. Links that start with <see cref="ActionPrefix"/> are actions, not pages. Pure.
/// </summary>
public static class CompendiumJourney
{
    /// <summary>The home page's id.</summary>
    public const string HomePage = "home";

    /// <summary>The id of the page listing what's not yet found.</summary>
    public const string FrontierPage = "frontier";

    /// <summary>The settings page's id.</summary>
    public const string SettingsPage = "settings";

    /// <summary>The prefix of a link that is an action.</summary>
    public const string ActionPrefix = "act:";

    /// <summary>The action that highlights the chests holding an item; the window carries it out.</summary>
    public const string HighlightAction = ActionPrefix + "highlight:";

    private const string NextAction = ActionPrefix + "next:";
    private const string RevealAction = ActionPrefix + "reveal:";
    private const string ConfirmAction = ActionPrefix + "confirm:";
    private const string TrackAction = ActionPrefix + "track:";
    private const string SpoilerAction = ActionPrefix + "spoil:";
    private const string SecondHintAction = ActionPrefix + "hint2:";
    private const string CountsAction = ActionPrefix + "counts";
    private const string HudAction = ActionPrefix + "hud";
    private const string NearAction = ActionPrefix + "near";
    private const int RecentLimit = 8;
    private const int ReadyLimit = 6;
    private const int ReachLimit = 3;
    private const int BestLimit = 3;

    /// <summary>Builds the book.</summary>
    /// <param name="data">The game's data.</param>
    /// <param name="knowledge">What the player has found.</param>
    /// <param name="journal">The player's compendium state.</param>
    /// <param name="stock">What the player carries and the base holds.</param>
    /// <param name="confirming">The silhouette whose name the player is about to reveal, or null.</param>
    /// <returns>The book.</returns>
    public static CompendiumBook Build(CompendiumData data, CompendiumKnowledge knowledge, CompendiumJournal journal, CompendiumStock stock, string? confirming)
    {
        if (journal == null)
        {
            throw new ArgumentNullException(nameof(journal));
        }

        if (stock == null)
        {
            throw new ArgumentNullException(nameof(stock));
        }

        CompendiumBook found = CompendiumBuilder.Build(data, knowledge);
        FrontierResult frontier = CompendiumFrontier.Build(data, knowledge, journal.Spoilers);
        var pages = new List<CompendiumPage>();
        foreach (CompendiumPage page in found.Pages)
        {
            pages.Add(page.Kind switch
            {
                CompendiumKind.Item => WithStock(page, data, knowledge, stock),
                CompendiumKind.Creature => WithBest(page, data, knowledge),
                _ => page,
            });
        }

        pages.Add(Home(found, data, knowledge, journal, stock, frontier));
        pages.Add(FrontierList(frontier, journal));
        pages.AddRange(frontier.Entries.Select(e => FrontierEntryPage(e, frontier, journal, confirming)));
        pages.Add(Settings(journal));
        return new CompendiumBook(pages);
    }

    /// <summary>Tells whether a page counts as something discovered, which can be new.</summary>
    /// <param name="page">The page.</param>
    /// <returns>True for an item, creature, biome or piece page.</returns>
    public static bool IsDiscovery(CompendiumPage page) =>
        page != null && (page.Kind == CompendiumKind.Item || page.Kind == CompendiumKind.Creature || page.Kind == CompendiumKind.Biome || page.Kind == CompendiumKind.Piece);

    /// <summary>Carries out an action link on the journal.</summary>
    /// <param name="journal">The journal.</param>
    /// <param name="action">The link.</param>
    /// <param name="confirming">The silhouette now waiting for a second click to reveal its name, or null.</param>
    /// <returns>True when the journal changed and should be saved.</returns>
    public static bool Apply(CompendiumJournal journal, string action, out string? confirming)
    {
        if (journal == null)
        {
            throw new ArgumentNullException(nameof(journal));
        }

        confirming = null;
        if (action == null || !action.StartsWith(ActionPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        if (action.StartsWith(NextAction, StringComparison.Ordinal))
        {
            return journal.ShowNextHint(action.Substring(NextAction.Length), 2);
        }

        if (action.StartsWith(RevealAction, StringComparison.Ordinal))
        {
            confirming = action.Substring(RevealAction.Length);
            return false;
        }

        if (action.StartsWith(ConfirmAction, StringComparison.Ordinal))
        {
            return journal.Reveal(action.Substring(ConfirmAction.Length));
        }

        if (action.StartsWith(TrackAction, StringComparison.Ordinal))
        {
            journal.ToggleTracked(action.Substring(TrackAction.Length));
            return true;
        }

        if (action.StartsWith(SpoilerAction, StringComparison.Ordinal) && TryEnum(action.Substring(SpoilerAction.Length), out SpoilerLevel spoilers))
        {
            journal.Spoilers = spoilers;
            return true;
        }

        if (action.StartsWith(SecondHintAction, StringComparison.Ordinal) && TryEnum(action.Substring(SecondHintAction.Length), out SecondHintRule rule))
        {
            journal.SecondHint = rule;
            return true;
        }

        switch (action)
        {
            case CountsAction:
                journal.ShowCounts = !journal.ShowCounts;
                return true;
            case HudAction:
                journal.ShowTracked = !journal.ShowTracked;
                return true;
            case NearAction:
                journal.TellWhenNear = !journal.TellWhenNear;
                return true;
            default:
                return false;
        }
    }

    /// <summary>Ranks the weapons the player has found by the damage they'd do to a creature, before skills and quality.</summary>
    /// <param name="creature">The creature.</param>
    /// <param name="data">The game's data.</param>
    /// <param name="knowledge">What the player has found.</param>
    /// <returns>The weapons and their damage, best first, without any that would do none.</returns>
    public static IReadOnlyList<KeyValuePair<ItemRecord, float>> BestWeapons(CreatureRecord creature, CompendiumData data, CompendiumKnowledge knowledge)
    {
        if (creature == null || data == null || knowledge == null)
        {
            throw new ArgumentNullException(creature == null ? nameof(creature) : data == null ? nameof(data) : nameof(knowledge));
        }

        return data.Items
            .Where(i => i.Category == ItemCategory.Weapon && knowledge.Items.Contains(i.Id))
            .Select(i => new KeyValuePair<ItemRecord, float>(i, DamageAgainst(i, creature)))
            .Where(p => p.Value > 0f)
            .OrderByDescending(p => p.Value)
            .ThenBy(p => p.Key.Name, StringComparer.OrdinalIgnoreCase)
            .Take(BestLimit)
            .ToList();
    }

    /// <summary>Works out a weapon's damage against a creature: each damage type times the creature's response to it. Chop and pickaxe damage don't hurt creatures.</summary>
    /// <param name="weapon">The weapon.</param>
    /// <param name="creature">The creature.</param>
    /// <returns>The damage.</returns>
    public static float DamageAgainst(ItemRecord weapon, CreatureRecord creature)
    {
        if (weapon == null || creature == null)
        {
            throw new ArgumentNullException(weapon == null ? nameof(weapon) : nameof(creature));
        }

        float total = 0f;
        foreach (DamageAmount damage in weapon.Damage)
        {
            if (damage.Type == CompendiumDamage.Chop || damage.Type == CompendiumDamage.Pickaxe)
            {
                continue;
            }

            DamageResponse response = creature.Responses.Where(r => r.Type == damage.Type).Select(r => r.Response).FirstOrDefault();
            total += damage.Amount * Multiplier(response);
        }

        return total;
    }

    /// <summary>Gets the game's damage multiplier for a response.</summary>
    /// <param name="response">The response.</param>
    /// <returns>The multiplier.</returns>
    public static float Multiplier(DamageResponse response) => response switch
    {
        DamageResponse.VeryWeak => 2f,
        DamageResponse.Weak => 1.5f,
        DamageResponse.SlightlyWeak => 1.25f,
        DamageResponse.SlightlyResistant => 0.75f,
        DamageResponse.Resistant => 0.5f,
        DamageResponse.VeryResistant => 0.25f,
        DamageResponse.Immune => 0f,
        _ => 1f,
    };

    private static bool TryEnum<T>(string text, out T value)
        where T : struct
    {
        value = default;
        if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int number) || !Enum.IsDefined(typeof(T), number))
        {
            return false;
        }

        value = (T)Enum.ToObject(typeof(T), number);
        return true;
    }

    private static string Get(string key) => StringTable.English.Get(key);

    private static CompendiumPage Rebuilt(CompendiumPage page, List<CompendiumSection> sections) =>
        new CompendiumPage(page.Id, page.Kind, page.Title, page.Group, page.Description, sections);

    private static bool CanUseStation(RecipeRecord recipe, CompendiumKnowledge knowledge) =>
        recipe.Station.Length == 0 || knowledge.Stations.Contains(recipe.Station);

    private static CompendiumPage WithStock(CompendiumPage page, CompendiumData data, CompendiumKnowledge knowledge, CompendiumStock stock)
    {
        string itemId = page.Id.Substring(CompendiumBuilder.ItemPage(string.Empty).Length);
        var lines = new List<CompendiumLine>();
        int carried = stock.CarriedOf(itemId);
        int held = stock.BaseOf(itemId);
        if (stock.InBase)
        {
            lines.Add(new CompendiumLine(CompendiumBuilder.Text(StringKeys.CompendiumStockLine, CompendiumBuilder.Number(held), CompendiumBuilder.Number(carried))));
            if (held > 0)
            {
                lines.Add(new CompendiumLine(Get(StringKeys.CompendiumStockShow), HighlightAction + itemId));
            }
        }
        else
        {
            lines.Add(new CompendiumLine(CompendiumBuilder.Text(StringKeys.CompendiumStockOutside, CompendiumBuilder.Number(carried))));
        }

        int make = data.Recipes.Where(r => r.ResultId == itemId && CanUseStation(r, knowledge)).Select(stock.CanMake).DefaultIfEmpty(0).Max();
        if (make > 0)
        {
            lines.Add(new CompendiumLine(CompendiumBuilder.Text(StringKeys.CompendiumStockMake, CompendiumBuilder.Number(make))));
        }

        var sections = page.Sections.ToList();
        sections.Insert(Math.Min(1, sections.Count), new CompendiumSection(Get(StringKeys.CompendiumSectionStock), lines));
        return Rebuilt(page, sections);
    }

    private static CompendiumPage WithBest(CompendiumPage page, CompendiumData data, CompendiumKnowledge knowledge)
    {
        string creatureId = page.Id.Substring(CompendiumBuilder.CreaturePage(string.Empty).Length);
        CreatureRecord? creature = data.Creatures.FirstOrDefault(c => c.Id == creatureId);
        if (creature == null)
        {
            return page;
        }

        List<CompendiumLine> lines = BestWeapons(creature, data, knowledge)
            .Select(p => new CompendiumLine(CompendiumBuilder.Text(StringKeys.CompendiumBestLine, p.Key.Name, CompendiumBuilder.Number(CompendiumFrontierRound(p.Value))), CompendiumBuilder.ItemPage(p.Key.Id)))
            .ToList();
        if (lines.Count == 0)
        {
            return page;
        }

        var sections = page.Sections.ToList();
        sections.Insert(Math.Min(2, sections.Count), new CompendiumSection(Get(StringKeys.CompendiumSectionBest), lines));
        return Rebuilt(page, sections);
    }

    private static float CompendiumFrontierRound(float value) => (float)Math.Round(value, 0, MidpointRounding.AwayFromZero);

    private static CompendiumPage Home(CompendiumBook found, CompendiumData data, CompendiumKnowledge knowledge, CompendiumJournal journal, CompendiumStock stock, FrontierResult frontier)
    {
        var sections = new List<CompendiumSection>();
        var journey = frontier.Progress
            .Select(p => new CompendiumLine(
                journal.ShowCounts
                    ? CompendiumBuilder.Text(StringKeys.CompendiumJourneyBiome, CompendiumBuilder.BiomeName(p.Biome), CompendiumBuilder.Number(p.Found), CompendiumBuilder.Number(p.Total))
                    : CompendiumBuilder.BiomeName(p.Biome),
                CompendiumBuilder.BiomePage(p.Biome)))
            .ToList();
        if (frontier.HasUnvisited)
        {
            journey.Add(new CompendiumLine(Get(StringKeys.CompendiumJourneyUnvisited)));
        }

        sections.Add(new CompendiumSection(Get(StringKeys.CompendiumSectionJourney), journey));

        List<CompendiumLine> recent = found.Pages
            .Where(p => IsDiscovery(p) && journal.Started && !journal.IsRead(p.Id))
            .OrderBy(p => p.Kind)
            .ThenBy(p => p.Title, StringComparer.OrdinalIgnoreCase)
            .Take(RecentLimit)
            .Select(p => new CompendiumLine(CompendiumBuilder.Text(StringKeys.CompendiumRecentNew, p.Title), p.Id))
            .ToList();
        if (recent.Count == 0)
        {
            recent.Add(new CompendiumLine(Get(StringKeys.CompendiumRecentNone)));
        }

        sections.Add(new CompendiumSection(Get(StringKeys.CompendiumSectionRecent), recent));

        var ready = new List<CompendiumLine>();
        if (!stock.InBase)
        {
            ready.Add(new CompendiumLine(Get(StringKeys.CompendiumReadyOutside)));
        }
        else
        {
            ready.AddRange(data.Recipes
                .Where(r => knowledge.Items.Contains(r.ResultId) && CanUseStation(r, knowledge))
                .GroupBy(r => r.ResultId, StringComparer.Ordinal)
                .Select(g => new { Id = g.Key, Make = g.Max(stock.CanMake), Page = found.Find(CompendiumBuilder.ItemPage(g.Key)) })
                .Where(x => x.Make > 0 && x.Page != null)
                .OrderBy(x => x.Page!.Title, StringComparer.OrdinalIgnoreCase)
                .Take(ReadyLimit)
                .Select(x => new CompendiumLine(CompendiumBuilder.Text(StringKeys.CompendiumReadyLine, x.Page!.Title, CompendiumBuilder.Number(x.Make)), x.Page.Id)));
            if (ready.Count == 0)
            {
                ready.Add(new CompendiumLine(Get(StringKeys.CompendiumReadyNone)));
            }
        }

        sections.Add(new CompendiumSection(Get(StringKeys.CompendiumSectionReady), ready));

        if (journal.Spoilers != SpoilerLevel.Hidden)
        {
            List<FrontierEntry> near = frontier.Entries.Where(e => !e.Ahead).ToList();
            var reach = near.Take(ReachLimit).Select(e => new CompendiumLine(Title(e, journal), e.Id)).ToList();
            reach.Add(near.Count == 0
                ? new CompendiumLine(Get(StringKeys.CompendiumReachNone))
                : new CompendiumLine(CompendiumBuilder.Text(StringKeys.CompendiumReachMore, CompendiumBuilder.Number(near.Count)), FrontierPage));
            sections.Add(new CompendiumSection(Get(StringKeys.CompendiumSectionReach), reach));
        }

        return new CompendiumPage(HomePage, CompendiumKind.Home, Get(StringKeys.CompendiumHomeTitle), string.Empty, string.Empty, sections);
    }

    private static string Title(FrontierEntry entry, CompendiumJournal journal) => journal.IsRevealed(entry.Id) ? entry.Name : entry.Hints[0];

    private static CompendiumPage FrontierList(FrontierResult frontier, CompendiumJournal journal)
    {
        var sections = new List<CompendiumSection>();
        foreach (IGrouping<CompendiumBiome, FrontierEntry> land in frontier.Entries.GroupBy(e => e.Biome))
        {
            BiomeProgress? progress = frontier.ProgressOf(land.Key);
            string heading = progress != null && journal.ShowCounts
                ? CompendiumBuilder.Text(StringKeys.CompendiumJourneyBiome, CompendiumBuilder.BiomeName(land.Key), CompendiumBuilder.Number(progress.Found), CompendiumBuilder.Number(progress.Total))
                : CompendiumBuilder.BiomeName(land.Key);
            sections.Add(new CompendiumSection(heading, land.Select(e => new CompendiumLine(Title(e, journal), e.Id)).ToList()));
        }

        string intro = Get(frontier.Entries.Count == 0 ? StringKeys.CompendiumFrontierEmpty : StringKeys.CompendiumFrontierIntro);
        return new CompendiumPage(FrontierPage, CompendiumKind.Home, Get(StringKeys.CompendiumFrontierTitle), string.Empty, intro, sections);
    }

    private static CompendiumPage FrontierEntryPage(FrontierEntry entry, FrontierResult frontier, CompendiumJournal journal, string? confirming)
    {
        bool revealed = journal.IsRevealed(entry.Id);
        int shown = Math.Min(journal.ShownHints(entry.Id), entry.Hints.Count);
        var hints = new List<CompendiumLine>();
        for (int i = 0; i < shown; i++)
        {
            hints.Add(new CompendiumLine(CompendiumBuilder.Text(StringKeys.CompendiumHintNumbered, CompendiumBuilder.Number(i + 1), entry.Hints[i])));
        }

        if (revealed)
        {
            hints.Add(new CompendiumLine(CompendiumBuilder.Text(StringKeys.CompendiumHintName, entry.Name)));
        }

        var next = new List<CompendiumLine>();
        if (!revealed && !entry.Ahead)
        {
            if (shown < entry.Hints.Count)
            {
                if (frontier.SecondHintOpen(entry, journal.SecondHint))
                {
                    next.Add(new CompendiumLine(Get(StringKeys.CompendiumActionNext), NextAction + entry.Id));
                }
                else if (journal.SecondHint == SecondHintRule.Never)
                {
                    next.Add(new CompendiumLine(Get(StringKeys.CompendiumHintNever)));
                }
                else
                {
                    BiomeProgress? land = frontier.ProgressOf(entry.Biome);
                    next.Add(new CompendiumLine(CompendiumBuilder.Text(
                        StringKeys.CompendiumHintLocked,
                        CompendiumBuilder.BiomeName(entry.Biome),
                        CompendiumBuilder.Number(land?.Found ?? 0),
                        CompendiumBuilder.Number(land?.Half ?? 0))));
                }
            }

            next.Add(confirming == entry.Id
                ? new CompendiumLine(Get(StringKeys.CompendiumActionConfirm), ConfirmAction + entry.Id)
                : new CompendiumLine(Get(StringKeys.CompendiumActionReveal), RevealAction + entry.Id));
        }

        if (!entry.Ahead)
        {
            next.Add(new CompendiumLine(Get(journal.IsTracked(entry.Id) ? StringKeys.CompendiumActionUntrack : StringKeys.CompendiumActionTrack), TrackAction + entry.Id));
        }

        var sections = new List<CompendiumSection> { new CompendiumSection(Get(StringKeys.CompendiumSectionHints), hints) };
        if (next.Count > 0)
        {
            sections.Add(new CompendiumSection(Get(StringKeys.CompendiumSectionNext), next));
        }

        string group = CompendiumBuilder.Text(
            entry.Kind == CompendiumKind.Creature ? StringKeys.CompendiumFrontierCreature : StringKeys.CompendiumFrontierItem,
            CompendiumBuilder.BiomeName(entry.Biome));
        return new CompendiumPage(entry.Id, CompendiumKind.Frontier, Title(entry, journal), group, string.Empty, sections);
    }

    private static CompendiumPage Settings(CompendiumJournal journal)
    {
        var spoilers = new List<CompendiumLine>
        {
            Choice(StringKeys.CompendiumSettingsHidden, journal.Spoilers == SpoilerLevel.Hidden, SpoilerAction + ((int)SpoilerLevel.Hidden).ToString(CultureInfo.InvariantCulture)),
            Choice(StringKeys.CompendiumSettingsHints, journal.Spoilers == SpoilerLevel.Hints, SpoilerAction + ((int)SpoilerLevel.Hints).ToString(CultureInfo.InvariantCulture)),
            Choice(StringKeys.CompendiumSettingsAhead, journal.Spoilers == SpoilerLevel.LookAhead, SpoilerAction + ((int)SpoilerLevel.LookAhead).ToString(CultureInfo.InvariantCulture)),
        };
        var second = new List<CompendiumLine>
        {
            Choice(StringKeys.CompendiumSettingsHalf, journal.SecondHint == SecondHintRule.HalfFound, SecondHintAction + ((int)SecondHintRule.HalfFound).ToString(CultureInfo.InvariantCulture)),
            Choice(StringKeys.CompendiumSettingsNow, journal.SecondHint == SecondHintRule.RightAway, SecondHintAction + ((int)SecondHintRule.RightAway).ToString(CultureInfo.InvariantCulture)),
            Choice(StringKeys.CompendiumSettingsNever, journal.SecondHint == SecondHintRule.Never, SecondHintAction + ((int)SecondHintRule.Never).ToString(CultureInfo.InvariantCulture)),
        };
        var display = new List<CompendiumLine>
        {
            Toggle(StringKeys.CompendiumSettingsCounts, journal.ShowCounts, CountsAction),
            Toggle(StringKeys.CompendiumSettingsHud, journal.ShowTracked, HudAction),
            Toggle(StringKeys.CompendiumSettingsNear, journal.TellWhenNear, NearAction),
        };
        var sections = new List<CompendiumSection>
        {
            new CompendiumSection(Get(StringKeys.CompendiumSectionSpoilers), spoilers),
            new CompendiumSection(Get(StringKeys.CompendiumSectionSecondHint), second),
            new CompendiumSection(Get(StringKeys.CompendiumSectionDisplay), display),
        };
        return new CompendiumPage(SettingsPage, CompendiumKind.Settings, Get(StringKeys.CompendiumSettingsTitle), string.Empty, Get(StringKeys.CompendiumSettingsIntro), sections);
    }

    private static CompendiumLine Choice(string key, bool chosen, string action) =>
        chosen ? new CompendiumLine(CompendiumBuilder.Text(StringKeys.CompendiumSettingsChosen, Get(key))) : new CompendiumLine(Get(key), action);

    private static CompendiumLine Toggle(string key, bool on, string action) =>
        new CompendiumLine(CompendiumBuilder.Text(StringKeys.CompendiumSettingsToggle, Get(key), Get(on ? StringKeys.CompendiumSettingsOn : StringKeys.CompendiumSettingsOff)), action);
}
