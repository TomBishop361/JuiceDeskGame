
using Game.AI;
using System;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UI;


public class GunBase : MonoBehaviour
{
    #region Vars
    [SerializeField]
    GunInputManagerBase _gunInputManager;

    [SerializeField] Image fillImage;

    IGunInputManager gunInputManager => _gunInputManager.InputManager;
    [SerializeField] WeaponManager weaponManager;
    [SerializeField]
    GunSO gunData;
    public string gunName { get; private set; }
    public GameObject gunObject { get; private set; }
    public Sprite gunIcon { get; private set; }

    //Gun Stats
    public int magSize { get; private set; }

    public int maxAmmoReserve { get; private set; }
    public float muzzleVilocity { get; private set; }
    public float damage { get; private set; }
    public int effectiveRange { get; private set; }
    public int meleeDamage { get; private set; }
    public float reloadSpeed { get; private set; }
    public float fireRate { get; private set; } //RoundsPerMin to RoundsPerSec

    public LayerMask aimMask;

    //logic
    public bool isDrawn;

    [Tooltip("Where the aiming raycast will shootfrom")]
    public GameObject AimOrigin;
    [Tooltip("Where the bullet will be shot from")]
    public GameObject BulletOrigin;

    bool isShooting;
    bool canShoot = true;

    
    public float _overHeat;
    [Range(0f,5f)]
    public float HeatBuildRate = 2f;
    public int CoolDownRate = 10;
    bool _OverHeated = false;
    
    public float StartNaturalCoolDownTime = 1;
    float StartNaturalCoolDownTimer;

   


    float overHeatLvl
    {
        get => _overHeat;
        set
        {
            _overHeat = Mathf.Clamp(value,0,100);
            fillImage.fillAmount = (overHeatLvl * 0.01f);
            fillImage.color = UIColour(fillImage.fillAmount);
        }
    }

    bool OverHeated
    {
        get => _OverHeated;
        set { 
            if (value == true) reloadTimer = reloadSpeed;
            _OverHeated = value;
        }
    }

    //BulletPool
    [SerializeField]
    BulletPoolManager bulletPoolManager;

    //GunAnimationHandler gunAnimationHandler;

    //Timers
    float shootTimer;
    float reloadTimer;

    //TPPGunShoot
    Vector3 ShootDir;


	#endregion

	[SerializeField] private float weaponNoiseInterval = 0.15f;

	private PlayerNoiseEmitter noiseEmitter;
	private float nextWeaponNoiseTime;

	public delegate void OnShoot();
    public event OnShoot onShot;

    public delegate void OnReload();
    public event OnReload onReload;

    private void OnEnable()
    {
        gunInputManager.onShootReceived += Shoot;

        
       // gunInputManager.onReload += reload;
    }

    private void OnDisable()
    {
        gunInputManager.onShootReceived -= Shoot;
       // gunInputManager.onReload -= reload;
    }

    private void Awake()
    {
        gunName = gunData.name;
        gunObject = gunData.gunObject;
        muzzleVilocity = gunData.muzzleVilocity;
        damage = gunData.damage;
        effectiveRange = gunData.effectiveRange;
        meleeDamage = gunData.meleeDamage;        
        fireRate = 1 / (gunData.fireRate / 60);  //RoundsPerMin to RoundsPerSec        
        reloadTimer = fireRate;
        if(isDrawn) Instantiate(gunObject, transform.position, transform.rotation, transform.parent);
		// gunAnimationHandler = gunObject.GetComponent<GunAnimationHandler>();
		noiseEmitter = GetComponentInParent<PlayerNoiseEmitter>();

	}

    private void FixedUpdate()
    {
        handleShoot(isShooting);
    }

    private void Update()
    {
        RofTimer();
        OverHeatedTimer();
        coolDown();
    }

    private void coolDown()
    {
        if (!isShooting)
        {
            StartNaturalCoolDownTimer -= Time.deltaTime;
        }
        if(StartNaturalCoolDownTimer <=0 && overHeatLvl > 0)
        {
            overHeatLvl -= (Time.deltaTime * CoolDownRate);
            overHeatLvl = Mathf.Clamp(overHeatLvl, 0, 100);
        }
    }

    void RofTimer()
    {
        if (!canShoot)
        {
            shootTimer -= Time.deltaTime;
        }
        if (shootTimer <= 0 ) canShoot = true;
    }

    Color UIColour(float t)
    {
        Color white = Color.white;
        Color orange = new Color(1.0f, 0.5f, 0.0f); 
        Color red = Color.red;

        if (t < 0.5f)
        {
            // Remap t from [0, 0.5] to [0, 1]
            return Color.Lerp(white, orange, t * 2.0f);
        }
        else
        {
            // Remap t from [0.5, 1] to [0, 1]
            return Color.Lerp(orange, red, (t - 0.5f) * 2.0f);
        }
    }

    void OverHeatedTimer()
    {
        if (OverHeated)
        {
            
            overHeatLvl -= (Time.deltaTime* CoolDownRate) ;

        }
        if (overHeatLvl <= 0 && OverHeated) reloadGun();
        
    }

    //void reload(bool reload)
    //{
    //    Debug.Log("reload");
    //    if (!OverHeated)
    //    {            
    //        OverHeated = true;          
    //    }
    //}

    void Shoot(bool shoot)
    {        
        isShooting = shoot;        
    }   

    private void handleShoot(bool isShooting)
    {
        if (isShooting && canShoot && !OverHeated && weaponManager.CanPrimaryFire)
        {
            StartNaturalCoolDownTimer = StartNaturalCoolDownTime;
            RaycastHit hit;
            Debug.DrawRay(Camera.main.transform.position, Camera.main.transform.forward, Color.red,2);
            if (Physics.Raycast(AimOrigin.transform.position, AimOrigin.transform.forward, out hit, 30f, aimMask, QueryTriggerInteraction.Ignore))
            {
                Debug.DrawLine(AimOrigin.transform.position, hit.point, Color.blue, 5f);
                ShootDir = (hit.point - BulletOrigin.transform.position).normalized;
               // Debug.Log("Hit Object Name " + hit.transform.name);               
            }
            else
            {
                ShootDir = AimOrigin.transform.forward;               
            }                 
            ShootBullet();
        }
        else if (overHeatLvl >= 100 && !OverHeated)
        {
            OverHeated = true;            
        }
    }

    void ShootBullet()
    {        
        canShoot = false;
        shootTimer = fireRate;

        onShot?.Invoke(); //For animation Script or audio or anything else to subscribe to        
        overHeatLvl += HeatBuildRate;
        Vector3 offset = Vector3.zero; //new Vector3(UnityEngine.Random.Range(-0.05f,0.05f), UnityEngine.Random.Range(-0.05f, 0.05f), UnityEngine.Random.Range(-0.05f, 0.05f));
        bulletPoolManager.ShootBullet(ShootDir.normalized + offset, BulletOrigin.transform.position , muzzleVilocity,damage); 
        EmitPrimaryFireNoise();
	}

    void reloadGun()
    {
        onReload?.Invoke();    
        Debug.Log("Reload Complete");        
        OverHeated = false;        
    }

	private void EmitPrimaryFireNoise() {
		if (Time.time < nextWeaponNoiseTime) {
			return;
		}

		noiseEmitter?.EmitWeaponNoise();
		nextWeaponNoiseTime = Time.time + weaponNoiseInterval;
	}
}
