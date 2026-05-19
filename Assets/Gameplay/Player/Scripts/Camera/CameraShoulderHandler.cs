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

    [Header("Temp Gun Movement")]
    //Temporary Move Gun Side
    [SerializeField] GameObject gunObj;
    [SerializeField] Vector3 RightSideGunPlacement;
    [SerializeField] Vector3 LeftSideGunPlacement;


    [Header("FOV")]
    float startFOV;
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
        
    }
    private void OnDisable()
    {
        wallRunController.OnWallRunStart -= changeShoulder;
        wallRunController.OnWallRunEnd -= resetDutch;
    }

    private void Update()
    {
        ChangeFOV();
    }

    void ChangeFOV()
    {   
        float t = Mathf.InverseLerp(8,15,Mathf.Clamp(_controller.Velocity, 8,15));
        //Debug.Log(t);
        cinemachineCamera.Lens.FieldOfView = Mathf.Lerp(cinemachineCamera.Lens.FieldOfView, startFOV + (FOVMulti*t), t);        
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
