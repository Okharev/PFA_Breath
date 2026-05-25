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

        // REFERENCE TO OUR NEW HUD OVERLAY BAR
        private SkillPointsBar pointsBar;
        private SkillTreeCanvas treeCanvas;

        private UIDocument uiDocument;
        private VisualElement viewport;

        public static bool IsOpen { get; private set; }

        private void Start()
        {
            uiDocument = GetComponent<UIDocument>();
            uiDocument.rootVisualElement.pickingMode = PickingMode.Ignore;

            viewport = new VisualElement();
            viewport.style.width = Length.Percent(100);
            viewport.style.height = Length.Percent(100);
            viewport.style.backgroundColor =
                new StyleColor(new Color(0.1f, 0.1f, 0.1f, 1f)); // Fully opaque background fix
            viewport.style.overflow = Overflow.Hidden;
            viewport.style.display = DisplayStyle.None;
            viewport.pickingMode = PickingMode.Position;

            // 2. Create the Canvas
            treeCanvas = new SkillTreeCanvas();
            treeCanvas.style.flexGrow = 1;
            treeCanvas.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50));
            treeCanvas.pickingMode = PickingMode.Position;

            // 3. Assemble and Bind Elements
            viewport.Add(treeCanvas);


            // Add HUD Currency Bar
            pointsBar = new SkillPointsBar();
            viewport.Add(pointsBar);

            // NEW ARCHITECTURAL ELEMENT: Spawning the master tooltip container onto the overlay screen stack
            SkillTooltip masterTooltip = new();
            viewport.Add(masterTooltip);

            uiDocument.rootVisualElement.Add(viewport);

            viewport.AddManipulator(new PanAndZoomManipulator(treeCanvas));

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