namespace BaseStorage.Plugin.Compendium;

using System;
using System.Collections.Generic;
using System.Text;
using BaseStorage.Core.Compendium;
using BaseStorage.Core.Ui;
using BaseStorage.Plugin.BaseMenu;
using BaseStorage.Plugin.Feeding;
using BaseStorage.Plugin.Patching;
using BaseStorage.Plugin.Ui;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The compendium's window. It is a child of the inventory screen, which already blocks movement and shows the cursor. The list reuses a
/// screen's worth of rows while scrolling. The book is built on the first open in each world. Main thread only.
/// </summary>
internal static class CompendiumWindow
{
    /// <summary>The patch key for the window's tick.</summary>
    internal const string TickKey = "BaseStorage.Plugin.Compendium.CompendiumWindow.Tick";

    private const string ClickKey = "BaseStorage.Plugin.Compendium.CompendiumWindow.Click";
    private const string TabHomeId = "compendium.tab.home";
    private const string TabFrontierId = "compendium.tab.frontier";
    private const string TabSettingsId = "compendium.tab.settings";
    private const string TabItemsId = "compendium.tab.items";
    private const string TabCreaturesId = "compendium.tab.creatures";
    private const string TabBiomesId = "compendium.tab.biomes";
    private const string TabPiecesId = "compendium.tab.pieces";
    private const string BackId = "compendium.back";
    private const string CloseId = "compendium.close";
    private const string RowId = "compendium.row.";
    private const float MaxWidth = 1180f;
    private const float MaxHeight = 760f;
    private const float ScreenMargin = 30f;
    private const float Margin = 12f;
    private const float BarHeight = 38f;
    private const float ListWidth = 320f;
    private const float RowHeight = 34f;
    private const float IconSize = 176f;
    private const float IconPad = 8f;
    private const float IconFrameSize = IconSize + (2f * IconPad);
    private const float TabWidth = 116f;
    private const float PageFontSize = 20f;

    private static readonly List<RowView> Rows = new List<RowView>();
    private static readonly Stack<string> History = new Stack<string>();
    private static readonly Vector3[] Corners = new Vector3[4];
    private static readonly List<Container> Holding = new List<Container>();
    private static readonly Color SilhouetteColor = new Color(0.02f, 0.02f, 0.03f, 1f);
    private static readonly Color RuneColor = new Color(0.55f, 0.77f, 0.85f, 0.9f);

    private static CompendiumBook? book;
    private static GameCompendiumSource? source;
    private static CompendiumData? data;
    private static IReadOnlyDictionary<string, Sprite>? icons;
    private static ZNetScene? bookWorld;
    private static RectTransform? root;
    private static ScrollRect? listScroll;
    private static RectTransform? listContent;
    private static ScrollRect? pageScroll;
    private static RectTransform? pageContent;
    private static TMP_Text? pageText;
    private static TMP_Text? listEmpty;
    private static Image? icon;
    private static Image? iconFrame;
    private static Outline? iconGlow;
    private static CompendiumKnowledge? knowledge;
    private static CompendiumStock stock = CompendiumStock.None;
    private static string? confirming;
    private static TMP_InputField? search;
    private static IReadOnlyList<CompendiumPage> results = Array.Empty<CompendiumPage>();
    private static CompendiumKind kind = CompendiumKind.Home;
    private static string? current;
    private static float listHeight;
    private static float pageHeight;

    /// <summary>Gets a value indicating whether the window is open.</summary>
    internal static bool IsOpen => root != null && root.gameObject.activeSelf;

    /// <summary>Gets a value indicating whether the search box has the keyboard, so typing can't close the inventory.</summary>
    internal static bool TypingBlocksClose =>
        IsOpen
        && search != null
        && search.isFocused
        && Player.m_localPlayer != null
        && !Player.m_localPlayer.IsDead()
        && !Player.m_localPlayer.IsTeleporting();

