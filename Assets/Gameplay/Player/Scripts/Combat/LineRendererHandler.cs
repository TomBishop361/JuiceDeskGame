using UnityEngine;

[RequireComponent (typeof(LineRenderer))]
public class LineRendererHandler : MonoBehaviour
{
    [SerializeField]LineRenderer lr;
    bool isTiming;
    public float LifeTime = 3;
    float timer;

    public void DrawLine(Vector3 Origin, Vector3 Target )
    {
        lr.enabled = true;
        lr.SetPosition(0, Origin);
        lr.SetPosition(1, Target);
        timer = LifeTime;
        isTiming = true;
        
    }

    private void Update()
    {
        if (isTiming)
        {
            timer -= Time.deltaTime;
            if (timer <= 0)
            {
                lr.enabled = false;
                isTiming = false;
            }
        }
        
    }


    private void OnValidate()
    {
        lr = GetComponent<LineRenderer>();
    }
}
