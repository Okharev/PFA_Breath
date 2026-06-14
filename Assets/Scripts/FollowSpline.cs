using UnityEngine;
using UnityEngine.Splines;

public class FollowSpline : MonoBehaviour
{
    [SerializeField]
    private SplineContainer splineContainer;

    public float Speed = 1;
    public float HorizontalOffset = 0;

    private void Update()
    {
        var localPoint = splineContainer.transform.InverseTransformPoint(transform.position);
        // High resolution and iterations to see if it improves the situation.
        SplineUtility.GetNearestPoint(splineContainer.Spline, localPoint, out var nearest, out var ratio, 10, 10);

        var tangent = SplineUtility.EvaluateTangent(splineContainer.Spline, ratio);

        var rotation = Quaternion.LookRotation(tangent);
        transform.rotation = rotation;

        var globalNearest = splineContainer.transform.TransformPoint(nearest);
        var perpendicular = Vector3.Cross(tangent, Vector3.up);
        var position = globalNearest + (perpendicular.normalized * HorizontalOffset);
        transform.position = position;

        transform.Translate(Vector3.forward * Speed * Time.deltaTime, Space.Self);
    }




}


