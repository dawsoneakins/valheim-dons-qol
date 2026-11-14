namespace BaseStorage.Core.Compendium;

using System;
using System.Collections.Generic;
using System.Linq;
using BaseStorage.Core.Ui;

/// <summary>Something the player hasn't found yet, shown as a silhouette with hints built from the game's data.</summary>
public sealed class FrontierEntry
{
    /// <summary>Initializes a new instance of the <see cref="FrontierEntry"/> class.</summary>
    /// <param name="subjectId">The page id the subject will have once found.</param>
    /// <param name="kind">Item or creature.</param>
    /// <param name="biome">The land it's listed under.</param>
    /// <param name="name">Its name, shown only once revealed.</param>
    /// <param name="hints">Its hints, in the order they're given.</param>
    /// <param name="iconId">The page id whose icon is its silhouette.</param>
    /// <param name="ahead">True when it's in a land the player hasn't visited.</param>
    /// <param name="day">True when it can be found by day.</param>
    /// <param name="night">True when it can be found at night.</param>
    public FrontierEntry(string subjectId, CompendiumKind kind, CompendiumBiome biome, string name, IReadOnlyList<string> hints, string iconId, bool ahead, bool day, bool night)
    {
        SubjectId = subjectId;
        Id = CompendiumFrontier.PageOf(subjectId);
        Kind = kind;
        Biome = biome;
        Name = name;
        Hints = hints;
        IconId = iconId;
        Ahead = ahead;
        Day = day;
        Night = night;
    }

    /// <summary>Gets its own page id.</summary>
    public string Id { get; }

    /// <summary>Gets the page id the subject will have once found.</summary>
    public string SubjectId { get; }

    /// <summary>Gets whether it's an item or a creature.</summary>
    public CompendiumKind Kind { get; }

    /// <summary>Gets the land it's listed under.</summary>
    public CompendiumBiome Biome { get; }

    /// <summary>Gets its name, to be shown only once revealed.</summary>
    public string Name { get; }

    /// <summary>Gets its hints, in the order they're given.</summary>
    public IReadOnlyList<string> Hints { get; }

    /// <summary>Gets the page id whose icon is its silhouette.</summary>
    public string IconId { get; }

    /// <summary>Gets a value indicating whether it's in a land the player hasn't visited.</summary>
    public bool Ahead { get; }

    /// <summary>Gets a value indicating whether it can be found by day.</summary>
    public bool Day { get; }

    /// <summary>Gets a value indicating whether it can be found at night.</summary>
    public bool Night { get; }
}

/// <summary>How much of one land the player has found.</summary>
public sealed class BiomeProgress
{
    /// <summary>Initializes a new instance of the <see cref="BiomeProgress"/> class.</summary>
    /// <param name="biome">The land.</param>
    /// <param name="found">How many of its creatures and items have been found.</param>
    /// <param name="total">How many it has.</param>
    public BiomeProgress(CompendiumBiome biome, int found, int total)
    {
        Biome = biome;
        Found = found;
        Total = total;
    }

    /// <summary>Gets the land.</summary>
    public CompendiumBiome Biome { get; }

    /// <summary>Gets how many of its creatures and items have been found.</summary>
    public int Found { get; }

    /// <summary>Gets how many it has.</summary>
    public int Total { get; }

    /// <summary>Gets how many must be found before second hints open under <see cref="SecondHintRule.HalfFound"/>.</summary>
    public int Half => (Total + 1) / 2;
}

/// <summary>What's not yet found, and how much of each visited land is.</summary>
public sealed class FrontierResult
{
    private readonly Dictionary<CompendiumBiome, BiomeProgress> progress;

    /// <summary>Initializes a new instance of the <see cref="FrontierResult"/> class.</summary>
    /// <param name="entries">The silhouettes, by land in journey order.</param>
    /// <param name="progress">The visited lands' progress, in journey order.</param>
    /// <param name="hasUnvisited">True when some land hasn't been visited.</param>
    public FrontierResult(IReadOnlyList<FrontierEntry> entries, IReadOnlyList<BiomeProgress> progress, bool hasUnvisited)
    {
        Entries = entries;
        Progress = progress;
        HasUnvisited = hasUnvisited;
        this.progress = progress.ToDictionary(p => p.Biome);
    }

