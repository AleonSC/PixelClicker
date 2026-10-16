// ---- System notes ----
// PixelPetsMenu.cs
//   Part of partial class PixelPets: the pet RIGHT-CLICK MENU. Right-click a pet on the field and a small menu opens at the pointer (the pet's name + 'Pet away');
//   'Pet away' switches that pet off (SetOn false, the same as its switch in the Toggles window's Pets tab - turn it back on there) with a poof sound.
//   A left / right click anywhere else, Escape, the pet vanishing or a menu opening closes it. While it is open, clicks don't reach the cube.
// ---------------------------------------------------
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class PixelPets
{
    [Header("Right-click menu")]
    [Tooltip("Text of the menu button that sends the pet away (switches it off).")]
    [SerializeField] private string petAwayText = "Pet away";

    private GameObject menuCanvas;
    private RectTransform menuPanel;
    private TMP_Text menuTitle;
    private Pet menuPet;
    private int menuOpenedFrame = -1;

    private bool MenuOpen => menuPet != null && menuCanvas != null && menuCanvas.activeSelf;

    /// <summary>Called each frame from UpdatePointer: opens the menu on a right-click on a pet, closes it on a click elsewhere.</summary>
    private void UpdatePetMenu(bool allowed)
    {
        if (MenuOpen)
        {
            bool gone = menuPet.off || menuPet.body == null || !menuPet.body.activeInHierarchy || !allowed || PixelCalm.InMenu;
            bool clickAway = Time.frameCount != menuOpenedFrame && (PixelInput.LeftPressed() || PixelInput.RightPressed())
                             && !RectTransformUtility.RectangleContainsScreenPoint(menuPanel, PixelInput.PointerPosition(), null);
            if (gone || clickAway || PixelKeys.KeyPressed(KeyCode.Escape)) CloseMenu();
        }
        if (!MenuOpen && allowed && held == null && hovered != null && PixelInput.RightPressed()) OpenMenu(hovered);
    }

    private void OpenMenu(Pet p)
    {
        if (menuCanvas == null) BuildMenu();
        menuPet = p;
        menuOpenedFrame = Time.frameCount;
        PixelUIKit.SetText(menuTitle, NameOf(p));
        menuCanvas.SetActive(true);
        RectTransform canvasRect = (RectTransform)menuCanvas.transform;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, PixelInput.PointerPosition(), null, out Vector2 local))
        {
            Rect r = canvasRect.rect;
            Vector2 size = menuPanel.sizeDelta;
            local.x = Mathf.Clamp(local.x + 12f, r.xMin + 8f, r.xMax - size.x - 8f);   // beside the pointer, kept on screen
            local.y = Mathf.Clamp(local.y - 8f, r.yMin + size.y + 8f, r.yMax - 8f);
            menuPanel.anchoredPosition = local;
        }
        PixelAudio.Play("ui_click");
    }

    private void CloseMenu()
    {
        menuPet = null;
        if (menuCanvas != null) menuCanvas.SetActive(false);
    }

    private void BuildMenu()
    {
        PixelUIKit.EnsureEventSystem();
        menuCanvas = PixelUIKit.CreateCanvas("Pet Menu", 640, new Vector2(1920f, 1080f), true);
        GameObject panel = new GameObject("Menu", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(menuCanvas.transform, false);
        PixelUIKit.StyleWindow(panel.GetComponent<Image>(), new Color(0.1f, 0.12f, 0.18f, 0.97f));
        menuPanel = panel.GetComponent<RectTransform>();
        menuPanel.anchorMin = menuPanel.anchorMax = new Vector2(0.5f, 0.5f);
        menuPanel.pivot = new Vector2(0f, 1f);
        menuPanel.sizeDelta = new Vector2(230f, 112f);

        menuTitle = PixelUIKit.CreateText(font, panel.transform, "Name", "", 22f, TextAlignmentOptions.Center, FontStyles.Bold, new Color(1f, 0.85f, 0.4f, 1f));
        menuTitle.enableAutoSizing = true; menuTitle.fontSizeMax = 22f; menuTitle.fontSizeMin = 12f;
        menuTitle.textWrappingMode = TextWrappingModes.NoWrap;
        RectTransform tr = menuTitle.rectTransform;
        tr.anchorMin = new Vector2(0f, 1f); tr.anchorMax = new Vector2(1f, 1f); tr.pivot = new Vector2(0.5f, 1f);
        tr.sizeDelta = new Vector2(-20f, 36f); tr.anchoredPosition = new Vector2(0f, -8f);

        Button away = PixelUIKit.CreateButton(font, panel.transform, "Pet Away", petAwayText, new Vector2(200f, 52f), new Color(0.5f, 0.25f, 0.25f, 1f), Color.white, 24f);
        RectTransform ar = away.GetComponent<RectTransform>();
        ar.anchorMin = ar.anchorMax = ar.pivot = new Vector2(0.5f, 0f);
        ar.anchoredPosition = new Vector2(0f, 12f);
        away.onClick.AddListener(() =>
        {
            Pet p = menuPet;
            CloseMenu();
            if (p == null) return;
            PixelAudio.Play("device_remove");
            SetOn(p.type, false);   // the same as its switch in Toggles > Pets (turn it back on there)
            PixelHints.Announce(NameOf(p) + " went home (Toggles > Pets brings it back)");
        });
        menuCanvas.SetActive(false);
    }
}
