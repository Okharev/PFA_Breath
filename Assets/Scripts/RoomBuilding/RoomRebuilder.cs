using System;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace RoomBuilding
{
    [Serializable]
    public struct TransformState
    {
        public float3 Position;
        public quaternion Rotation;
    }

    [Serializable]
    public struct PropRebuildData
    {
        public Transform PropTransform;
        public TransformState CleanState;
        public TransformState DestroyedState;

        public float Delay;
        public float Duration;
        public float ElapsedTime;
        public float ArcHeight;
        
        // Optimizations & Feedback
        public bool RequiresRebuild;
        public bool UseBezier;
        public bool HasLanded;
    }

    public class RoomRebuilder : MonoBehaviour
    {
        [Header("Rebuild Animation Settings")]
        [SerializeField] private float _minDelay = 0.1f;
        [SerializeField] private float _maxDelay = 1.5f;
        [SerializeField] private float _travelDuration = 1.0f;
        [SerializeField] private float _arcHeightMultiplier = 2.0f;

        [SerializeField] [Range(0f, 5f)] 
        [Tooltip("0 = no overshoot. 1.7 = standard snap. 3+ = cartoonish rubber-banding.")]
        private float _overshootIntensity = 1.70158f;
        
        [Header("Choreography & Game Feel")]
        [SerializeField] [Tooltip("Empty Transform in the center of the room to calculate the radial wave effect.")]
        private Transform _roomCenter;

        [Header("Optimization Thresholds")]
        [SerializeField] [Tooltip("Distance under which the prop will slide linearly instead of arcing.")]
        private float _bezierDistanceThreshold = 0.5f;
        [SerializeField] [Tooltip("Minimum distance or rotation change required to trigger a rebuild.")]
        private float _sleepDistanceThreshold = 0.01f;

        [SerializeField] private PropRebuildData[] _propsData;
        [SerializeField] private int _propCount;

        private bool _isRebuilding;

        //
        // Observer Pattern: Allows NavMesh, Audio, or VFX managers to react without tight coupling
        public event Action OnRebuildComplete;

        private void Update()
        {
            if (!_isRebuilding) return;

            bool allFinished = true;
            float dt = Time.deltaTime;

            for (int i = 0; i < _propCount; i++)
            {
                ref PropRebuildData data = ref _propsData[i];

                // Optimization: Sleep Culling
                if (!data.RequiresRebuild || data.ElapsedTime >= data.Duration + data.Delay) continue;

                allFinished = false;
                data.ElapsedTime += dt;

                if (data.ElapsedTime < data.Delay) continue;

                float t = (data.ElapsedTime - data.Delay) / data.Duration;
                
                // Micro-Feedback Hook: Detect the exact frame the object finishes its journey
                if (t >= 1f && !data.HasLanded)
                {
                    data.HasLanded = true;
                    // TODO: Trigger Audio or Particle System here
                    // e.g., VFXManager.Instance.PlayDust(data.CleanState.Position);
                }

                t = math.clamp(t, 0f, 1f);

                // Game Feel: Ease-Out Back (Creates a snappy overshoot and lock-in)
     
                float c3 = _overshootIntensity + 1f;
                float tMinus1 = t - 1f;
                float easeT = 1f + c3 * math.pow(tMinus1, 3) + _overshootIntensity * math.pow(tMinus1, 2);

                if (data.UseBezier)
                {
                    float3 controlPoint = data.DestroyedState.Position +
                                          (data.CleanState.Position - data.DestroyedState.Position) / 2f;
                    controlPoint.y += data.ArcHeight;

                    float3 m1 = math.lerp(data.DestroyedState.Position, controlPoint, easeT);
                    float3 m2 = math.lerp(controlPoint, data.CleanState.Position, easeT);

                    Debug.Log(i);

                    data.PropTransform.position = math.lerp(m1, m2, easeT);
                }
                else
                {
                    // Optimization: Linear Fallback for small nudges
                    data.PropTransform.position = math.lerp(data.DestroyedState.Position, data.CleanState.Position, easeT);
                }

                data.PropTransform.rotation = math.slerp(data.DestroyedState.Rotation, data.CleanState.Rotation, easeT);
            }

            if (allFinished)
            {
                _isRebuilding = false;
                Debug.Log("[RoomRebuilder] Room Rebuild Complete!");
                OnRebuildComplete?.Invoke();
            }
        }

        public void SnapshotCleanState(Transform[] props)
        {
            _propCount = props.Length;
            _propsData = new PropRebuildData[_propCount];

            for (int i = 0; i < _propCount; i++)
            {
                _propsData[i] = new PropRebuildData
                {
                    PropTransform = props[i],
                    CleanState = new TransformState
                    {
                        Position = props[i].position,
                        Rotation = props[i].rotation
                    }
                };
            }

#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
        }

        public void LoadCleanState()
        {
            if (_propsData == null || _propCount == 0) return;

            for (int i = 0; i < _propCount; i++)
            {
                ref PropRebuildData data = ref _propsData[i];
                if (data.PropTransform == null) continue;

#if UNITY_EDITOR
                Undo.RecordObject(data.PropTransform, "Load Clean State Transform");
#endif
                data.PropTransform.position = data.CleanState.Position;
                data.PropTransform.rotation = data.CleanState.Rotation;

                if (data.PropTransform.TryGetComponent(out Rigidbody rb))
                {
#if UNITY_EDITOR
                    Undo.RecordObject(rb, "Load Clean State Physics");
#endif
                    rb.linearVelocity = Vector3.zero; // Unity 6 standard
                    rb.angularVelocity = Vector3.zero;
                    rb.isKinematic = false;
                }
            }
        }

        public void SnapshotDirtyState()
        {
            CaptureCurrentAsDestroyedState();
            
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
            Debug.Log($"[RoomRebuilder] Successfully saved Dirty State for {_propCount} props.");
        }

        public void LoadDirtyState()
        {
            if (_propsData == null || _propCount == 0) return;

            for (int i = 0; i < _propCount; i++)
            {
                ref PropRebuildData data = ref _propsData[i];
                if (data.PropTransform == null) continue;

#if UNITY_EDITOR
                Undo.RecordObject(data.PropTransform, "Load Dirty State Transform");
#endif
                data.PropTransform.position = data.DestroyedState.Position;
                data.PropTransform.rotation = data.DestroyedState.Rotation;

                if (data.PropTransform.TryGetComponent<Rigidbody>(out Rigidbody rb))
                {
#if UNITY_EDITOR
                    Undo.RecordObject(rb, "Load Dirty State Physics");
#endif
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
            }
        }

        public void SnapshotDestroyedState()
        {
            CaptureCurrentAsDestroyedState();
        }


        public void TriggerRebuild()
        {
            if (_propsData == null || _propCount == 0) 
            {
                Debug.LogWarning($"[RoomRebuilder] No props found on {gameObject.name}. Instantly completing.");
                // Fire the event immediately so the Listener isn't waiting forever
                OnRebuildComplete?.Invoke();
                return; 
            }
    
            _isRebuilding = true;
        }

        /// <summary>
        /// Centralized logic for capturing the messy state, calculating optimizations, and choreographing the radial wave.
        /// </summary>
        private void CaptureCurrentAsDestroyedState()
        {
            if (_propsData == null || _propCount == 0) return;

            // Step 1: Find Max Distance for Radial Wave Normalization
            float maxDistFromCenter = 0.1f;
            if (_roomCenter != null)
            {
                for (int i = 0; i < _propCount; i++)
                {
                    float dist = math.distance(_propsData[i].CleanState.Position, _roomCenter.position);
                    if (dist > maxDistFromCenter) maxDistFromCenter = dist;
                }
            }

            // Step 2: Apply Math and Logic
            for (int i = 0; i < _propCount; i++)
            {
                ref PropRebuildData data = ref _propsData[i];
                if (data.PropTransform == null) continue;

                data.DestroyedState = new TransformState
                {
                    Position = data.PropTransform.position,
                    Rotation = data.PropTransform.rotation
                };

                float distance = math.distance(data.DestroyedState.Position, data.CleanState.Position);
                float rotDot = math.abs(math.dot(data.DestroyedState.Rotation.value, data.CleanState.Rotation.value));
                bool rotationChanged = rotDot < 0.999f;

                // Set Flags
                data.RequiresRebuild = distance > _sleepDistanceThreshold || rotationChanged;
                data.UseBezier = distance > _bezierDistanceThreshold;
                data.HasLanded = false;

                if (data.RequiresRebuild)
                {
                    // Radial Wave Choreography Calculation
                    if (_roomCenter != null)
                    {
                        float distanceFromCenter = math.distance(data.CleanState.Position, _roomCenter.position);
                        float normalizedDist = distanceFromCenter / maxDistFromCenter;
                        float noise = Random.Range(0f, 0.15f);
                        data.Delay = _minDelay + (normalizedDist * (_maxDelay - _minDelay)) + noise;
                    }
                    else
                    {
                        // Fallback to random popcorn if no center is assigned
                        data.Delay = Random.Range(_minDelay, _maxDelay);
                    }

                    data.Duration = _travelDuration;
                    data.ElapsedTime = 0f;

                    if (data.UseBezier)
                    {
                        data.ArcHeight = math.clamp(distance * 0.5f, 1f, 5f) * _arcHeightMultiplier;
                    }

                    if (data.PropTransform.TryGetComponent<Rigidbody>(out Rigidbody rb)) 
                        rb.isKinematic = true;
                }
                else
                {
                    // Cull from update loop instantly
                    data.ElapsedTime = data.Duration + data.Delay + 1f;
                }
            }
        }
    }
}