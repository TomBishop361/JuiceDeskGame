using System;
using System.Collections.Generic;
using UnityEngine;


public class TimerManager : MonoBehaviour
{
    [SerializeField]
    List<Timer> timers = new List<Timer>(); //pool

    int NewTimer(float time, Action Callback)
    {
        Timer _timer = new Timer(time,Callback,false);
        timers.Add(_timer);

        int id = timers.Count-1;

        return id;
    }

    void SetTimerState(int ID, bool isActive)
    {
        if (ID >= 0 && ID < timers.Count)
        {
            timers[ID].isActive = isActive;
        }
    }


    void RestartTimer(int ID, float time)
    {
        if (ID >= 0 && ID < timers.Count)
        {
            timers[ID].time = time;
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
                t.time -= Time.deltaTime;
                if (t.isActive && t.time <= 0)
                {
                    t.callback?.Invoke();
                    t.isActive = false;
                }
                timers[i] = t;
            }
        }
    }
}

[Serializable]
public class Timer
{
    public float time;
    public float timer;
    public Action callback;
    public bool isActive;

    public Timer(float time, Action callback, bool active)
    {
        this.time = time;
        this.callback = callback;
        this.isActive = active;
    }

    
}
