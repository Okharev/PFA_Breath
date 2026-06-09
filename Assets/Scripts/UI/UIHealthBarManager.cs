using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UIElements;

namespace UI
{
    [RequireComponent(typeof(UIDocument))]
    public class UIHealthBarManager : MonoBehaviour
    {
        public static UIHealthBarManager Instance { get; private set; }

        [Header("Assets")]
        [Tooltip("Assign the EnemyHealthBar.uxml here")]
        [SerializeField] private VisualTreeAsset healthBarTemplate;

        private UIDocument _uiDocument;
        private VisualElement _root;
    
        // Modern Unity built-in generic Object Pool
        private ObjectPool<VisualElement> _healthBarPool;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _uiDocument = GetComponent<UIDocument>();
            _root = _uiDocument.rootVisualElement;

            // Initialize the Object Pool
            _healthBarPool = new ObjectPool<VisualElement>(
                createFunc: CreateHealthBar,
                actionOnGet: bar => bar.style.display = DisplayStyle.Flex,
                actionOnRelease: bar => bar.style.display = DisplayStyle.None,
                actionOnDestroy: bar => bar.RemoveFromHierarchy(),
                collectionCheck: false,
                defaultCapacity: 20,
                maxSize: 100
            );
        }

        private VisualElement CreateHealthBar()
        {
            // Time Complexity: O(1) layout cost after initial instantiation
            VisualElement bar = healthBarTemplate.Instantiate().Q<VisualElement>("health-container");
            _root.Add(bar);
            return bar;
        }

        public VisualElement GetHealthBar() => _healthBarPool.Get();
        public void ReleaseHealthBar(VisualElement bar) => _healthBarPool.Release(bar);
    }
}