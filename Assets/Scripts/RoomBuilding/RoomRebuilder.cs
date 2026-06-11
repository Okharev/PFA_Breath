using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace RoomBuilding
{
    // On réutilise notre structure d'état
    public struct TransformState
    {
        public float3 Position;
        public quaternion Rotation;
    }

    // Données pour chaque objet, incluant son historique de positions
    public class PropRewindData
    {
        public Transform PropTransform;
        public Rigidbody Rb;
        
        // C'est ici que l'on stocke tout le chemin parcouru !
        public List<TransformState> PathHistory = new List<TransformState>();
    }

    public class RoomRebuilder : MonoBehaviour
    {
        [Header("Rewind Settings")]
        [SerializeField, Tooltip("Temps maximum d'enregistrement en secondes pour éviter de surcharger la mémoire.")]
        private float _maxRecordTime = 5f;
        
        [SerializeField, Tooltip("Vitesse à laquelle on rembobine le temps (1 = vitesse normale, 2 = deux fois plus vite).")]
        private float _rewindSpeedMultiplier = 2f;

        private PropRewindData[] _propsData;
        private int _propCount = 0;
        
        private bool _isRecording = false;
        private bool _isRewinding = false;

        // On calcule combien d'états on peut sauvegarder au maximum (ex: 5 sec * 50 frames par sec = 250 états)
        private int MaxFrames => Mathf.RoundToInt(_maxRecordTime / Time.fixedDeltaTime);

        /// <summary>
        /// Initialise le système avec tous les objets de la pièce.
        /// </summary>
        public void Initialize(Transform[] props)
        {
            _propCount = props.Length;
            _propsData = new PropRewindData[_propCount];

            for (int i = 0; i < _propCount; i++)
            {
                _propsData[i] = new PropRewindData
                {
                    PropTransform = props[i],
                    Rb = props[i].GetComponent<Rigidbody>(),
                    PathHistory = new List<TransformState>()
                };
            }
        }

        /// <summary>
        /// Lance l'enregistrement des positions. À appeler quand les objets commencent à bouger.
        /// </summary>
        public void StartRecording()
        {
            if (_propsData == null) return;
            _isRecording = true;
            _isRewinding = false;
        }

        /// <summary>
        /// Arrête l'enregistrement et lance le rembobinage fluide.
        /// </summary>
        public void TriggerRewind()
        {
            _isRecording = false;
            _isRewinding = true;

            // On désactive la physique de tous les objets pour qu'ils ne tombent pas pendant qu'on les remonte
            for (int i = 0; i < _propCount; i++)
            {
                if (_propsData[i].Rb != null)
                {
                    _propsData[i].Rb.isKinematic = true;
                }
            }
        }

        // FixedUpdate est utilisé car on veut se synchroniser avec le moteur physique
        private void FixedUpdate()
        {
            if (_isRecording)
            {
                RecordFrame();
            }
            else if (_isRewinding)
            {
                RewindFrame();
            }
        }

        private void RecordFrame()
        {
            for (int i = 0; i < _propCount; i++)
            {
                var data = _propsData[i];

                // On vérifie si l'objet est en train de dormir (ne bouge plus) pour économiser des calculs
                if (data.Rb != null && data.Rb.IsSleeping()) continue;

                // On ajoute la position actuelle à la fin de la liste
                data.PathHistory.Add(new TransformState
                {
                    Position = data.PropTransform.position,
                    Rotation = data.PropTransform.rotation
                });

                // Si la liste devient trop longue, on supprime le plus vieil enregistrement (à l'index 0)
                if (data.PathHistory.Count > MaxFrames)
                {
                    data.PathHistory.RemoveAt(0);
                }
            }
        }

        private void RewindFrame()
        {
            bool allFinished = true;

            // On lit plusieurs frames d'un coup selon la vitesse de rembobinage
            int framesToRead = Mathf.RoundToInt(1 * _rewindSpeedMultiplier);

            for (int i = 0; i < _propCount; i++)
            {
                var data = _propsData[i];

                if (data.PathHistory.Count > 0)
                {
                    allFinished = false;

                    // On retire des éléments à la fin de la liste (les plus récents)
                    int indexToRead = Mathf.Max(0, data.PathHistory.Count - framesToRead);
                    TransformState state = data.PathHistory[indexToRead];

                    // On applique l'ancienne position
                    data.PropTransform.position = state.Position;
                    data.PropTransform.rotation = state.Rotation;

                    // On supprime les frames qu'on vient de rembobiner
                    int countToRemove = data.PathHistory.Count - indexToRead;
                    data.PathHistory.RemoveRange(indexToRead, countToRemove);
                }
            }

            if (allFinished)
            {
                _isRewinding = false;
                Debug.Log("Rembobinage terminé ! Les objets sont revenus à leur position de départ.");
            }
        }
    }
}