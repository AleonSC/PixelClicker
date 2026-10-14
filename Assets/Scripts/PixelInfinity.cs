// ---- System notes ----
// PixelInfinity.cs
//   The Infinity Stones, the Gauntlet and the Infinity Cubelette (a homage; names are work in progress). Component added by PixelClicker.Awake.
//   * Six stones (PixelType.StoneSpace..StoneSoul 85-90, PixelClicker.IsInfinityStone, tiers made by EnsureGodCubes) are rare drops with exactly the God Cubes' rules (IsGodCube is true for them:
//     one of each, same spawn weight godCubeSpawnWeight, CanSpawn false once held, backpack stash) and each has its OWN find show (PixelGodFind.DrawStone: Space tesseract, Mind thought-bolts,
//     Reality tearing shapes, Power detonation, Time's backwards clock, Soul wisps) and code-made sound (PixelGodSynth.Stone). Looks: PixelLooks.StoneLook, looksVersion 60.
//   * When the player holds all six (and has no Cubelette and no Gauntlet) the WIZARD offers the Infinity Gauntlet (PixelWizardMinigame.ModifyWares + aside line): DeviceKind.Gauntlet 17,
//     a golden cube consumable (vendorOnly, hold 1; price = the Device's costs, editable in the Consumables Inspector).
//   * Right-click the Gauntlet in the inventory (PixelConsumables.TryUseItem) -> this window: a golden cube laid out flat (six faces). Pick a stone from the tray, click a face to place it (click a
//     placed one to take it back). With all six in place the Forge button makes the Infinity Cubelette: the Gauntlet and the six stones are used up, the Cubelette tier (PixelType.InfinityCubelette 91,
//     never rolled) is added and its show plays (PixelGodFind.DrawCubelette). Closing the window just puts the stones back (nothing is spent until the forge).
//   The Cubelette is a trophy for now (it has no use yet).
// ----------------------
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PixelInfinity : MonoBehaviour
{
    [Header("Gauntlet window")]
    [Tooltip("Size of one face socket (canvas units). 0 = 150.")]
    [SerializeField] private float socketSize = 0f;
    [Tooltip("Seconds of glowing before the forge makes the Cubelette. 0 = 1.6.")]
    [SerializeField] private float forgeSeconds = 0f;
    [Tooltip("What the Wizard says in his second speech box when he offers the Gauntlet.")]
    [SerializeField] private string wizardAside = "Six stones! In one pocket! Well, well, well... I have a little thing here, a Gauntlet, golden and rather shiny. Set them in its faces and something very small and very powerful will come out. For a fee, naturally.";

    public static readonly string[] StoneNames = { "Space Stone", "Mind Stone", "Reality Stone", "Power Stone", "Time Stone", "Soul Stone" };
    private static readonly Color[] StoneColors =
    {
        new Color(0.2f, 0.45f, 1f, 1f), new Color(1f, 0.85f, 0.15f, 1f), new Color(0.95f, 0.1f, 0.1f, 1f),
        new Color(0.68f, 0.22f, 0.95f, 1f), new Color(0.2f, 0.9f, 0.35f, 1f), new Color(1f, 0.55f, 0.1f, 1f),
    };
    private static readonly string[] StoneIds = { "stone_space", "stone_mind", "stone_reality", "stone_power", "stone_time", "stone_soul" };

    public static string StoneName(int i) => StoneNames[Mathf.Clamp(i, 0, 5)];
    public static Color StoneColor(int i) => StoneColors[Mathf.Clamp(i, 0, 5)];
    public static string StoneSoundId(int i) => StoneIds[Mathf.Clamp(i, 0, 5)];

    private static PixelInfinity instance;
    private PixelClicker clicker;
    private PixelConsumables consumables;
    private GameObject canvasRoot;
    private int gauntletItem = -1;
    private readonly int[] placed = new int[6];       // face -> stone (0-5) or -1
    private int selected = -1;
    private Image[] faceImages, faceGems, trayImages, trayGems;
    private TMP_Text[] trayLabels;
    private TMP_Text status;
    private Button forge;
    private bool forging;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; }

    private void Awake()
    {
        instance = this;
        PixelWindows.Register(this, 80, () => canvasRoot != null, Close);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        PixelWindows.Unregister(this);
        if (canvasRoot != null) Destroy(canvasRoot);
    }

    /// <summary>The text of the Wizard's second speech box when he offers the Gauntlet.</summary>
    public static string WizardAside => instance != null ? instance.wizardAside : "I have a Gauntlet for you.";

    /// <summary>True when the player holds all six stones (the Wizard's Gauntlet offer) and has neither the Cubelette nor a Gauntlet yet.</summary>
    public static bool ShouldOfferGauntlet(PixelClicker clicker, PixelConsumables consumables, int gauntletItem)
    {
        if (clicker == null || consumables == null || gauntletItem < 0 || consumables.ItemOwned(gauntletItem) > 0) return false;
        if (HeldCount(clicker) < 6) return false;
        int cub = clicker.IndexOf(PixelClicker.PixelType.InfinityCubelette);
        return cub < 0 || clicker.Tiers[cub].count < 1d;
    }

    private static int HeldCount(PixelClicker clicker)
    {
        int n = 0;
        for (int i = 0; i < 6; i++)
        {
            int t = clicker.IndexOf(PixelClicker.PixelType.StoneSpace + i);
            if (t >= 0 && clicker.Tiers[t].count >= 1d) n++;
        }
        return n;
    }

    private bool Holds(int stone)
    {
        int t = clicker.IndexOf(PixelClicker.PixelType.StoneSpace + stone);
        return t >= 0 && clicker.Tiers[t].count >= 1d;
    }

    /// <summary>Opens the Gauntlet window (called when the Gauntlet is used from the inventory). Returns false when it cannot open.</summary>
    public static bool Open(PixelConsumables consumables, int item)
    {
        if (instance == null) return false;
        instance.OpenWindow(consumables, item);
        return true;
    }

    private void OpenWindow(PixelConsumables owner, int item)
    {
        clicker = PixelFind.First<PixelClicker>();
        if (clicker == null) return;
        consumables = owner; gauntletItem = item;
        PixelWindows.CloseAllExcept(this);
        Close();
        for (int i = 0; i < 6; i++) placed[i] = -1;
        selected = -1; forging = false;
        Build();
        Refresh();
    }

    private void Close()
    {
        if (forging) return;
        if (canvasRoot != null) Destroy(canvasRoot);
        canvasRoot = null;
        PixelClicker.ExternalClickBlock = false;
    }

    // ------------------------------------------------------------------

    private static Image NewImage(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image img = go.GetComponent<Image>();
        img.color = color;
        return img;
    }

    private void Build()
    {
        TMP_FontAsset font = clicker.UIFont;
        canvasRoot = PixelUIKit.CreateCanvas("Infinity Gauntlet", 610, new Vector2(1920f, 1080f), true);
        PixelClicker.ExternalClickBlock = true;   // clicks in the window must not hit the cube

        Image dim = NewImage("Dim", canvasRoot.transform, new Color(0f, 0f, 0f, 0.55f));
        PixelUIKit.Stretch(dim.rectTransform);

        GameObject panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panelGo.transform.SetParent(canvasRoot.transform, false);
        RectTransform panel = panelGo.GetComponent<RectTransform>();
        panel.sizeDelta = new Vector2(1180f, 760f);
        PixelUIKit.StyleWindow(panelGo.GetComponent<Image>(), new Color(0.1f, 0.08f, 0.04f, 0.97f));

        TMP_Text title = PixelUIKit.CreateText(font, panel, "Title", "Infinity Gauntlet", 52f, TextAlignmentOptions.Center, FontStyles.Bold, new Color(1f, 0.85f, 0.35f, 1f));
        title.rectTransform.anchorMin = new Vector2(0f, 1f); title.rectTransform.anchorMax = new Vector2(1f, 1f); title.rectTransform.pivot = new Vector2(0.5f, 1f);
        title.rectTransform.sizeDelta = new Vector2(0f, 80f); title.rectTransform.anchoredPosition = new Vector2(0f, -16f);
        Button x = PixelUIKit.CreateButton(font, panel, "Close", "X", new Vector2(60f, 60f), new Color(0.5f, 0.22f, 0.22f, 1f), Color.white, 34f);
        x.GetComponent<RectTransform>().anchorMin = x.GetComponent<RectTransform>().anchorMax = new Vector2(1f, 1f);
        x.GetComponent<RectTransform>().anchoredPosition = new Vector2(-44f, -44f);
        x.onClick.AddListener(Close);

        // The cube laid flat: a cross of six faces on the left.
        float s = socketSize > 0f ? socketSize : 150f, gap = 10f;
        Vector2 origin = new Vector2(-330f, 70f);
        Vector2[] cells = { new Vector2(1, 1), new Vector2(0, 0), new Vector2(1, 0), new Vector2(2, 0), new Vector2(3, 0), new Vector2(1, -1) };   // top, left, front, right, back, bottom
        string[] names = { "Top", "Left", "Front", "Right", "Back", "Bottom" };
        faceImages = new Image[6]; faceGems = new Image[6];
        Image back = NewImage("Cube", panel, new Color(0.9f, 0.7f, 0.2f, 1f));
        back.rectTransform.sizeDelta = new Vector2(s * 4f + gap * 5f, s * 3f + gap * 4f);
        back.rectTransform.anchoredPosition = origin + new Vector2(s * 0.5f + gap * 0.5f, 0f);
        back.color = new Color(0.25f, 0.18f, 0.05f, 0.6f);
        for (int f = 0; f < 6; f++)
        {
            int face = f;
            GameObject go = new GameObject("Face " + names[f], typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(panel, false);
            RectTransform r = go.GetComponent<RectTransform>();
            r.sizeDelta = new Vector2(s, s);
            r.anchoredPosition = origin + new Vector2((cells[f].x - 1f) * (s + gap) + s * 1.0f + gap * 0.5f, cells[f].y * (s + gap));
            faceImages[f] = go.GetComponent<Image>();
            faceImages[f].color = new Color(0.95f, 0.75f, 0.22f, 1f);
            go.GetComponent<Button>().targetGraphic = faceImages[f];
            go.GetComponent<Button>().onClick.AddListener(() => ClickFace(face));
            faceGems[f] = NewImage("Gem", go.transform, Color.clear);
            faceGems[f].rectTransform.sizeDelta = new Vector2(s * 0.55f, s * 0.55f);
            faceGems[f].raycastTarget = false;
            TMP_Text lbl = PixelUIKit.CreateText(font, go.transform, "Name", names[f], 22f, TextAlignmentOptions.Bottom, FontStyles.Bold, new Color(0.3f, 0.2f, 0.02f, 1f));
            PixelUIKit.Stretch(lbl.rectTransform);
        }

        // The tray of stones on the right.
        trayImages = new Image[6]; trayGems = new Image[6]; trayLabels = new TMP_Text[6];
        TMP_Text trayTitle = PixelUIKit.CreateText(font, panel, "Tray Title", "Your stones", 34f, TextAlignmentOptions.Center, FontStyles.Bold, Color.white);
        trayTitle.rectTransform.sizeDelta = new Vector2(380f, 50f); trayTitle.rectTransform.anchoredPosition = new Vector2(380f, 230f);
        for (int i = 0; i < 6; i++)
        {
            int stone = i;
            GameObject go = new GameObject("Stone " + StoneNames[i], typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(panel, false);
            RectTransform r = go.GetComponent<RectTransform>();
            r.sizeDelta = new Vector2(380f, 62f);
            r.anchoredPosition = new Vector2(380f, 160f - i * 72f);
            trayImages[i] = go.GetComponent<Image>();
            go.GetComponent<Button>().targetGraphic = trayImages[i];
            go.GetComponent<Button>().onClick.AddListener(() => ClickStone(stone));
            trayGems[i] = NewImage("Gem", go.transform, StoneColors[i]);
            trayGems[i].rectTransform.anchorMin = trayGems[i].rectTransform.anchorMax = new Vector2(0f, 0.5f);
            trayGems[i].rectTransform.sizeDelta = new Vector2(38f, 38f); trayGems[i].rectTransform.anchoredPosition = new Vector2(40f, 0f);
            trayGems[i].raycastTarget = false;
            trayLabels[i] = PixelUIKit.CreateText(font, go.transform, "Label", StoneNames[i], 30f, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, Color.white);
            PixelUIKit.Stretch(trayLabels[i].rectTransform);
            trayLabels[i].rectTransform.offsetMin = new Vector2(76f, 0f);
        }

        status = PixelUIKit.CreateText(font, panel, "Status", "", 30f, TextAlignmentOptions.Center, FontStyles.Normal, new Color(1f, 0.92f, 0.7f, 1f));
        status.rectTransform.sizeDelta = new Vector2(1100f, 56f); status.rectTransform.anchoredPosition = new Vector2(0f, -262f);
        forge = PixelUIKit.CreateButton(font, panel, "Forge", "Forge the Cubelette", new Vector2(520f, 84f), new Color(0.85f, 0.62f, 0.12f, 1f), Color.white, 38f);
        forge.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -320f);
        forge.onClick.AddListener(() => { if (!forging) StartCoroutine(Forge()); });
        PixelPop.Show(panelGo);
    }

    private void Refresh()
    {
        if (canvasRoot == null) return;
        int placedCount = 0;
        for (int f = 0; f < 6; f++)
        {
            bool has = placed[f] >= 0; if (has) placedCount++;
            faceGems[f].color = has ? StoneColors[placed[f]] : new Color(0.3f, 0.2f, 0.02f, 0.35f);
        }
        for (int i = 0; i < 6; i++)
        {
            bool held = Holds(i), used = System.Array.IndexOf(placed, i) >= 0;
            trayImages[i].color = i == selected ? new Color(0.9f, 0.75f, 0.3f, 1f) : new Color(0.2f, 0.17f, 0.12f, 1f);
            trayGems[i].color = held ? (used ? new Color(StoneColors[i].r, StoneColors[i].g, StoneColors[i].b, 0.3f) : StoneColors[i]) : new Color(0.25f, 0.25f, 0.25f, 1f);
            trayLabels[i].text = StoneNames[i] + (held ? (used ? "  (placed)" : "") : "  (missing)");
            trayLabels[i].color = held ? Color.white : new Color(0.55f, 0.55f, 0.55f, 1f);
        }
        int cub = clicker.IndexOf(PixelClicker.PixelType.InfinityCubelette);
        bool haveCub = cub >= 0 && clicker.Tiers[cub].count >= 1d;
        forge.interactable = placedCount == 6 && !haveCub && !forging;
        status.text = haveCub ? "You already hold an Infinity Cubelette. Only one can exist."
            : placedCount == 6 ? "All six are set. Forge it!"
            : selected >= 0 ? "Now click a face of the cube to set the " + StoneNames[selected]
            : "Pick a stone, then click a face to set it in. (" + placedCount + " / 6)";
    }

    private void ClickStone(int stone)
    {
        if (forging || !Holds(stone) || System.Array.IndexOf(placed, stone) >= 0) return;
        selected = selected == stone ? -1 : stone;
        Refresh();
    }

    private void ClickFace(int face)
    {
        if (forging) return;
        if (placed[face] >= 0) { placed[face] = -1; PixelAudio.Play("ui_click"); }
        else if (selected >= 0) { placed[face] = selected; selected = -1; PixelAudio.Play("ui_click"); }
        Refresh();
    }

    private IEnumerator Forge()
    {
        forging = true;
        Refresh();
        float dur = forgeSeconds > 0f ? forgeSeconds : 1.6f, t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = t / dur;
            for (int f = 0; f < 6; f++)
            {
                if (faceGems[f] == null) yield break;
                float pulse = 1f + (0.25f + 0.5f * k) * Mathf.Sin(t * (10f + 20f * k) + f);
                faceGems[f].rectTransform.localScale = Vector3.one * pulse;
                faceImages[f].color = Color.Lerp(new Color(0.95f, 0.75f, 0.22f, 1f), Color.white, k * k);
            }
            yield return null;
        }

        // Use up the Gauntlet and the six stones, make the Cubelette.
        bool ok = consumables != null && consumables.TryRemoveItem(gauntletItem, 1);
        for (int i = 0; i < 6 && ok; i++) ok = clicker.TrySpend(PixelClicker.PixelType.StoneSpace + i, 1d);
        forging = false;
        if (!ok) { PixelHints.Announce("The Gauntlet could not be used up. Try again"); Refresh(); yield break; }
        int cub = clicker.IndexOf(PixelClicker.PixelType.InfinityCubelette);
        if (cub >= 0) clicker.AddCurrency(cub, 1d);
        PixelStats.Count("infinity.forged");
        Close();
        if (cub >= 0) PixelGodFind.PlayAtScreen(cub, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
    }
}
