using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;

public class Sniper : MonoBehaviour
{
    //Hold RightMouse to slow down time and aim
    //LMB while RMB to fire Sniper shot
    // While rmb zoom in camera, slow down timer
    // Post process effect (Enemy Highlight red?)

    [Header("References")]
    [SerializeField] CinemachineCamera _camera;    
    [SerializeField] GunInputManagerBase _gunInputManager;
    [SerializeField] WeaponManager _weaponManager;

    
    float aimTimer;
    

    IGunInputManager gunInputManager => _gunInputManager.InputManager;

    private void OnEnable()
    {
        gunInputManager.onSecondFire += AimInput;
    }
    private void OnDisable()
    {
        gunInputManager.onSecondFire -= AimInput;
    }

    bool aim = false;
    bool isAimmed;

    bool _aim { get { return aim; }
        set { 
            aim = value; 
            aimTimer = 0;
            
        } 
    }

    void AimInput(bool aim)
    {
        _aim = aim;
    }

    private void FixedUpdate()
    {
        if (_aim)
            AimIn();
        else
            AimOut();

        AimTimer();
        
    }

    void AimTimer()
    {
        if (aimTimer < 1)
            aimTimer += Time.deltaTime;
        else
            isAimmed = true;
    }

    void AimIn()
    {
        float t = aimTimer;
        _weaponManager.CanPrimaryFire = false;
        _camera.Lens.FieldOfView = Mathf.SmoothStep(_camera.Lens.FieldOfView, 50, t);  
        
    }

    void AimOut()
    {
        Debug.Log("AimOut");
        float t = aimTimer;
        _weaponManager.CanPrimaryFire = true;
        _camera.Lens.FieldOfView = Mathf.SmoothStep(_camera.Lens.FieldOfView, 90, t);


    }
   
}
