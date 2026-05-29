using System.Collections;
using Unity.Cinemachine;
using Unity.VisualScripting;
using UnityEngine;

public class CameraShoulderHandler : MonoBehaviour
{
    [Header("Shoulder")]
    [Range(-1, 1)]
    [SerializeField] float XShoulderOffset;
    [SerializeField] float DutchTilt;

    float targetOffset;
    float currentOffset;
    float targetDutch;
    float currentDutch;

    [Header("References")]
    [SerializeField] WallRunning wallRunController;
    [SerializeField] InputController _controller;
    [SerializeField] CinemachineThirdPersonFollow cineCam;
    [SerializeField] CinemachineCamera cinemachineCamera;
    
    Coroutine cameraLerp;
    Coroutine cameraFOVLerp;

    [Header("FOVs")]
    [SerializeField] float startFOV;
    [SerializeField] float boostedFov;
    [SerializeField] float sniperFov = 50f; // Added to match Sniper script

    private bool isSprinting = false;
    private bool isSniperAiming = false;
    private bool isRailGrinding = false;

    private void Start()
    {
        currentOffset = XShoulderOffset;
        startFOV = cinemachineCamera.Lens.FieldOfView;
        cineCam.ShoulderOffset.x = XShoulderOffset;
    }

    private void OnEnable()
    {
        wallRunController.OnWallRunStart += changeShoulder;
        wallRunController.OnWallRunEnd += resetDutch;
        EventManager.instance.Subscribe("OnSprint", SprintBoostFOV);
        EventManager.instance.Subscribe("OnRailGrind", RailGrindBoostFov);
        EventManager.instance.Subscribe("OnRailGrindEnd", resetFOV);
        EventManager.instance.Subscribe("OnSniperZoom", HandleSniperZoom);
    }

    private void OnDisable()
    {
        wallRunController.OnWallRunStart -= changeShoulder;
        wallRunController.OnWallRunEnd -= resetDutch;
        EventManager.instance.Unsubscribe("OnSprint", SprintBoostFOV);
        EventManager.instance.Unsubscribe("OnRailGrind", RailGrindBoostFov);
        EventManager.instance.Unsubscribe("OnRailGrindEnd", resetFOV);
        EventManager.instance.Unsubscribe("OnSniperZoom", HandleSniperZoom);
    }

    void HandleSniperZoom(object data)
    {
        // Fixed type checking (matching the byte passed from Sniper)
        if (data is byte aimByte)
        {
            isSniperAiming = (aimByte == 1);
            EvaluateTargetFOV();
        }
    }

    void SprintBoostFOV(object data)
    {
        if (data is bool boost)
        {
            isSprinting = boost;
            EvaluateTargetFOV();
        }
    }

    void RailGrindBoostFov(object data)
    {
        // Rail grind overrides normal FOV
        isRailGrinding = true;
        StartFOVLerp(boostedFov);
    }

    void resetFOV(object data)
    {
        isRailGrinding = false;
        EvaluateTargetFOV();
    }

    // Directs what the FOV priority should be
    void EvaluateTargetFOV()
    {
        if (isSniperAiming)
        {
            
            StartFOVLerp(sniperFov);
        }
        else if (isSprinting)
        {
            StartFOVLerp(boostedFov);
        }
        else if(isRailGrinding)
        {
            StartFOVLerp(boostedFov);
        }
        else
        {
            StartFOVLerp(startFOV);
        }
    }

    void StartFOVLerp(float targetFov)
    {
        if (cameraFOVLerp != null) StopCoroutine(cameraFOVLerp);
        cameraFOVLerp = StartCoroutine(FOVLerp(targetFov));
    }

    void changeShoulder(bool right)
    {
        if (right)
        {
            targetOffset = -XShoulderOffset;
            targetDutch = DutchTilt;
        }
        else
        {
            targetDutch = -DutchTilt;
        }

        if (cameraLerp != null) StopCoroutine(cameraLerp);
        cameraLerp = StartCoroutine(MoveCameraOffset());
    }

    void resetDutch()
    {
        targetOffset = XShoulderOffset;
        targetDutch = 0;
        if (cameraLerp != null) StopCoroutine(cameraLerp);
        cameraLerp = StartCoroutine(MoveCameraOffset());
    }

    IEnumerator FOVLerp(float newFov)
    {
        float t = 0;
        float currentFov = cinemachineCamera.Lens.FieldOfView;
        
        while (t < 1)
        {
            
            cinemachineCamera.Lens.FieldOfView = Mathf.SmoothStep(currentFov, newFov, t);
            t += Time.deltaTime * 4f; 
            yield return null;
        }
        cinemachineCamera.Lens.FieldOfView = newFov;
    }

    IEnumerator MoveCameraOffset()
    {
        float t = 0;
        while (t < 1)
        {
            currentDutch = Mathf.Lerp(currentDutch, targetDutch, t);
            currentOffset = Mathf.Lerp(currentOffset, targetOffset, t);
            t += Time.deltaTime;
            yield return null;
            cinemachineCamera.Lens.Dutch = currentDutch;
            cineCam.ShoulderOffset.x = currentOffset;
        }
    }
}