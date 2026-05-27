using System;
using System.Collections.Generic;
using UnityEngine;


public class TimerManager : MonoBehaviour
{
    [SerializeField]
    List<Timer> timers = new List<Timer>(); //pool
    public static TimerManager instance;

    private void Awake()
    {
        if (instance == null)
            instance = this;
        else
            Destroy(this);

    }

    public int NewTimer(float time, Action Callback, string Name)
    {
        Timer _timer = new Timer(time,Callback,false, Name);
        timers.Add(_timer);

        int id = timers.Count-1;

        return id;
    }

    public bool GetTimerState(int ID)
    {
        return timers[ID].isActive;
    }

    public void SetTimerState(int ID, bool isActive)
    {
        if (ID >= 0 && ID < timers.Count)
        {
            timers[ID].isActive = isActive;
        }
    }       


    public void RestartTimer(int ID)
    {
        if (ID >= 0 && ID < timers.Count)
        {
            timers[ID].timer = timers[ID].time;
            timers[ID].isActive = true;
        }
    }

    private void Update()
    {
        for (int i = 0; i < timers.Count; i++)
        {
            Timer t = timers[i];
            if (t.isActive)
            {
                t.timer -= Time.deltaTime;
                if (t.isActive && t.timer <= 0)
                {
                    t.timer = 0;
                    t.isActive = false;
                    t.callback?.Invoke();
                    
                }
                timers[i] = t;
            }
        }
    }
}

[Serializable]
public class Timer
{
    public string name;
    public float time;
    public float timer;
    public Action callback;
    public bool isActive;

    public Timer(float time, Action callback, bool active, string name)
    { 
        this.name = name;
        this.time = time;
        this.callback = callback;
        this.isActive = active;
    }

    
}
