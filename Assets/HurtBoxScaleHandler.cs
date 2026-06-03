using UnityEngine;

public class HurtBoxScaleHandler : MonoBehaviour
{
    [SerializeField] CapsuleCollider col;
    [SerializeField] float slideScale;
    float startYScale;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void OnEnable()
    {
        EventManager.instance.Subscribe("OnSlide", slideHandler);
        EventManager.instance.Subscribe("OnSlideEnd", slideHandler);
    }

    private void OnDisable()
    {
        EventManager.instance.Unsubscribe("OnSlide", slideHandler);
        EventManager.instance.Unsubscribe("OnSlideEnd", slideHandler);
    }

    private void Start()
    {
        startYScale = col.height;
    }

    void slideHandler(object data)
    {
        col.height = slideScale;
        col.center = Vector3.up * -0.35f;
    }

    void EndSlideHandler(object data)
    {
        col.center = Vector3.up * 0.15f;        
        col.height = startYScale;
    }
}
