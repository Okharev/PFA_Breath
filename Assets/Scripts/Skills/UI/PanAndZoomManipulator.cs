using UnityEngine;
using UnityEngine.UIElements;

namespace Skills.UI
{
    public class PanAndZoomManipulator : PointerManipulator
    {
        private const float MinZoom = 0.25f;
        private const float MaxZoom = 2.5f;
        private const float ZoomStep = 0.05f;
        private readonly VisualElement contentToMove;

        private bool isDragging;
        private Vector2 panOffset = Vector2.zero;
        private Vector2 pointerStartPosition;

        private float zoomLevel = 1f;

        public PanAndZoomManipulator(VisualElement content)
        {
            contentToMove = content;
        }

        protected override void RegisterCallbacksOnTarget()
        {
            // TrickleDown is required for panning background space safely
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
            // Explicitly check for Left Click (0) or Middle Click (2)
            if (evt.button == 0 || evt.button == 2)
            {
                // ARCHITECTURAL FIX: If left-clicking, verify the cursor isn't hovering over a skill node.
                // If it is a node, jump out early to let the click cascade down naturally to the node's ClickEvent.
                if (evt.button == 0 && IsTargetNode(evt.target as VisualElement)) return;

                isDragging = true;
                pointerStartPosition = evt.position;
                target.CapturePointer(evt.pointerId);
                evt.StopPropagation();

                Debug.Log("[Manipulator] Pointer Captured! Dragging started.");
            }
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

                Debug.Log("[Manipulator] Pointer Released.");
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
            zoomLevel = Mathf.Clamp(zoomLevel, MinZoom, MaxZoom);

            ApplyTransform();
            evt.StopPropagation();

            Debug.Log($"[Manipulator] Zoomed to: {zoomLevel}");
        }

        private void ApplyTransform()
        {
            contentToMove.style.translate = new Translate(panOffset.x, panOffset.y, 0);
            contentToMove.style.scale = new Scale(new Vector2(zoomLevel, zoomLevel));
        }

        // --- HIERARCHY TRAVERSAL HELPER ---
        // Time Complexity: O(D) where D represents layout depth. 
        // Because your UI layout tree is exceptionally shallow (depth <= 3), this runs at stable O(1) performance overhead.
        private bool IsTargetNode(VisualElement element)
        {
            while (element != null && element != target)
            {
                if (element is SkillNodeView) return true;
                element = element.parent;
            }

            return false;
        }
    }
}