    /// <summary>Opens the window, building the book if this world has none yet.</summary>
    internal static void Open()
    {
        InventoryGui? gui = InventoryGui.instance;
        if (gui == null || ObjectDB.instance == null || ZNetScene.instance == null)
        {
            return;
        }

        Player? player = Player.m_localPlayer;
        if (player == null)
        {
            return;
        }

        try
        {
            // The game's data is read once per world. The book is rebuilt on every open, because the player may have discovered more.
            EnsureData();
            knowledge = source!.ReadKnowledge(player);
            stock = ReadStock(player);
            confirming = null;
            Rebuild(player);
        }
        catch (Exception)
        {
            Notices.Show(NoticePlacement.TopLeft, StringKeys.CompendiumFailed);
            throw;
        }

        if (root == null)
        {
            Build(gui);
        }

        root!.gameObject.SetActive(true);
        root.SetAsLastSibling();
        Place(gui);
        RefreshList();
        if (current == null || book?.Find(current) == null)
        {
            ShowTab(kind);
        }
        else
        {
            Render(current);
        }
    }

    /// <summary>Gets the game's data for this world, reading it the first time.</summary>
    /// <param name="reader">The reader, which also reads the player's knowledge.</param>
    /// <param name="read">The data.</param>
    /// <returns>False when the world isn't ready.</returns>
    internal static bool TryData(out GameCompendiumSource? reader, out CompendiumData? read)
    {
        reader = null;
        read = null;
        if (ObjectDB.instance == null || ZNetScene.instance == null)
        {
            return false;
        }

        EnsureData();
        reader = source;
        read = data;
        return reader != null && read != null;
    }

    private static void EnsureData()
    {
        if (data == null || source == null || bookWorld != ZNetScene.instance)
        {
            source = new GameCompendiumSource();
            data = source.Read();
            icons = source.Icons;
            bookWorld = ZNetScene.instance;
        }
    }

    private static CompendiumStock ReadStock(Player player)
    {
        var inBase = new Dictionary<string, int>(StringComparer.Ordinal);
        var result = new CompendiumStock(StationFeeder.CountBase(player.transform.position, inBase));
        foreach (KeyValuePair<string, int> pair in inBase)
        {
            result.Base[pair.Key] = pair.Value;
        }

        foreach (ItemDrop.ItemData item in player.GetInventory().GetAllItems())
        {
            if (item.m_dropPrefab != null)
            {
                result.Carried.TryGetValue(item.m_dropPrefab.name, out int count);
                result.Carried[item.m_dropPrefab.name] = count + item.m_stack;
            }
        }

        return result;
    }

    /// <summary>Builds the book from what's known now. On the journal's first use, everything already known counts as read.</summary>
    private static void Rebuild(Player player)
    {
        CompendiumJournal journal = CompendiumJournalStore.For(player);
        book = CompendiumJourney.Build(data!, knowledge!, journal, stock, confirming);
        var discovered = new List<string>();
        foreach (CompendiumPage page in book.Pages)
        {
            if (CompendiumJourney.IsDiscovery(page))
            {
                discovered.Add(page.Id);
            }
        }

        if (journal.Start(discovered))
        {
            CompendiumJournalStore.Save(player);
        }
    }

    /// <summary>Notes what the player is meeting, and closes the window when the inventory has closed.</summary>
    internal static void Tick() => PatchSafety.Run(TickKey, static () =>
    {
        CompendiumSightingTracker.Tick();
        CompendiumTracker.Tick();
        if (IsOpen && !InventoryGui.IsVisible())
        {
            Close();
        }
    });

    /// <summary>Shows a page, remembering the one being left.</summary>
    /// <param name="id">The page's id.</param>
    internal static void Show(string id)
    {
        if (id.StartsWith(CompendiumJourney.ActionPrefix, StringComparison.Ordinal))
        {
            Act(id);
            return;
        }

        if (book?.Find(id) == null)
        {
            return;
        }

        if (current != null && current != id)
        {
            History.Push(current);
        }

        Render(id);
    }

    /// <summary>Carries out a link that is an action: a hint, a reveal, tracking, a setting, or highlighting chests.</summary>
    private static void Act(string action)
    {
        Player? player = Player.m_localPlayer;
        if (player == null || data == null || knowledge == null)
        {
            return;
        }

        if (action.StartsWith(CompendiumJourney.HighlightAction, StringComparison.Ordinal))
        {
            // The glow is on the chests in the world, so the inventory closes to show it.
            StationFeeder.FindHolding(player.transform.position, action.Substring(CompendiumJourney.HighlightAction.Length), Holding);
            if (Holding.Count > 0)
            {
                ChestHighlight.ShowAll(Holding);
                InventoryGui.instance?.Hide();
            }

            return;
        }

        CompendiumJournal journal = CompendiumJournalStore.For(player);
        if (CompendiumJourney.Apply(journal, action, out string? confirm))
        {
            CompendiumJournalStore.Save(player);
        }

        confirming = confirm;
        Rebuild(player);
        RefreshList();
        if (current != null && book?.Find(current) != null)
        {
            Render(current);
        }
        else
        {
            ShowTab(CompendiumKind.Home);
        }
    }

