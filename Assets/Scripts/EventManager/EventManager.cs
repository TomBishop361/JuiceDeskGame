using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages Event, allows for subscribing to events without referencing other scripts
/// </summary>
public class EventManager 
{
    private Dictionary<string, Action<object>> _table = new Dictionary<string, Action<object>>();

    public readonly static EventManager instance = new EventManager();
    private EventManager() { }

   
    public void Subscribe(string eventName, Action<object> handler)
    {
        if (_table.ContainsKey(eventName))
        {
            _table[eventName] += handler; 
        }
        else
        {
            _table.Add(eventName, handler);
        }
    }

    public void Unsubscribe(string eventName, Action<object> handler)
    {
        if (_table.TryGetValue(eventName, out Action<object> action))
        {
            action -= handler;

            if (action == null)
                _table.Remove(eventName);
            else
                _table[eventName] = action;
        }
    }

    public void Invoke(string eventName, object data = null)
    {
        if (_table.TryGetValue(eventName, out Action<object> action))
        {
            action?.Invoke(data); 
        }
    }

    


}
