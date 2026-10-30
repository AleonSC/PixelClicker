// Compile-check stubs for the Unity packages that are not on NuGet (UnityEngine.UI, EventSystems, TextMeshPro).
// ONLY signatures, no behaviour: they let a normal C# compiler check our own code (names, arguments, types) without Unity.
// They are NOT part of the game (this folder is outside Assets). Extend them when the check reports a missing UI / TMP member.
#pragma warning disable
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace UnityEngine.EventSystems
{
    public interface IPointerDownHandler { void OnPointerDown(PointerEventData eventData); }
    public interface IPointerUpHandler { void OnPointerUp(PointerEventData eventData); }
    public interface IPointerEnterHandler { void OnPointerEnter(PointerEventData eventData); }
    public interface IPointerExitHandler { void OnPointerExit(PointerEventData eventData); }
    public interface IPointerClickHandler { void OnPointerClick(PointerEventData eventData); }
    public interface IDragHandler { void OnDrag(PointerEventData eventData); }
    public interface IBeginDragHandler { void OnBeginDrag(PointerEventData eventData); }
    public interface IEndDragHandler { void OnEndDrag(PointerEventData eventData); }
    public interface IScrollHandler { void OnScroll(PointerEventData eventData); }
    public class UIBehaviour : MonoBehaviour { }
    public class BaseEventData { public GameObject selectedObject; public void Use() { } }
    public static class ExecuteEvents
    {
        public delegate void EventFunction<T1>(T1 handler, BaseEventData eventData);
        public static EventFunction<IScrollHandler> scrollHandler => null;
        public static GameObject ExecuteHierarchy<T>(GameObject root, BaseEventData eventData, EventFunction<T> callbackFunction) => null;
        public static bool Execute<T>(GameObject target, BaseEventData eventData, EventFunction<T> functor) => false;
    }
    public class PointerEventData : BaseEventData
    {
        public enum InputButton { Left, Right, Middle }
        public InputButton button;
        public Vector2 position, delta, scrollDelta, pressPosition;
        public GameObject pointerEnter, pointerPress, pointerDrag, pointerCurrentRaycast2;
        public int clickCount; public bool dragging; public Camera pressEventCamera, enterEventCamera;
        public PointerEventData(EventSystem es) { }
    }
    public struct RaycastResult { public GameObject gameObject; public float distance; public Vector3 worldPosition; public int sortingOrder, depth; }
    public class EventSystem : UIBehaviour
    {
        public static EventSystem current;
        public GameObject currentSelectedGameObject;
        public bool IsPointerOverGameObject() => false;
        public bool IsPointerOverGameObject(int pointerId) => false;
        public void RaycastAll(PointerEventData eventData, List<RaycastResult> raycastResults) { }
        public void SetSelectedGameObject(GameObject selected) { }
        public bool sendNavigationEvents; public int pixelDragThreshold;
    }
    public class BaseInputModule : UIBehaviour { }
    public class PointerInputModule : BaseInputModule { }
    public class StandaloneInputModule : PointerInputModule { }
    public enum EventTriggerType { PointerEnter, PointerExit, PointerDown, PointerUp, PointerClick, Drag, Drop, Scroll, UpdateSelected, Select, Deselect, Move, InitializePotentialDrag, BeginDrag, EndDrag, Submit, Cancel }
    public class EventTrigger : UIBehaviour
    {
        [Serializable] public class TriggerEvent : UnityEvent<BaseEventData> { }
        [Serializable] public class Entry { public EventTriggerType eventID; public TriggerEvent callback = new TriggerEvent(); }
        public List<Entry> triggers = new List<Entry>();
    }
}