    private static void ShowTab(CompendiumKind tab)
    {
        kind = tab;
        History.Clear();
        current = null;
        RefreshList();
        string? page = tab switch
        {
            CompendiumKind.Home => CompendiumJourney.HomePage,
            CompendiumKind.Frontier => CompendiumJourney.FrontierPage,
            CompendiumKind.Settings => CompendiumJourney.SettingsPage,
            _ => null,
        };
        if (page != null)
        {
            Render(page);
        }
    }

    private static void Close()
    {
        if (search != null)
        {
            search.DeactivateInputField();
        }

        root!.gameObject.SetActive(false);
    }

    private static void Build(InventoryGui gui)
    {
        GameObject node = VanillaButtonFactory.CreateNode(gui.m_inventoryRoot, "compendium.window", new Vector2(MaxWidth, MaxHeight), Vector2.zero, typeof(RectTransform), typeof(Image));
        PanelStyle.Wood(node.GetComponent<Image>(), gui);
        root = (RectTransform)node.transform;
        Rows.Clear();
        History.Clear();
        current = null;
        CreateBar(gui);
    }

    private static void Place(InventoryGui gui)
    {
        var parent = (RectTransform)gui.m_inventoryRoot;
        Canvas canvas = parent.GetComponentInParent<Canvas>().rootCanvas;
        var screen = (RectTransform)canvas.transform;
        Vector2 size = new Vector2(Mathf.Min(MaxWidth, screen.rect.width - ScreenMargin), Mathf.Min(MaxHeight, screen.rect.height - ScreenMargin));
        root!.sizeDelta = size;
        screen.GetWorldCorners(Corners);
        Vector3 center = parent.InverseTransformPoint((Corners[0] + Corners[2]) * 0.5f);
        Rect bounds = parent.rect;
        root.anchoredPosition = new Vector2(center.x - bounds.xMin - (size.x * 0.5f), center.y - bounds.yMax + (size.y * 0.5f));
        if (listScroll == null)
        {
            CreatePanes(gui, size);
        }
    }

    private static void CreateBar(InventoryGui gui)
    {
        Pin(VanillaButtonFactory.Create(gui.m_stackAllButton, root!, TabHomeId, StringKeys.CompendiumTabHome, static () => SelectKind(CompendiumKind.Home)), BarX(0));
        Pin(VanillaButtonFactory.Create(gui.m_stackAllButton, root!, TabItemsId, StringKeys.CompendiumTabItems, static () => SelectKind(CompendiumKind.Item)), BarX(1));
        Pin(VanillaButtonFactory.Create(gui.m_stackAllButton, root!, TabCreaturesId, StringKeys.CompendiumTabCreatures, static () => SelectKind(CompendiumKind.Creature)), BarX(2));
        Pin(VanillaButtonFactory.Create(gui.m_stackAllButton, root!, TabBiomesId, StringKeys.CompendiumTabBiomes, static () => SelectKind(CompendiumKind.Biome)), BarX(3));
        Pin(VanillaButtonFactory.Create(gui.m_stackAllButton, root!, TabPiecesId, StringKeys.CompendiumTabPieces, static () => SelectKind(CompendiumKind.Piece)), BarX(4));
        Pin(VanillaButtonFactory.Create(gui.m_stackAllButton, root!, TabFrontierId, StringKeys.CompendiumTabFrontier, static () => SelectKind(CompendiumKind.Frontier)), BarX(5));
        Pin(VanillaButtonFactory.Create(gui.m_stackAllButton, root!, TabSettingsId, StringKeys.CompendiumTabSettings, static () => SelectKind(CompendiumKind.Settings)), BarX(6));
        Pin(VanillaButtonFactory.Create(gui.m_stackAllButton, root!, BackId, StringKeys.CompendiumBack, static () => PatchSafety.Run(ClickKey, GoBack)), BarX(7));
    }

