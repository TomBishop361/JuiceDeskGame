using UnityEngine;
using System;

public class Test : MonoBehaviour
{
    public event Action OnTest = delegate { };

    private void Awake()
    {
        EventManager.instance.Subscribe("TestEvent", CallBack);
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
       EventManager.instance.Invoke("TestEvent");
    }

    // Update is called once per frame
    void Update()
    {
        
    }


    void CallBack(object data)
    {
        Debug.Log("CALLBACK");
    }
}
