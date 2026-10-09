// Stand-in for the uGUI package's UnityEngine.UI (com.unity.ugui).
// Signatures mirror the real API; bodies are empty. Only add members that exist in Unity.
using System;
using System.Collections.Generic;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace UnityEngine.UI
{
    public enum CanvasUpdate { Prelayout, Layout, PostLayout, PreRender, LatePreRender, MaxUpdateValue }
    public interface ICanvasElement
    {
        void Rebuild(CanvasUpdate executing);
        Transform transform { get; }
        void LayoutComplete();
        void GraphicUpdateComplete();
        bool IsDestroyed();
    }
    public interface IMaterialModifier { Material GetModifiedMaterial(Material baseMaterial); }
    public interface IMaskable { void RecalculateMasking(); }
    public interface IClippable { GameObject gameObject { get; } void RecalculateClipping(); RectTransform rectTransform { get; } void Cull(Rect clipRect, bool validRect); void SetClipRect(Rect value, bool validRect); void SetClipSoftness(Vector2 clipSoftness); }
    public interface IMeshModifier { void ModifyMesh(VertexHelper verts); }
    public interface ILayoutElement
    {
        void CalculateLayoutInputHorizontal(); void CalculateLayoutInputVertical();
        float minWidth { get; } float preferredWidth { get; } float flexibleWidth { get; }
        float minHeight { get; } float preferredHeight { get; } float flexibleHeight { get; }
        int layoutPriority { get; }
    }
    public interface ILayoutController { void SetLayoutHorizontal(); void SetLayoutVertical(); }
    public interface ILayoutGroup : ILayoutController { }
    public interface ILayoutSelfController : ILayoutController { }
    public interface ILayoutIgnorer { bool ignoreLayout { get; } }

    public class VertexHelper : IDisposable
    {
        public VertexHelper() { }
        public VertexHelper(Mesh m) { }
        public int currentVertCount => 0;
        public int currentIndexCount => 0;
        public void Clear() { }
        public void Dispose() { }
        public void PopulateUIVertex(ref UIVertex vertex, int i) { }
        public void SetUIVertex(UIVertex vertex, int i) { }
        public void FillMesh(Mesh mesh) { }
        public void AddVert(Vector3 position, Color32 color, Vector4 uv0, Vector4 uv1, Vector4 uv2, Vector4 uv3, Vector3 normal, Vector4 tangent) { }
        public void AddVert(Vector3 position, Color32 color, Vector4 uv0, Vector4 uv1, Vector3 normal, Vector4 tangent) { }
        public void AddVert(Vector3 position, Color32 color, Vector4 uv0) { }
        public void AddVert(UIVertex v) { }
        public void AddTriangle(int idx0, int idx1, int idx2) { }
        public void AddUIVertexQuad(UIVertex[] verts) { }
        public void AddUIVertexStream(List<UIVertex> verts, List<int> indices) { }
        public void AddUIVertexTriangleStream(List<UIVertex> verts) { }
        public void GetUIVertexStream(List<UIVertex> stream) { }
    }

    public abstract class Graphic : UIBehaviour, ICanvasElement
    {
        public static Material defaultGraphicMaterial => null;
        public virtual Color color { get; set; }
        public virtual bool raycastTarget { get; set; }
        public Vector4 raycastPadding { get; set; }
        public int depth => 0;
        public RectTransform rectTransform => null;
        public Canvas canvas => null;
        public CanvasRenderer canvasRenderer => null;
        public virtual Material defaultMaterial => null;
        public virtual Material material { get; set; }
        public virtual Material materialForRendering => null;
        public virtual Texture mainTexture => null;
        protected bool useLegacyMeshGeneration { get; set; }
        public virtual void SetAllDirty() { }
        public virtual void SetLayoutDirty() { }
        public virtual void SetVerticesDirty() { }
        public virtual void SetMaterialDirty() { }
        public virtual void Rebuild(CanvasUpdate update) { }
        public virtual void LayoutComplete() { }
        public virtual void GraphicUpdateComplete() { }
        public virtual void OnCullingChanged() { }
        protected virtual void UpdateGeometry() { }
        protected virtual void UpdateMaterial() { }
        protected virtual void OnPopulateMesh(VertexHelper vh) { }
        public virtual void SetNativeSize() { }
        public virtual bool Raycast(Vector2 sp, Camera eventCamera) => false;
        public Vector2 PixelAdjustPoint(Vector2 point) => point;
        public Rect GetPixelAdjustedRect() => default;
        public virtual void CrossFadeColor(Color targetColor, float duration, bool ignoreTimeScale, bool useAlpha) { }
        public virtual void CrossFadeColor(Color targetColor, float duration, bool ignoreTimeScale, bool useAlpha, bool useRGB) { }
        public virtual void CrossFadeAlpha(float alpha, float duration, bool ignoreTimeScale) { }
        public void RegisterDirtyLayoutCallback(UnityAction action) { }
        public void UnregisterDirtyLayoutCallback(UnityAction action) { }
        public void RegisterDirtyVerticesCallback(UnityAction action) { }
        public void UnregisterDirtyVerticesCallback(UnityAction action) { }
        public void RegisterDirtyMaterialCallback(UnityAction action) { }
        public void UnregisterDirtyMaterialCallback(UnityAction action) { }
#if UNITY_EDITOR
        protected override void OnValidate() { }
        protected override void Reset() { }
#endif
        protected override void OnEnable() { }
        protected override void OnDisable() { }
        protected override void OnDestroy() { }
        protected override void OnRectTransformDimensionsChange() { }
        protected override void OnTransformParentChanged() { }
        protected override void OnDidApplyAnimationProperties() { }
        protected override void OnCanvasHierarchyChanged() { }
    }

    public abstract class MaskableGraphic : Graphic, IClippable, IMaskable, IMaterialModifier
    {
        [Serializable] public class CullStateChangedEvent : UnityEvent<bool> { }
        public CullStateChangedEvent onCullStateChanged { get; set; }
        public bool maskable { get; set; }
        public bool isMaskingGraphic { get; set; }
        public virtual Material GetModifiedMaterial(Material baseMaterial) => baseMaterial;
        public virtual void Cull(Rect clipRect, bool validRect) { }
        public virtual void SetClipRect(Rect clipRect, bool validRect) { }
        public virtual void SetClipSoftness(Vector2 clipSoftness) { }
        public virtual void RecalculateClipping() { }
        public virtual void RecalculateMasking() { }
    }

    public class Image : MaskableGraphic, ILayoutElement
    {
        public enum Type { Simple, Sliced, Tiled, Filled }
        public enum FillMethod { Horizontal, Vertical, Radial90, Radial180, Radial360 }
        public enum OriginHorizontal { Left, Right }
        public enum OriginVertical { Bottom, Top }
        public enum Origin90 { BottomLeft, TopLeft, TopRight, BottomRight }
        public enum Origin180 { Bottom, Left, Top, Right }
        public enum Origin360 { Bottom, Right, Top, Left }
        public Sprite sprite { get; set; }
        public Sprite overrideSprite { get; set; }
        public Type type { get; set; }
        public bool preserveAspect { get; set; }
        public bool fillCenter { get; set; }
        public FillMethod fillMethod { get; set; }
        public float fillAmount { get; set; }
        public bool fillClockwise { get; set; }
        public int fillOrigin { get; set; }
        public float alphaHitTestMinimumThreshold { get; set; }
        public bool useSpriteMesh { get; set; }
        public float pixelsPerUnitMultiplier { get; set; }
        public float pixelsPerUnit => 0f;
        public bool hasBorder => false;
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

    public class RawImage : MaskableGraphic
    {
        public Texture texture { get; set; }
        public Rect uvRect { get; set; }
    }

    public class Text : MaskableGraphic, ILayoutElement
    {
        public Font font { get; set; }
        public virtual string text { get; set; }
        public int fontSize { get; set; }
        public FontStyle fontStyle { get; set; }
        public TextAnchor alignment { get; set; }
        public bool supportRichText { get; set; }
        public bool resizeTextForBestFit { get; set; }
        public HorizontalWrapMode horizontalOverflow { get; set; }
        public VerticalWrapMode verticalOverflow { get; set; }
        public float lineSpacing { get; set; }
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

    [Serializable]
    public struct ColorBlock : IEquatable<ColorBlock>
    {
        public Color normalColor { get; set; }
        public Color highlightedColor { get; set; }
        public Color pressedColor { get; set; }
        public Color selectedColor { get; set; }
        public Color disabledColor { get; set; }
        public float colorMultiplier { get; set; }
        public float fadeDuration { get; set; }
        public static ColorBlock defaultColorBlock;
        public bool Equals(ColorBlock other) => false;
    }

    [Serializable]
    public struct Navigation : IEquatable<Navigation>
    {
        [Flags] public enum Mode { None = 0, Horizontal = 1, Vertical = 2, Automatic = 3, Explicit = 4 }
        public Mode mode { get; set; }
        public bool wrapAround { get; set; }
        public Selectable selectOnUp { get; set; }
        public Selectable selectOnDown { get; set; }
        public Selectable selectOnLeft { get; set; }
        public Selectable selectOnRight { get; set; }
        public static Navigation defaultNavigation => default;
        public bool Equals(Navigation other) => false;
    }

    [Serializable]
    public class SpriteState { public Sprite highlightedSprite { get; set; } public Sprite pressedSprite { get; set; } public Sprite selectedSprite { get; set; } public Sprite disabledSprite { get; set; } }
    [Serializable]
    public class AnimationTriggers { public string normalTrigger { get; set; } public string highlightedTrigger { get; set; } public string pressedTrigger { get; set; } public string selectedTrigger { get; set; } public string disabledTrigger { get; set; } }

    public class Selectable : UIBehaviour, IMoveHandler, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        public enum Transition { None, ColorTint, SpriteSwap, Animation }
        public static Selectable[] allSelectablesArray => null;
        public static int allSelectableCount => 0;
        public Navigation navigation { get; set; }
        public Transition transition { get; set; }
        public ColorBlock colors { get; set; }
        public SpriteState spriteState { get; set; }
        public AnimationTriggers animationTriggers { get; set; }
        public Graphic targetGraphic { get; set; }
        public bool interactable { get; set; }
        public Image image { get; set; }
        public Animator animator => null;
        public virtual bool IsInteractable() => false;
        public virtual void Select() { }
        public virtual Selectable FindSelectable(Vector3 dir) => null;
        public virtual Selectable FindSelectableOnLeft() => null;
        public virtual Selectable FindSelectableOnRight() => null;
        public virtual Selectable FindSelectableOnUp() => null;
        public virtual Selectable FindSelectableOnDown() => null;
        protected virtual void DoStateTransition(SelectionState state, bool instant) { }
        protected enum SelectionState { Normal, Highlighted, Pressed, Selected, Disabled }
        protected SelectionState currentSelectionState => default;
        protected bool IsHighlighted() => false;
        protected bool IsPressed() => false;
        public virtual void OnMove(AxisEventData eventData) { }
        public virtual void OnPointerDown(PointerEventData eventData) { }
        public virtual void OnPointerUp(PointerEventData eventData) { }
        public virtual void OnPointerEnter(PointerEventData eventData) { }
        public virtual void OnPointerExit(PointerEventData eventData) { }
        public virtual void OnSelect(BaseEventData eventData) { }
        public virtual void OnDeselect(BaseEventData eventData) { }
    }

    public class Button : Selectable, IPointerClickHandler, ISubmitHandler
    {
        [Serializable] public class ButtonClickedEvent : UnityEvent { }
        public ButtonClickedEvent onClick { get; set; }
        public virtual void OnPointerClick(PointerEventData eventData) { }
        public virtual void OnSubmit(BaseEventData eventData) { }
    }

    public class Toggle : Selectable, IPointerClickHandler, ISubmitHandler, ICanvasElement
    {
        public enum ToggleTransition { None, Fade }
        [Serializable] public class ToggleEvent : UnityEvent<bool> { }
        public ToggleTransition toggleTransition;
        public Graphic graphic;
        public ToggleEvent onValueChanged = new ToggleEvent();
        public ToggleGroup group { get; set; }
        public bool isOn { get; set; }
        public void SetIsOnWithoutNotify(bool value) { }
        public virtual void Rebuild(CanvasUpdate executing) { }
        public virtual void LayoutComplete() { }
        public virtual void GraphicUpdateComplete() { }
        public virtual void OnPointerClick(PointerEventData eventData) { }
        public virtual void OnSubmit(BaseEventData eventData) { }
    }
    public class ToggleGroup : UIBehaviour
    {
        public bool allowSwitchOff { get; set; }
        public void NotifyToggleOn(Toggle toggle, bool sendCallback = true) { }
        public void UnregisterToggle(Toggle toggle) { }
        public void RegisterToggle(Toggle toggle) { }
        public bool AnyTogglesOn() => false;
        public IEnumerable<Toggle> ActiveToggles() => null;
        public Toggle GetFirstActiveToggle() => null;
        public void SetAllTogglesOff(bool sendCallback = true) { }
    }

    public class Slider : Selectable, IDragHandler, IInitializePotentialDragHandler, ICanvasElement
    {
        public enum Direction { LeftToRight, RightToLeft, BottomToTop, TopToBottom }
        [Serializable] public class SliderEvent : UnityEvent<float> { }
        public RectTransform fillRect { get; set; }
        public RectTransform handleRect { get; set; }
        public Direction direction { get; set; }
        public float minValue { get; set; }
        public float maxValue { get; set; }
        public bool wholeNumbers { get; set; }
        public virtual float value { get; set; }
        public float normalizedValue { get; set; }
        public SliderEvent onValueChanged { get; set; }
        public virtual void SetValueWithoutNotify(float input) { }
        public void SetDirection(Direction direction, bool includeRectLayouts) { }
        public virtual void Rebuild(CanvasUpdate executing) { }
        public virtual void LayoutComplete() { }
        public virtual void GraphicUpdateComplete() { }
        public virtual void OnDrag(PointerEventData eventData) { }
        public virtual void OnInitializePotentialDrag(PointerEventData eventData) { }
    }

    public class Scrollbar : Selectable, IBeginDragHandler, IDragHandler, IInitializePotentialDragHandler, ICanvasElement
    {
        public enum Direction { LeftToRight, RightToLeft, BottomToTop, TopToBottom }
        [Serializable] public class ScrollEvent : UnityEvent<float> { }
        public RectTransform handleRect { get; set; }
        public Direction direction { get; set; }
        public float value { get; set; }
        public float size { get; set; }
        public int numberOfSteps { get; set; }
        public ScrollEvent onValueChanged { get; set; }
        public virtual void SetValueWithoutNotify(float input) { }
        public void SetDirection(Direction direction, bool includeRectLayouts) { }
        public virtual void Rebuild(CanvasUpdate executing) { }
        public virtual void LayoutComplete() { }
        public virtual void GraphicUpdateComplete() { }
        public virtual void OnBeginDrag(PointerEventData eventData) { }
        public virtual void OnDrag(PointerEventData eventData) { }
        public virtual void OnInitializePotentialDrag(PointerEventData eventData) { }
    }

    public class ScrollRect : UIBehaviour, IInitializePotentialDragHandler, IBeginDragHandler, IEndDragHandler, IDragHandler, IScrollHandler, ICanvasElement, ILayoutElement, ILayoutGroup
    {
        public enum MovementType { Unrestricted, Elastic, Clamped }
        public enum ScrollbarVisibility { Permanent, AutoHide, AutoHideAndExpandViewport }
        [Serializable] public class ScrollRectEvent : UnityEvent<Vector2> { }
        public RectTransform content { get; set; }
        public bool horizontal { get; set; }
        public bool vertical { get; set; }
        public MovementType movementType { get; set; }
        public float elasticity { get; set; }
        public bool inertia { get; set; }
        public float decelerationRate { get; set; }
        public float scrollSensitivity { get; set; }
        public RectTransform viewport { get; set; }
        public Scrollbar horizontalScrollbar { get; set; }
        public Scrollbar verticalScrollbar { get; set; }
        public ScrollbarVisibility horizontalScrollbarVisibility { get; set; }
        public ScrollbarVisibility verticalScrollbarVisibility { get; set; }
        public float horizontalScrollbarSpacing { get; set; }
        public float verticalScrollbarSpacing { get; set; }
        public ScrollRectEvent onValueChanged { get; set; }
        public Vector2 velocity { get; set; }
        public Vector2 normalizedPosition { get; set; }
        public float horizontalNormalizedPosition { get; set; }
        public float verticalNormalizedPosition { get; set; }
        public virtual void Rebuild(CanvasUpdate executing) { }
        public virtual void LayoutComplete() { }
        public virtual void GraphicUpdateComplete() { }
        public virtual void StopMovement() { }
        public virtual void OnScroll(PointerEventData data) { }
        public virtual void OnInitializePotentialDrag(PointerEventData eventData) { }
        public virtual void OnBeginDrag(PointerEventData eventData) { }
        public virtual void OnEndDrag(PointerEventData eventData) { }
        public virtual void OnDrag(PointerEventData eventData) { }
        protected virtual void LateUpdate() { }
        protected virtual void SetContentAnchoredPosition(Vector2 position) { }
        protected virtual void SetNormalizedPosition(float value, int axis) { }
        public virtual void CalculateLayoutInputHorizontal() { }
        public virtual void CalculateLayoutInputVertical() { }
        public virtual float minWidth => 0f;
        public virtual float preferredWidth => 0f;
        public virtual float flexibleWidth => 0f;
        public virtual float minHeight => 0f;
        public virtual float preferredHeight => 0f;
        public virtual float flexibleHeight => 0f;
        public virtual int layoutPriority => 0;
        public virtual void SetLayoutHorizontal() { }
        public virtual void SetLayoutVertical() { }
    }

    public class Mask : UIBehaviour, IMaterialModifier
    {
        public RectTransform rectTransform => null;
        public bool showMaskGraphic { get; set; }
        public Graphic graphic => null;
        public virtual bool MaskEnabled() => false;
        public virtual Material GetModifiedMaterial(Material baseMaterial) => baseMaterial;
    }
    public class RectMask2D : UIBehaviour
    {
        public Vector4 padding { get; set; }
        public Vector2Int softness { get; set; }
        public Rect canvasRect => default;
        public RectTransform rectTransform => null;
    }

    public class CanvasScaler : UIBehaviour
    {
        public enum ScaleMode { ConstantPixelSize, ScaleWithScreenSize, ConstantPhysicalSize }
        public enum ScreenMatchMode { MatchWidthOrHeight = 0, Expand = 1, Shrink = 2 }
        public enum Unit { Centimeters, Millimeters, Inches, Points, Picas }
        public ScaleMode uiScaleMode { get; set; }
        public float referencePixelsPerUnit { get; set; }
        public float scaleFactor { get; set; }
        public Vector2 referenceResolution { get; set; }
        public ScreenMatchMode screenMatchMode { get; set; }
        public float matchWidthOrHeight { get; set; }
        public Unit physicalUnit { get; set; }
        public float fallbackScreenDPI { get; set; }
        public float defaultSpriteDPI { get; set; }
        public float dynamicPixelsPerUnit { get; set; }
        protected virtual void Update() { }
        protected virtual void Handle() { }
    }

    public class GraphicRaycaster : BaseRaycaster
    {
        public enum BlockingObjects { None = 0, TwoD = 1, ThreeD = 2, All = 3 }
        public bool ignoreReversedGraphics { get; set; }
        public BlockingObjects blockingObjects { get; set; }
        public override Camera eventCamera => null;
        public override void Raycast(PointerEventData eventData, List<RaycastResult> resultAppendList) { }
    }

    public abstract class BaseMeshEffect : UIBehaviour, IMeshModifier
    {
        protected Graphic graphic => null;
        public virtual void ModifyMesh(Mesh mesh) { }
        public abstract void ModifyMesh(VertexHelper vh);
    }
    public class Shadow : BaseMeshEffect
    {
        public Color effectColor { get; set; }
        public Vector2 effectDistance { get; set; }
        public bool useGraphicAlpha { get; set; }
        public override void ModifyMesh(VertexHelper vh) { }
    }
    public class Outline : Shadow { }

    public class LayoutElement : UIBehaviour, ILayoutElement, ILayoutIgnorer
    {
        public virtual bool ignoreLayout { get; set; }
        public virtual void CalculateLayoutInputHorizontal() { }
        public virtual void CalculateLayoutInputVertical() { }
        public virtual float minWidth { get; set; }
        public virtual float minHeight { get; set; }
        public virtual float preferredWidth { get; set; }
        public virtual float preferredHeight { get; set; }
        public virtual float flexibleWidth { get; set; }
        public virtual float flexibleHeight { get; set; }
        public virtual int layoutPriority { get; set; }
    }

    public abstract class LayoutGroup : UIBehaviour, ILayoutElement, ILayoutGroup
    {
        public RectOffset padding { get; set; }
        public TextAnchor childAlignment { get; set; }
        public virtual void CalculateLayoutInputHorizontal() { }
        public abstract void CalculateLayoutInputVertical();
        public virtual float minWidth => 0f;
        public virtual float preferredWidth => 0f;
        public virtual float flexibleWidth => 0f;
        public virtual float minHeight => 0f;
        public virtual float preferredHeight => 0f;
        public virtual float flexibleHeight => 0f;
        public virtual int layoutPriority => 0;
        public abstract void SetLayoutHorizontal();
        public abstract void SetLayoutVertical();
    }
    public abstract class HorizontalOrVerticalLayoutGroup : LayoutGroup
    {
        public float spacing { get; set; }
        public bool childForceExpandWidth { get; set; }
        public bool childForceExpandHeight { get; set; }
        public bool childControlWidth { get; set; }
        public bool childControlHeight { get; set; }
        public bool childScaleWidth { get; set; }
        public bool childScaleHeight { get; set; }
        public bool reverseArrangement { get; set; }
    }
    public class HorizontalLayoutGroup : HorizontalOrVerticalLayoutGroup
    {
        public override void CalculateLayoutInputVertical() { }
        public override void SetLayoutHorizontal() { }
        public override void SetLayoutVertical() { }
    }
    public class VerticalLayoutGroup : HorizontalOrVerticalLayoutGroup
    {
        public override void CalculateLayoutInputVertical() { }
        public override void SetLayoutHorizontal() { }
        public override void SetLayoutVertical() { }
    }
    public class GridLayoutGroup : LayoutGroup
    {
        public enum Corner { UpperLeft = 0, UpperRight = 1, LowerLeft = 2, LowerRight = 3 }
        public enum Axis { Horizontal = 0, Vertical = 1 }
        public enum Constraint { Flexible = 0, FixedColumnCount = 1, FixedRowCount = 2 }
        public Corner startCorner { get; set; }
        public Axis startAxis { get; set; }
        public Vector2 cellSize { get; set; }
        public Vector2 spacing { get; set; }
        public Constraint constraint { get; set; }
        public int constraintCount { get; set; }
        public override void CalculateLayoutInputVertical() { }
        public override void SetLayoutHorizontal() { }
        public override void SetLayoutVertical() { }
    }
    public class ContentSizeFitter : UIBehaviour, ILayoutSelfController
    {
        public enum FitMode { Unconstrained, MinSize, PreferredSize }
        public FitMode horizontalFit { get; set; }
        public FitMode verticalFit { get; set; }
        public virtual void SetLayoutHorizontal() { }
        public virtual void SetLayoutVertical() { }
    }
    public class LayoutRebuilder : ICanvasElement
    {
        public Transform transform => null;
        public bool IsDestroyed() => false;
        public static void ForceRebuildLayoutImmediate(RectTransform layoutRoot) { }
        public static void MarkLayoutForRebuild(RectTransform rect) { }
        public void Rebuild(CanvasUpdate executing) { }
        public void LayoutComplete() { }
        public void GraphicUpdateComplete() { }
    }
    public static class LayoutUtility
    {
        public static float GetPreferredWidth(RectTransform rect) => 0f;
        public static float GetPreferredHeight(RectTransform rect) => 0f;
        public static float GetMinWidth(RectTransform rect) => 0f;
        public static float GetMinHeight(RectTransform rect) => 0f;
    }

    public class CanvasUpdateRegistry
    {
        public static CanvasUpdateRegistry instance => null;
        public static void RegisterCanvasElementForLayoutRebuild(ICanvasElement element) { }
        public static void RegisterCanvasElementForGraphicRebuild(ICanvasElement element) { }
        public static bool IsRebuildingLayout() => false;
        public static bool IsRebuildingGraphics() => false;
    }

    public class Dropdown : Selectable
    {
        public class OptionData { public OptionData() { } public OptionData(string text) { } public string text { get; set; } public Sprite image { get; set; } }
        [Serializable] public class DropdownEvent : UnityEvent<int> { }
        public RectTransform template { get; set; }
        public Text captionText { get; set; }
        public Text itemText { get; set; }
        public List<OptionData> options { get; set; }
        public DropdownEvent onValueChanged { get; set; }
        public int value { get; set; }
        public void AddOptions(List<string> options) { }
        public void ClearOptions() { }
        public void RefreshShownValue() { }
    }

    public class InputField : Selectable
    {
        public string text { get; set; }
    }
}
