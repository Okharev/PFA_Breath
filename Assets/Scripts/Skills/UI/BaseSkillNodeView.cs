using System.Collections.Generic;
using Skills.Skills;
using UnityEngine;
using UnityEngine.UIElements;

namespace Skills.UI
{
    public static class SkillNodeFactory
    {
        /// <summary>
        /// Analyzes the polymorphic BaseNodeData and returns the appropriate Custom Visual Element.
        /// </summary>
        public static VisualElement CreateNodeView(BaseNodeData nodeData, bool isEditor = false)
        {
            // O(1) Type checking
            if (nodeData is EmotionNodeData emotionData)
            {
                return new EmotionSkillNodeView(emotionData, isEditor);
            }
            if (nodeData is GenericNodeData genericData)
            {
                return new GenericSkillNodeView(genericData, isEditor); 
            }

            // Fallback for extending the system later
            Debug.LogError($"[SkillNodeFactory] Unrecognized node type provided: {nodeData.GetType()}");
            return null;
        }
    }
    

    public abstract class BaseSkillNodeView : VisualElement
    {
        public BaseNodeData NodeData { get; protected set; }
        
        protected bool isEditorMode;
        protected Label titleLabel;
        
        // Scheduled Tasks for Click/Hold logic
        protected IVisualElementScheduledItem longPressTask;
        protected bool isLongPressHandled;

        public BaseSkillNodeView(BaseNodeData data, bool isEditor = false)
        {
            NodeData = data;
            isEditorMode = isEditor;

            // --- 1. SHARED POSITIONING & LAYOUT ---
            style.position = Position.Absolute;
            style.left = data.Position.x;
            style.top = data.Position.y;
            style.alignItems = Align.Center;
            style.justifyContent = Justify.Center;

            // --- 2. HARDWARE ACCELERATED TRANSITIONS ---
            style.transitionProperty = new StyleList<StylePropertyName>(new List<StylePropertyName> { new("scale") });
            style.transitionDuration = new StyleList<TimeValue>(new List<TimeValue> { new(0.15f) });
            style.transitionTimingFunction = new StyleList<EasingFunction>(new List<EasingFunction> { new(EasingMode.EaseOutCubic) });

            // --- 3. SHARED EVENT REGISTRATION ---
            RegisterCallback<AttachToPanelEvent>(OnAttach);
            RegisterCallback<DetachFromPanelEvent>(OnDetach);
            
            RegisterCallback<PointerEnterEvent>(OnPointerEnter);
            RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            
            // RESTORED: Input Callbacks
            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<PointerOutEvent>(OnPointerOut);
        }

        // --- LIFECYCLE MANAGEMENT ---
        protected virtual void OnAttach(AttachToPanelEvent evt)
        {
            SkillTreeManager.OnSkillTreeUpdated += RefreshVisualState;
        }

        protected virtual void OnDetach(DetachFromPanelEvent evt)
        {
            SkillTreeManager.OnSkillTreeUpdated -= RefreshVisualState;
        }

        // --- SHARED TOOLTIP & HOVER LOGIC ---
        protected virtual void OnPointerEnter(PointerEnterEvent evt)
        {
            style.scale = new StyleScale(new Vector2(1.05f, 1.05f));
            SkillTooltip.OnUpdateTooltip?.Invoke(NodeData.Description, evt.position);
        }

        protected virtual void OnPointerMove(PointerMoveEvent evt)
        {
            SkillTooltip.OnUpdateTooltip?.Invoke(NodeData.Description, evt.position);
        }

        protected virtual void OnPointerLeave(PointerLeaveEvent evt)
        {
            style.scale = new StyleScale(Vector2.one);
            SkillTooltip.OnHideTooltip?.Invoke();
        }

        // --- SHARED CLICK & LONG PRESS LOGIC ---
        protected virtual void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0 || isEditorMode) return;

            isLongPressHandled = false;

            // Schedule the long-press logic to fire in 500 milliseconds
            longPressTask = schedule.Execute(() =>
            {
                isLongPressHandled = true;
                OnLongPress(); // Calls the virtual method!
            }).StartingIn(500);

            evt.StopPropagation();
        }

        protected virtual void OnPointerUp(PointerUpEvent evt)
        {
            if (evt.button != 0 || isEditorMode) return;

            // If the user let go before 500ms, cancel the hold task
            if (longPressTask != null)
            {
                longPressTask.Pause();
                longPressTask = null;
            }

            // Short Click: Standard Unlock Behavior
            if (!isLongPressHandled)
            {
                if (SkillTreeManager.Instance != null && SkillTreeManager.Instance.TryUnlock(NodeData))
                {
                    Debug.Log($"[UI] Successfully leveled up {NodeData.NodeName}");
                }
            }

            evt.StopPropagation();
        }

        protected virtual void OnPointerOut(PointerOutEvent evt)
        {
            // Cancel the press if the user drags their mouse off the node while holding click
            if (longPressTask != null)
            {
                longPressTask.Pause();
                longPressTask = null;
            }
        }

        // --- VIRTUAL & ABSTRACT CONTRACTS ---
        
        // Virtual method. Child classes can override this if they have special Long Press behaviors (like equipping).
        protected virtual void OnLongPress() { }

        // Abstract method. Child classes MUST implement their visual refresh logic.
        public abstract void RefreshVisualState();
    }
}