    /// <summary>Gets the silhouettes, by land in journey order.</summary>
    public IReadOnlyList<FrontierEntry> Entries { get; }

    /// <summary>Gets the visited lands' progress, in journey order.</summary>
    public IReadOnlyList<BiomeProgress> Progress { get; }

    /// <summary>Gets a value indicating whether some land hasn't been visited.</summary>
    public bool HasUnvisited { get; }

    /// <summary>Finds a land's progress.</summary>
    /// <param name="biome">The land.</param>
    /// <returns>The progress, or null for a land not visited.</returns>
    public BiomeProgress? ProgressOf(CompendiumBiome biome) => progress.TryGetValue(biome, out BiomeProgress? found) ? found : null;

    /// <summary>Finds a silhouette by its page id.</summary>
    /// <param name="id">The page id.</param>
    /// <returns>The silhouette, or null.</returns>
    public FrontierEntry? Find(string? id) => Entries.FirstOrDefault(e => e.Id == id);

    /// <summary>Tells whether a silhouette's second hint can be given.</summary>
    /// <param name="entry">The silhouette.</param>
    /// <param name="rule">The player's rule.</param>
    /// <returns>True when it can.</returns>
    public bool SecondHintOpen(FrontierEntry entry, SecondHintRule rule)
    {
        if (entry == null)
        {
            throw new ArgumentNullException(nameof(entry));
        }

        if (entry.Ahead || entry.Hints.Count < 2 || rule == SecondHintRule.Never)
        {
            return false;
        }

        if (rule == SecondHintRule.RightAway)
        {
            return true;
        }

        BiomeProgress? land = ProgressOf(entry.Biome);
        return land != null && land.Found >= land.Half;
    }
}

/// <summary>
/// Works out what the player hasn't found in the lands they've visited, and builds each one's hints from the game's data, so other mods'
/// creatures and items get hints too. Nothing in a land the player hasn't visited is named or hinted at. Pure.
/// </summary>
public static class CompendiumFrontier
{
    /// <summary>The prefix of a silhouette's page id.</summary>
    public const string Prefix = "frontier:";

    private static readonly CompendiumBiome[] Journey =
    {
        CompendiumBiome.Meadows, CompendiumBiome.BlackForest, CompendiumBiome.Swamp, CompendiumBiome.Mountain, CompendiumBiome.Plains,
        CompendiumBiome.Mistlands, CompendiumBiome.Ashlands, CompendiumBiome.DeepNorth, CompendiumBiome.Ocean,
    };

    /// <summary>Gets a silhouette's page id from its subject's page id.</summary>
    /// <param name="subjectId">The subject's page id.</param>
    /// <returns>The silhouette's page id.</returns>
    public static string PageOf(string subjectId) => Prefix + subjectId;