namespace UnityEngine.UI
{
    using UnityEngine.EventSystems;
    public class VertexHelper : IDisposable
    {
        public int currentVertCount, currentIndexCount;
        public void Clear() { }
        public void AddVert(Vector3 position, Color32 color, Vector2 uv0) { }
        public void AddVert(UIVertex v) { }
        public void AddTriangle(int a, int b, int c) { }
        public void AddUIVertexQuad(UIVertex[] verts) { }
        public void AddVert(Vector3 position, Color32 color, Vector2 uv0, Vector2 uv1, Vector3 normal, Vector4 tangent) { }
        public void Dispose() { }
        public void FillMesh(Mesh m) { }
    }
    public abstract class Graphic : UIBehaviour
    {
        public virtual Color color { get; set; }
        public virtual bool raycastTarget { get; set; }
        public virtual Material material { get; set; }
        public virtual Material materialForRendering => null;
        public virtual Texture mainTexture => null;
        public RectTransform rectTransform => null;
        public Canvas canvas => null;
        public CanvasRenderer canvasRenderer => null;
        public virtual void SetAllDirty() { }
        public virtual void SetVerticesDirty() { }
        public virtual void SetMaterialDirty() { }
        public virtual void SetLayoutDirty() { }
        public virtual void CrossFadeAlpha(float alpha, float duration, bool ignoreTimeScale) { }
        public virtual void CrossFadeColor(Color targetColor, float duration, bool ignoreTimeScale, bool useAlpha) { }
        public virtual void Rebuild(CanvasUpdate update) { }
        protected virtual void OnPopulateMesh(VertexHelper vh) { }
        protected virtual void OnEnable() { }
        protected virtual void OnDisable() { }
        protected virtual void Awake() { }
        protected virtual void Start() { }
        protected virtual void OnDestroy() { }
        protected virtual void OnRectTransformDimensionsChange() { }
        protected virtual void UpdateMaterial() { }
        protected virtual void UpdateGeometry() { }
        public virtual Material defaultMaterial => null;
        public Rect GetPixelAdjustedRect() => default;
    }
    public enum CanvasUpdate { Prelayout, Layout, PostLayout, PreRender, LatePreRender, MaxUpdateValue }
    public abstract class MaskableGraphic : Graphic { public bool maskable; }
    public class Image : MaskableGraphic
    {
        public enum Type { Simple, Sliced, Tiled, Filled }
        public enum FillMethod { Horizontal, Vertical, Radial90, Radial180, Radial360 }
        public enum Origin360 { Bottom, Right, Top, Left }
        public enum Origin90 { BottomLeft, BottomRight, TopLeft, TopRight }
        public enum Origin180 { Bottom, Left, Top, Right }
        public enum OriginHorizontal { Left, Right }
        public enum OriginVertical { Bottom, Top }
        public Sprite sprite; public Sprite overrideSprite; public Type type; public bool preserveAspect; public bool fillCenter = true;
        public FillMethod fillMethod; public float fillAmount; public bool fillClockwise; public int fillOrigin;
        public float pixelsPerUnitMultiplier; public float pixelsPerUnit; public bool useSpriteMesh;
        public void SetNativeSize() { }
    }
    public class RawImage : MaskableGraphic { public Texture texture; public Rect uvRect; public void SetNativeSize() { } }
    public class Shadow : UIBehaviour { public Color effectColor; public Vector2 effectDistance; public bool useGraphicAlpha; }
    public class Outline : Shadow { }
    public class RectMask2D : UIBehaviour { public Vector4 padding; public Vector2Int softness; }
    public class Mask : UIBehaviour { public bool showMaskGraphic; }
    public class GraphicRaycaster : UIBehaviour { public bool ignoreReversedGraphics; }
    public class CanvasScaler : UIBehaviour
    {
        public enum ScaleMode { ConstantPixelSize, ScaleWithScreenSize, ConstantPhysicalSize }
        public enum ScreenMatchMode { MatchWidthOrHeight, Expand, Shrink }
        public ScaleMode uiScaleMode; public Vector2 referenceResolution; public ScreenMatchMode screenMatchMode; public float matchWidthOrHeight; public float scaleFactor; public float referencePixelsPerUnit;
    }
    public enum ColorBlockTransition { }
    [Serializable] public struct ColorBlock { public Color normalColor, highlightedColor, pressedColor, selectedColor, disabledColor; public float colorMultiplier, fadeDuration; public static ColorBlock defaultColorBlock; }
    public struct Navigation { public enum Mode { None, Horizontal, Vertical, Automatic, Explicit } public Mode mode; }
    public class Selectable : UIBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public enum Transition { None, ColorTint, SpriteSwap, Animation }
        public bool interactable; public Graphic targetGraphic; public Transition transition; public ColorBlock colors; public Navigation navigation;
        public virtual void OnPointerEnter(PointerEventData e) { } public virtual void OnPointerExit(PointerEventData e) { }
        public void Select() { }
        protected virtual void OnEnable() { } protected virtual void OnDisable() { } protected virtual void Awake() { } protected virtual void Start() { }
        public virtual bool IsInteractable() => true;
    }
    public class Button : Selectable, IPointerClickHandler
    {
        [Serializable] public class ButtonClickedEvent : UnityEvent { }
        public ButtonClickedEvent onClick = new ButtonClickedEvent();
        public virtual void OnPointerClick(PointerEventData e) { }
    }
    public class ToggleGroup : UIBehaviour { public bool allowSwitchOff; }
    public class Toggle : Selectable
    {
        [Serializable] public class ToggleEvent : UnityEvent<bool> { }
        public bool isOn; public Graphic graphic; public ToggleGroup group; public ToggleEvent onValueChanged = new ToggleEvent();
        public void SetIsOnWithoutNotify(bool value) { }
    }
    public class Slider : Selectable
    {
        public enum Direction { LeftToRight, RightToLeft, BottomToTop, TopToBottom }
        [Serializable] public class SliderEvent : UnityEvent<float> { }
        public float value, minValue, maxValue, normalizedValue; public bool wholeNumbers; public Direction direction; public RectTransform fillRect, handleRect;
        public SliderEvent onValueChanged = new SliderEvent();
        public void SetValueWithoutNotify(float input) { }
        public void SetDirection(Direction d, bool includeRectLayouts) { }
    }
    public class Scrollbar : Selectable
    {
        public enum Direction { LeftToRight, RightToLeft, BottomToTop, TopToBottom }
        [Serializable] public class ScrollEvent : UnityEvent<float> { }
        public float value, size; public int numberOfSteps; public RectTransform handleRect; public Direction direction; public ScrollEvent onValueChanged = new ScrollEvent();
        public void SetValueWithoutNotify(float input) { }
    }
    public class ScrollRect : UIBehaviour, IBeginDragHandler, IEndDragHandler, IDragHandler, IScrollHandler
    {
        public enum MovementType { Unrestricted, Elastic, Clamped }
        public enum ScrollbarVisibility { Permanent, AutoHide, AutoHideAndExpandViewport }
        [Serializable] public class ScrollRectEvent : UnityEvent<Vector2> { }
        public RectTransform content, viewport; public bool horizontal, vertical, inertia; public MovementType movementType; public float elasticity, decelerationRate, scrollSensitivity;
        public Scrollbar horizontalScrollbar, verticalScrollbar; public ScrollbarVisibility verticalScrollbarVisibility, horizontalScrollbarVisibility;
        public float horizontalNormalizedPosition, verticalNormalizedPosition; public Vector2 normalizedPosition, velocity; public ScrollRectEvent onValueChanged = new ScrollRectEvent();
        public RectTransform viewRect => null; public float verticalScrollbarSpacing, horizontalScrollbarSpacing;
        public void StopMovement() { }
        public virtual void OnBeginDrag(PointerEventData e) { } public virtual void OnEndDrag(PointerEventData e) { } public virtual void OnDrag(PointerEventData e) { } public virtual void OnScroll(PointerEventData e) { }
    }
    public class LayoutElement : UIBehaviour { public float minWidth, minHeight, preferredWidth, preferredHeight, flexibleWidth, flexibleHeight; public bool ignoreLayout; }
    public class LayoutGroup : UIBehaviour { public RectOffset padding; public TextAnchor childAlignment; }
    public class HorizontalOrVerticalLayoutGroup : LayoutGroup { public float spacing; public bool childForceExpandWidth, childForceExpandHeight, childControlWidth, childControlHeight; }
    public class HorizontalLayoutGroup : HorizontalOrVerticalLayoutGroup { }
    public class VerticalLayoutGroup : HorizontalOrVerticalLayoutGroup { }
    public class ContentSizeFitter : UIBehaviour { public enum FitMode { Unconstrained, MinSize, PreferredSize } public FitMode horizontalFit, verticalFit; }
    public static class LayoutRebuilder { public static void ForceRebuildLayoutImmediate(RectTransform r) { } public static void MarkLayoutForRebuild(RectTransform r) { } }
}

