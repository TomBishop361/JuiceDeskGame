using System;
using System.Runtime.CompilerServices;
using TMPro;
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
    [SerializeField] Camera _cameraTarget;
    [SerializeField] GunInputManagerBase _gunInputManager;
    [SerializeField] WeaponManager _weaponManager;    
    float aimTimer;
    [SerializeField] LayerMask hitMask;
    [SerializeField] LineRenderer ShotLineEffect;
    [SerializeField] AttackData _attackData;
    [SerializeField] Transform shotOrigin;
    IGunInputManager gunInputManager => _gunInputManager.InputManager;    

    public event Action OnShotTaken = delegate { };


    private void OnEnable()
    {
        gunInputManager.onSecondFire += AimInput;
         gunInputManager.onShootReceived += ShootInput;
    }
    private void OnDisable()
    {
        gunInputManager.onSecondFire -= AimInput;
        gunInputManager.onShootReceived -= ShootInput;
    }

    bool SniperShot;
    byte aim = 0; // The most minor storage optimisation known to man (also prevents repeated Aimout calls on update)
    bool aiming;
    bool isAimed;
    public bool isOnCoolDown;

    void ShootInput(bool shoot)
    {
        SniperShot = shoot;              
    }

    byte _aim { get { return aim; }
        set { 
            aim = value;
            aiming = true;
            aimTimer = 0;  
        } 
    }

    void AimInput(bool aim)
    {
        _aim = (byte)(aim ? 1 : 0);
    }

    private void Update()
    {
        if (_aim == 1)
        {
            AimIn();
        }
        else if (_aim == 0)
        {
            AimOut();
        }

        if(aiming)
            AimTimer();

        if (SniperShot)
        {
            HandleShoot();
        }
        
    }

    void resetCoolDown()
    {
        isOnCoolDown = false;
    }

    void HandleShoot()
    {        
        if (_aim ==1 && isAimed && !isOnCoolDown)
        {            
            ShotLineEffect.SetPosition(0, shotOrigin.position);            
            if (Physics.Raycast(_cameraTarget.transform.position, _cameraTarget.transform.forward.normalized, out RaycastHit hit, 100,hitMask))
            {                
                ShotLineEffect.SetPosition(1, hit.point);
                if (hit.transform.TryGetComponent<IDamageable>(out IDamageable damageable) || hit.transform.root.TryGetComponent<IDamageable>(out damageable))
                {
                    damageable.TakeDamage(_attackData);
                }               
            }
            else
            {
                ShotLineEffect.SetPosition(1, _cameraTarget.transform.forward * 10);
            }
            isOnCoolDown = true;
            OnShotTaken?.Invoke();
        }
    }


    void AimTimer()
    {
        if (aimTimer < 1)
            aimTimer += Time.unscaledDeltaTime;
        else
        {
            isAimed = true;
            aiming = false;
            _aim = 2;
        }        
    }

    void AimIn()
    {
        float t = aimTimer;
        _weaponManager.CanPrimaryFire = false;
        _camera.Lens.FieldOfView = Mathf.SmoothStep(_camera.Lens.FieldOfView, 50, t);
        Time.timeScale = Mathf.SmoothStep(Time.timeScale, 0.5f, t);        
    }

    void AimOut()
    {            
        Debug.Log("AimOutCalled");
        float t = aimTimer;
        _weaponManager.CanPrimaryFire = true;
        _camera.Lens.FieldOfView = Mathf.SmoothStep(_camera.Lens.FieldOfView, 90, t);
        Time.timeScale = Mathf.SmoothStep(Time.timeScale, 1, t);
    }
   
}
