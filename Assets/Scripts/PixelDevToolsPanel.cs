// ---- System notes ----
// PixelDevToolsPanel.cs
//   Part of PixelDevTools (partial class): building the Main tab widget board (BuildPanel and its card steps, the Resource Tools card, Widget) and the world tools behind its buttons. The system notes are in PixelDevTools.cs.
// ---------------------------------------------------
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public partial class PixelDevTools
{
    private void BuildPanel()
    {
        // The Main tab of the Dev Hub (PixelDevHub): everything below lives in its content root.
        PixelDevHub.Boot();
        canvasRoot = PixelDevHub.CanvasObject;
        RectTransform root = PixelDevHub.TabRoot("main");
        BuildTipBox();

        float y = 8f;
        float inner = PixelDevHub.ContentW - 100f; // usable width of a row (40 px left, 60 right: room for the scroll bar)

        GameObject tools = new GameObject("Tools View", typeof(RectTransform));
        tools.transform.SetParent(root, false);
        PixelUIKit.Stretch(tools.GetComponent<RectTransform>());

        // ONE widget board: every widget (add pixels, spawned pixel, minigames, middle click and the tools below) is a card in the same scrolling list. Drag a card by its header to
        // move it; HOLD the right button on a card to make it a FAVORITE (it snaps to the top and is tinted gold; hold again to let it go back).
        ScrollRect toolsScroll = PixelUIKit.CreateScrollView(tools.transform, "Tools Scroll", new Color(0.5f, 0.5f, 0.6f, 1f), 16f, 60f,
                                                             out RectTransform content, out GameObject toolsBar);
        RectTransform scrollRect = toolsScroll.GetComponent<RectTransform>();
        scrollRect.anchorMin = Vector2.zero;
        scrollRect.anchorMax = Vector2.one;
        scrollRect.offsetMin = new Vector2(0f, 8f);
        scrollRect.offsetMax = new Vector2(0f, -52f);   // (room above for the collapse-all bar)
        y = 0f; // rows are placed from the top of the scrolling content (each one becomes a draggable widget card)
        PixelCardBoard board = new PixelCardBoard(content, new PixelCardBoard.Style { sideGrip = true, foldColor = new Color(0.3f, 0.36f, 0.5f, 0.9f) },
                                                  height => PixelUIKit.UpdateScrollView(toolsScroll, toolsBar, height, PixelDevHub.ContentH - 16f), toolsScroll.viewport)
        {
            PrefKey = PrefPrefix + "WidgetOrder2",   // (the board saves the order, folded cards and favorites itself)
            Favorites = true,                        // (hold the right button on a card)
            SearchTab = "main",                      // (its cards show up in the hub's search bar)
        };

        // 'Collapse all' above the widgets: folds every card up to its header (and back: it reads 'Expand all' while everything is folded).
        Button collapse = PixelUIKit.CreateButton(font, tools.transform, "Collapse All Button", "Collapse all", new Vector2(220f, 36f), new Color(0.3f, 0.36f, 0.5f, 1f), textColor, 22f);
        RectTransform collapseRect = collapse.GetComponent<RectTransform>();
        collapseRect.anchorMin = collapseRect.anchorMax = collapseRect.pivot = new Vector2(0f, 1f);
        collapseRect.sizeDelta = new Vector2(220f, 36f);
        collapseRect.anchoredPosition = new Vector2(10f, -8f);
        TMP_Text collapseLabel = collapse.GetComponentInChildren<TMP_Text>();
        System.Action refreshCollapse = () => { collapseLabel.text = board.AllMinimized ? "Expand all" : "Collapse all"; };
        collapse.onClick.AddListener(() => board.SetAllMinimized(!board.AllMinimized));
        board.MinimizeChanged = refreshCollapse;

        // Widget: pixel dropdown | amount field | Add | Give pet
        int pinStart = content.childCount;
        float fieldW = inner * 0.30f, dropW = inner - fieldW - rowHeight * 2f - 30f;
        pixelDropdown = PixelUIKit.CreateDropdown(font, content, "Pixel Dropdown", new Vector2(dropW, rowHeight),
                                                  boxColor, listColor, textColor, fontSize);
        Place(pixelDropdown.GetComponent<RectTransform>(), 40f, y, dropW);
        amountField = PixelUIKit.CreateInputField(font, content, "Amount Field", new Vector2(fieldW, rowHeight),
                                                  boxColor, textColor, fontSize, "amount");
        amountField.text = defaultAmount;
        amountField.onValueChanged.AddListener(text => { if (rememberSettings) PixelPrefs.SetString(PrefPrefix + "Amount", text); });
        Place(amountField.GetComponent<RectTransform>(), 40f + dropW + 10f, y, fieldW);
        Button add = IconButton(content, "Add Button", DevIcons.PlusSprite(), new Color(0.3f, 0.78f, 0.38f, 1f), rowHeight);
        Place(add.GetComponent<RectTransform>(), 40f + dropW + fieldW + 20f, y, rowHeight);
        add.onClick.AddListener(AddSelected);
        AddTip(add.gameObject, addTipText);
        Button pet = IconButton(content, "Give Pet Button", DevIcons.PawSprite(), new Color(0.97f, 0.82f, 0.22f, 1f), rowHeight);
        Place(pet.GetComponent<RectTransform>(), 40f + dropW + fieldW + 30f + rowHeight, y, rowHeight);
        pet.onClick.AddListener(GivePet);
        AddTip(pet.gameObject, petTipText);
        Widget(content, board, "addpixel", pinStart, y);

        BuildSpawnWidgets(content, board, inner, y);
        int ws = content.childCount;

        y = BuildDevReportsCard(content, board, inner, y, ws);   // every report / snapshot tool in one card
        ws = content.childCount;

        y = BuildSwitchCards(content, board, inner, y, ws);   // reset counts + the tick box cards (clear key, infinite, no despawn, no limit, double floor, god)
        ws = content.childCount;

        y = BuildClickAndTimeCards(content, board, inner, y, ws);   // the click count and Time control cards

        BuildResourceToolsCard(content, board);
        board.Finish();   // the saved order, the layout and the scroll size
        refreshCollapse();

        // To-Do and Changelog are the first two sub-tabs of the hub's Docs tab (PixelDevDocs); they are rebuilt each time one is shown.
        BuildTodoView(PixelDevDocs.Root(0), 8f, inner);
        BuildChangelogView(PixelDevDocs.Root(1), 8f);
        PixelDevDocs.Shown += OnDocsShown;

        FillDropdowns();
        pixelDropdown.SetValueWithoutNotify(Mathf.Clamp(GetInt("PixelIndex", 0), 0, pixelDropdown.options.Count - 1));
        pixelDropdown.RefreshShownValue();
        pixelDropdown.onValueChanged.AddListener(v => SetInt("PixelIndex", v));
        FillSelector();
        if (spawnSelectorEnabled) ApplySelector(selectorDropdown.value);
        RefreshSpawnedPixelButton();
        if (godMode) SetGodMode(true);
        // Saved shapes / mockup pieces come back even while the hub is closed.
        PixelDevShapes.Boot();
        PixelDevMockup.Boot();
        PixelDevHub.OpenChanged += OnHubOpenChanged;
        if (PixelDevHub.IsOpen) FillDropdowns();
    }

    // BuildPanel, step: the Dev reports card (every report / snapshot tool, two buttons per row); ws = its first child. Returns the y under it.
    private float BuildDevReportsCard(RectTransform content, PixelCardBoard board, float inner, float y, int ws)
    {
        // ---- Dev reports: every report / snapshot tool in one card, two buttons per row. ----
        {
            float half = (inner - 10f) * 0.5f, x2 = 40f + half + 10f, y2 = y + rowHeight + WidgetRowGap, y3 = y2 + rowHeight + WidgetRowGap;

            // Row 1: crash report folder | snapshot bounds (waits so the panel can be closed first)
            Button report = PixelUIKit.CreateButton(font, content, "Report Folder Button", reportsCrashLabel, new Vector2(half, rowHeight), buttonColor, textColor, fontSize);
            Place(report.GetComponent<RectTransform>(), 40f, y, half);
            report.onClick.AddListener(PixelCrashLog.OpenFolder);
            report.interactable = PixelCrashLog.Available;   // (crash reports switched off: the button stays, greyed out)
            AddTip(report.gameObject, reportTipText);
            Button snap = PixelUIKit.CreateButton(font, content, "Bounds Snapshot Button", reportsBoundsLabel, new Vector2(half, rowHeight), buttonColor, textColor, fontSize);
            Place(snap.GetComponent<RectTransform>(), x2, y, half);
            snap.onClick.AddListener(() => StartCoroutine(ProbeRoutine(() => PixelBoundsSnapshot.Capture(), "Bounds snapshot")));

            // Row 2: mark this moment | UI layout dump (same wait; the keys F10 / F11 need no wait)
            Button moment = PixelUIKit.CreateButton(font, content, "Mark Moment Button", reportsMomentLabel, new Vector2(half, rowHeight), buttonColor, textColor, fontSize);
            Place(moment.GetComponent<RectTransform>(), 40f, y2, half);
            moment.onClick.AddListener(() => StartCoroutine(ProbeRoutine(() => PixelDevProbe.MarkMoment(), "Marked moment")));
            Button uiDump = PixelUIKit.CreateButton(font, content, "UI Layout Button", reportsUiLabel, new Vector2(half, rowHeight), buttonColor, textColor, fontSize);
            Place(uiDump.GetComponent<RectTransform>(), x2, y2, half);
            uiDump.onClick.AddListener(() => StartCoroutine(ProbeRoutine(() => PixelDevProbe.UiLayoutDump(), "UI layout dump")));

            // Row 3: skill balance report (no wait) | session recorder (Start, then Stop & save)
            Button balance = PixelUIKit.CreateButton(font, content, "Balance Report Button", reportsBalanceLabel, new Vector2(half, rowHeight), buttonColor, textColor, fontSize);
            Place(balance.GetComponent<RectTransform>(), 40f, y3, half);
            balance.onClick.AddListener(() => PixelBalanceReport.Capture(balanceManualCps, balanceAutoCps, (int[])balanceLevels.Clone()));
            Button session = PixelUIKit.CreateButton(font, content, "Session Recorder Button", sessionStartLabel, new Vector2(half, rowHeight), buttonColor, textColor, fontSize);
            Place(session.GetComponent<RectTransform>(), x2, y3, half);
            sessionLabel = session.GetComponentInChildren<TMP_Text>();
            session.onClick.AddListener(() =>
            {
                if (PixelSessionRecorder.Recording) PixelSessionRecorder.StopAndSave();
                else PixelSessionRecorder.StartRecording();
                RefreshSessionLabel();
            });
            AddTip(session.gameObject, "Records a real play session (clicking speed, income per minute, milestones, husks) and writes a report to paste to Claude. Only time in the game counts.");
            AddTip(balance.gameObject, "Balance report: XP and pay per click for every skill at the reference clicking speeds, saved and copied to the clipboard.");
            AddTip(moment.gameObject, "Mark this moment: a screenshot + a report of the game right now (waits 3 s so you can close this window; the F10 key does it at once).");
            AddTip(uiDump.gameObject, "UI layout dump: every open UI element as text - positions, sizes, texts (waits 3 s).");
            AddTip(snap.gameObject, "Snapshot bounds: the camera / floor / view-bounds numbers, for tuning where husks can go (waits 3 s).");
            Widget(content, board, "devreports", ws, y, 3);
            y += rowHeight + 24f;
            ws = content.childCount;
        }
        return y;
    }

    // BuildPanel, step: the Reset counts card and the tick box cards; ws = the first child of the next card. Returns the y under them.
    private float BuildSwitchCards(RectTransform content, PixelCardBoard board, float inner, float y, int ws)
    {
        // Row: reset every pixel count (inventory, Log, Bank) but keep the unlocks - needs a second click to confirm
        resetButton = PixelUIKit.CreateButton(font, content, "Reset Counts Button", resetCountsText,
                                              new Vector2(inner, rowHeight), buttonColor, textColor, fontSize);
        Place(resetButton.GetComponent<RectTransform>(), 40f, y, inner);
        resetLabel = resetButton.GetComponentInChildren<TMP_Text>();
        resetButton.onClick.AddListener(ResetCountsClicked);
        Widget(content, board, "reset", ws, y);
        y += rowHeight + 24f;
        ws = content.childCount;

        // Row: tick box for the clear key
        clearToggle = BuildToggleRow(content, clearToggleText, y, inner, clearKeyEnabled, on => { clearKeyEnabled = on; SetBool("ClearKey", on); });
        Widget(content, board, "clear", ws, y);
        y += rowHeight + 24f;
        ws = content.childCount;

        // Row: tick box for infinite resources
        infiniteToggle = BuildToggleRow(content, infiniteToggleText, y, inner, infiniteResources,
                       on => { infiniteResources = on; PixelClicker.InfiniteResources = on; SetBool("Infinite", on); });
        Widget(content, board, "infinite", ws, y);
        y += rowHeight + 24f;
        ws = content.childCount;


        // Row: tick box to stop old pixels despawning
        noDespawnToggle = BuildToggleRow(content, noDespawnToggleText, y, inner, disableDespawn, on =>
        {
            disableDespawn = on;
            OldPixelDespawn.DevNoDespawn = on;
            SetBool("NoDespawn", on);
        });
        Widget(content, board, "nodespawn", ws, y);
        y += rowHeight + 24f;
        ws = content.childCount;

        // Row: tick box to remove the old pixel limit
        noLimitToggle = BuildToggleRow(content, noLimitToggleText, y, inner, disableOldPixelLimit, on =>
        {
            disableOldPixelLimit = on;
            PixelClicker.DevNoOldPixelLimit = on;
            SetBool("NoLimit", on);
        });
        Widget(content, board, "nolimit", ws, y);
        y += rowHeight + 24f;
        ws = content.childCount;

        // Row: tick box that makes the floor circle twice as big
        doubleFloorToggle = BuildToggleRow(content, doubleFloorToggleText, y, inner, doubleFloor, on =>
        {
            doubleFloor = on;
            PixelFloor.DoubleArea = on;
            SetBool("DoubleFloor", on);
        });
        Widget(content, board, "doublefloor", ws, y);
        y += rowHeight + 24f;
        ws = content.childCount;

        // Row: tick box for god pixel mode
        godToggle = BuildToggleRow(content, godModeLabel, y, inner, godMode, SetGodMode, godTipText);
        Widget(content, board, "god", ws, y);
        y += rowHeight + 24f;
        ws = content.childCount;

        // Row: tick box for Edit text mode (click any text in the game to change it: PixelTextEditMode). Not remembered: it is off at every start.
        textEditToggle = BuildToggleRow(content, textEditModeLabel, y, inner, false, PixelTextEditMode.SetOn, textEditModeTip);
        PixelTextEditMode.Changed -= OnTextEditModeChanged;
        PixelTextEditMode.Changed += OnTextEditModeChanged;
        Widget(content, board, "textedit", ws, y);
        y += rowHeight + 24f;
        ws = content.childCount;
        return y;
    }

    // BuildPanel, step: the click count card and the Time control card; ws = the first child of the next card. Returns the y under them.
    private float BuildClickAndTimeCards(RectTransform content, PixelCardBoard board, float inner, float y, int ws)
    {
        // Row: how many clicks one click counts as (payouts, tough-pixel hits and the combo all scale with it)
        TMP_Text clickLabel = PixelUIKit.CreateText(font, content, "Click Count Label", clickCountText, fontSize,
                                                    TextAlignmentOptions.MidlineLeft, FontStyles.Normal, textColor);
        Place(clickLabel.rectTransform, 40f, y, inner * 0.6f - 10f);
        clickCountField = PixelUIKit.CreateInputField(font, content, "Click Count Field", new Vector2(inner * 0.4f, rowHeight),
                                                      boxColor, textColor, fontSize, "1");
        clickCountField.contentType = TMP_InputField.ContentType.IntegerNumber;
        clickCountField.SetTextWithoutNotify(clickCount.ToString());
        clickCountField.onValueChanged.AddListener(text =>
        {
            int.TryParse(text.Trim(), out int parsed);
            clickCount = Mathf.Clamp(parsed, 1, 1000000); // empty / invalid / 0 = a normal click
            PixelClicker.DevClickCount = clickCount;
            SetInt("ClickCount", clickCount);
        });
        Place(clickCountField.GetComponent<RectTransform>(), 40f + inner * 0.6f, y, inner * 0.4f);
        Widget(content, board, "clicks", ws, y);
        y += rowHeight + 24f;
        ws = content.childCount;

        // Time control: Pause / Play / Slow / Fast / Rewind (PixelTimeControl), and a status line under them
        {
            string[] names = { "Rewind", "Pause", "Play", "Slow", "Fast" };
            System.Action[] acts =
            {
                () => PixelTimeControl.Instance?.Rewind(), () => PixelTimeControl.Instance?.Pause(), () => PixelTimeControl.Instance?.Play(),
                () => PixelTimeControl.Instance?.Slow(), () => PixelTimeControl.Instance?.Fast(),
            };
            float bw = (inner - 4 * 8f) / names.Length;
            for (int i = 0; i < names.Length; i++)
            {
                System.Action act = acts[i];
                Button b = PixelUIKit.CreateButton(font, content, "Time " + names[i] + " Button", names[i], new Vector2(bw, rowHeight), buttonColor, textColor, fontSize * 0.8f);
                Place(b.GetComponent<RectTransform>(), 40f + i * (bw + 8f), y, bw);
                b.onClick.AddListener(() => act());
                AddTip(b.gameObject, timeControlTipText.Replace("0.1x (0.1x - 5x)", "0.1x (0.5x above 2x; 0.1x - 10x)"));   // (a scene saved before 10x keeps the old text)
            }
            timeStatus = PixelUIKit.CreateText(font, content, "Time Status", "", fontSize * 0.8f, TextAlignmentOptions.MidlineLeft, FontStyles.Normal, textColor);
            Place(timeStatus.rectTransform, 40f, y + rowHeight + WidgetRowGap, inner);
            RefreshTimeStatus();
            PixelTimeControl.Changed += RefreshTimeStatus;
            Widget(content, board, "timecontrol", ws, y, 2);
            y += 2f * rowHeight + WidgetRowGap + 24f;
        }
        return y;
    }

    private void RefreshTimeStatus()
    {
        if (timeStatus == null) return;
        PixelTimeControl tc = PixelTimeControl.Instance;
        timeStatus.text = tc == null ? "Time control is off in this build" : tc.StatusText() + "   -   " + tc.RecordedSeconds.ToString("0") + " s to rewind";
    }

    private void OnDocsShown(int sub)
    {
        if (sub == 0) RebuildTodo();
        else if (sub == 1) RebuildChangelog();
    }

    private void OnHubOpenChanged(bool open)
    {
        if (open && pixelDropdown != null) { FillDropdowns(); FillSelector(); }
        if (open) RefreshTimeStatus();
    }

    /// <summary>Turns the controls built since child number 'start' of 'content' (one row) into a draggable widget card at row position y.</summary>
    private static string WidgetTitle(string id)
    {
        switch (id)
        {
            case "resources": return "Resource Tools";
            case "timecontrol": return "Time control";
            case "addpixel": return "Add pixels";
            case "spawnpixel": return "Spawned pixel";
            case "spawnminigame": return "Minigames";
            case "middleclick": return "Middle click";
            case "focus": return "Focus potions";
            case "cubiebill": return "Cubie's bill";
            case "shopunlock": return "Unlock";
            case "maxxp": return "Max XP";
            case "huskpile": return "Husk pile";
            case "clearworld": return "Clear world";
            case "devreports": return "Dev reports";
            case "superrares": return "Super Rares";
            case "devitems": return "Dev items";
            case "reset": return "Reset counts";
            case "clear": return "Clear key";
            case "infinite": return "Infinite resources";
            case "nodespawn": return "Husk despawns";
            case "nolimit": return "Husk limit";
            case "god": return "God mode";
            case "textedit": return "Edit text mode";
            case "clicks": return "Click count";
            default: return id;
        }
    }

    /// <summary>The 'Clear world' button (confirmed): every placed device and every tester object goes.</summary>
    private void ClearWorld()
    {
        PixelConsumables consumables = PixelFind.Cached<PixelConsumables>();
        int devices = 0;
        if (consumables != null)
        {
            foreach (PixelPlacedDevice d in PixelPlacedDevice.All) if (d != null) devices++;
            consumables.ClearPlacedDevices();
        }
        int objects = PixelFeatureTester.ClearAllPlaced();
        PixelHints.Announce("Cleared " + devices + " device" + (devices == 1 ? "" : "s") + " and " + objects + " placed object" + (objects == 1 ? "" : "s"));
    }

    /// <summary>Puts a button's label back after a while (the 'click again' state ran out) - unless it was already changed back.</summary>
    private static System.Collections.IEnumerator ResetLabelLater(TMP_Text label, string text, float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        if (label != null) label.text = text;
    }

    /// <summary>The 'Fill floor' button: random unlocked husks dropped in a ring round the cube, a few per frame, until the floor limit is reached.</summary>
    private System.Collections.IEnumerator FillFloorWithHusks()
    {
        PixelClicker c = PixelFind.Cached<PixelClicker>();
        if (c == null || c.PixelTransform == null) yield break;
        List<int> pool = new List<int>();
        for (int i = 0; i < c.Tiers.Length; i++)
            if (c.Tiers[i] != null && c.Tiers[i].unlocked && !c.Tiers[i].flyAway) pool.Add(i);
        if (pool.Count == 0) yield break;
        float unit = c.PixelBaseSize;
        int guard = 1000;
        while (c.OldPixels.Count < c.OldPixelCap && guard-- > 0)
        {
            for (int k = 0; k < fillFloorPerFrame && c.OldPixels.Count < c.OldPixelCap; k++)
            {
                Vector2 ring = Random.insideUnitCircle.normalized * Random.Range(2f, 6f) * unit;
                Vector3 at = c.PixelTransform.position + new Vector3(ring.x, Random.Range(0f, 1f) * unit, ring.y);
                int tier = pool[Random.Range(0, pool.Count)];
                c.SpawnStoredPixel(tier, c.Tiers[tier].amountPerClick, at, Random.insideUnitSphere * 0.5f);
            }
            yield return null;
        }
    }

    // ---- Resource Tools: these tools share ONE card (in this order) instead of a card each; ids with a label of their own get no sub-title. ----
    private static readonly string[] ResourceToolIds = { "infinite", "god", "nodespawn", "huskpile", "clear", "nolimit", "spawnpixel", "middleclick", "addpixel", "focus", "clicks" };
    private static readonly HashSet<string> ResourceToolsWithOwnLabel = new HashSet<string> { "infinite", "god", "nodespawn", "clear", "nolimit", "clicks" };
    private readonly Dictionary<string, KeyValuePair<List<Transform>, float>> resourceParts = new Dictionary<string, KeyValuePair<List<Transform>, float>>();
    private const float ResourceSubtitleH = 26f;

    /// <summary>Builds the 'Resource Tools' card from the tools Widget set aside (ResourceToolIds), stacked in that order with a small title over the ones without a label.</summary>
    private void BuildResourceToolsCard(RectTransform content, PixelCardBoard board)
    {
        float hdr = PixelCardBoard.DefaultHeaderH, inner = PixelDevHub.ContentW - 100f;
        GameObject card = new GameObject("Widget resources", typeof(RectTransform), typeof(Image));
        card.transform.SetParent(content, false);
        RectTransform cr = card.GetComponent<RectTransform>();
        cr.anchorMin = cr.anchorMax = cr.pivot = new Vector2(0f, 1f);
        Image im = card.GetComponent<Image>();
        im.color = new Color(1f, 1f, 1f, 0.05f);
        im.raycastTarget = false;
        PixelUIKit.StyleBox(im);
        float acc = 0f;   // how far down the card the next tool starts (below the header strip)
        foreach (string id in ResourceToolIds)
        {
            if (!resourceParts.TryGetValue(id, out KeyValuePair<List<Transform>, float> part)) continue;
            if (!ResourceToolsWithOwnLabel.Contains(id))
            {
                TMP_Text sub = PixelUIKit.CreateText(font, card.transform, id + " Title", WidgetTitle(id), fontSize - 3f, TextAlignmentOptions.BottomLeft, FontStyles.Bold, new Color(0.6f, 0.75f, 1f, 1f));
                sub.raycastTarget = false;
                RectTransform sr = sub.rectTransform;
                sr.anchorMin = sr.anchorMax = sr.pivot = new Vector2(0f, 1f);
                sr.sizeDelta = new Vector2(inner, ResourceSubtitleH);
                sr.anchoredPosition = new Vector2(40f, -(8f + hdr + acc));
                acc += ResourceSubtitleH;
            }
            foreach (Transform k in part.Key)
            {
                k.SetParent(card.transform, false);
                RectTransform kr = (RectTransform)k;
                kr.anchoredPosition += new Vector2(0f, part.Value - 8f - hdr - acc);   // (built at row -y; now at this tool's place in the card)
                foreach (TMP_Dropdown dd in k.GetComponentsInChildren<TMP_Dropdown>(true)) PixelUIKit.LetListEscapeMasks(dd);
                foreach (Button b in k.GetComponentsInChildren<Button>(true)) PixelUIKit.FitOneLine(b.GetComponentInChildren<TMP_Text>(true));
            }
            acc += rowHeight + WidgetRowGap + 4f;
        }
        float cardH = hdr + 8f + acc + 4f;
        cr.sizeDelta = new Vector2(PixelDevHub.ContentW - 24f, cardH);
        board.Add("resources", cr, cardH, WidgetTitle("resources"));
    }

    private void Widget(RectTransform content, PixelCardBoard board, string id, int start, float y, int rows = 1)
    {
        List<Transform> kids = new List<Transform>();
        for (int i = start; i < content.childCount; i++) kids.Add(content.GetChild(i));
        if (System.Array.IndexOf(ResourceToolIds, id) >= 0) { resourceParts[id] = new KeyValuePair<List<Transform>, float>(kids, y); return; }   // (goes into the Resource Tools card)
        float hdr = PixelCardBoard.DefaultHeaderH;   // the card's header strip (title + minimize button) sits above the controls
        float cardH = rows * rowHeight + (rows - 1) * WidgetRowGap + 16f + hdr;   // (a widget with more rows places them WidgetRowGap apart)
        GameObject card = new GameObject("Widget " + id, typeof(RectTransform), typeof(Image));
        card.transform.SetParent(content, false);
        RectTransform cr = card.GetComponent<RectTransform>();
        cr.anchorMin = cr.anchorMax = cr.pivot = new Vector2(0f, 1f);
        cr.sizeDelta = new Vector2(PixelDevHub.ContentW - 24f, cardH);
        cr.anchoredPosition = new Vector2(0f, -(y - 8f));
        Image im = card.GetComponent<Image>();
        im.color = new Color(1f, 1f, 1f, 0.05f);
        im.raycastTarget = false;
        PixelUIKit.StyleBox(im);
        foreach (Transform k in kids)
        {
            k.SetParent(card.transform, false);
            RectTransform kr = (RectTransform)k;
            kr.anchoredPosition += new Vector2(0f, y - 8f - hdr);   // (they were placed at -y; the card starts 8 above that, and its header strip takes the top)
            foreach (TMP_Dropdown dd in k.GetComponentsInChildren<TMP_Dropdown>(true)) PixelUIKit.LetListEscapeMasks(dd);   // (an opened drop-down list must not be clipped by the scrolling board)
            foreach (Button b in k.GetComponentsInChildren<Button>(true)) PixelUIKit.FitOneLine(b.GetComponentInChildren<TMP_Text>(true));   // (long labels such as DESPAWN shrink to fit their button)
        }
        board.Add(id, cr, cardH, WidgetTitle(id));
    }
}
