using Unity.Mathematics;
using UnityEngine;

namespace RoomBuilding
{
    // A lightweight struct representing a specific moment in space
    public struct TransformState
    {
        public float3 Position;
        public quaternion Rotation;
    }

    // Holds all data required to interpolate a prop between two states
    public struct PropRebuildData
    {
        public Transform PropTransform;
        public TransformState CleanState;
        public TransformState DestroyedState;
        
        public float Delay;
        public float Duration;
        public float ElapsedTime;
        public float ArcHeight;
    }

    public class RoomRebuilder : MonoBehaviour
    {
        [Header("Rebuild Settings")]
        [SerializeField] private float _minDelay = 0f;
        [SerializeField] private float _maxDelay = 1.5f;
        [SerializeField] private float _travelDuration = 1.0f;
        [SerializeField] private float _arcHeightMultiplier = 2.0f;

        private PropRebuildData[] _propsData;
        private bool _isRebuilding = false;
        private int _propCount = 0;

        /// <summary>
        /// Phase 1: Call this when the room is pristine to save the Clean state.
        /// </summary>
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
        }

        /// <summary>
        /// Phase 2: Call this right after combat ends, before starting the rebuild.
        /// </summary>
        public void SnapshotDestroyedState()
        {
            if (_propsData == null) return;

            for (int i = 0; i < _propCount; i++)
            {
                ref PropRebuildData data = ref _propsData[i];
                
                // Save the messy state
                data.DestroyedState = new TransformState
                {
                    Position = data.PropTransform.position,
                    Rotation = data.PropTransform.rotation
                };

                // Prepare animation variables
                float distance = math.distance(data.DestroyedState.Position, data.CleanState.Position);
                data.Delay = UnityEngine.Random.Range(_minDelay, _maxDelay);
                data.Duration = _travelDuration;
                data.ElapsedTime = 0f;
                data.ArcHeight = math.clamp(distance * 0.5f, 1f, 5f) * _arcHeightMultiplier;

                // Disable physics to prepare for cinematic movement
                if (data.PropTransform.TryGetComponent<Rigidbody>(out var rb))
                {
                    rb.isKinematic = true;
                }
            }
        }

        /// <summary>
        /// Phase 3: Triggers the interpolation from Destroyed to Clean.
        /// </summary>
        public void TriggerRebuild()
        {
            if (_propsData == null || _propCount == 0) return;
            _isRebuilding = true;
        }

        private void Update()
        {
            if (!_isRebuilding) return;

            bool allFinished = true;
            float dt = Time.deltaTime;

            for (int i = 0; i < _propCount; i++)
            {
                ref PropRebuildData data = ref _propsData[i];

                if (data.ElapsedTime >= data.Duration + data.Delay) continue;

                allFinished = false;
                data.ElapsedTime += dt;

                if (data.ElapsedTime < data.Delay) continue; 

                // Normalized time (0 to 1)
                float t = (data.ElapsedTime - data.Delay) / data.Duration;
                t = math.clamp(t, 0f, 1f);

                // EaseOutCubic for a snappy landing
                float easeT = 1f - math.pow(1f - t, 3);

                // Quadratic Bezier Curve for Arced Pathing
                float3 controlPoint = data.DestroyedState.Position + (data.CleanState.Position - data.DestroyedState.Position) / 2f;
                controlPoint.y += data.ArcHeight;

                float3 m1 = math.lerp(data.DestroyedState.Position, controlPoint, easeT);
                float3 m2 = math.lerp(controlPoint, data.CleanState.Position, easeT);
                
                data.PropTransform.position = math.lerp(m1, m2, easeT);
                data.PropTransform.rotation = math.slerp(data.DestroyedState.Rotation, data.CleanState.Rotation, easeT);
            }

            if (allFinished)
            {
                _isRebuilding = false;
                Debug.Log("Room Rebuild Complete!");
            }
        }
    }
}