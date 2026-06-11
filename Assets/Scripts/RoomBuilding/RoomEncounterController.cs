using UnityEngine;

namespace RoomBuilding
{
    [RequireComponent(typeof(RoomRebuilder))]
    public class RoomEncounterController : MonoBehaviour
    {
        [Header("Room References")] 
        [SerializeField] [Tooltip("Assign the parent object holding all the props.")]
        private Transform _propParent;

        private RoomRebuilder _rebuilder;

        private void Awake()
        {
            _rebuilder = GetComponent<RoomRebuilder>();
        }

        [ContextMenu("1. Save Clean State (Editor Only)")]
        public void SaveCleanStateInEditor()
        {
            if (_propParent == null)
            {
                Debug.LogWarning("Attention : Assigne d'abord le '_propParent' dans l'inspecteur !");
                return;
            }

            if (_rebuilder == null) _rebuilder = GetComponent<RoomRebuilder>();

            Transform[] roomProps = new Transform[_propParent.childCount];
            for (int i = 0; i < _propParent.childCount; i++) 
                roomProps[i] = _propParent.GetChild(i);

            _rebuilder.SnapshotCleanState(roomProps);

            Debug.Log($"[RoomEncounterController] État initial sauvegardé avec succès pour {roomProps.Length} objets ! N'oublie pas de sauvegarder ta scène.");
        }

        [ContextMenu("2. Load Clean State (Editor Only)")]
        public void LoadCleanStateInEditor()
        {
            if (_rebuilder == null) _rebuilder = GetComponent<RoomRebuilder>();
            if (_rebuilder != null) _rebuilder.LoadCleanState();
        }

        [ContextMenu("3. Save Dirty State (Editor Only)")]
        public void SaveDirtyStateInEditor()
        {
            if (_rebuilder == null) _rebuilder = GetComponent<RoomRebuilder>();
            if (_rebuilder != null) _rebuilder.SnapshotDirtyState();
        }

        [ContextMenu("4. Load Dirty State (Editor Only)")]
        public void LoadDirtyStateInEditor()
        {
            if (_rebuilder == null) _rebuilder = GetComponent<RoomRebuilder>();
            if (_rebuilder != null) _rebuilder.LoadDirtyState();
        }

        [ContextMenu("5. Test Rebuild (Play Mode Only)")]
        public void TestRebuildFromEditor()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("Le test de reconstruction doit être lancé en mode 'Play' car il joue une animation !");
                return;
            }

            OnRoomCleared();
        }

        /// <summary>
        /// Call this method when your combat system detects all enemies are dead.
        /// </summary>
        public void OnRoomCleared()
        {
            Debug.Log("Room cleared! Executing rebuilding sequence.");

            _rebuilder.SnapshotDestroyedState();
            _rebuilder.TriggerRebuild();
        }
    }
}