using UnityEngine;

namespace RoomBuilding
{
    [RequireComponent(typeof(RoomRebuilder))]
    public class RoomEncounterController : MonoBehaviour
    {
        [Header("Room References")]
        [SerializeField, Tooltip("Assign the parent object holding all the props.")]
        private Transform _propParent;
        
        private RoomRebuilder _rebuilder;

        private void Awake()
        {
            _rebuilder = GetComponent<RoomRebuilder>();
        }

        private void Start()
        {
            // Gather all child props from the parent object
            Transform[] roomProps = new Transform[_propParent.childCount];
            for (int i = 0; i < _propParent.childCount; i++)
            {
                roomProps[i] = _propParent.GetChild(i);
            }

            // Phase 1: Capture the pristine state immediately when the level loads
            _rebuilder.SnapshotCleanState(roomProps);
        }

        private void Update()
        {
            // FOR TESTING ONLY: Press 'R' to simulate the end of the room encounter
            // if (Input.GetKeyDown(KeyCode.KeyCode.R))
            // {
            //     OnRoomCleared();
            // }
        }

        /// <summary>
        /// Call this method when your combat system detects all enemies are dead.
        /// </summary>
        public void OnRoomCleared()
        {
            Debug.Log("Room cleared! Executing dual-snapshot rebuild.");
            
            // Phase 2: Capture the exact destroyed state right now
            _rebuilder.SnapshotDestroyedState();

            // Phase 3: Begin the interpolation from Destroyed back to Clean
            _rebuilder.TriggerRebuild();
        }
    }
}