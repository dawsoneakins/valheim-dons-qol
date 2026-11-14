namespace BaseStorage.Plugin.Compendium;

using System;
using System.Collections.Generic;
using System.Text;
using BaseStorage.Core.Compendium;
using BaseStorage.Core.Ui;
using BaseStorage.Plugin.Ui;
using TMPro;
using UnityEngine;

/// <summary>
/// Shows the tracked hints below the minimap while playing. A hint whose land and time of day match where the player is gets marked, and
/// the player is told once when they arrive. Finding a tracked thing says so and stops tracking it. Checks once a second, and rebuilds
/// the silhouettes every few seconds. Main thread only.
/// </summary>
internal static class CompendiumTracker
{
    private const float CheckSeconds = 1f;
    private const float RebuildSeconds = 5f;
    private const float Width = 380f;
    private const float Height = 220f;

    private static readonly HashSet<string> WasHere = new HashSet<string>(StringComparer.Ordinal);
    private static readonly List<string> Done = new List<string>();

    private static float nextCheck;
    private static float nextRebuild;
    private static FrontierResult? frontier;
    private static CompendiumKnowledge? knowledge;
    private static TMP_Text? label;
    private static Hud? builtFor;

    /// <summary>Updates the list when it's time.</summary>
    internal static void Tick()
    {
        if (Time.unscaledTime < nextCheck)
        {
            return;
        }

        nextCheck = Time.unscaledTime + CheckSeconds;
        Player? player = Player.m_localPlayer;
        CompendiumJournal? journal = player == null ? null : CompendiumJournalStore.For(player);
        if (player == null || journal == null || journal.Tracked.Count == 0 || (!journal.ShowTracked && !journal.TellWhenNear))
        {
            Hide();
            return;
        }

        if (!CompendiumWindow.TryData(out GameCompendiumSource? source, out CompendiumData? data))
        {
            Hide();
            return;
        }

        if (frontier == null || knowledge == null || Time.unscaledTime >= nextRebuild)
        {
            knowledge = source!.ReadKnowledge(player);
            frontier = CompendiumFrontier.Build(data!, knowledge, journal.Spoilers == SpoilerLevel.Hidden ? SpoilerLevel.Hints : journal.Spoilers);
            nextRebuild = Time.unscaledTime + RebuildSeconds;
        }

        bool night = EnvMan.instance != null && EnvMan.IsNight();
        bool mapped = GameCompendiumSource.TryMapBiome(player.GetCurrentBiome(), out CompendiumBiome here);
        var text = new StringBuilder(256);
        text.Append("<color=#FFC44D>").Append(StringTable.English.Get(StringKeys.CompendiumHudTitle)).Append("</color>");
        Done.Clear();
        foreach (string id in journal.Tracked)
        {
            FrontierEntry? entry = frontier.Find(id);
            if (entry == null)
            {
                Done.Add(id);
                continue;
            }

            string title = journal.IsRevealed(id) ? entry.Name : entry.Hints[0];
            bool rightPlace = mapped && here == entry.Biome && (night ? entry.Night : entry.Day);
            text.Append('\n').Append(rightPlace ? StringTable.English.Format(StringKeys.CompendiumHudHere, title) : title);
            if (rightPlace && WasHere.Add(id) && journal.TellWhenNear)
            {
                Notices.Show(NoticePlacement.TopLeft, StringKeys.CompendiumHudNear, title);
            }
            else if (!rightPlace)
            {
                WasHere.Remove(id);
            }
        }

        bool changed = false;
        foreach (string id in Done)
        {
            if (Found(id, knowledge, data!, out string name))
            {
                Notices.Show(NoticePlacement.Center, StringKeys.CompendiumHudFound, name);
                changed |= journal.Untrack(id);
            }
        }

        if (changed)
        {
            CompendiumJournalStore.Save(player);
        }

        if (!journal.ShowTracked || journal.Tracked.Count == 0)
        {
            Hide();
            return;
        }

        Show(text.ToString());
    }

    private static bool Found(string id, CompendiumKnowledge knowledge, CompendiumData data, out string name)
    {
        name = string.Empty;
        string subject = id.StartsWith(CompendiumFrontier.Prefix, StringComparison.Ordinal) ? id.Substring(CompendiumFrontier.Prefix.Length) : id;
        string item = CompendiumBuilder.ItemPage(string.Empty);
        string creature = CompendiumBuilder.CreaturePage(string.Empty);
        if (subject.StartsWith(item, StringComparison.Ordinal) && knowledge.Items.Contains(subject.Substring(item.Length)))
        {
            name = data.Items.Find(i => i.Id == subject.Substring(item.Length))?.Name ?? string.Empty;
            return true;
        }

        if (subject.StartsWith(creature, StringComparison.Ordinal) && knowledge.Creatures.Contains(subject.Substring(creature.Length)))
        {
            name = data.Creatures.Find(c => c.Id == subject.Substring(creature.Length))?.Name ?? string.Empty;
            return true;
        }

        return false;
    }

    private static void Show(string text)
    {
        Hud? hud = Hud.instance;
        TMP_Text? template = InventoryGui.instance == null ? null : InventoryGui.instance.m_recipeDecription;
        if (hud == null || template == null)
        {
            return;
        }

        if (label == null || builtFor != hud)
        {
            GameObject node = VanillaButtonFactory.CreateNode(hud.m_rootObject.transform, "compendium.tracker", new Vector2(Width, Height), Vector2.zero, typeof(RectTransform), typeof(TextMeshProUGUI));
            var rect = (RectTransform)node.transform;
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-24f, -300f);
            label = node.GetComponent<TMP_Text>();
            label.font = template.font;
            label.fontSize = 18f;
            label.color = Color.white;
            label.richText = true;
            label.raycastTarget = false;
            label.alignment = TextAlignmentOptions.TopRight;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.outlineWidth = 0.2f;
            label.outlineColor = Color.black;
            builtFor = hud;
        }

        label.text = text;
        label.gameObject.SetActive(true);
    }

    private static void Hide()
    {
        if (label != null)
        {
            label.gameObject.SetActive(false);
        }
    }
}
