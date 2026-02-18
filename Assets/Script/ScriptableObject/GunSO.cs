using UnityEngine;

[CreateAssetMenu(fileName = "GunSO", menuName = "Guns", order = 1)]
public class GunSO : ScriptableObject
{
    
    public string gunName;
    
    public GameObject gunObject ;
    
    public Sprite gunIcon ;

    //get stats
    public int magSize;

    public int maxAmmoReserve;

    public float muzzleVilocity ;
    
    public int damage ;
    
    public int effectiveRange ;
    
    public int meleeDamage ;
    
    public float reloadSpeed ;
    
    public float fireRate ;
                                  




}
