using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

public class RailScript : MonoBehaviour
{
    public bool normalDir;
    public SplineContainer railSpline;
    public float totalSplineLength;

	// Start is called once before the first execution of Update after the MonoBehaviour is created
	void Start()
    {
        railSpline = GetComponent<SplineContainer>();
        totalSplineLength = railSpline.CalculateLength();
    }

    public Vector3 LocalToWorldConversion(Vector3 localPoint)
    {
        Vector3 worldPoint = transform.TransformPoint(localPoint);
        return worldPoint; 
    }

    public float3 worldToLocalConvertion(Vector3 worldPoint)
    {
        float3 localPos = transform.InverseTransformPoint(worldPoint);
        return localPos;

    }

    public float calclulateTargetRailPoint(Vector3 playerPos, out Vector3 worldPosOnSpline)
    {
        float3 nearestPoint;
        float t;
        SplineUtility.GetNearestPoint(railSpline.Spline, worldToLocalConvertion(playerPos), out nearestPoint, out t);
        worldPosOnSpline = LocalToWorldConversion(nearestPoint);
        return t;
    }

    public void CalculateDirection(float3 railForward, Vector3 playerForward)
    {
        float angle = Vector3.Angle(railForward, playerForward.normalized);
        if(angle > 90f) normalDir = false;
        else normalDir = true;
    }

  
}
