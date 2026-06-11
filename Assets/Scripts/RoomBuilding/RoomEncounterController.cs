using UnityEngine;

// Nous incluons UnityEditor pour pouvoir utiliser Undo et EditorUtility
#if UNITY_EDITOR
using UnityEditor;
#endif

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

        /// <summary>
        /// Exécute cette fonction directement dans l'éditeur Unity pour sauvegarder l'état propre.
        /// </summary>
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
            {
                roomProps[i] = _propParent.GetChild(i);
            }

            // _rebuilder.SnapshotCleanState(roomProps);
            
            Debug.Log($"[RoomEncounterController] État initial sauvegardé avec succès pour {roomProps.Length} objets ! N'oublie pas de sauvegarder ta scène.");
        }

        /// <summary>
        /// Simule la physique dans l'éditeur pour laisser tomber et placer les objets naturellement.
        /// </summary>
        [ContextMenu("2. Simulate Physics (Drop Objects)")]
        public void SimulatePhysicsInEditor()
        {
#if UNITY_EDITOR
            if (Application.isPlaying)
            {
                Debug.LogWarning("Ce bouton est conçu pour être utilisé dans l'éditeur (hors mode Play).");
                return;
            }

            if (_propParent == null) return;

            // On récupère tous les objets qui ont un Rigidbody pour qu'ils puissent tomber
            Rigidbody[] rbs = _propParent.GetComponentsInChildren<Rigidbody>();
            if (rbs.Length == 0)
            {
                Debug.LogWarning("Aucun composant Rigidbody n'a été trouvé. La physique a besoin de Rigidbodies pour fonctionner !");
                return;
            }

            // On garde en mémoire si les objets étaient kinématiques ou non pour les remettre dans leur état d'origine à la fin
            bool[] originalKinematicStates = new bool[rbs.Length];

            for (int i = 0; i < rbs.Length; i++)
            {
                originalKinematicStates[i] = rbs[i].isKinematic;
                rbs[i].isKinematic = false; // On les force à réagir à la gravité
                
                // Permet d'utiliser Ctrl+Z si le résultat de la chute ne nous plaît pas
                Undo.RecordObject(rbs[i].transform, "Simulate Physics Drop");
            }

            // On désactive la simulation automatique pour prendre le contrôle
            Physics.autoSimulation = false;

            // On simule environ 3 secondes de chute (150 frames * 0.02 secondes)
            for (int i = 0; i < 150; i++)
            {
                Physics.Simulate(Time.fixedDeltaTime);
            }

            // On réactive la simulation automatique
            Physics.autoSimulation = true;

            // On restaure les états et on indique à Unity que les objets ont bougé
            for (int i = 0; i < rbs.Length; i++)
            {
                rbs[i].isKinematic = originalKinematicStates[i];
                EditorUtility.SetDirty(rbs[i].transform);
            }

            Debug.Log($"[RoomEncounterController] Physique simulée sur {rbs.Length} objets. Ils sont tombés en place !");
#endif
        }

        /// <summary>
        /// Option de menu pour tester la reconstruction directement depuis l'éditeur.
        /// </summary>
        [ContextMenu("3. Test Rebuild (Play Mode Only)")]
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
            Debug.Log("Room cleared! Executing dual-snapshot rebuild.");
            
            // _rebuilder.SnapshotDestroyedState();
            // _rebuilder.TriggerRebuild();
        }
    }
}