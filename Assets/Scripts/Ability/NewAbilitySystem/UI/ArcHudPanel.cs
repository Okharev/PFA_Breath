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
        private Button m_skipTurnButton;
        
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
    
            SetupBackgroundImage(); // NEW: Must be created first so it sits at the back
            SetupNeuralBackground();
            CreateAmmoDisplay();
            RebuildSlots();

            RegisterCallback<GeometryChangedEvent>(evt => PositionChildElements());
        }
        
        private void SetupBackgroundImage()
        {
            m_backgroundImageElement = new VisualElement();
            m_backgroundImageElement.style.position = Position.Absolute;
    
            // Optional: Add a subtle scaling transition if you want it to pop in
            // m_backgroundImageElement.style.transitionProperty = new List<StylePropertyName> { new StylePropertyName("scale") };
            // m_backgroundImageElement.style.transitionDuration = new List<TimeValue> { new TimeValue(0.2f, TimeUnit.Second) };

            Insert(0, m_backgroundImageElement); 
        }

        public SpellSlot GetSlot(int index)
        {
            if (index >= 0 && index < m_spellSlots.Count)
                return m_spellSlots[index];
            return null;
        }
        
        public AmmoDisplay GetAmmoDisplay() => m_ammoDisplay;
        public Button GetSkipTurnButton() => m_skipTurnButton;

        private void SetupNeuralBackground()
        {
            m_neuralBackground = new VisualElement();
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

        private void CreateAmmoDisplay()
        {
            m_ammoDisplay = new AmmoDisplay();
            Add(m_ammoDisplay);

            // NEW: Create and add the skip turn button
            m_skipTurnButton = new Button { text = "SKIP", name = "skip-turn-button" };
            m_skipTurnButton.AddToClassList("skip-turn-button");
            m_skipTurnButton.style.position = Position.Absolute;
            Add(m_skipTurnButton);
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

                m_spellSlots[i].style.width = SlotSize;
                m_spellSlots[i].style.height = SlotSize;
                m_spellSlots[i].style.left = x;
                m_spellSlots[i].style.top = y;
                m_spellSlots[i].style.rotate = new Rotate(currentAngle + 90f);

                float uvX = x / width;
                float uvY = 1f - (y / height); 

                if (m_instancedNeuralMaterial != null && i < s_NodeIDs.Length)
                {
                    m_instancedNeuralMaterial.SetVector(s_NodeIDs[i], new Vector4(uvX, uvY, 0, 0));
                }
            }

// 4. Position Ammo Display (Centered at half-radius)
            float centralAngleRad = (startAngle + endAngle) / 2f * Mathf.Deg2Rad;
            float ammoX = center.x + (Mathf.Cos(centralAngleRad) * (Radius * 0.5f));
            float ammoY = center.y + (Mathf.Sin(centralAngleRad) * (Radius * 0.5f));
            
            m_ammoDisplay.style.left = ammoX;
            m_ammoDisplay.style.top = ammoY;

            // 5. Position Skip Button (Centered slightly deeper into the corner)
            float skipX = center.x + (Mathf.Cos(centralAngleRad) * (Radius * 0.25f));
            float skipY = center.y + (Mathf.Sin(centralAngleRad) * (Radius * 0.25f));

            m_skipTurnButton.style.left = skipX;
            m_skipTurnButton.style.top = skipY;
            // Center the button's pivot point so it perfectly aligns with the math
            m_skipTurnButton.style.translate = new Translate(new Length(-50, LengthUnit.Percent), new Length(-50, LengthUnit.Percent));
            

        }
    }

    // --- MODULAR SUB-COMPONENTS ---

    [UxmlElement]
    public partial class SpellSlot : VisualElement
    {
        public const string s_className = "spell-slot";
        
        [UxmlAttribute("slot-index")]
        public int SlotIndex { get; set; }

        private Label m_hotkeyLabel;
        private Label m_cooldownLabel;
        private VisualElement m_channelProgress;

        public SpellSlot()
        {
            AddToClassList(s_className);

            m_hotkeyLabel = new Label { name = "hotkey-label" }; 
            m_hotkeyLabel.AddToClassList("hotkey-text");
            Add(m_hotkeyLabel);

            m_cooldownLabel = new Label { text = "" };
            m_cooldownLabel.AddToClassList("cooldown-text");
            m_cooldownLabel.style.display = DisplayStyle.None;
            Add(m_cooldownLabel);

            m_channelProgress = new VisualElement();
            m_channelProgress.AddToClassList("channel-progress");
            m_channelProgress.style.display = DisplayStyle.None;
            Add(m_channelProgress);
        }

        public void SetAbility(Sprite icon)
        {
            style.backgroundImage = icon != null ? new StyleBackground(icon) : null;
        }

        public void SetHotkey(string text)
        {
            m_hotkeyLabel.text = text;
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