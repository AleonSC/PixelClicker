// ---- System notes ----
// PixelTextEditMode.cs
//   Dev Hub > 'Edit text mode' (a tick box widget in the Main tab): while it is on, a see-through layer over the whole screen catches the mouse,
//   the text under the pointer gets a yellow highlight, and a LEFT CLICK on any text opens a box to change it (multi-line, rich text tags shown
//   as typed). Save puts the new text everywhere that shows exactly the same text and writes it to Resources/TextEdits.txt (PixelTextEdits.cs:
//   it ships with the game); Reset brings the game's own text back; Cancel / Escape closes the box. A text that is already edited shows its
//   original under the title. The mouse wheel still scrolls the list under the pointer. The banner at the top (Done) or Escape ends the mode;
//   game clicks, buttons and the cube are blocked meanwhile. Only UI texts (on a canvas) can be picked; texts inside input fields cannot.
//   Texts are found with PixelUIKit.TextUnderPointer (the same picker as the Dev Hub's own right-click label editing, PixelDevTextEdit).
// ---------------------------------------------------
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PixelTextEditMode : MonoBehaviour
{
    [Header("Look")]
    [Tooltip("The banner at the top of the screen while the mode is on.")]
    [SerializeField] private string bannerText = "EDIT TEXT MODE - click any text to change it";
    [Tooltip("Hint under the edit field.")]
    [SerializeField] private string hintText = "Changes every place that shows exactly this text. Saved to Resources/TextEdits.txt.";
    [Tooltip("Colour of the highlight over the text under the pointer.")]
    [SerializeField] private Color highlightColor = new Color(1f, 0.85f, 0.2f, 0.28f);
    [Tooltip("Sorting order of the mode's canvas (above every game window and the Dev Hub).")]
    [SerializeField] private int sortingOrder = 32500;
    [Min(0.1f)]
    [Tooltip("Seconds between looking for the texts on screen again (new windows, new labels).")]
    [SerializeField] private float rescanSeconds = 0.5f;

    private static PixelTextEditMode instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; Changed = null; }

    /// <summary>Is the mode on?</summary>
    public static bool IsOn => instance != null && instance.canvasObject != null && instance.canvasObject.activeSelf;

    /// <summary>Fires when the mode goes on or off (the Dev Hub tick box follows it).</summary>
    public static event System.Action<bool> Changed;

    /// <summary>Turns the mode on or off (the Dev Hub tick box).</summary>
    public static void SetOn(bool on)
    {
        if (on && instance == null)
        {
            GameObject go = new GameObject("Pixel Text Edit Mode");
            PixelTextEdits edits = PixelFind.Cached<PixelTextEdits>();
            if (edits != null) go.transform.SetParent(edits.transform, false);
            instance = go.AddComponent<PixelTextEditMode>();
        }
        if (instance == null) return;
        instance.Apply(on);
    }

    private GameObject canvasObject;
    private RectTransform canvasRect, highlight, box;
    private TMP_FontAsset font;
    private TMP_Text originalLabel;
    private TMP_InputField input;
    private Button resetButton;
    private TMP_Text editing;          // the text being edited
    private string editingOriginal;    // the game's own text behind it
    private TMP_Text[] candidates = new TMP_Text[0];
    private float rescanTimer;
    private readonly object modeOwner = new object();   // (Escape: the box closes first, then the mode ends)
    private readonly List<RaycastResult> hits = new List<RaycastResult>();
    private readonly Vector3[] corners = new Vector3[4];

    private void OnDestroy()
    {
        PixelWindows.Unregister(this);
        PixelWindows.Unregister(modeOwner);
        if (canvasObject != null) Destroy(canvasObject);
        if (instance == this) instance = null;
    }

    private void Apply(bool on)
    {
        if (on && canvasObject == null) Build();
        if (canvasObject == null || canvasObject.activeSelf == on) return;
        canvasObject.SetActive(on);
        CloseBox();
        rescanTimer = 0f;
        Changed?.Invoke(on);
    }

    // ---------------- building ----------------

    private void Build()
    {
        PixelClicker clicker = PixelFind.Cached<PixelClicker>();
        font = clicker != null ? clicker.UIFont : null;
        PixelUIKit.EnsureEventSystem();
        canvasObject = PixelUIKit.CreateCanvas("Edit Text Mode", sortingOrder, new Vector2(1920f, 1080f), true);
        canvasObject.transform.SetParent(transform, false);
        canvasRect = canvasObject.GetComponent<RectTransform>();

        // The see-through layer: catches every click so nothing in the game reacts while texts are picked.
        GameObject blocker = new GameObject("Blocker", typeof(RectTransform), typeof(Image));
        blocker.transform.SetParent(canvasObject.transform, false);
        blocker.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
        PixelUIKit.Stretch(blocker.GetComponent<RectTransform>());

        GameObject hl = new GameObject("Highlight", typeof(RectTransform), typeof(Image));
        hl.transform.SetParent(canvasObject.transform, false);
        Image hli = hl.GetComponent<Image>();
        hli.color = highlightColor;
        hli.raycastTarget = false;
        highlight = hl.GetComponent<RectTransform>();
        highlight.anchorMin = highlight.anchorMax = new Vector2(0.5f, 0.5f);
        highlight.pivot = new Vector2(0f, 0f);
        hl.SetActive(false);

        BuildBanner();
        BuildBox();
        PixelWindows.Register(this, 241, () => box != null && box.gameObject.activeSelf, CloseBox);   // escape-only: the edit box
        PixelWindows.Register(modeOwner, 240, () => IsOn, () => Apply(false));                       // escape-only: the mode itself
        canvasObject.SetActive(false);
    }

    private void BuildBanner()
    {
        GameObject bar = new GameObject("Banner", typeof(RectTransform), typeof(Image));
        bar.transform.SetParent(canvasObject.transform, false);
        PixelUIKit.StyleWindow(bar.GetComponent<Image>(), new Color(0.12f, 0.1f, 0.02f, 0.95f));
        RectTransform br = bar.GetComponent<RectTransform>();
        br.anchorMin = br.anchorMax = br.pivot = new Vector2(0.5f, 1f);
        br.sizeDelta = new Vector2(760f, 62f);
        br.anchoredPosition = new Vector2(0f, -12f);
        TMP_Text label = PixelUIKit.CreateText(font, br, "Text", bannerText, 24f, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, new Color(1f, 0.88f, 0.35f, 1f));
        label.raycastTarget = false;
        PlaceTL(label.rectTransform, 18f, 6f, 580f, 50f);
        PixelUIKit.FitOneLine(label, 14f);
        Button done = PixelUIKit.CreateButton(font, br, "Done", "Done", new Vector2(140f, 44f), ButtonRole.Success, 22f);
        PlaceTL(done.GetComponent<RectTransform>(), 608f, 9f, 140f, 44f);
        done.onClick.AddListener(() => Apply(false));
    }

    private void BuildBox()
    {
        GameObject go = new GameObject("Edit Box", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(canvasObject.transform, false);
        PixelUIKit.StyleWindow(go.GetComponent<Image>(), new Color(0.1f, 0.12f, 0.2f, 0.99f));
        box = go.GetComponent<RectTransform>();
        box.anchorMin = box.anchorMax = box.pivot = new Vector2(0.5f, 0.5f);
        box.sizeDelta = new Vector2(640f, 372f);

        TMP_Text title = PixelUIKit.CreateText(font, box, "Title", "EDIT TEXT", 24f, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, Color.white);
        title.raycastTarget = false;
        PlaceTL(title.rectTransform, 18f, 10f, 600f, 32f);
        originalLabel = PixelUIKit.CreateText(font, box, "Original", "", 17f, TextAlignmentOptions.TopLeft, FontStyles.Italic, new Color(0.7f, 0.76f, 0.9f, 1f));
        originalLabel.raycastTarget = false;
        originalLabel.richText = false;
        originalLabel.overflowMode = TextOverflowModes.Ellipsis;
        PlaceTL(originalLabel.rectTransform, 18f, 44f, 604f, 44f);

        input = PixelUIKit.CreateInputField(font, box, "Edit Field", new Vector2(604f, 150f), new Color(0.05f, 0.06f, 0.09f, 1f), Color.white, 22f, "text");
        input.lineType = TMP_InputField.LineType.MultiLineNewline;
        input.richText = false;   // (tags show as typed, so they can be edited)
        if (input.textComponent != null) { input.textComponent.richText = false; input.textComponent.alignment = TextAlignmentOptions.TopLeft; input.textComponent.textWrappingMode = TextWrappingModes.Normal; }
        PlaceTL(input.GetComponent<RectTransform>(), 18f, 92f, 604f, 150f);

        TMP_Text hint = PixelUIKit.CreateText(font, box, "Hint", hintText, 16f, TextAlignmentOptions.TopLeft, FontStyles.Normal, new Color(0.7f, 0.76f, 0.9f, 1f));
        hint.raycastTarget = false;
        PlaceTL(hint.rectTransform, 18f, 250f, 604f, 44f);

        MakeButton("Save", ButtonRole.Success, 18f, Save);
        resetButton = MakeButton("Reset", ButtonRole.Danger, 222f, ResetText);
        MakeButton("Cancel", ButtonRole.Neutral, 426f, CloseBox);
        box.gameObject.SetActive(false);
    }

    private Button MakeButton(string label, ButtonRole role, float x, UnityEngine.Events.UnityAction action)
    {
        Button b = PixelUIKit.CreateButton(font, box, label, label, new Vector2(196f, 48f), role, 22f);
        PlaceTL(b.GetComponent<RectTransform>(), x, 306f, 196f, 48f);
        b.onClick.AddListener(action);
        return b;
    }

    private static void PlaceTL(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(x, -y);
    }

    // ---------------- picking ----------------

    private bool Pickable(TMP_Text label)
    {
        if (label.transform.IsChildOf(canvasObject.transform)) return false;              // (our own banner / box)
        if (label.GetComponentInParent<TMP_InputField>() != null) return false;            // (typed text)
        if (label.color.a < 0.05f || label.canvasRenderer.GetInheritedAlpha() < 0.05f) return false;   // (hidden / faded away)
        return true;
    }

    private void Update()
    {
        if (!IsOn) return;
        if (PixelGameMode.OnTitle) { Apply(false); return; }
        rescanTimer -= Time.unscaledDeltaTime;
        if (rescanTimer <= 0f) { rescanTimer = rescanSeconds; candidates = PixelFind.All<TMP_Text>(); }

        bool boxOpen = box.gameObject.activeSelf;
        bool overBox = boxOpen && RectTransformUtility.RectangleContainsScreenPoint(box, PixelInput.PointerPosition(), null);
        TMP_Text under = overBox ? null : PixelUIKit.TextUnderPointer(candidates, Pickable);
        ShowHighlight(boxOpen && editing != null ? editing : under);

        if (!overBox) ForwardScroll();
        if (PixelInput.LeftPressed() && !overBox && under != null) OpenBox(under);
    }

    // The mouse wheel goes to the scroll list under the layer (the layer itself would swallow it).
    private void ForwardScroll()
    {
        float wheel = PixelInput.ScrollY();
        if (Mathf.Abs(wheel) < 0.001f || EventSystem.current == null) return;
        PointerEventData data = new PointerEventData(EventSystem.current) { position = PixelInput.PointerPosition(), scrollDelta = new Vector2(0f, wheel) };
        hits.Clear();
        EventSystem.current.RaycastAll(data, hits);
        foreach (RaycastResult hit in hits)
        {
            if (hit.gameObject == null || hit.gameObject.transform.IsChildOf(canvasObject.transform)) continue;
            ExecuteEvents.ExecuteHierarchy(hit.gameObject, data, ExecuteEvents.scrollHandler);
            return;
        }
    }

    // The yellow box over a text (its rectangle on screen, turned into this canvas's units).
    private void ShowHighlight(TMP_Text label)
    {
        bool on = label != null && label.canvas != null;
        if (highlight.gameObject.activeSelf != on) highlight.gameObject.SetActive(on);
        if (!on) return;
        Canvas root = label.canvas.rootCanvas;
        Camera cam = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
        label.rectTransform.GetWorldCorners(corners);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, RectTransformUtility.WorldToScreenPoint(cam, corners[0]), null, out Vector2 min);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, RectTransformUtility.WorldToScreenPoint(cam, corners[2]), null, out Vector2 max);
        highlight.anchoredPosition = min - new Vector2(3f, 3f);
        highlight.sizeDelta = max - min + new Vector2(6f, 6f);
    }

    // ---------------- the box ----------------

    private void OpenBox(TMP_Text label)
    {
        editing = label;
        editingOriginal = PixelTextEdits.OriginalOf(label.text);
        bool edited = PixelTextEdits.IsEdited(label.text);
        PixelUIKit.SetText(originalLabel, edited ? "Original: " + editingOriginal.Replace("\n", " / ") : "The game's own text");
        resetButton.interactable = edited;
        input.text = label.text;
        box.gameObject.SetActive(true);
        box.SetAsLastSibling();
        // Beside the pointer, kept on the screen.
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, PixelInput.PointerPosition(), null, out Vector2 local))
        {
            Vector2 half = canvasRect.rect.size * 0.5f, size = box.sizeDelta;
            local += new Vector2(size.x * 0.5f + 30f, 0f);
            if (local.x + size.x * 0.5f > half.x - 10f) local.x -= size.x + 60f;
            local.x = Mathf.Clamp(local.x, -half.x + size.x * 0.5f + 10f, half.x - size.x * 0.5f - 10f);
            local.y = Mathf.Clamp(local.y, -half.y + size.y * 0.5f + 10f, half.y - size.y * 0.5f - 90f);
            box.anchoredPosition = local;
        }
        input.Select();
        input.ActivateInputField();
    }

    private void Save()
    {
        if (editing != null && editingOriginal != null)
        {
            PixelTextEdits.Set(editingOriginal, input.text);
            if (editing != null) editing.text = PixelTextEdits.Apply(editingOriginal);
            PixelAudio.Play("ui_click");
        }
        CloseBox();
    }

    private void ResetText()
    {
        if (editingOriginal != null) PixelTextEdits.Remove(editingOriginal);
        if (editing != null) editing.text = editingOriginal;
        CloseBox();
    }

    private void CloseBox()
    {
        if (box != null) box.gameObject.SetActive(false);
        editing = null;
        editingOriginal = null;
    }
}
