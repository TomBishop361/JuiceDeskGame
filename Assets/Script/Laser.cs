using UnityEngine;

public class Laser : MonoBehaviour
{
    public GameObject LaserObj;
    bool _isActive = false;
    public float DownTime;
    public float UpTime;
    float DownTimer;
    float UpTimer;

    private void Start()
    {
        LaserObj.SetActive(isActive);
        UpTimer = UpTime;
        DownTimer = DownTime;   
    }

    bool isActive {  
        get { 
            return _isActive;
        } 
        set { 
            LaserObj.SetActive(value);
            UpTimer = UpTime;
            DownTimer = DownTime;
            _isActive = value;
        }
    }

    private void Update()
    {
        if (isActive)
        {
            if (UpTimer <= 0) isActive = false;
            UpTimer -= Time.deltaTime;
        }
        else
        {
            if(DownTimer <= 0) isActive = true;
            DownTimer -= Time.deltaTime;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if(LaserObj == null)
        {
            LaserObj = transform.GetChild(0).gameObject;
        }
    }

#endif
}
