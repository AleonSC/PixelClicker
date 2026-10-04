using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Developer cheat button for Pixel Clicker.
///
/// Adds a button in a screen corner (bottom-right by default). Each press immediately adds a set
/// amount (default 10) to every pixel type - both the current amount (the count panel / "pouch")
/// and the lifetime total shown in the Log. It goes through PixelClicker.AddCurrency, so tier
/// unlock thresholds, UI refreshes and events all react as if you had collected the pixels.
///
/// Add it to any GameObject. By default it removes itself in non-development builds.
/// </summary>
public class PixelDevTools : MonoBehaviour
{
    public enum ButtonCorner { TopLeft, TopRight, BottomLeft, BottomRight }

    [Header("References")]
    [Tooltip("The PixelClicker to add to. Found automatically if left empty.")]
    [SerializeField] private PixelClicker clicker;

    [Tooltip("Font for the button. Empty = TextMeshPro default font.")]
    [SerializeField] private TMP_FontAsset font;

    [Header("Cheat")]
    [Tooltip("Amount added to each pixel type per press.")]
    [SerializeField] private double amountToAdd = 10;

    [Tooltip("Also add to tiers that aren't unlocked yet (e.g. Red/Green/Blue before buying RGB). " +
             "Off = only unlocked tiers.")]
    [SerializeField] private bool includeLockedTiers = false;

    [Tooltip("Only exist in the Editor and Development Builds. In a release build the button removes itself.")]
    [SerializeField] private bool devBuildsOnly = true;

    [Header("Button")]
    [Tooltip("Corner the button sits in.")]
    [SerializeField] private ButtonCorner corner = ButtonCorner.BottomRight;

    [Tooltip("Distance from the screen edge (canvas units).")]
    [SerializeField] private Vector2 margin = new Vector2(30f, 30f);

    [Tooltip("Button size.")]
    [SerializeField] private Vector2 size = new Vector2(260f, 80f);

    [Tooltip("Button text. {0} = the amount.")]
    [SerializeField] private string label = "DEV: +{0} all";

    [Tooltip("Button text size.")]
    [SerializeField] private float fontSize = 32f;

    [Tooltip("Button background colour.")]
    [SerializeField] private Color buttonColor = new Color(0.8f, 0.25f, 0.25f, 1f);

    [Tooltip("Button text colour.")]
    [SerializeField] private Color textColor = Color.white;

    [Header("Canvas")]
    [Tooltip("Sorting order of the canvas.")]
    [SerializeField] private int sortingOrder = 300;

    [Tooltip("Reference resolution for the canvas scaler.")]
    [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);

    private GameObject canvasRoot;

    private void Start()
    {
        if (devBuildsOnly && !Debug.isDebugBuild)
        {
            Destroy(this);
            return;
        }

        if (clicker == null)
        {
#if UNITY_2023_1_OR_NEWER
            clicker = FindFirstObjectByType<PixelClicker>();
#else
            clicker = FindObjectOfType<PixelClicker>();
#endif
        }

        if (clicker == null)
        {
            Debug.LogError("PixelDevTools: no PixelClicker found in the scene.", this);
            enabled = false;
            return;
        }

        EnsureEventSystem();
        BuildButton();
    }

    private void OnDestroy()
    {
        if (canvasRoot != null) Destroy(canvasRoot);
    }

    /// <summary>Adds the amount to every (eligible) pixel type. Also callable from other scripts or a UnityEvent.</summary>
    public void AddToAll()
    {
        PixelClicker.PixelTier[] tiers = clicker.Tiers;
        for (int i = 0; i < tiers.Length; i++)
        {
            if (!includeLockedTiers && !tiers[i].unlocked) continue;
            clicker.AddCurrency(i, amountToAdd);
        }
    }

    // ------------------------------------------------------------------
    // UI
    // ------------------------------------------------------------------

    private static void EnsureEventSystem()
    {
#if UNITY_2023_1_OR_NEWER
        if (FindFirstObjectByType<EventSystem>() != null) return;
#else
        if (FindObjectOfType<EventSystem>() != null) return;
#endif
        GameObject es = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
        es.AddComponent<StandaloneInputModule>();
#endif
    }

    private void BuildButton()
    {
        canvasRoot = new GameObject("PixelDevTools Canvas");
        Canvas canvas = canvasRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = canvasRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.matchWidthOrHeight = 0.5f;

        canvasRoot.AddComponent<GraphicRaycaster>();

        GameObject go = new GameObject("Dev Button", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(canvasRoot.transform, false);

        Image image = go.GetComponent<Image>();
        image.color = buttonColor;
        go.GetComponent<Button>().targetGraphic = image;

        Vector2 anchor = new Vector2(
            corner == ButtonCorner.TopRight || corner == ButtonCorner.BottomRight ? 1f : 0f,
            corner == ButtonCorner.TopLeft || corner == ButtonCorner.TopRight ? 1f : 0f);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.sizeDelta = size;
        rt.anchoredPosition = new Vector2(anchor.x > 0.5f ? -margin.x : margin.x,
                                          anchor.y > 0.5f ? -margin.y : margin.y);

        GameObject textGo = new GameObject("Label", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);
        TextMeshProUGUI tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = string.Format(label, PixelClicker.FormatNumber(amountToAdd));
        tmp.fontSize = fontSize;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = textColor;
        tmp.raycastTarget = false;
        if (font != null) tmp.font = font;

        RectTransform tr = tmp.rectTransform;
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = tr.offsetMax = Vector2.zero;

        go.GetComponent<Button>().onClick.AddListener(AddToAll);
    }
}
