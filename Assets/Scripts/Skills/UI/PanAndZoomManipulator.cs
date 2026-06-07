using UnityEngine;
using UnityEngine.UIElements;

namespace Skills.UI
{
    public class PanAndZoomManipulator : PointerManipulator
    {
        private const float MaxZoom = 2.5f;
        private const float ZoomStep = 0.05f;
        
        private readonly VisualElement contentToMove;
        private readonly VisualElement viewport;

        private bool isDragging;
        private Vector2 panOffset = Vector2.zero;
        private Vector2 pointerStartPosition;

        private float zoomLevel = 1f;

        public PanAndZoomManipulator(VisualElement content, VisualElement viewport)
        {
            this.contentToMove = content;
            this.viewport = viewport;
        }

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
            target.RegisterCallback<PointerMoveEvent>(OnPointerMove, TrickleDown.TrickleDown);
            target.RegisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);
            target.RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut, TrickleDown.TrickleDown);
            target.RegisterCallback<WheelEvent>(OnWheel, TrickleDown.TrickleDown);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<PointerDownEvent>(OnPointerDown);
            target.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
            target.UnregisterCallback<PointerUpEvent>(OnPointerUp);
            target.UnregisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
            target.UnregisterCallback<WheelEvent>(OnWheel);
        }
        
        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button == 0 || evt.button == 2)
            {
                if (evt.button == 0 && IsTargetNode(evt.target as VisualElement)) return;

                isDragging = true;
                pointerStartPosition = evt.position;
                target.CapturePointer(evt.pointerId);
                evt.StopPropagation();
            }
        }
        
        public void CenterOn(Vector2 screenCenter, Vector2 mapCenter)
        {
            panOffset = screenCenter - mapCenter;
            ApplyTransform();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (isDragging && target.HasPointerCapture(evt.pointerId))
            {
                Vector2 pointerDelta = (Vector2)evt.position - pointerStartPosition;
                panOffset += pointerDelta / zoomLevel;
                ApplyTransform();

                pointerStartPosition = evt.position;
                evt.StopPropagation();
            }
        }
        
        private void OnPointerUp(PointerUpEvent evt)
        {
            if (isDragging && target.HasPointerCapture(evt.pointerId))
            {
                isDragging = false;
                target.ReleasePointer(evt.pointerId);
                evt.StopPropagation();
            }
        }

        private void OnPointerCaptureOut(PointerCaptureOutEvent evt)
        {
            isDragging = false;
        }

        private void OnWheel(WheelEvent evt)
        {
            float scrollDelta = -evt.delta.y;
            zoomLevel += scrollDelta * ZoomStep;
            
            // ApplyTransform now handles both Zoom and Pan clamping
            ApplyTransform();
            evt.StopPropagation();
        }

        private void ApplyTransform()
        {
            // 1. Enforce Zoom Boundaries
            zoomLevel = Mathf.Clamp(zoomLevel, GetDynamicMinZoom(), MaxZoom);

            // 2. Enforce Pan Boundaries
            ClampPanOffset();

            // 3. Apply to Hardware
            contentToMove.style.translate = new Translate(panOffset.x, panOffset.y, 0);
            contentToMove.style.scale = new Scale(new Vector2(zoomLevel, zoomLevel));
        }

        /// <summary>
        /// Calculates the maximum allowed pan distance and restricts the camera from leaving the map boundaries.
        /// Accounts for UI Toolkit's Top-Left layout alignment and Center (50%) scale origin.
        /// </summary>
        private void ClampPanOffset()
        {
            // Safety check: Ensure the layout engine has processed dimensions before doing math
            if (viewport == null || float.IsNaN(viewport.layout.width) || viewport.layout.width == 0f) return;

            float contentWidth = contentToMove.layout.width;
            float contentHeight = contentToMove.layout.height;
            float viewWidth = viewport.layout.width;
            float viewHeight = viewport.layout.height;

            // Step A: Calculate the absolute minimum X and Y (How far left/up we can drag before the right/bottom edge shows)
            float minX = (viewWidth - (contentWidth / 2f) * (1f + zoomLevel)) / zoomLevel;
            float minY = (viewHeight - (contentHeight / 2f) * (1f + zoomLevel)) / zoomLevel;

            // Step B: Calculate the absolute maximum X and Y (How far right/down we can drag before the top/left edge shows)
            float maxX = (contentWidth * zoomLevel - contentWidth) / (2f * zoomLevel);
            float maxY = (contentHeight * zoomLevel - contentHeight) / (2f * zoomLevel);

            // Step C: Hard clamp the user's pan offset against these precise boundaries
            panOffset.x = Mathf.Clamp(panOffset.x, minX, maxX);
            panOffset.y = Mathf.Clamp(panOffset.y, minY, maxY);
        }

        /// <summary>
        /// Calculates the minimum zoom required to ensure the map fully covers the viewport.
        /// </summary>
        private float GetDynamicMinZoom()
        {
            if (viewport == null || float.IsNaN(viewport.layout.width) || viewport.layout.width == 0f)
                return 0.25f;

            float scaleX = viewport.layout.width / contentToMove.layout.width;
            float scaleY = viewport.layout.height / contentToMove.layout.height;

            return Mathf.Max(scaleX, scaleY);
        }

        private bool IsTargetNode(VisualElement element)
        {
            while (element != null && element != target)
            {
                if (element is BaseSkillNodeView) return true;
                element = element.parent;
            }
            return false;
        }
    }
}