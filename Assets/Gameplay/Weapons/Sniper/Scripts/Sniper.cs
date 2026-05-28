using Game.Audio;
using System;
using System.Collections;
using Unity.Cinemachine;
using UnityEngine;



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
    [SerializeField] LayerMask DamagehitMask;
    
    [SerializeField] AttackData _attackData;
    [SerializeField] float sniperHitRadius = 1f;
    [SerializeField] Transform shotOrigin;

    [SerializeField] float StartFOV = 65;
    [SerializeField] float ZoomFOV = 50;

    [SerializeField] LaserVFXManager laserVFX;

	public bool isOnCoolDown;

	[Header("Sniper SFX")]	
	[Tooltip("Time into the sniper charge/shoot SFX where the actual shot blast happens.")]
	[SerializeField] private float sniperShotFireDelay = 1.02f;

	private bool isFiringSniper;

	IGunInputManager gunInputManager => _gunInputManager.InputManager;    

    public event Action OnShotTaken = delegate { };

    private void Start()
    {
        StartFOV = _camera.Lens.FieldOfView;
    }

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
    byte aim = 0; 
    bool aiming;
    bool isAimed;

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
        if (PauseManager.IsPaused) return;

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

	void HandleShoot() {
        if (_aim != 0 && isAimed && !isOnCoolDown && !isFiringSniper) {
			StartCoroutine(SniperChargeThenShootRoutine());
		} 
    }

	private IEnumerator SniperChargeThenShootRoutine() {
		isFiringSniper = true;
		isOnCoolDown = true;

        EventManager.instance.Invoke("SniperShot",shotOrigin); // Calls Event

		// Wait until the actual shot moment inside the audio clip
		yield return new WaitForSeconds(sniperShotFireDelay);

		FireSniperShot();

		OnShotTaken?.Invoke();

		isFiringSniper = false;
	}

	private void FireSniperShot() {
		//ShotLineEffect.SetPosition(0, shotOrigin.position);    
		if (Physics.Raycast(_cameraTarget.transform.position, _camera.transform.forward.normalized, out RaycastHit hit, 100, hitMask)) {
			RaycastHit[] results = new RaycastHit[5];

			if (Physics.SphereCastNonAlloc(_cameraTarget.transform.position, sniperHitRadius, _camera.transform.forward.normalized, results, 100, DamagehitMask) > 0) {
				foreach (RaycastHit _hit in results) {
					if (_hit.transform == null) continue;

					if (_hit.transform.TryGetComponent<IDamageable>(out IDamageable damageable) || _hit.transform.root.TryGetComponent<IDamageable>(out damageable)) {
						damageable.TakeDamage(_attackData);
					}
				}
			}
			laserVFX.FireLaser(shotOrigin.position, Quaternion.LookRotation(hit.point - shotOrigin.position), Vector3.Distance(shotOrigin.position, hit.point));
		}
		else {
			// Prevent using original hit point since this else block will occur when hit is invalid
			Vector3 endPoint = _cameraTarget.transform.position + _camera.transform.forward * 30.0f;
			laserVFX.FireLaser(shotOrigin.position, Quaternion.LookRotation(endPoint - shotOrigin.position), 30f);
		}
	}


	void AimTimer()
    {
        if (aimTimer < 1)
            aimTimer += Time.unscaledDeltaTime;
        else
        {
           if(_aim == 1)
            {
                isAimed = true;
            }
            else
            {
                isAimed= false;
            }

                aiming = false;
        }        
    }

    void AimIn()
    {        
        float t = aimTimer;
        _weaponManager.CanPrimaryFire = false;
        _camera.Lens.FieldOfView = Mathf.SmoothStep(_camera.Lens.FieldOfView, ZoomFOV, t);
        Time.timeScale = Mathf.SmoothStep(Time.timeScale, 0.5f, t);        
    }

    void AimOut()
    {  
        float t = aimTimer;
        _weaponManager.CanPrimaryFire = true;
        _camera.Lens.FieldOfView = Mathf.SmoothStep(_camera.Lens.FieldOfView, StartFOV, t);
        Time.timeScale = Mathf.SmoothStep(Time.timeScale, 1, t);
    }
   
}
