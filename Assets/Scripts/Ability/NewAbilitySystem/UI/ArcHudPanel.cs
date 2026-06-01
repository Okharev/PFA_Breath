using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ability.NewAbilitySystem.UI
{
    public enum ScreenCorner
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    /// <summary>
    /// A procedural UI Toolkit component that arranges child elements along an arc
    /// and feeds coordinate data to a Shader Graph for "neural" connections.
    /// </summary>
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

        [UxmlAttribute("neural-material")]
        public Material NeuralMaterialAsset { get; set; }

        // --- INTERNAL STATE ---

        private readonly List<SpellSlot> m_spellSlots = new List<SpellSlot>();
        private AmmoDisplay m_ammoDisplay;
        
        // Shader Integration
        private VisualElement m_neuralBackground;
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
            
            SetupNeuralBackground();
            CreateAmmoDisplay();
            RebuildSlots();

            // Re-calculate positions whenever the geometry changes (e.g., screen resize)
            RegisterCallback<GeometryChangedEvent>(evt => PositionChildElements());
        }

        // --- PUBLIC API FOR CONTROLLER ---
        
        public SpellSlot GetSlot(int index)
        {
            if (index >= 0 && index < m_spellSlots.Count)
                return m_spellSlots[index];
            return null;
        }
        
        public AmmoDisplay GetAmmoDisplay() => m_ammoDisplay;

        // --- INITIALIZATION ---

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
            foreach (var slot in m_spellSlots) Remove(slot);
            m_spellSlots.Clear();

            for (int i = 0; i < NumSlots; i++)
            {
                var slot = new SpellSlot { SlotIndex = i };
                m_spellSlots.Add(slot);
                Add(slot); // Add on top of the neural background
            }
            
            UpdateLayout();
        }

        private void CreateAmmoDisplay()
        {
            m_ammoDisplay = new AmmoDisplay();
            Add(m_ammoDisplay);
        }

        private void UpdateLayout()
        {
            // Instantiate material if it was set via inspector after construction
            if (m_instancedNeuralMaterial == null && NeuralMaterialAsset != null)
            {
                m_instancedNeuralMaterial = new Material(NeuralMaterialAsset);
                m_neuralBackground.style.unityMaterial = m_instancedNeuralMaterial;
                
            }

            MarkDirtyRepaint();
            PositionChildElements();
        }

        // --- MATHEMATICAL LAYOUT ---

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

            // 1. Calculate Angular Padding
            float effectiveStart = startAngle + AngularPadding;
            float effectiveEnd = endAngle - AngularPadding;
            float angleStep = NumSlots > 1 ? (effectiveEnd - effectiveStart) / (NumSlots - 1) : 0f;

            // 2. Feed total count to Shader Graph
            if (m_instancedNeuralMaterial != null)
            {
                m_instancedNeuralMaterial.SetFloat(s_NodeCount, m_spellSlots.Count);
            }

            // 3. Position Slots and feed coordinates to Shader Graph
            for (int i = 0; i < m_spellSlots.Count; i++)
            {
                float currentAngle = NumSlots > 1 
                    ? effectiveStart + (i * angleStep) 
                    : (startAngle + endAngle) / 2f; 

                float angleRad = currentAngle * Mathf.Deg2Rad;

                // Cartesian placement
                float x = center.x + (Mathf.Cos(angleRad) * Radius);
                float y = center.y + (Mathf.Sin(angleRad) * Radius);

                m_spellSlots[i].style.left = x;
                m_spellSlots[i].style.top = y;
                
                // Tangential alignment allows CSS to slide it inward during scale/hover
                m_spellSlots[i].style.rotate = new Rotate(currentAngle + 90f);

                // Pass UV data to shader (Y axis is inverted between UI Toolkit and Shader Graph)
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