    private static float BarX(int index) => Margin + (index * (TabWidth + Margin));

    private static void Pin(Button? button, float x)
    {
        if (button != null)
        {
            VanillaButtonFactory.Pin((RectTransform)button.transform, new Vector2(TabWidth, BarHeight), new Vector2(x, -Margin));
        }
    }

    private static void CreatePanes(InventoryGui gui, Vector2 size)
    {
        Pin(VanillaButtonFactory.Create(gui.m_stackAllButton, root!, CloseId, StringKeys.CompendiumClose, static () => PatchSafety.Run(ClickKey, Close)), size.x - Margin - TabWidth);

        float top = -(2f * Margin) - BarHeight;
        CreateSearch(gui, new Vector2(Margin, top));
        float listTop = top - BarHeight - Margin;
        listHeight = size.y + listTop - Margin;
        listContent = CreateScroll(root!, "compendium.list", new Vector2(ListWidth, listHeight), new Vector2(Margin, listTop), out listScroll);
        listScroll.onValueChanged.AddListener(static _ => PatchSafety.Run(ClickKey, LayOutRows));
        listEmpty = Label(gui, listScroll.viewport, "compendium.list.empty", new Vector2(ListWidth, RowHeight), Vector2.zero, PageFontSize);
        if (listEmpty != null)
        {
            listEmpty.text = StringTable.English.Get(StringKeys.CompendiumEmpty);
        }

        int count = Mathf.CeilToInt(listHeight / RowHeight) + 2;
        for (int i = 0; i < count; i++)
        {
            CreateRow(gui, i);
        }

        float pageX = (2f * Margin) + ListWidth;
        float pageWidth = size.x - pageX - Margin;
        pageHeight = size.y + top - Margin;
        pageContent = CreateScroll(root!, "compendium.page", new Vector2(pageWidth, pageHeight), new Vector2(pageX, top), out pageScroll);
        pageScroll.GetComponent<Image>().color = PanelStyle.Reading;
        pageText = Label(gui, pageContent, "compendium.page.text", new Vector2(pageWidth - IconFrameSize - (3f * Margin), pageHeight), new Vector2(Margin, -Margin), PageFontSize);
        if (pageText != null)
        {
            pageText.richText = true;
            pageText.raycastTarget = true;
            pageText.textWrappingMode = TextWrappingModes.Normal;
            pageText.alignment = TextAlignmentOptions.TopLeft;
            pageText.gameObject.AddComponent<CompendiumLinkClicks>();
            pageText.text = StringTable.English.Get(StringKeys.CompendiumPick);
        }

        GameObject frameNode = VanillaButtonFactory.CreateNode(root!, "compendium.icon.frame", new Vector2(IconFrameSize, IconFrameSize), new Vector2(size.x - Margin - IconFrameSize - Margin, top - Margin), typeof(RectTransform), typeof(Image));
        iconFrame = frameNode.GetComponent<Image>();
        iconFrame.color = PanelStyle.Inset;
        iconFrame.raycastTarget = false;
        iconFrame.enabled = false;
        GameObject iconNode = VanillaButtonFactory.CreateNode(frameNode.transform, "compendium.icon", new Vector2(IconSize, IconSize), new Vector2(IconPad, -IconPad), typeof(RectTransform), typeof(Image));
        icon = iconNode.GetComponent<Image>();
        iconGlow = iconNode.AddComponent<Outline>();
        iconGlow.effectColor = RuneColor;
        iconGlow.effectDistance = new Vector2(2f, -2f);
        iconGlow.enabled = false;
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        icon.enabled = false;
    }

    private static void CreateRow(InventoryGui gui, int index)
    {
        var view = new RowView();
        Button? button = VanillaButtonFactory.Create(gui.m_stackAllButton, listContent!, RowId + index.ToString(System.Globalization.CultureInfo.InvariantCulture), StringKeys.CompendiumEmpty, () => PatchSafety.Run(ClickKey, () =>
        {
            if (view.PageId != null)
            {
                History.Clear();
                current = null;
                Show(view.PageId);
            }
        }));
        if (button != null)
        {
            view.Button = button;
            Rows.Add(view);
        }
    }

