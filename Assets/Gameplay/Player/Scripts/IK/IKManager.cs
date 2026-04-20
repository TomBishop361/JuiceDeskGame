using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.LowLevel;
using static InputController;

public class IKManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] GameObject PlayerIKRoot;
    [SerializeField] GameObject _camera;
    [SerializeField] InputController _controller;
    [SerializeField] Grapple _grapple;
    [SerializeField] bool debugIK;

    [Header("Gun IK")]
    [SerializeField] GameObject IKGunTarget;
    [SerializeField] float minIKDistance = 0.6f;
    [SerializeField] float maxIKDistance = 1.6f;

    [Header("Grapple IK")]
    [SerializeField] GameObject IKGrappleTarget;
    [Tooltip("Graple Rig")]
    [SerializeField] Rig _rig;

    private void OnEnable()
    {
        _grapple.OnGrapple += StartGrappleIK;
        _grapple.OnGrappleEnd += EndGrappleIK;
    }
    private void OnDisable()
    {
        _grapple.OnGrapple -= StartGrappleIK;
        _grapple.OnGrappleEnd -= EndGrappleIK;
    }

    void StartGrappleIK(Vector3 position)
    {
        IKGrappleTarget.transform.position = position;
       _rig.weight = 1f;
    }

    void EndGrappleIK()
    {
       _rig.weight = 0f;
    }

    private void LateUpdate()
    {
        UpdateGunIK();
    }

    void UpdateGunIK()
    {
        float minClamp = -70;
        float MaxClamp = 160;
        bool WallRight = _controller.WallRunRight;
        if (WallRight) { 
        minClamp = -160;
        MaxClamp = 70;
        }
        
        if (debugIK) return;
        //Vector3 desiredIKPosition = _camera.transform.position + _camera.transform.forward * 5;
        Vector3 shoulderOffset = new Vector3(0.9f, 0.65f, -0.53f);

        // Build desired position from camera basis
        Vector3 desiredIKPosition =
            _camera.transform.position +
            _camera.transform.forward * 5f +
            _camera.transform.right * shoulderOffset.x +
            _camera.transform.up * shoulderOffset.y;

        Vector3 localTarget = PlayerIKRoot.transform.InverseTransformPoint(desiredIKPosition);

        // Clamp horizontal angle
        float angle = Mathf.Atan2(localTarget.x, localTarget.z) * Mathf.Rad2Deg;
        angle = Mathf.Clamp(angle, minClamp, MaxClamp);

        //Set Max Distance based on what end of the clamp
        float t = 0;

        if (WallRight)
        {
            t = Mathf.InverseLerp(MaxClamp, minClamp, angle); 
        }
        else
        {
            t = Mathf.InverseLerp(minClamp, MaxClamp, angle);
        }


            // Clamp vertical
            localTarget.y = Mathf.Clamp(localTarget.y, -89f, 89f);

        // Rebuild position
        float dist = localTarget.magnitude;

        Vector3 clamped = new Vector3(
            Mathf.Sin(angle * Mathf.Deg2Rad) * dist,
            localTarget.y,
            Mathf.Cos(angle * Mathf.Deg2Rad) * dist
        );
        Vector3 clampedWorldSpace = PlayerIKRoot.transform.TransformPoint(clamped);

        t = Mathf.Pow(t, 0.75f);   // slower near 0, faster near 1
        float dynamicMaxDistance = Mathf.Lerp(minIKDistance, maxIKDistance, t);
        //Clamp max distance from player

        Vector3 dirToIk = clampedWorldSpace - PlayerIKRoot.transform.position;
        if (dirToIk.magnitude > dynamicMaxDistance)
            dirToIk = dirToIk.normalized * dynamicMaxDistance;

        Vector3 newIKPosition = PlayerIKRoot.transform.position + dirToIk;

        IKGunTarget.transform.position = newIKPosition;

    }
}
