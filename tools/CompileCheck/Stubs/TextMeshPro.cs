// Stand-in for TextMeshPro (the TMPro namespace as shipped in com.unity.ugui 2.0 / Unity 6).
// Signatures mirror the real API; bodies are empty. Only add members that exist in Unity.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TMPro
{
    [Flags]
    public enum FontStyles { Normal = 0x0, Bold = 0x1, Italic = 0x2, Underline = 0x4, LowerCase = 0x8, UpperCase = 0x10, SmallCaps = 0x20, Strikethrough = 0x40, Superscript = 0x80, Subscript = 0x100, Highlight = 0x200 }
    public enum FontWeight { Thin = 100, ExtraLight = 200, Light = 300, Regular = 400, Medium = 500, SemiBold = 600, Bold = 700, Heavy = 800, Black = 900 }

    public enum TextAlignmentOptions
    {
        TopLeft = 257, Top = 258, TopRight = 260, TopJustified = 264, TopFlush = 272, TopGeoAligned = 288,
        Left = 513, Center = 514, Right = 516, Justified = 520, Flush = 528, CenterGeoAligned = 544,
        BottomLeft = 1025, Bottom = 1026, BottomRight = 1028, BottomJustified = 1032, BottomFlush = 1040, BottomGeoAligned = 1056,
        BaselineLeft = 2049, Baseline = 2050, BaselineRight = 2052, BaselineJustified = 2056, BaselineFlush = 2064, BaselineGeoAligned = 2080,
        MidlineLeft = 4097, Midline = 4098, MidlineRight = 4100, MidlineJustified = 4104, MidlineFlush = 4112, MidlineGeoAligned = 4128,
        CaplineLeft = 8193, Capline = 8194, CaplineRight = 8196, CaplineJustified = 8200, CaplineFlush = 8208, CaplineGeoAligned = 8224,
        Converted = 65535
    }
    public enum HorizontalAlignmentOptions { Left = 1, Center = 2, Right = 4, Justified = 8, Flush = 16, Geometry = 32 }
    public enum VerticalAlignmentOptions { Top = 256, Middle = 512, Bottom = 1024, Baseline = 2048, Geometry = 4096, Capline = 8192 }
    public enum TextOverflowModes { Overflow = 0, Ellipsis = 1, Masking = 2, Truncate = 3, ScrollRect = 4, Page = 5, Linked = 6 }
    public enum TextWrappingModes { NoWrap = 0, Normal = 1, PreserveWhitespace = 2, PreserveWhitespaceNoWrap = 3 }
    public enum TextureMappingOptions { Character = 0, Line = 1, Paragraph = 2, MatchAspect = 3 }
    public enum TextRenderFlags { DontRender = 0x0, Render = 0xFF }
    public enum TMP_TextElementType { Character, Sprite }

    public class TMP_Asset : ScriptableObject
    {
        public int instanceID => 0;
        public int hashCode;
        public Material material;
        public int materialHashCode;
    }

    public class TMP_FontAsset : TMP_Asset
    {
        public static TMP_FontAsset CreateFontAsset(Font font) => null;
        public static TMP_FontAsset CreateFontAsset(Font font, int samplingPointSize, int atlasPadding, UnityEngine.TextCore.LowLevel.GlyphRenderMode renderMode, int atlasWidth, int atlasHeight, AtlasPopulationMode atlasPopulationMode = AtlasPopulationMode.Dynamic, bool enableMultiAtlasSupport = true) => null;
        public Font sourceFontFile => null;
        public AtlasPopulationMode atlasPopulationMode { get; set; }
        public UnityEngine.TextCore.FaceInfo faceInfo { get; set; }
        public Texture2D atlasTexture => null;
        public Texture2D[] atlasTextures { get; set; }
        public List<TMP_FontAsset> fallbackFontAssetTable;
        public bool isMultiAtlasTexturesEnabled { get; set; }
        public bool HasCharacter(int character) => false;
        public bool HasCharacter(char character, bool searchFallbacks = false, bool tryAddCharacter = false) => false;
        public bool HasCharacters(string text) => false;
        public bool TryAddCharacters(string characters, bool includeFontFeatures = false) => false;
        public void ClearFontAssetData(bool setAtlasSizeToZero = false) { }
    }
    public enum AtlasPopulationMode { Static = 0x0, Dynamic = 0x1, DynamicOS = 0x2 }

    public class TMP_SpriteAsset : TMP_Asset { }
    public class TMP_StyleSheet : ScriptableObject { }

    public class TMP_Settings : ScriptableObject
    {
        public static TMP_Settings instance => null;
        public static TMP_Settings LoadDefaultSettings() => null;
        public static TMP_FontAsset defaultFontAsset { get; set; }
        public static float defaultFontSize => 0f;
        public static List<TMP_FontAsset> fallbackFontAssets { get; set; }
        public static TMP_SpriteAsset defaultSpriteAsset { get; set; }
        public static bool enableEmojiSupport { get; set; }
    }

    public struct TMP_CharacterInfo
    {
        public char character;
        public int index;
        public int stringLength;
        public TMP_TextElementType elementType;
        public int pageNumber;
        public int lineNumber;
        public int vertexIndex;
        public int materialReferenceIndex;
        public bool isVisible;
        public Vector3 topLeft;
        public Vector3 bottomLeft;
        public Vector3 topRight;
        public Vector3 bottomRight;
        public float origin;
        public float xAdvance;
        public float ascender;
        public float baseLine;
        public float descender;
        public float pointSize;
        public float scale;
        public Color32 color;
        public FontStyles style;
    }
    public struct TMP_LineInfo
    {
        public int characterCount;
        public int visibleCharacterCount;
        public int firstCharacterIndex;
        public int firstVisibleCharacterIndex;
        public int lastCharacterIndex;
        public int lastVisibleCharacterIndex;
        public float length;
        public float lineHeight;
        public float ascender;
        public float baseline;
        public float descender;
        public float width;
    }
    public struct TMP_MeshInfo
    {
        public Mesh mesh;
        public int vertexCount;
        public Vector3[] vertices;
        public Color32[] colors32;
    }
    public class TMP_TextInfo
    {
        public TMP_Text textComponent;
        public int characterCount;
        public int spriteCount;
        public int spaceCount;
        public int wordCount;
        public int linkCount;
        public int lineCount;
        public int pageCount;
        public int materialCount;
        public TMP_CharacterInfo[] characterInfo;
        public TMP_LineInfo[] lineInfo;
        public TMP_MeshInfo[] meshInfo;
    }

    public abstract class TMP_Text : MaskableGraphic
    {
        public virtual string text { get; set; }
        public TMP_FontAsset font { get; set; }
        public virtual Material fontSharedMaterial { get; set; }
        public virtual Material[] fontSharedMaterials { get; set; }
        public Material fontMaterial { get; set; }
        public virtual Material[] fontMaterials { get; set; }
        public override Color color { get; set; }
        public float alpha { get; set; }
        public bool enableVertexGradient { get; set; }
        public VertexGradient colorGradient { get; set; }
        public TMP_SpriteAsset spriteAsset { get; set; }
        public bool tintAllSprites { get; set; }
        public bool overrideColorTags { get; set; }
        public Color32 faceColor { get; set; }
        public Color32 outlineColor { get; set; }
        public float outlineWidth { get; set; }
        public float fontSize { get; set; }
        public float fontScale => 0f;
        public FontWeight fontWeight { get; set; }
        public float pixelsPerUnit => 0f;
        public bool enableAutoSizing { get; set; }
        public float fontSizeMin { get; set; }
        public float fontSizeMax { get; set; }
        public FontStyles fontStyle { get; set; }
        public bool isUsingBold => false;
        public HorizontalAlignmentOptions horizontalAlignment { get; set; }
        public VerticalAlignmentOptions verticalAlignment { get; set; }
        public TextAlignmentOptions alignment { get; set; }
        public float characterSpacing { get; set; }
        public float wordSpacing { get; set; }
        public float lineSpacing { get; set; }
        public float lineSpacingAdjustment { get; set; }
        public float paragraphSpacing { get; set; }
        public float characterWidthAdjustment { get; set; }
        public TextWrappingModes textWrappingMode { get; set; }
        public float wordWrappingRatios { get; set; }
        public TextOverflowModes overflowMode { get; set; }
        public bool isTextOverflowing => false;
        public int firstOverflowCharacterIndex => 0;
        public TMP_Text linkedTextComponent { get; set; }
        public bool isTextTruncated => false;
        public bool extraPadding { get; set; }
        public bool richText { get; set; }
        public bool emojiFallbackSupport { get; set; }
        public bool parseCtrlCharacters { get; set; }
        public bool isOverlay { get; set; }
        public bool isOrthographic { get; set; }
        public bool enableCulling { get; set; }
        public bool ignoreVisibility { get; set; }
        public TextureMappingOptions horizontalMapping { get; set; }
        public TextureMappingOptions verticalMapping { get; set; }
        public float mappingUvLineOffset { get; set; }
        public TextRenderFlags renderMode { get; set; }
        public VertexSortingOrder geometrySortingOrder { get; set; }
        public bool isTextObjectScaleStatic { get; set; }
        public bool vertexBufferAutoSizeReduction { get; set; }
        public int firstVisibleCharacter { get; set; }
        public int maxVisibleCharacters { get; set; }
        public int maxVisibleWords { get; set; }
        public int maxVisibleLines { get; set; }
        public bool useMaxVisibleDescender { get; set; }
        public int pageToDisplay { get; set; }
        public virtual Vector4 margin { get; set; }
        public TMP_TextInfo textInfo => null;
        public bool havePropertiesChanged { get; set; }
        public bool isUsingLegacyAnimationComponent { get; set; }
        public new Transform transform => null;
        public new RectTransform rectTransform => null;
        public virtual bool autoSizeTextContainer { get; set; }
        public virtual Mesh mesh => null;
        public bool isVolumetricText { get; set; }
        public Bounds bounds => default;
        public Bounds textBounds => default;
        public float flexibleHeight => 0f;
        public float flexibleWidth => 0f;
        public float minWidth => 0f;
        public float minHeight => 0f;
        public float maxWidth => 0f;
        public float maxHeight => 0f;
        public virtual float preferredWidth => 0f;
        public virtual float preferredHeight => 0f;
        public virtual float renderedWidth => 0f;
        public virtual float renderedHeight => 0f;
        public int layoutPriority => 0;
        public static event Func<int, string, TMP_FontAsset> OnFontAssetRequest { add { } remove { } }
        public virtual event Action<TMP_TextInfo> OnPreRenderText { add { } remove { } }
        public virtual void ForceMeshUpdate(bool ignoreActiveState = false, bool forceTextReparsing = false) { }
        public virtual void UpdateGeometry(Mesh mesh, int index) { }
        public virtual void UpdateVertexData(TMP_VertexDataUpdateFlags flags) { }
        public virtual void UpdateVertexData() { }
        public virtual void SetVertices(Vector3[] vertices) { }
        public virtual void UpdateMeshPadding() { }
        public virtual void ClearMesh() { }
        public virtual string GetParsedText() => null;
        public void SetText(string sourceText) { }
        public void SetText(string sourceText, bool syncTextInputBox = true) { }
        public void SetText(string sourceText, float arg0) { }
        public void SetText(string sourceText, float arg0, float arg1) { }
        public void SetText(string sourceText, float arg0, float arg1, float arg2) { }
        public void SetText(System.Text.StringBuilder sourceText) { }
        public void SetText(char[] sourceText) { }
        public void SetCharArray(char[] sourceText) { }
        public Vector2 GetPreferredValues() => default;
        public Vector2 GetPreferredValues(float width, float height) => default;
        public Vector2 GetPreferredValues(string text) => default;
        public Vector2 GetPreferredValues(string text, float width, float height) => default;
        public Vector2 GetRenderedValues() => default;
        public Vector2 GetRenderedValues(bool onlyVisibleCharacters) => default;
        public TMP_TextInfo GetTextInfo(string text) => null;
        public virtual void ComputeMarginSize() { }
        public virtual TMP_TextInfo GetTextInfo() => null;
        public virtual void CalculateLayoutInputHorizontal() { }
        public virtual void CalculateLayoutInputVertical() { }
    }

    [Flags] public enum TMP_VertexDataUpdateFlags { None = 0x0, Vertices = 0x1, Uv0 = 0x2, Uv2 = 0x4, Uv4 = 0x8, Colors32 = 0x10, All = 0xFF }
    public enum VertexSortingOrder { Normal, Reverse }
    [Serializable]
    public struct VertexGradient
    {
        public Color topLeft, topRight, bottomLeft, bottomRight;
        public VertexGradient(Color color) { topLeft = topRight = bottomLeft = bottomRight = color; }
        public VertexGradient(Color color0, Color color1, Color color2, Color color3) { topLeft = color0; topRight = color1; bottomLeft = color2; bottomRight = color3; }
    }

    public class TextMeshProUGUI : TMP_Text, ILayoutElement
    {
        public override Material materialForRendering => null;
        public override bool autoSizeTextContainer { get; set; }
        public override Mesh mesh => null;
        public new CanvasRenderer canvasRenderer => null;
        public override void CalculateLayoutInputHorizontal() { }
        public override void CalculateLayoutInputVertical() { }
    }

    public class TextMeshPro : TMP_Text, ILayoutElement
    {
        public int sortingLayerID { get; set; }
        public int sortingOrder { get; set; }
        public override bool autoSizeTextContainer { get; set; }
        public TextContainer textContainer => null;
        public new Transform transform => null;
        public Renderer renderer => null;
        public override Mesh mesh => null;
        public MeshFilter meshFilter => null;
        public MaskingTypes maskType { get; set; }
        public void SetMask(MaskingTypes type, Vector4 maskCoords) { }
        public void SetMask(MaskingTypes type, Vector4 maskCoords, float softnessX, float softnessY) { }
        public override void CalculateLayoutInputHorizontal() { }
        public override void CalculateLayoutInputVertical() { }
    }
    public enum MaskingTypes { MaskOff = 0, MaskHard = 1, MaskSoft = 2 }
    public class TextContainer : UIBehaviour { }

    public class TMP_InputField : Selectable, IUpdateSelectedHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler, ISubmitHandler, ICancelHandler, ICanvasElement, ILayoutElement, IScrollHandler
    {
        public enum ContentType { Standard, Autocorrected, IntegerNumber, DecimalNumber, Alphanumeric, Name, EmailAddress, Password, Pin, Custom }
        public enum InputType { Standard, AutoCorrect, Password }
        public enum CharacterValidation { None, Digit, Integer, Decimal, Alphanumeric, Name, Regex, EmailAddress, CustomValidator }
        public enum LineType { SingleLine, MultiLineSubmit, MultiLineNewline }
        public delegate char OnValidateInput(string text, int charIndex, char addedChar);
        [Serializable] public class SubmitEvent : UnityEvent<string> { }
        [Serializable] public class OnChangeEvent : UnityEvent<string> { }
        [Serializable] public class SelectionEvent : UnityEvent<string> { }
        [Serializable] public class TextSelectionEvent : UnityEvent<string, int, int> { }
        [Serializable] public class TouchScreenKeyboardEvent : UnityEvent<TouchScreenKeyboard.Status> { }
        public bool shouldHideMobileInput { get; set; }
        public bool shouldHideSoftKeyboard { get; set; }
        public string text { get; set; }
        public void SetTextWithoutNotify(string input) { }
        public bool isFocused => false;
        public float caretBlinkRate { get; set; }
        public int caretWidth { get; set; }
        public RectTransform textViewport { get; set; }
        public TMP_Text textComponent { get; set; }
        public Graphic placeholder { get; set; }
        public Scrollbar verticalScrollbar { get; set; }
        public float scrollSensitivity { get; set; }
        public Color caretColor { get; set; }
        public bool customCaretColor { get; set; }
        public Color selectionColor { get; set; }
        public SubmitEvent onEndEdit { get; set; }
        public SubmitEvent onSubmit { get; set; }
        public SelectionEvent onSelect { get; set; }
        public SelectionEvent onDeselect { get; set; }
        public TextSelectionEvent onTextSelection { get; set; }
        public TextSelectionEvent onEndTextSelection { get; set; }
        public OnChangeEvent onValueChanged { get; set; }
        public TouchScreenKeyboardEvent onTouchScreenKeyboardStatusChanged { get; set; }
        public OnValidateInput onValidateInput { get; set; }
        public int characterLimit { get; set; }
        public float pointSize { get; set; }
        public TMP_FontAsset fontAsset { get; set; }
        public bool onFocusSelectAll { get; set; }
        public bool resetOnDeActivation { get; set; }
        public bool restoreOriginalTextOnEscape { get; set; }
        public bool isRichTextEditingAllowed { get; set; }
        public ContentType contentType { get; set; }
        public LineType lineType { get; set; }
        public int lineLimit { get; set; }
        public InputType inputType { get; set; }
        public TouchScreenKeyboardType keyboardType { get; set; }
        public CharacterValidation characterValidation { get; set; }
        public bool readOnly { get; set; }
        public bool richText { get; set; }
        public bool multiLine => false;
        public char asteriskChar { get; set; }
        public bool wasCanceled => false;
        public int caretPosition { get; set; }
        public int selectionAnchorPosition { get; set; }
        public int selectionFocusPosition { get; set; }
        public int stringPosition { get; set; }
        public void MoveTextEnd(bool shift) { }
        public void MoveTextStart(bool shift) { }
        public void MoveToEndOfLine(bool shift, bool ctrl) { }
        public void MoveToStartOfLine(bool shift, bool ctrl) { }
        public void ActivateInputField() { }
        public void DeactivateInputField(bool clearSelection = false) { }
        public void ForceLabelUpdate() { }
        public void SetGlobalPointSize(float pointSize) { }
        public void SetGlobalFontAsset(TMP_FontAsset fontAsset) { }
        public virtual void Rebuild(CanvasUpdate update) { }
        public virtual void LayoutComplete() { }
        public virtual void GraphicUpdateComplete() { }
        public virtual void OnUpdateSelected(BaseEventData eventData) { }
        public virtual void OnBeginDrag(PointerEventData eventData) { }
        public virtual void OnDrag(PointerEventData eventData) { }
        public virtual void OnEndDrag(PointerEventData eventData) { }
        public virtual void OnPointerClick(PointerEventData eventData) { }
        public virtual void OnSubmit(BaseEventData eventData) { }
        public virtual void OnCancel(BaseEventData eventData) { }
        public virtual void OnScroll(PointerEventData eventData) { }
        public virtual void CalculateLayoutInputHorizontal() { }
        public virtual void CalculateLayoutInputVertical() { }
        public virtual float minWidth => 0f;
        public virtual float preferredWidth => 0f;
        public virtual float flexibleWidth => 0f;
        public virtual float minHeight => 0f;
        public virtual float preferredHeight => 0f;
        public virtual float flexibleHeight => 0f;
        public virtual int layoutPriority => 0;
    }

    public class TMP_Dropdown : Selectable, IPointerClickHandler, ISubmitHandler, ICancelHandler
    {
        [Serializable]
        public class OptionData
        {
            public OptionData() { }
            public OptionData(string text) { }
            public OptionData(Sprite image) { }
            public OptionData(string text, Sprite image) { }
            public OptionData(string text, Sprite image, Color color) { }
            public string text { get; set; }
            public Sprite image { get; set; }
            public Color color { get; set; }
        }
        [Serializable] public class OptionDataList { public List<OptionData> options { get; set; } }
        [Serializable] public class DropdownEvent : UnityEvent<int> { }
        public RectTransform template { get; set; }
        public TMP_Text captionText { get; set; }
        public Image captionImage { get; set; }
        public Graphic placeholder { get; set; }
        public TMP_Text itemText { get; set; }
        public Image itemImage { get; set; }
        public List<OptionData> options { get; set; }
        public DropdownEvent onValueChanged { get; set; }
        public float alphaFadeSpeed { get; set; }
        public int value { get; set; }
        public bool IsExpanded => false;
        public bool MultiSelect { get; set; }
        public void SetValueWithoutNotify(int input) { }
        public void RefreshShownValue() { }
        public void AddOptions(List<OptionData> options) { }
        public void AddOptions(List<string> options) { }
        public void AddOptions(List<Sprite> options) { }
        public void ClearOptions() { }
        public virtual void OnPointerClick(PointerEventData eventData) { }
        public virtual void OnSubmit(BaseEventData eventData) { }
        public virtual void OnCancel(BaseEventData eventData) { }
        public void Show() { }
        public void Hide() { }
        protected virtual GameObject CreateBlocker(Canvas rootCanvas) => null;
        protected virtual void DestroyBlocker(GameObject blocker) { }
        protected virtual GameObject CreateDropdownList(GameObject template) => null;
        protected virtual void DestroyDropdownList(GameObject dropdownList) { }
    }
}
