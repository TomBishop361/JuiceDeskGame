using UnityEngine;

public class BobbingMotion : MonoBehaviour
{
    public float amplitude = 0.2f;
    public float frequency = 1f;

    private Vector3 startPos;
    private float phaseOffset;

    void Start()
    {
        startPos = transform.position;

        // Give each object a random starting phase
        phaseOffset = Random.Range(0f, Mathf.PI * 2f);
    }

    void Update()
    {
        float offset = Mathf.Sin(Time.time * frequency + phaseOffset) * amplitude;
        transform.position = startPos + new Vector3(0, offset, 0);
    }
}