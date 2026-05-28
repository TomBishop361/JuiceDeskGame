using System.Collections;
using Unity.Cinemachine;
using Unity.VisualScripting;
using UnityEngine;

public class CameraShoulderHandler : MonoBehaviour
{
    [Header("Shoulder")]    
    [Range(-1,1)]
    [SerializeField]float XShoulderOffset;
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


    [Header("FOV")]
    
    public float FOVMulti;

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
        
        
    }
    private void OnDisable()
    {
        wallRunController.OnWallRunStart -= changeShoulder;
        wallRunController.OnWallRunEnd -= resetDutch;

        EventManager.instance.Unsubscribe("OnSprint", SprintBoostFOV);
        EventManager.instance.Unsubscribe("OnRailGrind", RailGrindBoostFov);
        EventManager.instance.Unsubscribe("OnRailGrindEnd", resetFOV);
    }

    private void Update()
    {
        //ChangeFOV();
    }

    void resetFOV(object data)
    {
        if (cameraFOVLerp != null) StopCoroutine(cameraFOVLerp);
        cameraFOVLerp = StartCoroutine(FOVLerp(startFOV));
        
    }

    void RailGrindBoostFov(object data)
    {
        if (cameraLerp != null) StopCoroutine(cameraFOVLerp);
        cameraFOVLerp = StartCoroutine(FOVLerp(boostedFov));
    }

    void SprintBoostFOV( object data)
    {
        Debug.Log("SPRINT BOOST");
        if (data is bool boost)
        {
            if (boost)
            {
                if (cameraFOVLerp != null) StopCoroutine(cameraFOVLerp);
                cameraFOVLerp = StartCoroutine(FOVLerp(boostedFov));
            }
            else
            {
                resetFOV(null);
            }
        }
        else return;
            
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

        //Begin Lerp
        if (cameraLerp != null) StopCoroutine(cameraLerp);
        cameraLerp = StartCoroutine("MoveCameraOffset");
    }

    void resetDutch()
    {
        targetOffset = XShoulderOffset;
        targetDutch = 0;
        if (cameraLerp != null) StopCoroutine(cameraLerp);
        cameraLerp = StartCoroutine("MoveCameraOffset");
    }

    IEnumerator FOVLerp(float newFov)
    {
        Debug.Log("Lerping");
        float t = 0;
        while (t < 1)
        {
            cinemachineCamera.Lens.FieldOfView = Mathf.Lerp(cinemachineCamera.Lens.FieldOfView, newFov, t);
            t += Time.deltaTime;
            yield return null;            
        }
    }

    IEnumerator MoveCameraOffset()
    {
        Debug.Log("Lerping");
        float t =0;
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
