// Stand-in for the uGUI package's UnityEngine.EventSystems (com.unity.ugui).
// Signatures mirror the real API; bodies are empty. Only add members that exist in Unity.
using System.Collections.Generic;

namespace UnityEngine.EventSystems
{
    public abstract class UIBehaviour : MonoBehaviour
    {
        protected virtual void Awake() { }
        protected virtual void OnEnable() { }
        protected virtual void Start() { }
        protected virtual void OnDisable() { }
        protected virtual void OnDestroy() { }
        public virtual bool IsActive() => false;
        protected virtual void OnRectTransformDimensionsChange() { }
        protected virtual void OnBeforeTransformParentChanged() { }
        protected virtual void OnTransformParentChanged() { }
        protected virtual void OnDidApplyAnimationProperties() { }
        protected virtual void OnCanvasGroupChanged() { }
        protected virtual void OnCanvasHierarchyChanged() { }
        public bool IsDestroyed() => false;
#if UNITY_EDITOR
        protected virtual void OnValidate() { }
        protected virtual void Reset() { }
#endif
    }

    public class EventSystem : UIBehaviour
    {
        public static EventSystem current { get; set; }
        public bool sendNavigationEvents { get; set; }
        public int pixelDragThreshold { get; set; }
        public GameObject firstSelectedGameObject { get; set; }
        public GameObject currentSelectedGameObject => null;
        public bool alreadySelecting => false;
        public BaseInputModule currentInputModule => null;
        public bool isFocused => false;
        public void SetSelectedGameObject(GameObject selected) { }
        public void SetSelectedGameObject(GameObject selected, BaseEventData pointer) { }
        public bool IsPointerOverGameObject() => false;
        public bool IsPointerOverGameObject(int pointerId) => false;
        public void RaycastAll(PointerEventData eventData, List<RaycastResult> raycastResults) { }
        public void UpdateModules() { }
    }

    public abstract class BaseInputModule : UIBehaviour
    {
        public BaseInput input => null;
        public BaseInput inputOverride { get; set; }
        public abstract void Process();
    }
    public class BaseInput : UIBehaviour { }
    public abstract class PointerInputModule : BaseInputModule { }
    public class StandaloneInputModule : PointerInputModule
    {
        public string horizontalAxis { get; set; }
        public string verticalAxis { get; set; }
        public string submitButton { get; set; }
        public string cancelButton { get; set; }
        public float inputActionsPerSecond { get; set; }
        public float repeatDelay { get; set; }
        public override void Process() { }
    }

    public abstract class AbstractEventData
    {
        protected bool m_Used;
        public virtual void Reset() { }
        public virtual void Use() { }
        public virtual bool used => false;
    }
    public class BaseEventData : AbstractEventData
    {
        public BaseEventData(EventSystem eventSystem) { }
        public BaseInputModule currentInputModule => null;
        public GameObject selectedObject { get; set; }
    }
    public enum MoveDirection { Left, Up, Right, Down, None }
    public class AxisEventData : BaseEventData
    {
        public AxisEventData(EventSystem eventSystem) : base(eventSystem) { }
        public Vector2 moveVector { get; set; }
        public MoveDirection moveDir { get; set; }
    }

    public class PointerEventData : BaseEventData
    {
        public enum InputButton { Left = 0, Right = 1, Middle = 2 }
        public enum FramePressState { Pressed, Released, PressedAndReleased, NotChanged }
        public PointerEventData(EventSystem eventSystem) : base(eventSystem) { }
        public GameObject pointerEnter { get; set; }
        public GameObject lastPress => null;
        public GameObject rawPointerPress { get; set; }
        public GameObject pointerDrag { get; set; }
        public GameObject pointerClick { get; set; }
        public RaycastResult pointerCurrentRaycast { get; set; }
        public RaycastResult pointerPressRaycast { get; set; }
        public List<GameObject> hovered = new List<GameObject>();
        public bool eligibleForClick { get; set; }
        public int pointerId { get; set; }
        public Vector2 position { get; set; }
        public Vector2 delta { get; set; }
        public Vector2 pressPosition { get; set; }
        public float clickTime { get; set; }
        public int clickCount { get; set; }
        public Vector2 scrollDelta { get; set; }
        public bool useDragThreshold { get; set; }
        public bool dragging { get; set; }
        public InputButton button { get; set; }
        public float pressure { get; set; }
        public bool IsPointerMoving() => false;
        public bool IsScrolling() => false;
        public Camera enterEventCamera => null;
        public Camera pressEventCamera => null;
        public GameObject pointerPress { get; set; }
    }

