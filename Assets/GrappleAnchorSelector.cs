using UnityEngine;

public class GrappleAnchorSelector : MonoBehaviour
{
    [Header("Detection Settings")]
    [Range(0.7f, 1f)]
    [SerializeField] float viewThreshold = 0.92f; // 1.0 is center, 0.9 is roughly the inner screen area
    [SerializeField] AnchorPoint[] anchorPoints;
    [SerializeField] Camera _camera;

    public AnchorPoint bestAnchor;

    public delegate void anchorFound(AnchorPoint anchor);
    public event anchorFound OnAnchorFound;

    


    private void Update()
    {
        GetBestAnchorInView(out bestAnchor);
        OnAnchorFound(bestAnchor);
    }

    public AnchorPoint GetBestAnchorInView(out AnchorPoint result)
    {
        AnchorPoint bestTarget = null;
        float closestToCenter = -1f; // Dot product ranges from -1 to 1

        foreach (AnchorPoint anchor in anchorPoints)
        {
            // 1. Get direction from camera to the anchor
            Vector3 dirToAnchor = (anchor.transform.position - _camera.transform.position).normalized;

            // 2. Calculate Dot Product against Camera Forward
            float dot = Vector3.Dot(_camera.transform.forward, dirToAnchor);

            // 3. Check if it's within our "FOV" threshold and better than the last one found
            if (dot > viewThreshold && dot > closestToCenter)
            {
                // ensure the anchor isn't behind a wall
                 //if (Physics.Linecast(_camera.transform.position, anchor.position, Ground)) continue;

                closestToCenter = dot;
                bestTarget = anchor;
            }
        }
        result = bestTarget;
        return result;
    }
}
