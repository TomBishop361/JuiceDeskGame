using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public class ScreenShaderHandler : MonoBehaviour
{
    //[SerializeField] private UniversalRendererData rendererData; // Drag your Forward Renderer asset here
    [SerializeField]
    FullScreenPassRendererFeature RendererFeature;
    [SerializeField] Material screenShader;

    private void OnEnable()
    {
        EventManager.instance.Subscribe("OnSprint", SprintBoostFOV);

        EventManager.instance.Subscribe("OnWallrun", EnableHandler);
        EventManager.instance.Subscribe("OnSlide", EnableHandler);

        EventManager.instance.Subscribe("OnSlideEnd", DisableHandler);

        EventManager.instance.Subscribe("OnWallrunEnd", DisableHandler);

        EventManager.instance.Subscribe("OnRailGrind", EnableHandler);
        EventManager.instance.Subscribe("OnRailGrindEnd", DisableHandler);
    }
    
    private void OnDisable()
    {
        EventManager.instance.Unsubscribe("OnSprint", SprintBoostFOV);

        EventManager.instance.Unsubscribe("OnWallrun", EnableHandler);
        EventManager.instance.Unsubscribe("OnSlide", EnableHandler);

        EventManager.instance.Unsubscribe("OnSlideEnd", DisableHandler);
        EventManager.instance.Unsubscribe("OnWallrunEnd", DisableHandler);

        EventManager.instance.Unsubscribe("OnRailGrindEnd", DisableHandler);
        EventManager.instance.Unsubscribe("OnRailGrind", EnableHandler);
    }

    

    void SprintBoostFOV(object data)
    {
        if (data is bool pressed)
        {
            if (pressed)
                setAlpha(1);
            else
                setAlpha(0);
        }
    }

    void DisableHandler(object data)
    {
        setAlpha(0);
    }
    void EnableHandler(object data)
    {
        setAlpha(1);
    }

    void setAlpha(float value)
    {
        // screenShader.SetFloat("Alpha", value);
        RendererFeature.passMaterial.SetFloat("_FSAlpha", value);
    }
}
