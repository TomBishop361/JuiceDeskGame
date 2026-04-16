using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class GunBase : MonoBehaviour
{
    #region Vars
    [SerializeField]
    GunInputManagerBase _gunInputManager;
    IGunInputManager gunInputManager => _gunInputManager.InputManager;

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

    public LayerMask hitMask;

    //logic
    public bool isDrawn;

    [Tooltip("Where the aiming raycast will shootfrom")]
    public GameObject AimOrigin;
    [Tooltip("Where the bullet will be shot from")]
    public GameObject BulletOrigin;

    bool isShooting;
    bool canShoot = true;
    public int currentAmmo;
    bool _isReloading = false;

    bool isReloading
    {
        get => _isReloading;
        set { 
            if (value == true) reloadTimer = reloadSpeed;
            _isReloading = value;
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


    

    public delegate void OnShoot();
    public event OnShoot onShot;

    public delegate void OnReload();
    public event OnReload onReload;

    private void OnEnable()
    {
        gunInputManager.onShootReceived += Shoot;
        gunInputManager.onReload += reload;
    }

    private void OnDisable()
    {
        gunInputManager.onShootReceived -= Shoot;
        gunInputManager.onReload -= reload;
    }

    private void Awake()
    {
        gunName = gunData.name;
        gunObject = gunData.gunObject;
        muzzleVilocity = gunData.muzzleVilocity;
        damage = gunData.damage;
        effectiveRange = gunData.effectiveRange;
        meleeDamage = gunData.meleeDamage;
        reloadSpeed = gunData.reloadSpeed;
        fireRate = 1 / (gunData.fireRate / 60);  //RoundsPerMin to RoundsPerSec
        magSize = gunData.magSize;
        maxAmmoReserve = gunData.maxAmmoReserve;
        currentAmmo = magSize;
        reloadTimer = fireRate;

        if(isDrawn) Instantiate(gunObject, transform.position, transform.rotation, transform.parent);
       // gunAnimationHandler = gunObject.GetComponent<GunAnimationHandler>();
    }

    private void FixedUpdate()
    {
        handleShoot(isShooting);
    }

    private void Update()
    {
        RofTimer();
        ReloadTimer();
    }

    void RofTimer()
    {
        if (!canShoot)
        {
            shootTimer -= Time.deltaTime;
        }
        if (shootTimer <= 0 ) canShoot = true;
    }

    void ReloadTimer()
    {
        if (isReloading)
        {
            Debug.Log("Reloading");
            reloadTimer -= Time.deltaTime;
        }
        if (reloadTimer <= 0 && isReloading) reloadGun();
        
    }

    void reload(bool reload)
    {
        Debug.Log("reload");
        if (!isReloading)
        {            
            isReloading = true;          
        }
    }

    void Shoot(bool shoot)
    {        
        isShooting = shoot;        
    }   

    private void handleShoot(bool isShooting)
    {
        if (isShooting && canShoot && currentAmmo > 0 && !isReloading)
        {
            RaycastHit hit;
            Debug.DrawRay(Camera.main.transform.position, Camera.main.transform.forward, Color.red,2);
            if (Physics.Raycast(AimOrigin.transform.position, AimOrigin.transform.forward, out hit, 30f, hitMask))
            {
                Debug.DrawLine(AimOrigin.transform.position, hit.point, Color.blue, 5f);
                ShootDir = (hit.point - BulletOrigin.transform.position).normalized;                
                Debug.Log("Hit Object Name " + hit.transform.name);
            }
            else
                ShootDir = AimOrigin.transform.forward;
                 
            ShootBullet();
        }
        else if (currentAmmo <= 0 && !isReloading)
        {
            isReloading = true;            
        }
    }

    void ShootBullet()
    {        
        canShoot = false;
        shootTimer = fireRate;

        onShot?.Invoke(); //For animation Script or audio or anything else to subscribe to        
        currentAmmo--;
        Vector3 offset = Vector3.zero; //new Vector3(UnityEngine.Random.Range(-0.05f,0.05f), UnityEngine.Random.Range(-0.05f, 0.05f), UnityEngine.Random.Range(-0.05f, 0.05f));
        bulletPoolManager.ShootBullet(ShootDir.normalized + offset, BulletOrigin.transform.position , muzzleVilocity,damage);       
    }

    void reloadGun()
    {
        onReload?.Invoke();    
        Debug.Log("Reload Complete");
        currentAmmo = magSize;
        isReloading = false;        
    }
    
}
