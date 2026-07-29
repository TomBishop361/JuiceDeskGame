using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class AutoFocusDistance : MonoBehaviour
{
    [Header("References")]
    public Volume volume;
    public Camera targetCamera;
    public Transform target;

    private DepthOfField depthOfField;

    private void Start()
    {
        if (volume == null)
        {
            Debug.LogError("No Volume assigned.");
            enabled = false;
            return;
        }

        if (!volume.profile.TryGet(out depthOfField))
        {
            Debug.LogError("No Depth Of Field override found in the Volume Profile.");
            enabled = false;
            return;
        }

        if (targetCamera == null)
            targetCamera = Camera.main;
    }

    private void Update()
    {
        if (target == null || targetCamera == null)
            return;

        // Distance from the camera to the target
        Vector3 cameraSpacePos = targetCamera.transform.InverseTransformPoint(target.position);
        depthOfField.focusDistance.Override(cameraSpacePos.z);
    }
}