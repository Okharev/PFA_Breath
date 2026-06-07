using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

namespace Skills.UI
{
    [RequireComponent(typeof(UIDocument))]
    public class SkillTreeUIController : MonoBehaviour
    {
        [Header("Data Source")] public SkillTreeGraph targetGraph;

        [Header("Input Settings")] [Tooltip("Define the key to open the skill tree. Default is 'K'.")]
        public InputAction toggleMenuAction = new("ToggleSkillTree", binding: "<Keyboard>/k");

        private bool isMenuOpen;

        // REFERENCE HUD OVERLAY BAR
        private SkillPointsBar pointsBar;
        private SkillTreeCanvas treeCanvas;

        private UIDocument uiDocument;
        private VisualElement viewport;
        
        [Header("Visuals")]
        [SerializeField] private Texture2D backgroundTexture;

        public static bool IsOpen { get; private set; }

        private void Start()
        {
            uiDocument = GetComponent<UIDocument>();
            uiDocument.rootVisualElement.pickingMode = PickingMode.Ignore;

            // 1. Create the Viewport (The static window/camera)
            viewport = new VisualElement
            {
                style =
                {
                    width = Length.Percent(100),
                    height = Length.Percent(100),
                    overflow = Overflow.Hidden,
                    display = DisplayStyle.None
                },
                pickingMode = PickingMode.Position
            };

            // 2. Create the Canvas (The movable world space)
            treeCanvas = new SkillTreeCanvas
            {
                style =
                {
                    // Center the transform origin so zooming scales from the center
                    transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50))
                },
                pickingMode = PickingMode.Position
            };

            // --- THE FIX: APPLY BACKGROUND TO THE CANVAS ---
            if (backgroundTexture != null)
            {
                treeCanvas.style.backgroundImage = new StyleBackground(backgroundTexture);
        
                // CRITICAL: Lock the canvas size strictly to the texture resolution.
                // This guarantees O(1) exact alignment. A node saved at (500, 500) 
                // will ALWAYS rest exactly on pixel (500, 500) of your map art.
                treeCanvas.style.width = backgroundTexture.width;
                treeCanvas.style.height = backgroundTexture.height;
            }
            else
            {
                // Fallback scaling if no map is provided
                treeCanvas.style.flexGrow = 1; 
            }
            // -----------------------------------------------

            // 3. Assemble and Bind Elements
            viewport.Add(treeCanvas);

            // Add HUD Currency Bar
            pointsBar = new SkillPointsBar();
            viewport.Add(pointsBar);

            // Spawning the master tooltip container onto the overlay screen stack
            SkillTooltip masterTooltip = new();
            viewport.Add(masterTooltip);

            uiDocument.rootVisualElement.Add(viewport);

            // Attach the Manipulator to the viewport, but tell it to move the canvas
            viewport.AddManipulator(new PanAndZoomManipulator(treeCanvas, viewport));

            if (targetGraph != null) treeCanvas.Populate(targetGraph);
        }

        private void OnEnable()
        {
            toggleMenuAction.Enable();
            toggleMenuAction.performed += HandleMenuToggleInput;
        }

        private void OnDisable()
        {
            toggleMenuAction.performed -= HandleMenuToggleInput;
            toggleMenuAction.Disable();
        }

        private void HandleMenuToggleInput(InputAction.CallbackContext context)
        {
            Debug.Log("[SkillTreeUI] Input detected. Toggling menu.");
            ToggleMenu();
        }

        private void ToggleMenu()
        {
            isMenuOpen = !isMenuOpen;
            IsOpen = isMenuOpen;

            if (isMenuOpen)
            {
                viewport.style.display = DisplayStyle.Flex;
                treeCanvas.Populate(targetGraph);

                // Ensure text elements sync accurately the exact millisecond the UI turns active
                pointsBar.Refresh();

                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                viewport.style.display = DisplayStyle.None;
                Cursor.lockState = CursorLockMode.Confined;
            }
        }
    }
}