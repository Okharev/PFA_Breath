// Updated SkillTreeUIController.cs
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

namespace Skills.UI
{
    [RequireComponent(typeof(UIDocument))]
    public class SkillTreeUIController : MonoBehaviour
    {
        public static SkillTreeUIController Instance { get; private set; }
        public static bool IsOpen { get; private set; }

        [Header("Data Source")] 
        public SkillTreeGraph targetGraph;

        // Note: Removed the "K" key InputAction here.
        [Header("Input Settings")]
        [Tooltip("Key to close the menu (e.g., Escape).")]
        public InputAction closeMenuAction = new("CloseSkillTree", binding: "<Keyboard>/escape");

        private SkillPointsBar pointsBar;
        private SkillTreeCanvas treeCanvas;
        private UIDocument uiDocument;
        private VisualElement viewport;
        
        [Header("Visuals")]
        [SerializeField] private Texture2D backgroundTexture;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }
        
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
                             display = DisplayStyle.None,
                             backgroundColor = new Color(0.05f, 0.05f, 0.05f, 1f)
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
                     
                     // --- APPLY BACKGROUND TO THE CANVAS ---
                     if (backgroundTexture != null)
                     {
                         treeCanvas.style.backgroundImage = new StyleBackground(backgroundTexture);
         
                         // 1. PREVENT SQUISHING (CRITICAL FIX)
                         // Forces the Flexbox engine to respect absolute pixel dimensions even if they overflow the screen.
                         treeCanvas.style.flexShrink = 0;
             
                         // 2. EXPLICIT SCALE MODE
                         // Ensures the texture is mapped 1:1 without arbitrary stretching.
                         treeCanvas.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
         
                         // 3. LOCK DIMENSIONS
                         // Guarantees O(1) exact alignment for your nodes.
                         treeCanvas.style.width = backgroundTexture.width;
                         treeCanvas.style.height = backgroundTexture.height;
                     }
                     else
                     {
                         // Fallback scaling if no map is provided
                         treeCanvas.style.flexGrow = 1; 
                     }
         
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
            closeMenuAction.Enable();
            closeMenuAction.performed += _ => CloseMenu();
        }

        private void OnDisable()
        {
            closeMenuAction.Disable();
            closeMenuAction.performed -= _ => CloseMenu();
        }

        // Exposed publicly to be called by the Checkpoint Bench
        public void OpenMenu()
        {
            if (IsOpen) return;
            IsOpen = true;

            viewport.style.display = DisplayStyle.Flex;
            treeCanvas.Populate(targetGraph);
            pointsBar.Refresh();

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void CloseMenu()
        {
            if (!IsOpen) return;
            IsOpen = false;

            viewport.style.display = DisplayStyle.None;
            
            // Re-confine cursor to game window
            Cursor.lockState = CursorLockMode.Confined;
        }
    }
}