namespace TMPro
{
    using UnityEngine.EventSystems;
    public enum TextAlignmentOptions { TopLeft = 257, Top = 258, TopRight = 260, TopJustified = 264, Left = 513, Center = 514, Right = 516, Justified = 520, BottomLeft = 1025, Bottom = 1026, BottomRight = 1028, BottomJustified = 1032, MidlineLeft = 4097, Midline = 4098, MidlineRight = 4100, MidlineJustified = 4104, CaplineLeft = 8193, Capline = 8194, CaplineRight = 8196, BaselineLeft = 2049, Baseline = 2050, BaselineRight = 2052, TopFlush = 272, MidlineFlush = 4112, BottomFlush = 1040 }
    [Flags] public enum FontStyles { Normal = 0, Bold = 1, Italic = 2, Underline = 4, LowerCase = 8, UpperCase = 16, SmallCaps = 32, Strikethrough = 64, Superscript = 128, Subscript = 256 }
    public enum TextOverflowModes { Overflow, Ellipsis, Masking, Truncate, ScrollRect, Page, Linked }
    public enum TextWrappingModes { NoWrap, Normal, PreserveWhitespace, PreserveWhitespaceNoWrap }
    public enum TextureMappingOptions { Character, Line, Paragraph, MatchAspect }
    public class TMP_FontAsset : ScriptableObject { public Texture2D atlasTexture; public Material material; public List<TMP_FontAsset> fallbackFontAssetTable; public static TMP_FontAsset CreateFontAsset(Font font) => null; }
    public struct TMP_CharacterInfo { public char character; public bool isVisible; public Vector3 topLeft, bottomLeft, topRight, bottomRight; public int vertexIndex, materialReferenceIndex, lineNumber; public float xAdvance, ascender, descender, baseLine; public int index; public float origin; }
    public struct TMP_LineInfo { public int characterCount, firstCharacterIndex, lastCharacterIndex; public float width, ascender, descender, lineHeight; }
    public struct TMP_MeshInfo { public Vector3[] vertices; public Color32[] colors32; public Vector2[] uvs0; }
    public class TMP_TextInfo { public int characterCount, lineCount, wordCount, pageCount, materialCount, linkCount; public TMP_CharacterInfo[] characterInfo; public TMP_LineInfo[] lineInfo; public TMP_MeshInfo[] meshInfo; public TMP_LinkInfo[] linkInfo; }
    public struct TMP_LinkInfo { public int linkIdFirstCharacterIndex, linkIdLength, linkTextfirstCharacterIndex, linkTextLength; public string GetLinkID() => null; public string GetLinkText() => null; }
    public static class TMP_TextUtilities
    {
        public static int FindIntersectingLink(TMP_Text text, UnityEngine.Vector3 position, UnityEngine.Camera camera) => -1;
        public static int FindIntersectingCharacter(TMP_Text text, UnityEngine.Vector3 position, UnityEngine.Camera camera, bool visibleOnly) => -1;
    }
    public abstract class TMP_Text : MaskableGraphic
    {
        public virtual string text { get; set; }
        public float alpha;
        public float fontSize, fontSizeMin, fontSizeMax; public TMP_FontAsset font; public Material fontMaterial, fontSharedMaterial; public FontStyles fontStyle; public bool enableAutoSizing; public bool richText; public bool enableWordWrapping;
        public TextWrappingModes textWrappingMode; public TextOverflowModes overflowMode; public TextAlignmentOptions alignment; public int maxVisibleCharacters, maxVisibleLines, firstVisibleCharacter, pageToDisplay;
        public float lineSpacing, characterSpacing, wordSpacing, paragraphSpacing, margin2; public Vector4 margin; public bool isTextObjectScaleStatic, parseCtrlCharacters, extraPadding, enableKerning, isOrthographic, enableVertexGradient, overrideColorTags, isRightToLeftText;
        public Color faceColor; public Color32 outlineColor; public float outlineWidth; public Vector2 fontSizeDelta;
        public TMP_TextInfo textInfo; public bool isTextTruncated; public int characterCount; public float preferredWidth, preferredHeight, flexibleWidth, flexibleHeight, minWidth, minHeight; public Bounds textBounds, bounds;
        public Vector2 GetPreferredValues() => default; public Vector2 GetPreferredValues(string text) => default; public Vector2 GetPreferredValues(float width, float height) => default; public Vector2 GetPreferredValues(string text, float width, float height) => default;
        public Vector2 GetRenderedValues() => default; public Vector2 GetRenderedValues(bool onlyVisibleCharacters) => default;
        public virtual void ForceMeshUpdate() { } public virtual void ForceMeshUpdate(bool ignoreActiveState) { } public virtual void ForceMeshUpdate(bool ignoreActiveState, bool forceTextReparsing) { }
        public void SetText(string text) { } public void SetText(string text, bool syncTextInputBox) { } public void SetText(string text, float arg0) { } public void SetText(string text, float arg0, float arg1) { } public void SetText(System.Text.StringBuilder text) { }
        public void UpdateMeshPadding() { } public virtual void UpdateVertexData() { } public virtual void UpdateVertexData(TMP_VertexDataUpdateFlags f) { } public void UpdateFontAsset() { } public void ClearMesh() { }
        public Vector3 GetTextBounds() => default;
    }
    [Flags] public enum TMP_VertexDataUpdateFlags { None = 0, Vertices = 1, Uv0 = 2, Uv2 = 4, Colors32 = 16, All = 255 }
    public class TextMeshProUGUI : TMP_Text { public new CanvasRenderer canvasRenderer => null; }
    public class TextMeshPro : TMP_Text { public int sortingOrder; public int sortingLayerID; public Renderer renderer2; public MeshRenderer meshRenderer => null; }
    public class TMP_InputField : Selectable, IPointerClickHandler
    {
        public enum ContentType { Standard, Autocorrected, IntegerNumber, DecimalNumber, Alphanumeric, Name, EmailAddress, Password, Pin, Custom }
        public enum LineType { SingleLine, MultiLineSubmit, MultiLineNewline }
        public enum CharacterValidation { None, Digit, Integer, Decimal, Alphanumeric, Name, Regex, EmailAddress, CustomValidator }
        [Serializable] public class SubmitEvent : UnityEvent<string> { }
        [Serializable] public class OnChangeEvent : UnityEvent<string> { }
        public string text; public TMP_Text textComponent, placeholder; public Graphic placeholderGraphic; public RectTransform textViewport; public ContentType contentType; public LineType lineType; public CharacterValidation characterValidation;
        public int characterLimit, caretPosition, selectionAnchorPosition, selectionFocusPosition, stringPosition; public bool isFocused, readOnly, richText, customCaretColor, shouldHideMobileInput, resetOnDeActivation, restoreOriginalTextOnEscape; public Color caretColor, selectionColor; public int caretWidth; public float caretBlinkRate;
        public SubmitEvent onEndEdit = new SubmitEvent(); public SubmitEvent onSubmit = new SubmitEvent(); public OnChangeEvent onValueChanged = new OnChangeEvent(); public SubmitEvent onSelect = new SubmitEvent(); public SubmitEvent onDeselect = new SubmitEvent();
        public TMP_FontAsset fontAsset; public float pointSize;
        public void ActivateInputField() { } public void DeactivateInputField() { } public void SetTextWithoutNotify(string input) { } public void MoveTextEnd(bool shift) { } public void MoveTextStart(bool shift) { } public void ForceLabelUpdate() { } public void SelectAll() { }
        public virtual void OnPointerClick(PointerEventData e) { }
        public static void ResetInputField() { }
    }
    public class TMP_Dropdown : Selectable, IPointerClickHandler
    {
        [Serializable] public class OptionData { public string text; public Sprite image; public OptionData() { } public OptionData(string text) { } public OptionData(Sprite image) { } public OptionData(string text, Sprite image) { } public OptionData(string text, Sprite image, Color color) { } }
        [Serializable] public class OptionDataList { public List<OptionData> options; }
        [Serializable] public class DropdownEvent : UnityEvent<int> { }
        public RectTransform template; public TMP_Text captionText, itemText; public Image captionImage, itemImage; public List<OptionData> options; public int value; public DropdownEvent onValueChanged = new DropdownEvent(); public float alphaFadeSpeed; public bool IsExpanded => false;
        public void ClearOptions() { } public void AddOptions(List<string> options) { } public void AddOptions(List<OptionData> options) { } public void AddOptions(List<Sprite> options) { } public void SetValueWithoutNotify(int input) { } public void RefreshShownValue() { } public void Show() { } public void Hide() { }
        public virtual void OnPointerClick(PointerEventData e) { }
    }
    public class FastAction<A> { public void Add(Action<A> rhs) { } public void Remove(Action<A> rhs) { } }
    public static class TMPro_EventManager { public static readonly FastAction<UnityEngine.Object> TEXT_CHANGED_EVENT = new FastAction<UnityEngine.Object>(); }
    public class TMP_Settings : ScriptableObject { public static TMP_Settings instance; public static TMP_FontAsset defaultFontAsset; }
    public class TMP_Text2 { }
}