    private static void CreateSearch(InventoryGui gui, Vector2 position)
    {
        GameObject box = VanillaButtonFactory.CreateNode(root!, "compendium.search", new Vector2(ListWidth, BarHeight), position, typeof(RectTransform), typeof(Image));
        box.SetActive(false);
        box.GetComponent<Image>().color = PanelStyle.Field;
        GameObject areaNode = VanillaButtonFactory.CreateNode(box.transform, "compendium.search.area", Vector2.zero, Vector2.zero, typeof(RectTransform), typeof(RectMask2D));
        var area = (RectTransform)areaNode.transform;
        Stretch(area, Margin);
        TMP_Text? text = Label(gui, area, "compendium.search.text", Vector2.zero, Vector2.zero, PageFontSize);
        TMP_Text? hint = Label(gui, area, "compendium.search.hint", Vector2.zero, Vector2.zero, PageFontSize);
        if (text == null || hint == null)
        {
            box.SetActive(true);
            return;
        }

        Stretch((RectTransform)text.transform, 0f);
        Stretch((RectTransform)hint.transform, 0f);
        text.richText = false;
        hint.text = StringTable.English.Get(StringKeys.CompendiumSearch);
        hint.fontStyle = FontStyles.Italic;
        hint.alpha = 0.6f;
        TMP_InputField input = box.AddComponent<TMP_InputField>();
        input.transition = Selectable.Transition.None;
        input.textViewport = area;
        input.textComponent = text;
        input.placeholder = hint;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.onValueChanged.AddListener(static _ => PatchSafety.Run(ClickKey, RefreshList));
        search = input;
        box.SetActive(true);
    }