    /// <summary>Works out the silhouettes and each visited land's progress.</summary>
    /// <param name="data">The game's data.</param>
    /// <param name="knowledge">What the player has found.</param>
    /// <param name="spoilers">How much to show.</param>
    /// <returns>The result.</returns>
    public static FrontierResult Build(CompendiumData data, CompendiumKnowledge knowledge, SpoilerLevel spoilers)
    {
        if (data == null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        if (knowledge == null)
        {
            throw new ArgumentNullException(nameof(knowledge));
        }

        var items = data.Items.GroupBy(i => i.Id, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var creatures = data.Creatures.GroupBy(c => c.Id, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var lands = new Dictionary<CompendiumBiome, Land>();
        foreach (CompendiumBiome biome in Journey)
        {
            lands[biome] = new Land();
        }

        foreach (SpawnRecord spawn in data.Spawns)
        {
            if (lands.TryGetValue(spawn.Biome, out Land? land) && creatures.TryGetValue(spawn.CreatureId, out CreatureRecord? creature) && !creature.Boss)
            {
                land.Creatures.Add(spawn.CreatureId);
            }
        }

        foreach (ResourceRecord resource in data.Resources)
        {
            foreach (CompendiumBiome biome in resource.Biomes)
            {
                if (lands.TryGetValue(biome, out Land? land))
                {
                    land.Items.UnionWith(resource.ItemIds.Where(items.ContainsKey));
                }
            }
        }

        foreach (Land land in lands.Values)
        {
            foreach (string creatureId in land.Creatures.ToList())
            {
                land.Items.UnionWith(creatures[creatureId].Drops.Select(d => d.ItemId).Where(items.ContainsKey));
            }
        }

        var progress = new List<BiomeProgress>();
        var entries = new List<FrontierEntry>();
        var listed = new HashSet<string>(StringComparer.Ordinal);
        var context = new Context(data, knowledge, items, creatures);
        foreach (CompendiumBiome biome in Journey.Where(knowledge.Biomes.Contains))
        {
            Land land = lands[biome];
            int found = land.Creatures.Count(knowledge.Creatures.Contains) + land.Items.Count(knowledge.Items.Contains);
            progress.Add(new BiomeProgress(biome, found, land.Creatures.Count + land.Items.Count));
            if (spoilers != SpoilerLevel.Hidden)
            {
                AddEntries(entries, listed, land, biome, context, ahead: false);
            }
        }

        CompendiumBiome? next = Journey.Where(b => b != CompendiumBiome.Ocean && !knowledge.Biomes.Contains(b)).Select(b => (CompendiumBiome?)b).FirstOrDefault();
        if (spoilers == SpoilerLevel.LookAhead && next.HasValue)
        {
            AddEntries(entries, listed, lands[next.Value], next.Value, context, ahead: true);
        }

        return new FrontierResult(entries, progress, Journey.Any(b => !knowledge.Biomes.Contains(b)));
    }

    /// <summary>Rounds a health figure the way a hint states it: to 5 below 100, else to 10.</summary>
    /// <param name="health">The health.</param>
    /// <returns>The rounded figure.</returns>
    public static int About(float health)
    {
        int step = health < 100f ? 5 : 10;
        return Math.Max(step, (int)Math.Round(health / step, MidpointRounding.AwayFromZero) * step);
    }

    private static string LowerFirst(string text) => text.Length == 0 ? text : char.ToLowerInvariant(text[0]) + text.Substring(1);

    private static void AddEntries(List<FrontierEntry> entries, HashSet<string> listed, Land land, CompendiumBiome biome, Context context, bool ahead)
    {
        foreach (string id in land.Creatures.Where(c => !context.Knowledge.Creatures.Contains(c)).OrderBy(c => c, StringComparer.Ordinal))
        {
            string subject = CompendiumBuilder.CreaturePage(id);
            if (listed.Add(subject))
            {
                entries.Add(CreatureEntry(context.Creatures[id], biome, context, ahead));
            }
        }

        foreach (string id in land.Items.Where(i => !context.Knowledge.Items.Contains(i)).OrderBy(i => i, StringComparer.Ordinal))
        {
            string subject = CompendiumBuilder.ItemPage(id);
            if (listed.Add(subject))
            {
                entries.Add(ItemEntry(context.Items[id], biome, context, ahead));
            }
        }
    }

    private static FrontierEntry CreatureEntry(CreatureRecord creature, CompendiumBiome biome, Context context, bool ahead)
    {
        List<SpawnRecord> spawns = context.Data.Spawns.Where(s => s.CreatureId == creature.Id && s.Biome == biome).ToList();
        bool day = spawns.Count == 0 || spawns.Any(s => s.Day);
        bool night = spawns.Count == 0 || spawns.Any(s => s.Night);
        string? trophy = creature.Drops.Select(d => d.ItemId).FirstOrDefault(i => context.Items.TryGetValue(i, out ItemRecord? item) && item.Category == ItemCategory.Trophy);
        string icon = trophy != null ? CompendiumBuilder.ItemPage(trophy) : CompendiumBuilder.CreaturePage(creature.Id);
        IReadOnlyList<string> hints;
        if (ahead)
        {
            hints = new[] { StringTable.English.Get(StringKeys.CompendiumHintAhead) };
        }
        else
        {
            int kinds = creature.Drops.Select(d => d.ItemId).Distinct(StringComparer.Ordinal).Count(context.Items.ContainsKey);
            int known = creature.Drops.Select(d => d.ItemId).Distinct(StringComparer.Ordinal).Count(context.Knowledge.Items.Contains);
            hints = new[]
            {
                CompendiumBuilder.Text(StringKeys.CompendiumHintWhere, CompendiumBuilder.BiomeName(biome), LowerFirst(CompendiumBuilder.TimeName(day, night))),
                creature.Boss
                    ? CompendiumBuilder.Text(StringKeys.CompendiumHintBoss, CompendiumBuilder.Number(About(creature.Health)))
                    : CompendiumBuilder.Text(StringKeys.CompendiumHintCreature, CompendiumBuilder.Number(About(creature.Health)), CompendiumBuilder.Number(kinds), CompendiumBuilder.Number(known)),
            };
        }

        return new FrontierEntry(CompendiumBuilder.CreaturePage(creature.Id), CompendiumKind.Creature, biome, creature.Name, hints, icon, ahead, day, night);
    }

    private static FrontierEntry ItemEntry(ItemRecord item, CompendiumBiome biome, Context context, bool ahead)
    {
        IReadOnlyList<string> hints;
        if (ahead)
        {
            hints = new[] { StringTable.English.Get(StringKeys.CompendiumHintAhead) };
        }
        else
        {
            CreatureRecord? knownDropper = context.Data.Creatures.FirstOrDefault(c => context.Knowledge.Creatures.Contains(c.Id) && c.Drops.Any(d => d.ItemId == item.Id));
            ResourceRecord? source = context.Data.Resources.FirstOrDefault(r => r.Biomes.Contains(biome) && r.ItemIds.Contains(item.Id));
            CreatureRecord? unknownDropper = context.Data.Creatures.FirstOrDefault(c => c.Drops.Any(d => d.ItemId == item.Id));
            string land = CompendiumBuilder.BiomeName(biome);
            string first = knownDropper != null
                ? CompendiumBuilder.Text(StringKeys.CompendiumHintDroppedKnown, knownDropper.Name)
                : source != null
                    ? CompendiumBuilder.Text(StringKeys.CompendiumHintGathered, land)
                    : CompendiumBuilder.Text(StringKeys.CompendiumHintDroppedUnknown, land);
            int uses = context.Data.Recipes.Count(r => r.Ingredients.Any(i => i.ItemId == item.Id));
            string category = CompendiumBuilder.CategoryName(item.Category);
            string second = source != null && knownDropper == null
                ? CompendiumBuilder.Text(StringKeys.CompendiumHintFrom, source.Name)
                : knownDropper == null && unknownDropper != null
                    ? CompendiumBuilder.Text(StringKeys.CompendiumHintDropperHealth, CompendiumBuilder.Number(About(unknownDropper.Health)))
                    : uses > 0
                        ? CompendiumBuilder.Text(StringKeys.CompendiumHintUses, category, CompendiumBuilder.Number(uses))
                        : CompendiumBuilder.Text(StringKeys.CompendiumHintNoUses, category);
            hints = new[] { first, second };
        }

        return new FrontierEntry(CompendiumBuilder.ItemPage(item.Id), CompendiumKind.Item, biome, item.Name, hints, CompendiumBuilder.ItemPage(item.Id), ahead, day: true, night: true);
    }

    private sealed class Land
    {
        internal HashSet<string> Creatures { get; } = new HashSet<string>(StringComparer.Ordinal);

        internal HashSet<string> Items { get; } = new HashSet<string>(StringComparer.Ordinal);
    }

    private sealed class Context
    {
        internal Context(CompendiumData data, CompendiumKnowledge knowledge, Dictionary<string, ItemRecord> items, Dictionary<string, CreatureRecord> creatures)
        {
            Data = data;
            Knowledge = knowledge;
            Items = items;
            Creatures = creatures;
        }

        internal CompendiumData Data { get; }

        internal CompendiumKnowledge Knowledge { get; }

        internal Dictionary<string, ItemRecord> Items { get; }

        internal Dictionary<string, CreatureRecord> Creatures { get; }
    }
}