    public struct RaycastResult
    {
        public GameObject gameObject { get; set; }
        public BaseRaycaster module;
        public float distance;
        public float index;
        public int depth;
        public int sortingLayer;
        public int sortingOrder;
        public Vector3 worldPosition;
        public Vector3 worldNormal;
        public Vector2 screenPosition;
        public int displayIndex;
        public bool isValid => false;
        public void Clear() { }
    }

    public abstract class BaseRaycaster : UIBehaviour
    {
        public abstract void Raycast(PointerEventData eventData, List<RaycastResult> resultAppendList);
        public abstract Camera eventCamera { get; }
        public virtual int sortOrderPriority => 0;
        public virtual int renderOrderPriority => 0;
    }
    public class PhysicsRaycaster : BaseRaycaster
    {
        public override Camera eventCamera => null;
        public override void Raycast(PointerEventData eventData, List<RaycastResult> resultAppendList) { }
    }

    public interface IEventSystemHandler { }
    public interface IPointerMoveHandler : IEventSystemHandler { void OnPointerMove(PointerEventData eventData); }
    public interface IPointerEnterHandler : IEventSystemHandler { void OnPointerEnter(PointerEventData eventData); }
    public interface IPointerExitHandler : IEventSystemHandler { void OnPointerExit(PointerEventData eventData); }
    public interface IPointerDownHandler : IEventSystemHandler { void OnPointerDown(PointerEventData eventData); }
    public interface IPointerUpHandler : IEventSystemHandler { void OnPointerUp(PointerEventData eventData); }
    public interface IPointerClickHandler : IEventSystemHandler { void OnPointerClick(PointerEventData eventData); }
    public interface IInitializePotentialDragHandler : IEventSystemHandler { void OnInitializePotentialDrag(PointerEventData eventData); }
    public interface IBeginDragHandler : IEventSystemHandler { void OnBeginDrag(PointerEventData eventData); }
    public interface IDragHandler : IEventSystemHandler { void OnDrag(PointerEventData eventData); }
    public interface IEndDragHandler : IEventSystemHandler { void OnEndDrag(PointerEventData eventData); }
    public interface IDropHandler : IEventSystemHandler { void OnDrop(PointerEventData eventData); }
    public interface IScrollHandler : IEventSystemHandler { void OnScroll(PointerEventData eventData); }
    public interface IUpdateSelectedHandler : IEventSystemHandler { void OnUpdateSelected(BaseEventData eventData); }
    public interface ISelectHandler : IEventSystemHandler { void OnSelect(BaseEventData eventData); }
    public interface IDeselectHandler : IEventSystemHandler { void OnDeselect(BaseEventData eventData); }
    public interface IMoveHandler : IEventSystemHandler { void OnMove(AxisEventData eventData); }
    public interface ISubmitHandler : IEventSystemHandler { void OnSubmit(BaseEventData eventData); }
    public interface ICancelHandler : IEventSystemHandler { void OnCancel(BaseEventData eventData); }

    public static class ExecuteEvents
    {
        public delegate void EventFunction<T1>(T1 handler, BaseEventData eventData);
        public static EventFunction<IPointerEnterHandler> pointerEnterHandler => null;
        public static EventFunction<IPointerExitHandler> pointerExitHandler => null;
        public static EventFunction<IPointerDownHandler> pointerDownHandler => null;
        public static EventFunction<IPointerUpHandler> pointerUpHandler => null;
        public static EventFunction<IPointerClickHandler> pointerClickHandler => null;
        public static EventFunction<IBeginDragHandler> beginDragHandler => null;
        public static EventFunction<IDragHandler> dragHandler => null;
        public static EventFunction<IEndDragHandler> endDragHandler => null;
        public static EventFunction<IDropHandler> dropHandler => null;
        public static EventFunction<IScrollHandler> scrollHandler => null;
        public static EventFunction<ISubmitHandler> submitHandler => null;
        public static EventFunction<ICancelHandler> cancelHandler => null;
        public static bool Execute<T>(GameObject target, BaseEventData eventData, EventFunction<T> functor) where T : IEventSystemHandler => false;
        public static GameObject ExecuteHierarchy<T>(GameObject root, BaseEventData eventData, EventFunction<T> callbackFunction) where T : IEventSystemHandler => null;
        public static bool CanHandleEvent<T>(GameObject go) where T : IEventSystemHandler => false;
        public static GameObject GetEventHandler<T>(GameObject root) where T : IEventSystemHandler => null;
    }
}
