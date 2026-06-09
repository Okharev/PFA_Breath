using System.Collections.Generic;
using Skills.Skills;
using UnityEngine;
using UnityEngine.UIElements;

namespace Skills.UI
{

        public static class UIToolkitUtility
        {
            private static Texture2D cachedRadialGlow;

            public static Texture2D GetRadialGlowTexture()
            {
                if (cachedRadialGlow != null) return cachedRadialGlow;

                int size = 78; // Resolution of the glow
                cachedRadialGlow = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    // CRITICAL: Prevents Unity from trying to save this runtime texture into your scene
                    hideFlags = HideFlags.HideAndDontSave 
                };

                Color[] pixels = new Color[size * size];
                float center = size / 2f;
                float radius = size / 2f;

                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                        float normalizedDist = Mathf.Clamp01(dist / radius);
                    
                        // Smoothstep creates a beautiful, natural falloff for the bloom
                        float alpha = 1f - Mathf.SmoothStep(0f, 1f, normalizedDist);
                    
                        // Pure white color, driven entirely by the alpha channel
                        pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                    }
                }

                cachedRadialGlow.SetPixels(pixels);
                cachedRadialGlow.Apply();

                return cachedRadialGlow;
            }
        }
    
    
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
        
        protected VisualElement iconElement;
        
        protected bool isEditorMode;
        protected Label titleLabel;
        
        // Scheduled Tasks for Click/Hold logic
        protected IVisualElementScheduledItem longPressTask;
        protected bool isLongPressHandled;
        protected VisualElement glowElement;

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
            // ADDED: "border-color" and "border-width" to make the glow smooth
            style.transitionProperty = new StyleList<StylePropertyName>(new List<StylePropertyName> 
            { 
                new("scale"), 
                new("border-color"),
                new("border-width")
            });
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
        
        /// <summary>
        /// Generates a perfectly circular icon layer. 
        /// Kept modular so derived classes can pass in specific sprites.
        /// </summary>
    protected void CreateIconLayer(Sprite sprite)
    {
        // 1. CREATE THE BLOOM LAYER FIRST (So it sits underneath the icon)
        glowElement = new VisualElement
        {
            pickingMode = PickingMode.Ignore,
            style =
            {
                position = Position.Absolute,
                // Expand outwards by 25% on all sides to create the spill
                left = new Length(-25, LengthUnit.Percent),
                right = new Length(-25, LengthUnit.Percent),
                top = new Length(-25, LengthUnit.Percent),
                bottom = new Length(-25, LengthUnit.Percent),
                
                backgroundImage = new StyleBackground(UIToolkitUtility.GetRadialGlowTexture()),
                unityBackgroundScaleMode = ScaleMode.ScaleToFit,
                
                opacity = 0f, // Start invisible
                
                // Hardware-accelerated fade for the bloom effect
                transitionProperty = new StyleList<StylePropertyName>(new List<StylePropertyName> { new("opacity") }),
                transitionDuration = new StyleList<TimeValue>(new List<TimeValue> { new(0.3f) }),
                transitionTimingFunction = new StyleList<EasingFunction>(new List<EasingFunction> { new(EasingMode.EaseOutCubic) })
            }
        };
        Add(glowElement);

        // 2. CREATE THE ICON LAYER
        if (sprite != null)
        {
            iconElement = new VisualElement
            {
                pickingMode = PickingMode.Ignore,
                style =
                {
                    position = Position.Absolute,
                    left = 0, top = 0, right = 0, bottom = 0,
                    borderTopLeftRadius = Length.Percent(50),
                    borderTopRightRadius = Length.Percent(50),
                    borderBottomLeftRadius = Length.Percent(50),
                    borderBottomRightRadius = Length.Percent(50),
                    backgroundImage = new StyleBackground(sprite),
                    unityBackgroundScaleMode = ScaleMode.ScaleToFit
                }
            };
            Add(iconElement);
        }
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
// --- SHARED TOOLTIP & HOVER LOGIC ---
        protected virtual void OnPointerEnter(PointerEnterEvent evt)
        {
            style.scale = new StyleScale(new Vector2(1.05f, 1.05f));
            // CHANGED: Passing the whole NodeData object
            SkillTooltip.OnUpdateTooltip?.Invoke(NodeData, evt.position); 
        }

        protected virtual void OnPointerMove(PointerMoveEvent evt)
        {
            // CHANGED: Passing the whole NodeData object
            SkillTooltip.OnUpdateTooltip?.Invoke(NodeData, evt.position);
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