    private static void Stretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(inset, 0f);
        rect.offsetMax = new Vector2(-inset, 0f);
    }

    private static RectTransform CreateScroll(Transform parent, string id, Vector2 size, Vector2 position, out ScrollRect scroll)
    {
        GameObject node = VanillaButtonFactory.CreateNode(parent, id, size, position, typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        node.GetComponent<Image>().color = PanelStyle.Inset;
        GameObject content = VanillaButtonFactory.CreateNode(node.transform, id + ".content", size, Vector2.zero, typeof(RectTransform));
        scroll = node.GetComponent<ScrollRect>();
        scroll.viewport = (RectTransform)node.transform;
        scroll.content = (RectTransform)content.transform;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.inertia = false;
        scroll.scrollSensitivity = PanelStyle.ScrollSpeed;
        return scroll.content;
    }

    private static TMP_Text? Label(InventoryGui gui, Transform parent, string id, Vector2 size, Vector2 position, float fontSize)
    {
        TMP_Text? template = gui.m_recipeDecription;
        if (template == null)
        {
            return null;
        }

        GameObject node = VanillaButtonFactory.CreateNode(parent, id, size, position, typeof(RectTransform), typeof(TextMeshProUGUI));
        TMP_Text text = node.GetComponent<TMP_Text>();
        text.font = template.font;
        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.richText = false;
        text.raycastTarget = false;
        return text;
    }

    private static void SelectKind(CompendiumKind selected) => PatchSafety.Run(ClickKey, selected, static value => ShowTab(value));

    private static void RefreshList()
    {
        if (book == null || listContent == null)
        {
            return;
        }

        results = book.Search(kind, search == null ? string.Empty : search.text);
        listContent.sizeDelta = new Vector2(listContent.sizeDelta.x, Mathf.Max(listHeight, results.Count * RowHeight));
        listContent.anchoredPosition = Vector2.zero;
        if (listEmpty != null)
        {
            listEmpty.gameObject.SetActive(results.Count == 0);
        }

        LayOutRows();
    }

    /// <summary>Puts the pooled rows where the scroll position shows them.</summary>
    private static void LayOutRows()
    {
        if (listContent == null)
        {
            return;
        }

        int first = Mathf.Max(0, Mathf.FloorToInt(listContent.anchoredPosition.y / RowHeight));
        for (int i = 0; i < Rows.Count; i++)
        {
            RowView row = Rows[i];
            int index = first + i;
            bool used = index < results.Count;
            row.Button!.gameObject.SetActive(used);
            row.PageId = used ? results[index].Id : null;
            if (used)
            {
                VanillaButtonFactory.Pin((RectTransform)row.Button.transform, new Vector2(ListWidth - 8f, RowHeight - 2f), new Vector2(4f, -index * RowHeight));
                VanillaButtonFactory.SetLabel(row.Button, StringKeys.CompendiumRow, results[index].Title);
            }
        }
    }

    private static void GoBack()
    {
        if (History.Count > 0)
        {
            Render(History.Pop());
        }
    }

    private static void Render(string id)
    {
        CompendiumPage? page = book?.Find(id);
        if (page == null || pageText == null || pageContent == null)
        {
            return;
        }

        current = id;
        Player? player = Player.m_localPlayer;
        if (player != null && CompendiumJourney.IsDiscovery(page) && CompendiumJournalStore.For(player).MarkRead(id))
        {
            CompendiumJournalStore.Save(player);
        }

        pageText.text = PageMarkup(page);
        pageText.ForceMeshUpdate();
        var textRect = (RectTransform)pageText.transform;
        float height = pageText.preferredHeight + (2f * Margin);
        textRect.sizeDelta = new Vector2(textRect.sizeDelta.x, height);
        pageContent.sizeDelta = new Vector2(pageContent.sizeDelta.x, Mathf.Max(pageHeight, height));
        pageContent.anchoredPosition = Vector2.zero;
        if (icon != null)
        {
            // A silhouette is its subject's icon in black, with a faint outline so it reads on the dark frame.
            bool silhouette = page.Kind == CompendiumKind.Frontier;
            bool named = silhouette && player != null && CompendiumJournalStore.For(player).IsRevealed(id);
            string iconId = silhouette ? id.Substring(CompendiumFrontier.Prefix.Length) : id;
            Sprite? sprite = null;
            icons?.TryGetValue(iconId, out sprite);
            icon.sprite = sprite;
            icon.color = silhouette && !named ? SilhouetteColor : Color.white;
            icon.enabled = sprite != null;
            iconGlow!.enabled = silhouette && !named && sprite != null;
            iconFrame!.enabled = sprite != null;
        }
    }

    private static string PageMarkup(CompendiumPage page)
    {
        var text = new StringBuilder(512);
        text.Append("<size=32><color=#FFC44D>").Append(Escape(page.Title)).Append("</color></size>\n");
        if (page.Group.Length > 0)
        {
            text.Append("<i><color=#BBBBBB>").Append(Escape(page.Group)).Append("</color></i>\n");
        }

        if (page.Description.Length > 0)
        {
            text.Append('\n').Append(page.Description).Append('\n');
        }

        for (int i = 0; i < page.Sections.Count; i++)
        {
            CompendiumSection section = page.Sections[i];
            text.Append("\n<b><color=#FFC44D>").Append(Escape(section.Heading)).Append("</color></b>\n");
            for (int j = 0; j < section.Lines.Count; j++)
            {
                CompendiumLine line = section.Lines[j];
                if (line.Link != null)
                {
                    text.Append("<link=\"").Append(Escape(line.Link)).Append("\"><color=#7FC8FF><u>").Append(Escape(line.Text)).Append("</u></color></link>\n");
                }
                else
                {
                    text.Append(Escape(line.Text)).Append('\n');
                }
            }
        }

        return text.ToString();
    }

    /// <summary>Stops a name from another mod being read as markup.</summary>
    private static string Escape(string value) => value.Replace("<", "<noparse><</noparse>");

    private sealed class RowView
    {
        internal Button? Button { get; set; }

        internal string? PageId { get; set; }
    }
}

/// <summary>Follows a clicked link in the page's text.</summary>
internal sealed class CompendiumLinkClicks : MonoBehaviour, IPointerClickHandler
{
    private const string ClickKey = "BaseStorage.Plugin.Compendium.CompendiumLinkClicks.Click";

    /// <inheritdoc />
    public void OnPointerClick(PointerEventData eventData) => PatchSafety.Run(ClickKey, () =>
    {
        TMP_Text text = GetComponent<TMP_Text>();
        int link = TMP_TextUtilities.FindIntersectingLink(text, eventData.position, eventData.pressEventCamera);
        if (link >= 0)
        {
            CompendiumWindow.Show(text.textInfo.linkInfo[link].GetLinkID());
        }
    });
}
