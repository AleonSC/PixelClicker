// ---- System notes ----
// PixelUITheme.cs
//   The button ROLES (UI unification step 3): one Inspector place for what each kind of button looks like. A button is made with a role
//   (PixelUIKit.CreateButton(..., ButtonRole role, ...)) or switched to one later (PixelUIKit.SetRole - a tab turning Selected), and takes its
//   colour from here; 'follows' = it also takes the player's custom button colour (Settings > Interface > 'Custom UI button colour').
//   Roles: Neutral (ordinary buttons, the window X / header buttons, inactive tabs), Primary (the main action of a box: Buy, Play...),
//   Selected (the open tab / chosen item), Success (green: confirm, accept), Danger (red: quit, delete, reject). Neutral, Primary and
//   Selected follow the custom colour; Success and Danger keep their meaning colour. Buttons made with a plain colour are role Custom
//   (the old rule: only weakly coloured ones follow the custom colour). Changing a colour here in Play mode recolours every button at once.
// ---------------------------------------------------
using UnityEngine;

public enum ButtonRole { Custom, Neutral, Primary, Selected, Success, Danger }

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

    private void Awake() { instance = this; PixelUIKit.RefreshButtons(); }
    private void OnDestroy() { if (instance == this) instance = null; }
    private void OnValidate() { if (Application.isPlaying && instance == this) PixelUIKit.RefreshButtons(); }   // (tuning in Play mode shows at once)

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
