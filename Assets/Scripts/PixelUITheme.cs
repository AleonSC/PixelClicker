// ---- System notes ----
// PixelUITheme.cs
//   The button ROLES (UI unification step 3): one Inspector place for what each kind of button looks like. A button is made with a role
//   (PixelUIKit.CreateButton(..., ButtonRole role, ...)) or switched to one later (PixelUIKit.SetRole - a tab turning Selected), and takes its
//   colour from here; 'follows' = it also takes the player's custom button colour (Settings > Interface > 'Custom UI button colour').
//   Roles: Neutral (ordinary buttons, the window X / header buttons, inactive tabs), Primary (the main action of a box: Buy, Play...),
//   Selected (the open tab / chosen item), Success (green: confirm, accept), Danger (red: quit, delete, reject). Neutral, Primary and
//   Selected follow the custom colour; Success and Danger keep their meaning colour. Buttons made with a plain colour are role Custom
//   (the old rule: only weakly coloured ones follow the custom colour). Changing a colour here in Play mode recolours every button at once.
//   THE THEME (UI unification step 4): also the window backgrounds (windowColor for big windows / dialogs, toolWindowColor for tool windows -
//   PixelUIKit.StyleWindow(image, WindowShade) / CreateWindow without a colour), title colour, text / dim text colour (WindowColor... statics,
//   read when a window is built) and the highlight frame colour (PixelHighlight). Live: buttons, window backgrounds, titles. On rebuild: body text.
// ---------------------------------------------------
using UnityEngine;

public enum ButtonRole { Custom, Neutral, Primary, Selected, Success, Danger }

/// <summary>Which theme colour a window background takes (PixelUIKit.StyleWindow(image, shade) / CreateWindow without a colour).</summary>
public enum WindowShade { Window, ToolWindow }

public class PixelUITheme : MonoBehaviour
{
    [System.Serializable]
    public class RoleLook
    {
        [Tooltip("The button colour for this role.")]
        public Color color = Color.grey;
        [Tooltip("Also take the player's custom button colour (Settings > Interface > 'Custom UI button colour') when it is on.")]
        public bool followsCustomColor = true;
        public RoleLook(Color c, bool follows) { color = c; followsCustomColor = follows; }
    }

    [Header("Button roles")]
    [Tooltip("Ordinary buttons, the window X and header buttons, inactive tabs.")]
    [SerializeField] private RoleLook neutral = new RoleLook(new Color(0.3f, 0.3f, 0.35f, 1f), true);
    [Tooltip("The main action of a box (Buy, Play, Show...).")]
    [SerializeField] private RoleLook primary = new RoleLook(new Color(0.25f, 0.45f, 0.8f, 1f), true);
    [Tooltip("The open tab / the chosen item of a row of buttons.")]
    [SerializeField] private RoleLook selected = new RoleLook(new Color(0.2f, 0.45f, 0.9f, 1f), true);
    [Tooltip("Confirm / accept (green).")]
    [SerializeField] private RoleLook success = new RoleLook(new Color(0.2f, 0.55f, 0.3f, 1f), false);
    [Tooltip("Quit / delete / reject (red).")]
    [SerializeField] private RoleLook danger = new RoleLook(new Color(0.5f, 0.22f, 0.22f, 1f), false);

    [Header("Windows (backgrounds of every standard window; live)")]
    [Tooltip("Big windows and dialogs: Inventory, Log, Shop, Skills, Crafting, Bank, Toggles, Database, the pause menu...")]
    [SerializeField] private Color windowColor = new Color(0.1f, 0.1f, 0.13f, 0.97f);
    [Tooltip("Small tool windows: View, Pocket, Next up (a little bluer so they read as tools).")]
    [SerializeField] private Color toolWindowColor = new Color(0.1f, 0.12f, 0.2f, 0.95f);

    [Header("Text")]
    [Tooltip("Window titles (live).")]
    [SerializeField] private Color titleColor = Color.white;
    [Tooltip("Ordinary text in windows (taken when a window is built: shows after a restart).")]
    [SerializeField] private Color textColor = Color.white;
    [Tooltip("Quieter text: hints, captions, 'none yet' lines (taken when a window is built).")]
    [SerializeField] private Color dimTextColor = new Color(0.7f, 0.75f, 0.85f, 1f);

    [Header("Highlight")]
    [Tooltip("The pulsing frame that points the player at something (PixelHighlight: the Shop button, a skill card...).")]
    [SerializeField] private Color highlightColor = new Color(1f, 0.82f, 0.25f, 1f);

    private static readonly Color DefWindow = new Color(0.1f, 0.1f, 0.13f, 0.97f), DefTool = new Color(0.1f, 0.12f, 0.2f, 0.95f),
                                  DefDim = new Color(0.7f, 0.75f, 0.85f, 1f), DefHighlight = new Color(1f, 0.82f, 0.25f, 1f);
    public static Color WindowColor => instance != null ? instance.windowColor : DefWindow;
    public static Color ToolWindowColor => instance != null ? instance.toolWindowColor : DefTool;
    public static Color ColorOf(WindowShade shade) => shade == WindowShade.ToolWindow ? ToolWindowColor : WindowColor;
    public static Color TitleColor => instance != null ? instance.titleColor : Color.white;
    public static Color TextColor => instance != null ? instance.textColor : Color.white;
    public static Color DimTextColor => instance != null ? instance.dimTextColor : DefDim;
    public static Color HighlightColor => instance != null ? instance.highlightColor : DefHighlight;

    private static PixelUITheme instance;
    // The same values as the fields above, used before the component exists (or if it is missing).
    private static readonly RoleLook[] fallback =
    {
        new RoleLook(new Color(0.3f, 0.3f, 0.35f, 1f), false),   // Custom (unused)
        new RoleLook(new Color(0.3f, 0.3f, 0.35f, 1f), true),
        new RoleLook(new Color(0.25f, 0.45f, 0.8f, 1f), true),
        new RoleLook(new Color(0.2f, 0.45f, 0.9f, 1f), true),
        new RoleLook(new Color(0.2f, 0.55f, 0.3f, 1f), false),
        new RoleLook(new Color(0.5f, 0.22f, 0.22f, 1f), false),
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; }

    private void Awake() { instance = this; RefreshAll(); }
    private void OnDestroy() { if (instance == this) instance = null; }
    private void OnValidate() { if (Application.isPlaying && instance == this) RefreshAll(); }   // (tuning in Play mode shows at once)

    /// <summary>Recolours everything that follows the theme live: buttons, window backgrounds, window titles.</summary>
    private static void RefreshAll()
    {
        PixelUIKit.RefreshButtons();
        PixelUIKit.RefreshWindows();
        PixelUIKit.RefreshTitles();
    }

    private RoleLook Look(ButtonRole role)
    {
        switch (role)
        {
            case ButtonRole.Neutral: return neutral;
            case ButtonRole.Primary: return primary;
            case ButtonRole.Selected: return selected;
            case ButtonRole.Success: return success;
            case ButtonRole.Danger: return danger;
            default: return fallback[0];
        }
    }

    /// <summary>The colour of a role (before the custom button colour).</summary>
    public static Color ColorOf(ButtonRole role) => instance != null ? instance.Look(role).color : fallback[(int)role].color;

    /// <summary>Does this role take the player's custom button colour?</summary>
    public static bool Follows(ButtonRole role) => instance != null ? instance.Look(role).followsCustomColor : fallback[(int)role].followsCustomColor;
}
