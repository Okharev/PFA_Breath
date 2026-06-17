using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ability.NewAbilitySystem.UI
{
public enum ScreenCorner { TopLeft, TopRight, BottomLeft, BottomRight }

    [UxmlElement]
    public partial class ArcHudPanel : VisualElement
    {
        public const string s_className = "arc-hud-panel";

        // --- EXPOSED UI BUILDER PROPERTIES ---

        private ScreenCorner _corner = ScreenCorner.BottomRight;
        
        [UxmlAttribute("corner")]
        public ScreenCorner Corner 
        { 
            get => _corner; 
            set { _corner = value; UpdateLayout(); } 
        }
        
        [UxmlAttribute("inner-radius-multiplier")]
        public float InnerRadiusMultiplier 
        { 
            get => _innerRadiusMultiplier; 
            set { _innerRadiusMultiplier = value; UpdateLayout(); } 
        }
        private float _innerRadiusMultiplier = 0.6f;

        [UxmlAttribute("ammo-width")]
        public float AmmoWidth 
        { 
            get => _ammoWidth; 
            set { _ammoWidth = value; UpdateLayout(); } 
        }
        private float _ammoWidth = 120f;

        [UxmlAttribute("skip-button-size")]
        public float SkipButtonSize 
        { 
            get => _skipButtonSize; 
            set { _skipButtonSize = value; UpdateLayout(); } 
        }
        private float _skipButtonSize = 60f;

        [UxmlAttribute("element-padding")]
        public float ElementPadding 
        { 
            get => _elementPadding; 
            set { _elementPadding = value; UpdateLayout(); } 
        }
        private float _elementPadding = 15f;
        
        private Texture2D _skipIcon;
        [UxmlAttribute("skip-icon")]
        public Texture2D SkipIcon
        {
            get => _skipIcon;
            set { _skipIcon = value; UpdateSkipIcon(); }
        }

        private float _radius = 200f;
        [UxmlAttribute("radius")]
        public float Radius 
        { 
            get => _radius; 
            set { _radius = value; UpdateLayout(); } 
        }

        private int _numSlots = 4;
        [UxmlAttribute("num-slots")]
        public int NumSlots 
        { 
            get => _numSlots; 
            set { _numSlots = value; RebuildSlots(); } 
        }

        private float _angularPadding = 15f;
        [UxmlAttribute("angular-padding")]
        public float AngularPadding 
        { 
            get => _angularPadding; 
            set { _angularPadding = value; UpdateLayout(); } 
        }
        
        private float _slotSize = 60f;
        [UxmlAttribute("slot-size")]
        public float SlotSize 
        { 
            get => _slotSize; 
            set { _slotSize = value; UpdateLayout(); } 
        }

        [UxmlAttribute("neural-material")]
        public Material NeuralMaterialAsset { get; set; }

        // NEW: Background Image Property
        private Texture2D _backgroundImage;
        [UxmlAttribute("background-image")]
        public Texture2D BackgroundImage
        {
            get => _backgroundImage;
            set { _backgroundImage = value; UpdateBackgroundImage(); }
        }
        
        private float _backgroundSize = 250f;
        [UxmlAttribute("background-size")]
        public float BackgroundSize
        {
            get => _backgroundSize;
            set { _backgroundSize = value; UpdateLayout(); }
        }

        private Vector2 _backgroundOffset = Vector2.zero;
        [UxmlAttribute("background-offset")]
        public Vector2 BackgroundOffset
        {
            get => _backgroundOffset;
            set { _backgroundOffset = value; UpdateLayout(); }
        }

        // --- INTERNAL STATE ---

        private readonly List<SpellSlot> m_spellSlots = new List<SpellSlot>();
        private AmmoDisplay m_ammoDisplay;
        private VisualElement m_skipTurnButton;
        private Image m_skipIconImage;
        
        private VisualElement m_neuralBackground;
        private VisualElement m_backgroundImageElement; // NEW
        private Material m_instancedNeuralMaterial;
        
        private static readonly int s_NodeCount = Shader.PropertyToID("_NodeCount");
        private static readonly int[] s_NodeIDs = new int[]
        {
            Shader.PropertyToID("_Node0"), Shader.PropertyToID("_Node1"),
            Shader.PropertyToID("_Node2"), Shader.PropertyToID("_Node3"),
            Shader.PropertyToID("_Node4"), Shader.PropertyToID("_Node5"),
            Shader.PropertyToID("_Node6"), Shader.PropertyToID("_Node7")
        };

        public ArcHudPanel()
        {
            AddToClassList(s_className);
    
            pickingMode = PickingMode.Ignore;
            
            SetupBackgroundImage(); // NEW: Must be created first so it sits at the back
            SetupNeuralBackground();
            CreateAmmoDisplay();
            RebuildSlots();

            RegisterCallback<GeometryChangedEvent>(evt => PositionChildElements());
        }
        private void SetupBackgroundImage()
        {
            m_backgroundImageElement = new VisualElement();
            
            // 2. ADD THIS: Let clicks pass through the background image layer
            m_backgroundImageElement.pickingMode = PickingMode.Ignore; 
            
            m_backgroundImageElement.style.position = Position.Absolute;
            Insert(0, m_backgroundImageElement); 
        }

        public SpellSlot GetSlot(int index)
        {
            if (index >= 0 && index < m_spellSlots.Count)
                return m_spellSlots[index];
            return null;
        }
        
        public AmmoDisplay GetAmmoDisplay() => m_ammoDisplay;
        public VisualElement GetSkipTurnButton() => m_skipTurnButton;
        private void SetupNeuralBackground()
        {
            m_neuralBackground = new VisualElement();
            
            // 3. ADD THIS: Let clicks pass through the neural shader layer
            m_neuralBackground.pickingMode = PickingMode.Ignore; 
            
            m_neuralBackground.style.position = Position.Absolute;
            m_neuralBackground.style.width = Length.Percent(100);
            m_neuralBackground.style.height = Length.Percent(100);
            Insert(0, m_neuralBackground); 
        }
        private void RebuildSlots()
        {
            foreach (SpellSlot slot in m_spellSlots) Remove(slot);
            m_spellSlots.Clear();

            for (int i = 0; i < NumSlots; i++)
            {
                SpellSlot slot = new SpellSlot { SlotIndex = i };
                m_spellSlots.Add(slot);
                Add(slot); 
            }
            
            UpdateLayout();
        }

// Add this variable near your other UI element variables at the top of ArcHudPanel
        private VisualElement m_actionContainer;


        private void CreateAmmoDisplay()
        {
            // 1. Create a FlexBox Row Wrapper
            m_actionContainer = new VisualElement { name = "action-container" };
            m_actionContainer.style.position = Position.Absolute;
            m_actionContainer.style.flexDirection = FlexDirection.Row;
            m_actionContainer.style.alignItems = Align.Center; 
    
            // 2. Setup Ammo Display
            m_ammoDisplay = new AmmoDisplay();
            m_ammoDisplay.style.position = Position.Relative;
            m_ammoDisplay.style.translate = new Translate(0, 0); 
    
            // 3. Setup Skip Button (The White Structural Container)
            m_skipTurnButton = new VisualElement { name = "skip-turn-button" };
            m_skipTurnButton.AddToClassList("skip-turn-button");
            m_skipTurnButton.style.position = Position.Relative;
            m_skipTurnButton.style.marginLeft = 15f; 

            // NEW: Setup the Foreground Icon (The Hourglass)
            m_skipIconImage = new Image { name = "skip-icon-image" };
            m_skipIconImage.AddToClassList("skip-turn-icon");
            m_skipIconImage.pickingMode = PickingMode.Ignore; // Crucial: Let the parent catch clicks
            m_skipTurnButton.Add(m_skipIconImage);

            // 4. Build the hierarchy
            m_actionContainer.Add(m_ammoDisplay);
            m_actionContainer.Add(m_skipTurnButton);
            Add(m_actionContainer);
    
            UpdateSkipIcon();
        }

        private void UpdateSkipIcon()
        {
            if (m_skipIconImage != null)
            {
                // Assign the texture directly to the Image component, bypassing USS background properties
                m_skipIconImage.image = _skipIcon;
        
                // Hide the image element entirely if there's no texture assigned
                m_skipIconImage.style.display = _skipIcon != null ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }
        // NEW: Updates the UI Toolkit background image dynamically
        private void UpdateBackgroundImage()
        {
            if (m_backgroundImageElement != null)
            {
                m_backgroundImageElement.style.backgroundImage = _backgroundImage != null 
                    ? new StyleBackground(_backgroundImage) 
                    : new StyleBackground(StyleKeyword.Null);
            }
        }

        private void UpdateLayout()
        {
            if (m_instancedNeuralMaterial == null && NeuralMaterialAsset != null)
            {
                m_instancedNeuralMaterial = new Material(NeuralMaterialAsset);
                m_neuralBackground.style.unityMaterial = m_instancedNeuralMaterial;
            }

            MarkDirtyRepaint();
            PositionChildElements();
        }

        private void GetAngleRange(out float startAngle, out float endAngle)
        {
            switch (Corner)
            {
                case ScreenCorner.TopLeft:      startAngle = 0f;   endAngle = 90f;  break;
                case ScreenCorner.TopRight:     startAngle = 90f;  endAngle = 180f; break;
                case ScreenCorner.BottomRight:  startAngle = 180f; endAngle = 270f; break;
                case ScreenCorner.BottomLeft:   startAngle = 270f; endAngle = 360f; break;
                default:                        startAngle = 180f; endAngle = 270f; break;
            }
        }

        private Vector2 GetCenterPoint()
        {
            var rect = contentRect;
            return Corner switch
            {
                ScreenCorner.TopLeft => new Vector2(0, 0),
                ScreenCorner.TopRight => new Vector2(rect.width, 0),
                ScreenCorner.BottomLeft => new Vector2(0, rect.height),
                ScreenCorner.BottomRight => new Vector2(rect.width, rect.height),
                _ => Vector2.zero,
            };
        }

        private void PositionChildElements()
        {
            if (NumSlots < 1 || float.IsNaN(contentRect.width)) return;

            GetAngleRange(out float startAngle, out float endAngle);
            Vector2 center = GetCenterPoint();
            float width = contentRect.width;
            float height = contentRect.height;

            float effectiveStart = startAngle + AngularPadding;
            float effectiveEnd = endAngle - AngularPadding;
            
            // FIXED: Standard O(N) linear distribution of nodes
            float angleStep = NumSlots > 1 ? (effectiveEnd - effectiveStart) / (NumSlots - 1) : 0f;

            if (m_instancedNeuralMaterial != null)
                m_instancedNeuralMaterial.SetFloat(s_NodeCount, m_spellSlots.Count);
            
            // --- Position and constraint the Background Image ---
            if (m_backgroundImageElement != null)
            {
                // 1. Constrain size using the independent BackgroundSize property
                m_backgroundImageElement.style.width = BackgroundSize;
                m_backgroundImageElement.style.height = BackgroundSize;

                // 2. Clear old anchors to prevent stretching
                m_backgroundImageElement.style.top = StyleKeyword.Null;
                m_backgroundImageElement.style.bottom = StyleKeyword.Null;
                m_backgroundImageElement.style.left = StyleKeyword.Null;
                m_backgroundImageElement.style.right = StyleKeyword.Null;

                // 3. Pin to the active corner and apply the tuning offset
                switch (Corner)
                {
                    case ScreenCorner.TopLeft:
                        m_backgroundImageElement.style.top = BackgroundOffset.y;
                        m_backgroundImageElement.style.left = BackgroundOffset.x;
                        break;
                    case ScreenCorner.TopRight:
                        m_backgroundImageElement.style.top = BackgroundOffset.y;
                        m_backgroundImageElement.style.right = BackgroundOffset.x;
                        break;
                    case ScreenCorner.BottomLeft:
                        m_backgroundImageElement.style.bottom = BackgroundOffset.y;
                        m_backgroundImageElement.style.left = BackgroundOffset.x;
                        break;
                    case ScreenCorner.BottomRight:
                        m_backgroundImageElement.style.bottom = BackgroundOffset.y;
                        m_backgroundImageElement.style.right = BackgroundOffset.x;
                        break;
                }
            }
            
            for (int i = 0; i < m_spellSlots.Count; i++)
            {
                float currentAngle = NumSlots > 1 
                    ? effectiveStart + (i * angleStep) 
                    : (startAngle + endAngle) / 2f; 

                float angleRad = currentAngle * Mathf.Deg2Rad;

                float x = center.x + (Mathf.Cos(angleRad) * Radius);
                float y = center.y + (Mathf.Sin(angleRad) * Radius);

                float rotationAngle = currentAngle + 90f;

                m_spellSlots[i].style.width = SlotSize;
                m_spellSlots[i].style.height = SlotSize;
                m_spellSlots[i].style.left = x;
                m_spellSlots[i].style.top = y;
                
                // Keep the root pivot rotated so USS translate physics work
                m_spellSlots[i].style.rotate = new Rotate(rotationAngle);
                
                // NEW: Pass the rotation down to counter-rotate the inner visual box
                m_spellSlots[i].SetCounterRotation(rotationAngle);

                float uvX = x / width;
                float uvY = 1f - (y / height); 

                if (m_instancedNeuralMaterial != null && i < s_NodeIDs.Length)
                {
                    m_instancedNeuralMaterial.SetVector(s_NodeIDs[i], new Vector4(uvX, uvY, 0, 0));
                }
            }
                    
        // 4. Position Ammo Display (Centered)
            float centralAngleDeg = (startAngle + endAngle) / 2f;
            float centralAngleRad = centralAngleDeg * Mathf.Deg2Rad;
            
            // Use the exposed multiplier to adjust how far out the inner elements sit
            float innerRadius = Radius * InnerRadiusMultiplier; 

            float ammoX = center.x + (Mathf.Cos(centralAngleRad) * innerRadius);
            float ammoY = center.y + (Mathf.Sin(centralAngleRad) * innerRadius);
            
            m_ammoDisplay.style.left = ammoX;
            m_ammoDisplay.style.top = ammoY;
            
            // Force the Ammo width so the UI matches the Builder property
            m_ammoDisplay.style.width = AmmoWidth;

            // 5. Position Skip Button (Side-by-side with Ammo Display)
            // Force the Skip button size so the UI matches the Builder property
            m_skipTurnButton.style.width = SkipButtonSize;
            m_skipTurnButton.style.height = SkipButtonSize;

            // Calculate the required arc distance (s) using exposed properties
            float requiredArcDistance = (AmmoWidth / 2f) + (SkipButtonSize / 2f) + ElementPadding;
            float angularOffsetDeg = innerRadius > 0f ? (requiredArcDistance / innerRadius) * Mathf.Rad2Deg : 45f;
            float skipAngleRad = (centralAngleDeg - angularOffsetDeg) * Mathf.Deg2Rad; 

            // Calculate exact pixel position
            float skipX = center.x + (Mathf.Cos(skipAngleRad) * innerRadius) - (SkipButtonSize / 2f);
            float skipY = center.y + (Mathf.Sin(skipAngleRad) * innerRadius) - (SkipButtonSize / 2f);

            m_skipTurnButton.style.left = skipX;
            m_skipTurnButton.style.top = skipY;

            // Guarantee it renders on top of the background layers
            m_skipTurnButton.BringToFront();

        }
    }

    // --- MODULAR SUB-COMPONENTS ---

[UxmlElement]
    public partial class SpellSlot : VisualElement
    {
        public const string s_className = "spell-slot";
        
        [UxmlAttribute("slot-index")]
        public int SlotIndex { get; set; }

        private VisualElement m_visualContainer;
        private Image m_iconImage; // USE DEDICATED UI TOOLKIT IMAGE CONTROL
        private Label m_cooldownLabel;
        private VisualElement m_channelProgress;

        public SpellSlot()
        {
            AddToClassList(s_className);

            // 1. VISUAL CONTAINER
            m_visualContainer = new VisualElement { name = "visual-container" };
            m_visualContainer.AddToClassList("spell-slot-visual");
            
            // Force layout natively so it cannot collapse inside the rotated parent
            m_visualContainer.style.position = Position.Absolute;
            m_visualContainer.style.width = Length.Percent(100);
            m_visualContainer.style.height = Length.Percent(100);
            Add(m_visualContainer);

            // 2. DEDICATED IMAGE CONTROL
            m_iconImage = new Image { name = "ability-icon" };
            
            // Force Absolute Anchors to guarantee it matches the 60x60 parent size
            m_iconImage.style.position = Position.Absolute;
            m_iconImage.style.width = Length.Percent(100);
            m_iconImage.style.height = Length.Percent(100);
            m_iconImage.scaleMode = ScaleMode.ScaleToFit;
            
            // Match the 12px border radius from your USS so the sprite doesn't clip the corners
            m_iconImage.style.borderTopLeftRadius = 12;
            m_iconImage.style.borderTopRightRadius = 12;
            m_iconImage.style.borderBottomLeftRadius = 12;
            m_iconImage.style.borderBottomRightRadius = 12;
            m_visualContainer.Add(m_iconImage);



            m_cooldownLabel = new Label { text = "" };
            m_cooldownLabel.AddToClassList("cooldown-text");
            m_cooldownLabel.style.display = DisplayStyle.None;
            m_visualContainer.Add(m_cooldownLabel); 

            m_channelProgress = new VisualElement();
            m_channelProgress.AddToClassList("channel-progress");
            m_channelProgress.style.display = DisplayStyle.None;
            m_visualContainer.Add(m_channelProgress); 
        }

        public void SetAbility(Sprite icon)
        {
            // Assign directly to the Image component's sprite property
            m_iconImage.sprite = icon;
            
            // Hide the image element entirely if there's no sprite to ensure the grey slot looks clean
            m_iconImage.style.display = icon != null ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void SetCounterRotation(float parentRotation)
        {
            m_visualContainer.style.rotate = new Rotate(-parentRotation);
        }



        public void SetCooldown(int remainingTurns)
        {
            if (remainingTurns <= 0)
            {
                RemoveFromClassList("ability-on-cooldown");
                m_cooldownLabel.style.display = DisplayStyle.None;
            }
            else
            {
                AddToClassList("ability-on-cooldown");
                m_cooldownLabel.style.display = DisplayStyle.Flex;
                m_cooldownLabel.text = remainingTurns.ToString();
            }
        }
    }

    [UxmlElement]
    public partial class AmmoDisplay : VisualElement
    {
        public const string s_className = "ammo-display";
        private Label m_ammoLabel;

        public AmmoDisplay()
        {
            AddToClassList(s_className);
            m_ammoLabel = new Label { text = "--/--", name = "ammo-text" };
            Add(m_ammoLabel);
        }

        public void UpdateAmmo(int current, int max)
        {
            m_ammoLabel.text = $"{current}/{max}";
        }
    }
}